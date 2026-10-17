using System.Numerics;
using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Expressions;
using HollowWake.Core.Sim;
using HollowWake.Core.World;
using HollowWake.Core.WorldGen;

namespace HollowWake.Core.Territory;

/// <summary>A wellspring of the Loom in the world: bindable, contestable, yielding.</summary>
public sealed class Fount : IComponent
{
    public required string Id;
    public string Name = "";
    public float Radius = 48;
    public string? HoldingId;
    public double Influence;
    public Entity Binder = Entity.None;
    public double BindingCompleteAt = double.NegativeInfinity;
    public Dictionary<string, double> Yield = new(StringComparer.Ordinal);
}

/// <summary>A player's or group's bound territory: its founts, decrees, structures and treasury.</summary>
public sealed class Holding
{
    public required string Id { get; init; }
    public string Name { get; set; } = "";
    public required string OwnerCharacterId { get; init; }
    public string? Faction { get; set; }
    public HashSet<string> Founts { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Decrees { get; } = new(StringComparer.Ordinal);
    public List<PlacedStructure> Structures { get; } = new();
    public Dictionary<string, double> Treasury { get; } = new(StringComparer.Ordinal);
    public double Influence { get; set; }
    public double CreatedAt { get; init; }
}

public sealed record PlacedStructure(string Structure, double X, double Y, double RaisedAt);

/// <summary>An active world event instance with its scope.</summary>
public sealed class ActiveWorldEvent
{
    public required string Id { get; init; }
    public required string Definition { get; init; }
    public double StartedAt { get; init; }
    public double EndsAt { get; init; }
    public HashSet<RegionCoord> Regions { get; } = new();
    public HashSet<ZoneCoord> Zones { get; } = new();
}

/// <summary>
/// State shared by every zone of a world: holdings, world events, generation overlays, day cycle.
/// The server persists it; zones read it through their simulation's services.
/// </summary>
public sealed class WorldState
{
    public Dictionary<string, Holding> Holdings { get; } = new(StringComparer.Ordinal);
    public List<ActiveWorldEvent> Events { get; } = new();
    public GenerationOverlays Overlays { get; }
    public double DayLengthSeconds { get; set; } = 1800;
    public double WorldTime { get; set; }

    public WorldState(GenerationOverlays overlays)
    {
        Overlays = overlays;
    }

    public Holding? HoldingOfFount(string fountId) => Holdings.Values.FirstOrDefault(h => h.Founts.Contains(fountId));

    public double TimeOfDay => DayLengthSeconds <= 0 ? 0.5 : (WorldTime % DayLengthSeconds) / DayLengthSeconds;
}

/// <summary>
/// Binding founts, enacting decrees, raising structures and collecting yields. Rules decide timings
/// and costs; decrees and structures are content; their effects reach entities through the same
/// modifier engine as everything else.
/// </summary>
public sealed class TerritoryService
{
    private readonly WorldState _world;

    public TerritoryService(WorldState world)
    {
        _world = world;
    }

    public WorldState World => _world;

    public bool StartBinding(Simulation sim, Entity player, Entity fountEntity, out string reason)
    {
        reason = "";
        if (!sim.World.TryGet<Fount>(fountEntity, out var fount) || !sim.World.TryGet<Player>(player, out var p) || !sim.World.TryGet<Stats>(player, out var stats))
        {
            reason = "invalid";
            return false;
        }
        var minLevel = (int)sim.Content.Rules.Number("territory.min_level", 5);
        if (stats.Level < minLevel)
        {
            reason = "level";
            return false;
        }
        if (!fount.Binder.IsNone && sim.World.IsAlive(fount.Binder) && fount.Binder != player)
        {
            reason = "in_progress";
            return false;
        }
        var holding = fount.HoldingId is null ? null : _world.Holdings.GetValueOrDefault(fount.HoldingId);
        if (holding is not null && holding.OwnerCharacterId == p.CharacterId)
        {
            reason = "already_yours";
            return false;
        }
        if (holding is not null && !sim.Content.Rules.Flag("territory.contestable", true))
        {
            reason = "bound";
            return false;
        }
        var ownedCount = _world.Holdings.Values.Where(h => h.OwnerCharacterId == p.CharacterId).Sum(h => h.Founts.Count);
        if (ownedCount >= sim.Content.Rules.Number("territory.max_founts_per_character", 3))
        {
            reason = "too_many";
            return false;
        }
        var duration = sim.Content.Rules.Eval("territory.bind_time", "30 + 15 * holding_founts", new DictScope().Set("holding_founts", holding?.Founts.Count ?? 0).Set("level", stats.Level));
        fount.Binder = player;
        fount.BindingCompleteAt = sim.Clock.Time + duration;
        stats.Flags.Add("binding");
        sim.Events.Publish(new FountBindingStarted(player, fount.Id, duration) { Tick = sim.Clock.Tick });
        if (holding is not null)
        {
            sim.Events.Publish(new FountContested(fount.Id, holding.Id, player) { Tick = sim.Clock.Tick });
        }
        return true;
    }

    public void CancelBinding(Simulation sim, Fount fount)
    {
        if (fount.Binder.IsNone)
        {
            return;
        }
        sim.World.Get<Stats>(fount.Binder)?.Flags.Remove("binding");
        fount.Binder = Entity.None;
        fount.BindingCompleteAt = double.NegativeInfinity;
    }

    /// <summary>Called each tick by the territory system: completes bindings whose rite finished uninterrupted.</summary>
    public void Advance(Simulation sim)
    {
        var now = sim.Clock.Time;
        foreach (var (e, fount, t) in sim.World.Query<Fount, Transform>())
        {
            if (fount.Binder.IsNone)
            {
                continue;
            }
            var binder = fount.Binder;
            if (!sim.World.IsAlive(binder) || sim.World.Has<Dead>(binder) || sim.World.Get<Transform>(binder) is not { } bt || bt.GroundDistanceTo(t.Position) > Math.Max(4, fount.Radius * 0.15))
            {
                CancelBinding(sim, fount);
                continue;
            }
            if (sim.World.TryGet<Stats>(binder, out var bs) && bs.Recently("hurt") && sim.Content.Rules.Flag("territory.damage_interrupts", true))
            {
                CancelBinding(sim, fount);
                continue;
            }
            if (now >= fount.BindingCompleteAt)
            {
                Complete(sim, binder, e, fount);
            }
        }
    }

    private void Complete(Simulation sim, Entity player, Entity fountEntity, Fount fount)
    {
        var p = sim.World.Get<Player>(player)!;
        var identity = sim.World.Get<Identity>(player);
        var previous = fount.HoldingId is null ? null : _world.Holdings.GetValueOrDefault(fount.HoldingId);
        previous?.Founts.Remove(fount.Id);
        var holding = _world.Holdings.Values.FirstOrDefault(h => h.OwnerCharacterId == p.CharacterId);
        if (holding is null)
        {
            holding = new Holding
            {
                Id = "holding." + p.CharacterId,
                Name = (identity?.Name ?? "A") + "'s Reach",
                OwnerCharacterId = p.CharacterId,
                Faction = identity?.Faction,
                CreatedAt = sim.Clock.Time,
            };
            _world.Holdings[holding.Id] = holding;
        }
        holding.Founts.Add(fount.Id);
        fount.HoldingId = holding.Id;
        fount.Influence = 1;
        CancelBinding(sim, fount);
        sim.Events.Publish(new FountBound(player, fount.Id, holding.Id) { Tick = sim.Clock.Tick });
        RefreshOverlays(sim, holding);
    }

    public bool Enact(Simulation sim, Entity player, string holdingId, string decreeId, out string reason)
    {
        reason = "";
        if (!_world.Holdings.TryGetValue(holdingId, out var holding) || !sim.World.TryGet<Player>(player, out var p) || holding.OwnerCharacterId != p.CharacterId)
        {
            reason = "not_owner";
            return false;
        }
        if (!sim.Content.TryGet<DecreeDef>(decreeId, out var decree) || decree is null)
        {
            reason = "unknown_decree";
            return false;
        }
        if (holding.Decrees.Contains(decreeId))
        {
            reason = "already";
            return false;
        }
        var have = Grants(sim, holding);
        if (!decree.Requires.IsEmpty && !have.HasAll(decree.Requires))
        {
            reason = "requires";
            return false;
        }
        if (!decree.Excludes.IsEmpty && have.HasAny(decree.Excludes))
        {
            reason = "excluded";
            return false;
        }
        if (!Pay(holding, decree.Cost))
        {
            reason = "cost";
            return false;
        }
        holding.Decrees.Add(decreeId);
        sim.Events.Publish(new DecreeEnacted(holdingId, decreeId) { Tick = sim.Clock.Tick });
        RefreshOverlays(sim, holding);
        return true;
    }

    public bool Repeal(Simulation sim, Entity player, string holdingId, string decreeId)
    {
        if (!_world.Holdings.TryGetValue(holdingId, out var holding) || !sim.World.TryGet<Player>(player, out var p) || holding.OwnerCharacterId != p.CharacterId)
        {
            return false;
        }
        var removed = holding.Decrees.Remove(decreeId);
        if (removed)
        {
            RefreshOverlays(sim, holding);
        }
        return removed;
    }

    public bool Build(Simulation sim, Entity player, string holdingId, string structureId, Vector3 point, out string reason)
    {
        reason = "";
        if (!_world.Holdings.TryGetValue(holdingId, out var holding) || !sim.World.TryGet<Player>(player, out var p) || holding.OwnerCharacterId != p.CharacterId)
        {
            reason = "not_owner";
            return false;
        }
        if (!sim.Content.TryGet<StructureDef>(structureId, out var structure) || structure is null)
        {
            reason = "unknown_structure";
            return false;
        }
        if (holding.Structures.Count(s => s.Structure == structureId) >= structure.MaxPerHolding)
        {
            reason = "limit";
            return false;
        }
        if (!structure.Requires.IsEmpty && !Grants(sim, holding).HasAll(structure.Requires))
        {
            reason = "requires";
            return false;
        }
        var inside = sim.World.Query<Fount, Transform>().Any(f => f.C1.HoldingId == holdingId && f.C2.GroundDistanceTo(point) <= f.C1.Radius);
        if (!inside)
        {
            reason = "outside_territory";
            return false;
        }
        if (!Pay(holding, structure.Cost))
        {
            reason = "cost";
            return false;
        }
        holding.Structures.Add(new PlacedStructure(structureId, point.X, point.Y, sim.Clock.Time));
        if (structure.Landmark is not null && sim.Content.TryGet<LandmarkDef>(structure.Landmark, out var landmark) && landmark is not null)
        {
            TerrainEdits.Stamp(sim.Terrain, landmark, (int)Math.Floor(point.X), (int)Math.Floor(point.Y), sim.Terrain.HeightAt(point.X, point.Y), structureId);
        }
        sim.Events.Publish(new StructureRaised(holdingId, structureId, point) { Tick = sim.Clock.Tick });
        return true;
    }

    private Tags.TagSet Grants(Simulation sim, Holding holding)
    {
        var tags = new List<string>();
        foreach (var s in holding.Structures)
        {
            tags.Add("structure." + s.Structure);
            if (sim.Content.TryGet<StructureDef>(s.Structure, out var def) && def is not null)
            {
                tags.AddRange(def.Grants);
            }
        }
        foreach (var d in holding.Decrees)
        {
            tags.Add("decree." + d);
        }
        tags.Add("founts." + holding.Founts.Count);
        return Tags.TagSet.From(tags);
    }

    private static bool Pay(Holding holding, Dictionary<string, double> cost)
    {
        foreach (var (currency, amount) in cost)
        {
            if (holding.Treasury.GetValueOrDefault(currency) < amount)
            {
                return false;
            }
        }
        foreach (var (currency, amount) in cost)
        {
            holding.Treasury[currency] = holding.Treasury.GetValueOrDefault(currency) - amount;
        }
        return true;
    }

    /// <summary>Generation overlays from decrees over the holding's founts, so the land itself answers to policy.</summary>
    private void RefreshOverlays(Simulation sim, Holding holding)
    {
        var parameters = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var d in holding.Decrees)
        {
            if (sim.Content.TryGet<DecreeDef>(d, out var decree) && decree is not null)
            {
                foreach (var (k, v) in decree.Generation)
                {
                    parameters[k] = parameters.GetValueOrDefault(k) + v;
                }
            }
        }
        foreach (var (_, fount, t) in sim.World.Query<Fount, Transform>())
        {
            if (fount.HoldingId != holding.Id)
            {
                continue;
            }
            var id = "holding:" + holding.Id + ":" + fount.Id;
            if (parameters.Count == 0)
            {
                _world.Overlays.Remove(id);
            }
            else
            {
                _world.Overlays.Add(new GenerationOverlay { Id = id, Circle = (t.Position.X, t.Position.Y, fount.Radius), Params = new Dictionary<string, double>(parameters, StringComparer.Ordinal), Priority = 10 });
            }
        }
    }

    /// <summary>Modifiers a holding applies to entities within its founts: decrees and structures.</summary>
    public IEnumerable<Modifier> HoldingModifiers(Simulation sim, Holding holding)
    {
        foreach (var d in holding.Decrees)
        {
            if (sim.Content.TryGet<DecreeDef>(d, out var decree) && decree is not null)
            {
                foreach (var m in decree.Modifiers)
                {
                    yield return m.WithSource("territory:" + holding.Id);
                }
            }
        }
        foreach (var s in holding.Structures)
        {
            if (sim.Content.TryGet<StructureDef>(s.Structure, out var def) && def is not null)
            {
                foreach (var m in def.Modifiers)
                {
                    yield return m.WithSource("territory:" + holding.Id);
                }
            }
        }
    }

    /// <summary>Collects yields from founts into treasuries. Scheduled by the territory system.</summary>
    public void CollectYields(Simulation sim)
    {
        var fountsByHolding = new Dictionary<string, List<Fount>>(StringComparer.Ordinal);
        foreach (var (_, fount) in sim.World.Query<Fount>())
        {
            if (fount.HoldingId is null)
            {
                continue;
            }
            if (!fountsByHolding.TryGetValue(fount.HoldingId, out var list))
            {
                list = new List<Fount>();
                fountsByHolding[fount.HoldingId] = list;
            }
            list.Add(fount);
        }
        foreach (var (holdingId, founts) in fountsByHolding)
        {
            if (!_world.Holdings.TryGetValue(holdingId, out var holding))
            {
                continue;
            }
            var yield = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var f in founts)
            {
                foreach (var (currency, amount) in f.Yield)
                {
                    yield[currency] = yield.GetValueOrDefault(currency) + amount * f.Influence;
                }
            }
            foreach (var s in holding.Structures)
            {
                if (sim.Content.TryGet<StructureDef>(s.Structure, out var def) && def is not null)
                {
                    foreach (var (currency, amount) in def.Yield)
                    {
                        yield[currency] = yield.GetValueOrDefault(currency) + amount;
                    }
                }
            }
            foreach (var d in holding.Decrees)
            {
                if (sim.Content.TryGet<DecreeDef>(d, out var decree) && decree is not null)
                {
                    foreach (var (currency, amount) in decree.Upkeep)
                    {
                        yield[currency] = yield.GetValueOrDefault(currency) - amount;
                    }
                }
            }
            foreach (var (currency, amount) in yield)
            {
                holding.Treasury[currency] = Math.Max(0, holding.Treasury.GetValueOrDefault(currency) + amount);
            }
            if (yield.Count > 0)
            {
                sim.Events.Publish(new HoldingYield(holdingId, yield) { Tick = sim.Clock.Tick });
            }
        }
    }
}

/// <summary>Runs binding rites, applies holding modifiers to entities inside founts, schedules yields.</summary>
public sealed class TerritorySystem : SimSystem
{
    public override string Name => "territory";
    public override SimPhase Phase => SimPhase.React;
    public override int Order => 10;

    private double _nextYieldAt = double.NegativeInfinity;
    private double _nextApplyAt = double.NegativeInfinity;

    public override void Update(Simulation sim)
    {
        if (!sim.TryService<TerritoryService>(out var service))
        {
            return;
        }
        service.Advance(sim);
        var now = sim.Clock.Time;
        if (now >= _nextYieldAt)
        {
            _nextYieldAt = now + sim.Content.Rules.Number("territory.yield_interval", 300);
            if (sim.Clock.Tick > 1)
            {
                service.CollectYields(sim);
            }
        }
        if (now >= _nextApplyAt)
        {
            _nextApplyAt = now + 1.0;
            ApplyHoldingModifiers(sim, service);
        }
    }

    private static void ApplyHoldingModifiers(Simulation sim, TerritoryService service)
    {
        var founts = sim.World.Query<Fount, Transform>().Where(f => f.C1.HoldingId is not null).ToList();
        foreach (var (e, stats, t) in sim.World.Query<Stats, Transform>())
        {
            var inside = founts.FirstOrDefault(f => f.C2.GroundDistanceTo(t.Position) <= f.C1.Radius);
            var holding = inside.C1?.HoldingId is { } hid ? service.World.Holdings.GetValueOrDefault(hid) : null;
            var currentSource = stats.Attributes.AllModifiers.Select(m => m.Source).FirstOrDefault(s => s.StartsWith("territory:", StringComparison.Ordinal));
            var wanted = holding is null ? null : "territory:" + holding.Id;
            if (currentSource == wanted)
            {
                continue;
            }
            if (currentSource is not null)
            {
                stats.Attributes.RemoveSource(currentSource);
            }
            if (holding is not null)
            {
                stats.Attributes.AddRange(service.HoldingModifiers(sim, holding));
            }
        }
    }
}

/// <summary>Runtime edits to terrain: stamping structures raised by players.</summary>
public static class TerrainEdits
{
    public static void Stamp(Terrain terrain, LandmarkDef landmark, int cx, int cy, float baseHeight, string origin)
    {
        var rows = landmark.Rows;
        var height = rows.Count;
        var width = rows.Count == 0 ? 0 : rows.Max(r => r.Length);
        var ax = landmark.AnchorX > 0 ? landmark.AnchorX : width / 2;
        var ay = landmark.AnchorY > 0 ? landmark.AnchorY : height / 2;
        for (var ry = 0; ry < height; ry++)
        {
            var row = rows[ry];
            for (var rx = 0; rx < row.Length; rx++)
            {
                var ch = row[rx];
                if (ch == ' ' || ch == '.' || !landmark.Legend.TryGetValue(ch.ToString(), out var cell))
                {
                    continue;
                }
                var wx = cx + (rx - ax);
                var wy = cy + (height - 1 - ry - ay);
                var (chunk, i) = terrain.Locate(new CellCoord(wx, wy));
                var h = baseHeight + (float)cell.Height;
                var tags = cell.Tags.With("structure", "built." + origin);
                if (cell.Layer)
                {
                    chunk.AddLayer(i, new Surface(h, cell.Walkable, tags));
                }
                else if (cell.Surface)
                {
                    chunk.Height[i] = h;
                    chunk.Flags[i] = CellFlags.Structure | (cell.Walkable ? CellFlags.Walkable : CellFlags.Blocked);
                    chunk.SurfaceTag[i] = chunk.Intern(tags.With("ground"));
                }
                chunk.Modified = true;
            }
        }
    }
}
