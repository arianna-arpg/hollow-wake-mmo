using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Expressions;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Patterns;

public sealed class SpawnOptions
{
    public Entity Owner { get; init; } = Entity.None;
    public int? Level { get; init; }
    public string? Rarity { get; init; }
    public bool RollRarity { get; init; }
    public string? Name { get; init; }
    public string? Faction { get; init; }
    public string? Group { get; init; }
    public Maths.Rng? Rng { get; init; }
}

/// <summary>What a kill or a chest yields.</summary>
public sealed class Drops : IComponent
{
    public string? Table;
    public Expr? Xp;
    public double DropChance = 1;
}

/// <summary>The context a component binder receives.</summary>
public sealed class BindContext
{
    public required Simulation Sim { get; init; }
    public required Entity Entity { get; init; }
    public required PatternDef Pattern { get; init; }
    public required JsonObject Data { get; init; }
    public required SpawnOptions Options { get; init; }
    public required Maths.Rng Rng { get; init; }

    public T Bind<T>() => Data.Deserialize<T>(ContentJson.Options) ?? throw new InvalidOperationException($"Cannot bind component data to {typeof(T).Name}.");
    public string? Str(string key) => Data[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
    public double Num(string key, double fallback) => JsonNum.Of(Data[key], fallback);
    public bool Flag(string key, bool fallback) => Data[key] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : fallback;
}

/// <summary>
/// Spawns entities from patterns. Pattern components are bound by name through a registry, so adding a
/// component type to the engine (or a mod) is one registration and content can use it immediately.
/// </summary>
public sealed class PatternFactory
{
    private readonly ContentRegistry _content;
    private readonly Dictionary<string, Action<BindContext>> _binders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (PatternDef Def, Dictionary<string, JsonObject> Components)> _resolved = new(StringComparer.Ordinal);

    public PatternFactory(ContentRegistry content)
    {
        _content = content;
        RegisterBuiltins();
    }

    public void Register(string component, Action<BindContext> binder) => _binders[component] = binder;

    public IEnumerable<string> Components => _binders.Keys;

    /// <summary>The pattern with its extends chain merged, cached.</summary>
    public (PatternDef Def, Dictionary<string, JsonObject> Components) Resolve(string patternId)
    {
        if (_resolved.TryGetValue(patternId, out var cached))
        {
            return cached;
        }
        var def = _content.Require<PatternDef>(patternId);
        var chain = new List<PatternDef>();
        var cur = def;
        var guard = 0;
        while (cur is not null && guard++ < 16)
        {
            chain.Add(cur);
            cur = cur.Extends is null ? null : _content.Get<PatternDef>(cur.Extends);
        }
        chain.Reverse();
        var merged = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var tags = TagSet.Empty;
        foreach (var p in chain)
        {
            foreach (var (name, data) in p.Components)
            {
                if (merged.TryGetValue(name, out var existing))
                {
                    ContentLoader.MergePatch(existing, data);
                }
                else
                {
                    merged[name] = (JsonObject)data.DeepClone();
                }
            }
            tags = tags.Union(p.Tags);
        }
        var effective = def with { Tags = tags, Level = def.Level, Faction = def.Faction ?? chain.LastOrDefault(p => p.Faction is not null)?.Faction };
        var result = (effective, merged);
        _resolved[patternId] = result;
        return result;
    }

    public Entity Spawn(Simulation sim, string patternId, Vector3 position, SpawnOptions? options = null)
    {
        options ??= new SpawnOptions();
        if (!_content.Has<PatternDef>(patternId))
        {
            sim.Log.Warn($"unknown pattern '{patternId}'");
            return Entity.None;
        }
        var (def, components) = Resolve(patternId);
        var e = sim.World.Create();
        var rng = options.Rng ?? sim.RngFor(e, "spawn:" + patternId);
        var snapped = sim.Terrain.SnapToGround(position, 2.0);
        sim.World.Add(e, new Transform { Position = snapped, Radius = (float)def.Radius, Height = (float)def.Height, Yaw = (float)(rng.NextDouble() * Math.PI * 2) });
        var identity = sim.World.Add(e, new Identity
        {
            Name = options.Name ?? def.DisplayName,
            Pattern = patternId,
            Tags = def.Tags.With("pattern." + patternId),
            Faction = options.Faction ?? def.Faction,
            Owner = options.Owner,
            Look = def.Look ?? patternId,
        });
        var ctxBase = new { sim, e, def, options, rng };
        foreach (var (name, data) in components)
        {
            if (!_binders.TryGetValue(name, out var binder))
            {
                sim.Log.Warn($"pattern '{patternId}' uses unknown component '{name}'");
                continue;
            }
            try
            {
                binder(new BindContext { Sim = sim, Entity = e, Pattern = def, Data = data, Options = options, Rng = rng });
            }
            catch (Exception ex)
            {
                sim.Log.Error($"pattern '{patternId}' component '{name}' failed to bind", ex);
            }
        }
        _ = ctxBase;
        if (sim.World.TryGet<Stats>(e, out var stats))
        {
            stats.Tags = identity.Tags.With(identity.Faction is null ? "unaligned" : "faction." + identity.Faction);
            if ((options.RollRarity || options.Rarity is not null) && def.Tags.Has("monster"))
            {
                ApplyRarity(sim, e, identity, stats, options.Rarity, rng);
            }
            stats.FillResources();
        }
        sim.Spatial.Update(e, snapped);
        sim.Events.Publish(new EntitySpawned(e, patternId, snapped) { Tick = sim.Clock.Tick });
        return e;
    }

    private void ApplyRarity(Simulation sim, Entity e, Identity identity, Stats stats, string? forced, Maths.Rng rng)
    {
        var rarities = _content.Each<MonsterRarityDef>().OrderBy(r => r.Rank).ToList();
        if (rarities.Count == 0)
        {
            return;
        }
        MonsterRarityDef rarity;
        if (forced is not null)
        {
            rarity = _content.Get<MonsterRarityDef>(forced) ?? rarities[0];
        }
        else
        {
            var idx = rng.PickWeighted(rarities.Select(r => r.Weight).ToList());
            rarity = rarities[Math.Max(0, idx)];
        }
        identity.Rarity = rarity.Id;
        identity.Tags = identity.Tags.With("rarity." + rarity.Id);
        stats.Tags = stats.Tags.With("rarity." + rarity.Id);
        stats.Attributes.AddRange(rarity.Modifiers, "rarity:" + rarity.Id);
        if (sim.World.TryGet<Transform>(e, out var t))
        {
            t.Radius *= (float)rarity.SizeScale;
            t.Height *= (float)rarity.SizeScale;
        }
        var pool = _content.Each<MonsterAffixDef>().Where(a => rarity.AffixPool.IsEmpty || a.Pool.HasAny(rarity.AffixPool) || a.Pool.IsEmpty).OrderBy(a => a.Id, StringComparer.Ordinal).ToList();
        var names = new List<string>();
        for (var i = 0; i < rarity.AffixCount && pool.Count > 0; i++)
        {
            var idx = rng.PickWeighted(pool.Select(a => a.Weight).ToList());
            if (idx < 0)
            {
                break;
            }
            var affix = pool[idx];
            pool.RemoveAt(idx);
            stats.Attributes.AddRange(affix.Modifiers, "affix:" + affix.Id);
            names.Add(affix.DisplayName);
            if (affix.Weaves.Count > 0)
            {
                var book = sim.World.GetOrAdd<WeaveBook>(e);
                foreach (var w in affix.Weaves)
                {
                    book.Add(w);
                }
            }
        }
        if (rarity.Rank > 0)
        {
            identity.Name = (names.Count > 0 ? string.Join(" ", names) + " " : "") + identity.Name;
        }
        stats.SetGauge("rarity_rank", rarity.Rank);
    }

    // ------------------------------------------------------------ built-in binders

    private void RegisterBuiltins()
    {
        Register("stats", c =>
        {
            var stats = c.Sim.World.Get<Stats>(c.Entity) ?? c.Sim.World.Add(c.Entity, new Stats(c.Sim.Content));
            stats.UseRng(c.Rng);
            stats.Level = c.Options.Level ?? Math.Max(1, c.Pattern.Level);
            if (c.Data["attributes"] is JsonObject attrs)
            {
                foreach (var (k, v) in attrs)
                {
                    if (JsonNum.TryDouble(v, out var d))
                    {
                        stats.Attributes.SetBase(k, d);
                    }
                    else if (v is JsonValue sv && sv.TryGetValue<string>(out var s) && Expr.TryParse(s, out var expr, out _))
                    {
                        stats.Attributes.SetBase(k, expr!.Evaluate(stats));
                    }
                }
            }
            if (c.Data["modifiers"] is JsonArray mods)
            {
                var list = mods.Deserialize<List<Modifier>>(ContentJson.Options) ?? new List<Modifier>();
                stats.Attributes.AddRange(list, "pattern");
            }
            if (c.Data["flags"] is JsonArray flags)
            {
                foreach (var f in flags)
                {
                    if (f is JsonValue fv && fv.TryGetValue<string>(out var fs))
                    {
                        stats.Flags.Add(fs);
                    }
                }
            }
        });
        Register("motion", c =>
        {
            var m = c.Sim.World.GetOrAdd<Motion>(c.Entity);
            m.Flying = c.Flag("flying", false);
            m.StopDistance = (float)c.Num("stop_distance", 0.3);
        });
        Register("behavior", c =>
        {
            var id = c.Str("id") ?? c.Str("behavior");
            if (id is null || !c.Sim.Content.Has<BehaviorDef>(id))
            {
                c.Sim.Log.Warn($"pattern '{c.Pattern.Id}' behavior '{id}' unknown");
                return;
            }
            var t = c.Sim.World.Get<Transform>(c.Entity)!;
            c.Sim.World.Add(c.Entity, new Behavior { BehaviorId = id, Home = t.Position, NextThinkAt = c.Sim.Clock.Time + c.Rng.NextDouble() * 0.5 });
            c.Sim.World.GetOrAdd<Motion>(c.Entity);
        });
        Register("weaves", c =>
        {
            var book = c.Sim.World.GetOrAdd<WeaveBook>(c.Entity);
            if (c.Data["list"] is JsonArray arr)
            {
                foreach (var spec in arr.Deserialize<List<WeaveSpec>>(ContentJson.Options) ?? new List<WeaveSpec>())
                {
                    book.Add(spec);
                }
            }
        });
        Register("drops", c =>
        {
            c.Sim.World.Add(c.Entity, new Drops
            {
                Table = c.Str("table"),
                Xp = c.Data["xp"] is null ? null : c.Data["xp"].Deserialize<Expr>(ContentJson.Options),
                DropChance = c.Num("chance", 1),
            });
        });
        Register("interactable", c =>
        {
            var i = c.Sim.World.GetOrAdd<Interactable>(c.Entity);
            i.Kind = c.Str("kind") ?? "generic";
            i.Ref = c.Str("ref") ?? c.Options.Group;
            i.Tags = c.Data["tags"] is null ? TagSet.Empty : c.Data["tags"].Deserialize<TagSet>(ContentJson.Options) ?? TagSet.Empty;
            i.Range = (float)c.Num("range", 2.5);
            if (c.Data["data"] is JsonObject data)
            {
                foreach (var (k, v) in data)
                {
                    i.Data[k] = v?.ToString() ?? "";
                }
            }
        });
        Register("prop", c =>
        {
            var p = c.Sim.World.Add(c.Entity, new Prop { Scale = (float)c.Num("scale", 1), Blocking = c.Flag("blocking", false), Resource = c.Str("resource") });
            if (p.Blocking)
            {
                var t = c.Sim.World.Get<Transform>(c.Entity)!;
                c.Sim.Service<BlockedCells>().Block(World.CellCoord.Of(t.Position.X, t.Position.Y), c.Entity);
            }
        });
        Register("lifetime", c => c.Sim.World.Add(c.Entity, new Lifetime { ExpiresAt = c.Sim.Clock.Time + c.Num("seconds", 30) }));
        Register("inventory", c =>
        {
            var inv = c.Sim.World.Add(c.Entity, new Inventory((int)c.Num("capacity", 40)));
            if (c.Data["currency"] is JsonObject cur)
            {
                foreach (var (k, v) in cur)
                {
                    if (JsonNum.TryDouble(v, out var d))
                    {
                        inv.AddCurrency(k, d);
                    }
                }
            }
        });
        Register("equipment", c =>
        {
            c.Sim.World.GetOrAdd<Equipment>(c.Entity);
            if (c.Data["grants"] is JsonArray grants && c.Sim.TryService<Items.ItemGenerator>(out var gen) && c.Sim.TryService<Items.EquipmentService>(out var equip))
            {
                var level = c.Sim.World.Get<Stats>(c.Entity)?.Level ?? 1;
                foreach (var g in grants.Deserialize<List<ItemGrant>>(ContentJson.Options) ?? new List<ItemGrant>())
                {
                    var item = gen.Roll(g.Item, g.Level ?? level, g.Rarity ?? "common", c.Rng);
                    equip.Equip(c.Sim, c.Entity, item, null, fromInventory: false);
                }
            }
        });
        Register("statuses", c => c.Sim.World.GetOrAdd<Statuses>(c.Entity));
        Register("fount", c =>
        {
            c.Sim.World.Add(c.Entity, new Territory.Fount
            {
                Id = c.Str("id") ?? c.Options.Group ?? c.Entity.Key.ToString(),
                Name = c.Str("name") ?? c.Pattern.DisplayName,
                Radius = (float)c.Num("radius", 48),
            });
        });
    }
}

/// <summary>Cells blocked by props and structures that entities placed at runtime.</summary>
public sealed class BlockedCells
{
    private readonly Dictionary<World.CellCoord, HashSet<Entity>> _cells = new();

    public void Block(World.CellCoord cell, Entity by)
    {
        if (!_cells.TryGetValue(cell, out var set))
        {
            set = new HashSet<Entity>();
            _cells[cell] = set;
        }
        set.Add(by);
    }

    public void Unblock(World.CellCoord cell, Entity by)
    {
        if (_cells.TryGetValue(cell, out var set))
        {
            set.Remove(by);
            if (set.Count == 0)
            {
                _cells.Remove(cell);
            }
        }
    }

    public void UnblockAll(Entity by)
    {
        foreach (var (cell, set) in _cells.ToList())
        {
            if (set.Remove(by) && set.Count == 0)
            {
                _cells.Remove(cell);
            }
        }
    }

    public bool IsBlocked(World.CellCoord cell) => _cells.ContainsKey(cell);
}
