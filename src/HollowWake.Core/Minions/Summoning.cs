using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Expressions;
using HollowWake.Core.Maths;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;
using HollowWake.Core.Weaves;

namespace HollowWake.Core.Minions;

/// <summary>One choice of a weighted pool: the pattern, its weight, tags the body wears and the behavior it runs.</summary>
internal sealed record PoolEntry(string Pattern, double Weight, TagSet Tags, string? Behavior);

/// <summary>
/// Where bodies appear: around the focus (the target, else the point), the caster, the point clamped to
/// <c>range</c> or the target; in a ring at a random bearing (the default), scattered in a disc, along a line
/// across the facing, or evenly around a circle.
/// </summary>
internal sealed class Placement
{
    public string At = "focus";
    public Expr? Range;
    public string Shape = "around";
    public Expr? Radius;
    public Expr? Spacing;

    public static Placement Read(JsonNode? node, EffectCompileContext ctx)
    {
        var place = new Placement();
        if (node is null)
        {
            return place;
        }
        if (node is not JsonObject p)
        {
            throw new InvalidOperationException($"'place' must be an object in {ctx.Owner}.");
        }
        place.At = ctx.ChoiceOf(p["at"], "place.at", "focus", "focus", "caster", "point", "target");
        place.Range = ctx.ExprOf(p["range"]);
        place.Shape = ctx.ChoiceOf(p["shape"], "place.shape", p["scatter"] is null ? "around" : "scatter", "around", "scatter", "line", "circle");
        place.Radius = ctx.ExprOf(p["radius"] ?? p["scatter"]);
        place.Spacing = ctx.ExprOf(p["spacing"]);
        return place;
    }

    public Vector3 Center(EffectContext ctx)
    {
        var caster = ctx.SourceTransform?.Position ?? ctx.Point;
        switch (At)
        {
            case "caster":
                return caster;
            case "point":
            {
                var point = ctx.Point;
                if (Range is not null)
                {
                    var range = ctx.Eval(Range, 0);
                    var dx = point.X - caster.X;
                    var dy = point.Y - caster.Y;
                    var d = MathF.Sqrt(dx * dx + dy * dy);
                    if (range > 0 && d > range)
                    {
                        var f = (float)(range / d);
                        point = ctx.Sim.Terrain.SnapToGround(new Vector3(caster.X + dx * f, caster.Y + dy * f, point.Z), 3);
                    }
                }
                return point;
            }
            default:
                return ctx.Focus;
        }
    }

    /// <summary>Where body <paramref name="index"/> of <paramref name="count"/> stands.</summary>
    public Vector3 Position(EffectContext ctx, Vector3 center, int index, int count, double ring)
    {
        switch (Shape)
        {
            case "scatter":
            {
                var radius = Radius is null ? ring : ctx.Eval(Radius, ring);
                var ang = ctx.Rng.NextDouble() * Math.PI * 2;
                var dist = radius * Math.Sqrt(ctx.Rng.NextDouble());
                return center + new Vector3((float)(Math.Cos(ang) * dist), (float)(Math.Sin(ang) * dist), 0);
            }
            case "line":
            {
                var spacing = Spacing is null ? 1 : ctx.Eval(Spacing, 1);
                var facing = ctx.Direction.LengthSquared() > 0.001f ? Vector2.Normalize(ctx.Direction) : Outward(ctx, center);
                var across = new Vector2(-facing.Y, facing.X);
                var offset = (float)((index - (count - 1) * 0.5) * spacing);
                return center + new Vector3(across.X * offset, across.Y * offset, 0);
            }
            case "circle":
            {
                var radius = Radius is null ? ring : ctx.Eval(Radius, ring);
                var ang = index * Math.PI * 2 / Math.Max(1, count);
                return center + new Vector3((float)(Math.Cos(ang) * radius), (float)(Math.Sin(ang) * radius), 0);
            }
            default:
            {
                var radius = (float)(Radius is null ? ring : ctx.Eval(Radius, ring));
                var ang = ctx.Rng.NextDouble() * Math.PI * 2;
                return center + new Vector3((float)Math.Cos(ang) * radius, (float)Math.Sin(ang) * radius, 0);
            }
        }
    }

    private static Vector2 Outward(EffectContext ctx, Vector3 center)
    {
        var caster = ctx.SourceTransform?.Position ?? center;
        var away = new Vector2(center.X - caster.X, center.Y - caster.Y);
        return away.LengthSquared() > 0.0001f ? Vector2.Normalize(away) : ctx.SourceTransform?.Forward ?? Vector2.UnitX;
    }
}

/// <summary>A contract's terms: what it reserves of each resource, how long a fallen body takes to return, and whether casting again ends it.</summary>
internal sealed record ContractSpec(Dictionary<string, Expr> Reserve, Expr Respawn, bool Toggle);

/// <summary>Unlife: after <c>delay</c> seconds the body loses <c>fraction</c> of its maximum life a second, growing by <c>growth</c> a second.</summary>
internal sealed record DecaySpec(Expr Delay, Expr Fraction, Expr Growth);

/// <summary>Waves: one cast summons <c>count</c> waves <c>interval</c> seconds apart, each later wave paying <c>cost_factor</c> of the weave's cost.</summary>
internal sealed record WavesSpec(Expr Count, Expr Interval, Expr? CostFactor);

/// <summary>A dome a construct carries: its radius, what it does to hostile projectiles, and how much speed a slowed one keeps.</summary>
internal sealed record InterceptSpec(Expr Radius, string Mode, Expr Slow);

/// <summary>
/// A summon or a deploy, compiled from its parameters: which bodies it raises (a pattern, a weighted pool or
/// a corpse's own kind), how many, for how long, how many may stand at once in its group or pool group,
/// where they appear, what they run and inherit, and the terms they serve under (contracts, decay, waves,
/// replenishment, presences). Summons, deploys, contract respawns and replenishment all give birth here.
/// </summary>
internal sealed class SummonRecipe
{
    public string? Pattern;
    public PoolEntry[] Pool = Array.Empty<PoolEntry>();
    public double[] Weights = Array.Empty<double>();
    public required Expr Count;
    public Expr? Lifetime;
    public required Expr MaxActive;
    public string? Group;
    public string? PoolGroup;
    public string? ExclusiveGroup;
    public Placement Place = new();
    public string? Behavior;
    public bool DefaultBehavior;
    public bool Inherit;
    public bool Construct;
    public bool InheritCaster;
    public ContractSpec? Contract;
    public DecaySpec? Decay;
    public IReadOnlyList<Modifier>? Presence;
    public Expr? Replenish;
    public WavesSpec? Waves;
    public string? FromCorpse;
    public string? RaiseAs;
    public Expr? SearchRadius;
    public InterceptSpec? Intercept;
    public CompiledEffect[] OnBirth = Array.Empty<CompiledEffect>();
    public CompiledEffect[] OnDeath = Array.Empty<CompiledEffect>();

    /// <summary>The parameters <c>summon</c> and <c>deploy</c> share.</summary>
    public static readonly string[] SharedParams =
    {
        "pattern", "pool", "count", "max_active", "group", "pool_group", "exclusive_group", "place", "behavior", "inherit",
    };

    /// <summary>The variable death effects read: the fallen body's maximum life.</summary>
    public const string BodyLifeMax = "body_life_max";

    public static SummonRecipe Read(JsonObject p, EffectCompileContext ctx, bool construct)
    {
        var recipe = new SummonRecipe
        {
            Pattern = ctx.StringOf(p["pattern"]),
            Count = ctx.ExprOf(p["count"], Expr.Parse("weave.summon.count"))!,
            Lifetime = ctx.ExprOf(construct ? p["lifetime"] : p["duration"], Expr.Parse("weave.duration")),
            MaxActive = ctx.ExprOf(p["max_active"], Expr.Parse("weave.summon.max"))!,
            Group = ctx.StringOf(p["group"]),
            PoolGroup = ctx.StringOf(p["pool_group"]),
            ExclusiveGroup = ctx.StringOf(p["exclusive_group"]),
            Place = Placement.Read(p["place"], ctx),
            Behavior = ctx.StringOf(p["behavior"]),
            DefaultBehavior = !construct,
            Inherit = ctx.BoolOf(p["inherit"], !construct),
            Construct = construct,
            InheritCaster = construct && ctx.BoolOf(p["inherit_caster"], false),
            FromCorpse = CorpseMode(p["from_corpse"], ctx),
            RaiseAs = ctx.StringOf(p["raise_as"]),
            SearchRadius = ctx.ExprOf(p["search_radius"]),
            Replenish = p["replenish"] is JsonObject replenish ? ctx.ExprOf(replenish["interval"], Expr.Constant(4)) : null,
            OnBirth = ctx.CompileList(p["on_birth"], "on_birth"),
            OnDeath = ctx.CompileList(p["on_death"], "on_death"),
        };
        if (p["pool"] is { } poolNode)
        {
            recipe.Pool = ReadPool(poolNode, ctx);
            recipe.Weights = recipe.Pool.Select(e => e.Weight).ToArray();
        }
        if (recipe.Pattern is null && recipe.Pool.Length == 0 && recipe.FromCorpse is null)
        {
            throw new InvalidOperationException($"{(construct ? "deploy" : "summon")} needs 'pattern', 'pool' or 'from_corpse' in {ctx.Owner}.");
        }
        if (recipe.Pattern is not null && !ctx.Content.Has<PatternDef>(recipe.Pattern))
        {
            throw new InvalidOperationException($"{(construct ? "deploy" : "summon")} names unknown pattern '{recipe.Pattern}' in {ctx.Owner}.");
        }
        if (recipe.RaiseAs is not null)
        {
            if (recipe.FromCorpse is null || recipe.Pattern is not null || recipe.Pool.Length > 0)
                throw new InvalidOperationException($"raise_as requires from_corpse and cannot have a fallback pattern or pool in {ctx.Owner}.");
            if (!ctx.Content.Has<PatternDef>(recipe.RaiseAs))
                throw new InvalidOperationException($"summon names unknown raise_as pattern '{recipe.RaiseAs}' in {ctx.Owner}.");
        }
        if (recipe.Behavior is not null && !ctx.Content.Has<BehaviorDef>(recipe.Behavior))
        {
            throw new InvalidOperationException($"summon names unknown behavior '{recipe.Behavior}' in {ctx.Owner}.");
        }
        if (p["contract"] is JsonObject contract)
        {
            var reserve = new Dictionary<string, Expr>(StringComparer.Ordinal);
            if (contract["reserve"] is JsonObject reserves)
            {
                foreach (var (resource, amount) in reserves)
                {
                    reserve[resource] = ctx.ExprOf(amount, Expr.Constant(0))!;
                }
            }
            recipe.Contract = new ContractSpec(reserve, ctx.ExprOf(contract["respawn"], Expr.Constant(8))!, ctx.BoolOf(contract["toggle"], true));
        }
        if (p["decay"] is JsonObject decay)
        {
            recipe.Decay = new DecaySpec(ctx.ExprOf(decay["delay"], Expr.Constant(0))!, ctx.ExprOf(decay["fraction"], Expr.Constant(0.05))!, ctx.ExprOf(decay["growth"], Expr.Constant(1.1))!);
        }
        if (p["waves"] is JsonObject waves)
        {
            recipe.Waves = new WavesSpec(ctx.ExprOf(waves["count"], Expr.Constant(1))!, ctx.ExprOf(waves["interval"], Expr.Constant(1))!, ctx.ExprOf(waves["cost_factor"]));
        }
        if (p["presence"] is JsonArray presence)
        {
            recipe.Presence = presence.Deserialize<List<Modifier>>(ContentJson.Options) ?? new List<Modifier>();
        }
        if (p["intercept"] is JsonObject intercept)
        {
            recipe.Intercept = new InterceptSpec(
                ctx.ExprOf(intercept["radius"], Expr.Constant(3))!,
                ctx.ChoiceOf(intercept["mode"], "intercept.mode", "dissolve", "dissolve", "deflect", "slow"),
                ctx.ExprOf(intercept["slow"], Expr.Constant(0.35))!);
        }
        if (recipe.Contract is not null && (recipe.Decay is not null || recipe.Waves is not null || recipe.Replenish is not null))
        {
            throw new InvalidOperationException($"a contract summon cannot also decay, come in waves or replenish, in {ctx.Owner}.");
        }
        return recipe;
    }

    private static string? CorpseMode(JsonNode? node, EffectCompileContext ctx)
    {
        if (node is null)
        {
            return null;
        }
        if (node is JsonValue v && v.TryGetValue<bool>(out var flag))
        {
            return flag ? "nearest" : null;
        }
        return ctx.ChoiceOf(node, "from_corpse", "nearest", "nearest", "target");
    }

    private static PoolEntry[] ReadPool(JsonNode node, EffectCompileContext ctx)
    {
        if (node is not JsonArray arr || arr.Count == 0)
        {
            throw new InvalidOperationException($"'pool' must be a list of patterns in {ctx.Owner}.");
        }
        var entries = new List<PoolEntry>();
        foreach (var item in arr)
        {
            PoolEntry entry = item switch
            {
                JsonValue v when v.TryGetValue<string>(out var id) => new PoolEntry(id, 1, TagSet.Empty, null),
                JsonObject o => new PoolEntry(
                    ctx.StringOf(o["pattern"]) ?? throw new InvalidOperationException($"a pool entry needs 'pattern' in {ctx.Owner}."),
                    ctx.NumberOf(o["weight"], 1),
                    ctx.TagsOf(o["tags"]),
                    ctx.StringOf(o["behavior"])),
                _ => throw new InvalidOperationException($"a pool entry must be a pattern or an object in {ctx.Owner}."),
            };
            if (!ctx.Content.Has<PatternDef>(entry.Pattern))
            {
                throw new InvalidOperationException($"pool names unknown pattern '{entry.Pattern}' in {ctx.Owner}.");
            }
            if (entry.Behavior is not null && !ctx.Content.Has<BehaviorDef>(entry.Behavior))
            {
                throw new InvalidOperationException($"pool entry '{entry.Pattern}' names unknown behavior '{entry.Behavior}' in {ctx.Owner}.");
            }
            entries.Add(entry);
        }
        return entries.ToArray();
    }

    // ------------------------------------------------------------ running

    /// <summary>
    /// The summoning weave: the host slot's weave when a meta, trigger or <c>cast</c> runs this summon for it,
    /// else the running weave. Its key names the bodies' weave and contract, its tags and modifiers fold their
    /// inheritance, and its glyphs board their weaves.
    /// </summary>
    internal static CompiledWeave? HostOf(EffectContext ctx) => (ctx.Host ?? ctx.Weave)?.Root;

    /// <summary>The group the bodies join: the named group, else the pattern, else the summoning weave.</summary>
    public string GroupFor(EffectContext ctx) => Group ?? (Pool.Length == 0 && Pattern is not null ? Pattern : HostOf(ctx)?.Key ?? Pattern ?? Pool[0].Pattern);

    /// <summary>A cast of the summon: toggles its contract, or raises a wave and schedules the rest.</summary>
    public void Run(EffectContext ctx)
    {
        var service = MinionService.Of(ctx.Sim);
        MinionContract? contract = null;
        if (Contract is not null)
        {
            var key = HostOf(ctx)?.Key ?? GroupFor(ctx);
            if (service.ContractFor(ctx.Origin, key) is { } running)
            {
                if (Contract.Toggle)
                {
                    service.EndContract(running);
                    return;
                }
                contract = running;
            }
            else
            {
                contract = StartContract(ctx, service, key);
            }
        }
        Wave(ctx, service, contract);
        if (Waves is not null)
        {
            ScheduleWaves(ctx, service);
        }
    }

    private MinionContract StartContract(EffectContext ctx, MinionService service, string key)
    {
        var slot = SlotOf(ctx);
        var host = HostOf(ctx);
        var contract = new MinionContract(ctx.Origin, key, GroupFor(ctx))
        {
            ExclusiveGroup = ExclusiveGroup,
            Slot = slot,
            Weave = host,
            Context = host?.Tags ?? TagSet.Empty,
            Respawn = Math.Max(0, ctx.Eval(Contract!.Respawn, 8)),
            Recipe = this,
        };
        if (ctx.Sim.World.TryGet<Stats>(ctx.Origin, out var owner))
        {
            foreach (var (resource, amount) in Contract.Reserve)
            {
                var value = ctx.Eval(amount, 0);
                if (value > 0)
                {
                    owner.Attributes.Add(new Modifier { Attribute = resource + ".reserved", Form = ModifierForm.Flat, Value = value, Source = contract.Source });
                }
            }
        }
        if (slot is not null)
        {
            slot.State[MinionContract.SlotState] = 1;
        }
        service.AddContract(contract);
        return contract;
    }

    /// <summary>
    /// The owner's weave slot the cast came from: the context's slot when the runtime knows it, else the slot
    /// holding the summoning weave.
    /// </summary>
    private static WeaveSlot? SlotOf(EffectContext ctx)
    {
        if (ctx.Slot is { } known)
        {
            return known;
        }
        if (HostOf(ctx) is not { } host || !ctx.Sim.World.TryGet<WeaveBook>(ctx.Origin, out var book))
        {
            return null;
        }
        foreach (var slot in book.Slots)
        {
            if (ReferenceEquals(slot.Compiled?.Root, host))
            {
                return slot;
            }
        }
        return null;
    }

    private void ScheduleWaves(EffectContext ctx, MinionService service)
    {
        var waves = (int)Math.Round(ctx.Eval(Waves!.Count, 1));
        if (waves <= 1)
        {
            return;
        }
        var interval = Math.Max(ctx.Sim.Clock.Dt, ctx.Eval(Waves.Interval, 1));
        var factor = Waves.CostFactor is null ? 0 : Math.Max(0, ctx.Eval(Waves.CostFactor, 0));
        var dry = new WaveRun();
        for (var k = 1; k < waves; k++)
        {
            var later = ctx.Fork();
            ctx.Sim.Scheduler.After(interval * k, () => LaterWave(later, service, factor, dry));
        }
    }

    private sealed class WaveRun
    {
        public bool Dry;
    }

    private void LaterWave(EffectContext ctx, MinionService service, double factor, WaveRun run)
    {
        var world = ctx.Sim.World;
        if (run.Dry || !world.IsAlive(ctx.Origin) || world.Has<Dead>(ctx.Origin))
        {
            return;
        }
        if (factor > 0 && ctx.Weave is { } weave && world.TryGet<Stats>(ctx.Origin, out var stats))
        {
            var costs = ctx.Sim.Service<WeaveRuntime>().Costs(weave, stats);
            foreach (var (resource, cost) in costs)
            {
                if (stats.Resource(resource) < cost * factor - 1e-6)
                {
                    run.Dry = true;
                    return;
                }
            }
            foreach (var (resource, cost) in costs)
            {
                stats.AddResource(resource, -cost * factor);
            }
        }
        Wave(ctx, service, null);
    }

    /// <summary>One wave: makes room under the capacity, dismisses an exclusive rival, then gives birth.</summary>
    public void Wave(EffectContext ctx, MinionService service, MinionContract? contract)
    {
        var sim = ctx.Sim;
        var owner = ctx.Origin;
        var ownerStats = sim.World.Get<Stats>(owner);
        var count = Math.Clamp((int)Math.Round(ctx.Eval(Count, 1)), 1, (int)sim.Content.Rules.Number("minions.max_per_cast", 32));
        List<Entity>? corpses = null;
        if (FromCorpse is not null)
        {
            corpses = service.RentList();
            FindCorpses(ctx, count, corpses);
            if (corpses.Count > 0)
            {
                count = corpses.Count;
            }
            else if (Pattern is null && Pool.Length == 0)
            {
                service.ReturnList(corpses);
                sim.Events.Publish(new SummonFizzled(owner, HostOf(ctx)?.Key, "no_corpse") { Tick = sim.Clock.Tick });
                return;
            }
        }
        var group = GroupFor(ctx);
        service.MakeRoom(owner, group, PoolGroup, Capacity(ctx, ownerStats), count);
        if (ExclusiveGroup is not null)
        {
            service.DismissExclusive(owner, ExclusiveGroup, group);
        }
        var ring = sim.Content.Rules.Number("minions.ring", 1.2);
        var center = Place.Center(ctx);
        for (var i = 0; i < count; i++)
        {
            if (corpses is { Count: > 0 })
            {
                var corpse = corpses[i];
                if (Corpses.PatternOf(sim.World, corpse) is { } kind && sim.World.TryGet<Transform>(corpse, out var at))
                {
                    Birth(ctx, service, owner, ownerStats, RaiseAs ?? kind, null, group, at.Position, contract, corpse);
                }
                continue;
            }
            var entry = Pick(ctx.Rng);
            var pattern = entry?.Pattern ?? Pattern!;
            Birth(ctx, service, owner, ownerStats, pattern, entry, group, Place.Position(ctx, center, i, count, ring), contract, Entity.None);
        }
        if (corpses is not null)
        {
            service.ReturnList(corpses);
        }
    }

    /// <summary>
    /// How many bodies may stand at once: <c>max_active</c>, or with a pool group the owner's
    /// <c>minion.max</c> based on it, folded with the weave's tags plus <c>pool.{group}</c>.
    /// </summary>
    public int Capacity(EffectContext ctx, Stats? ownerStats)
    {
        var max = Math.Max(1, (int)Math.Round(ctx.Eval(MaxActive, 8)));
        if (PoolGroup is null || ownerStats is null)
        {
            return max;
        }
        var host = HostOf(ctx);
        var context = (host?.Tags ?? TagSet.Empty).With("pool." + PoolGroup);
        return Math.Max(1, (int)Math.Round(ownerStats.Attributes.Get("minion.max", ownerStats, context, host?.Modifiers, max)));
    }

    /// <summary>A pool entry by weight from the cast's random stream; null for a single pattern.</summary>
    private PoolEntry? Pick(Rng rng)
    {
        if (Pool.Length == 0)
        {
            return null;
        }
        var i = rng.PickWeighted(Weights);
        return Pool[Math.Max(0, i)];
    }

    private void FindCorpses(EffectContext ctx, int count, List<Entity> into)
    {
        var world = ctx.Sim.World;
        if (FromCorpse == "target")
        {
            // A cast drops a dead target from its context and aims at where it lay, so a targeted corpse is
            // the context's target when it still is one, else the corpse lying at the point.
            if (Corpses.Is(world, ctx.Target))
            {
                into.Add(ctx.Target);
                return;
            }
            var reach = (float)ctx.Sim.Content.Rules.Number("minions.corpse_target_reach", 0.5);
            var at = Entity.None;
            var nearest = float.MaxValue;
            foreach (var (e, _, d) in ctx.Sim.Spatial.Within(ctx.Point, reach))
            {
                if (d < nearest && Corpses.Is(world, e))
                {
                    at = e;
                    nearest = d;
                }
            }
            if (!at.IsNone)
            {
                into.Add(at);
            }
            return;
        }
        var radius = (float)(SearchRadius is null ? ctx.Sim.Content.Rules.Number("minions.corpse_search", 6) : ctx.Eval(SearchRadius, 6));
        var center = ctx.Focus;
        for (var n = 0; n < count; n++)
        {
            var best = Entity.None;
            var bestDistance = float.MaxValue;
            foreach (var (e, _, d) in ctx.Sim.Spatial.Within(center, radius))
            {
                if (d < bestDistance && Corpses.Is(world, e) && !into.Contains(e))
                {
                    best = e;
                    bestDistance = d;
                }
            }
            if (best.IsNone)
            {
                return;
            }
            into.Add(best);
        }
    }

    /// <summary>
    /// Whether raising a corpse consumes it: as the summoning weave's form says when it targets corpses
    /// (<c>target: { kind: corpse, consume }</c>), else always.
    /// </summary>
    internal static bool ConsumesCorpses(EffectContext ctx) => HostOf(ctx)?.Form.Target is not { Kind: "corpse" } spec || spec.Consume;

    /// <summary>
    /// Raises one body: spawns the pattern at the owner's level, makes it the owner's minion of the group,
    /// gives it its tags, behavior, inheritance and terms, fills its resources, consumes the corpse it rose
    /// from, and runs the birth effects on it.
    /// </summary>
    internal Entity Birth(EffectContext ctx, MinionService service, Entity owner, Stats? ownerStats, string pattern, PoolEntry? entry, string group, Vector3 at, MinionContract? contract, Entity corpse)
    {
        var sim = ctx.Sim;
        var e = sim.Service<PatternFactory>().Spawn(sim, pattern, at, new SpawnOptions { Owner = owner, Level = ownerStats?.Level ?? ctx.SourceStats?.Level });
        if (e.IsNone)
        {
            return e;
        }
        // A summoned copy carries the combat kit, never the wild encounter reward.
        sim.World.Remove<Drops>(e);
        var now = sim.Clock.Time;
        var lifetime = contract is null && Lifetime is not null ? ctx.Eval(Lifetime, 20) : 0;
        var host = HostOf(ctx);
        var minion = sim.World.Add(e, new Minion
        {
            Owner = owner,
            ExpiresAt = lifetime > 0 ? now + lifetime : double.PositiveInfinity,
            Group = group,
            PoolGroup = PoolGroup,
            ExclusiveGroup = ExclusiveGroup,
            Weave = host?.Key,
            BornAt = now,
            Sequence = service.NextSequence(),
            Construct = Construct,
            InheritCaster = InheritCaster,
            Contract = contract,
            Presence = Presence,
            Recipe = OnDeath.Length > 0 ? this : null,
            SummonWeave = OnDeath.Length > 0 ? host : null,
        });
        if (Presence is not null)
        {
            service.PresencesSeen = true;
        }
        var stats = sim.World.Get<Stats>(e);
        // Anatomy stays; encounter-only classifiers do not describe an owned body.
        if (sim.World.TryGet<Identity>(e, out var ownedIdentity))
        {
            ownedIdentity.Tags = service.OwnedTags(ownedIdentity.Tags);
            if (stats is not null) stats.Tags = service.OwnedTags(stats.Tags);
        }
        var tags = service.BodyTags(Construct);
        if (entry is { Tags.IsEmpty: false })
        {
            tags = tags.Union(entry.Tags);
        }
        if (!tags.IsEmpty && sim.World.TryGet<Identity>(e, out var identity))
        {
            identity.Tags = identity.Tags.Union(tags);
            if (stats is not null)
            {
                stats.Tags = stats.Tags.Union(tags);
            }
        }
        var behavior = entry?.Behavior ?? Behavior ?? (DefaultBehavior ? service.DefaultBehavior : null);
        if (behavior is not null)
        {
            SetBehavior(sim, e, behavior, at);
        }
        if (host is not null)
        {
            CrewForwarding.Board(sim, e, host, Construct);
        }
        if (stats is not null)
        {
            if (Inherit && ownerStats is not null)
            {
                service.Inherit(minion, stats, owner, ownerStats, host);
            }
            stats.FillResources();
            if (Decay is not null)
            {
                minion.Decay = new MinionDecay
                {
                    StartsAt = now + Math.Max(0, ctx.Eval(Decay.Delay, 0)),
                    Rate = stats.ResourceMax("life") * Math.Max(0, ctx.Eval(Decay.Fraction, 0.05)),
                    Growth = Math.Max(0, ctx.Eval(Decay.Growth, 1.1)),
                };
            }
        }
        if (Intercept is not null)
        {
            sim.World.Add(e, new Intercept
            {
                Radius = (float)Math.Max(0.5, ctx.Eval(Intercept.Radius, 3)),
                Mode = Intercept.Mode,
                Slow = (float)Math.Clamp(ctx.Eval(Intercept.Slow, 0.35), 0, 1),
            });
        }
        service.Track(owner);
        if (!corpse.IsNone)
        {
            sim.Events.Publish(new CorpseRaised(corpse, e, pattern) { Tick = sim.Clock.Tick });
            if (ConsumesCorpses(ctx))
            {
                service.Dismiss(corpse);
            }
        }
        sim.Events.Publish(new MinionSummoned(e, owner, group, minion.Weave, Construct) { Tick = sim.Clock.Tick });
        if (OnBirth.Length > 0)
        {
            CompiledEffect.ExecuteAll(OnBirth, ctx.WithTarget(e).Deeper());
        }
        return e;
    }

    /// <summary>
    /// Runs the death effects of a body that just died, where it fell: as its owner while the owner stands
    /// (else as the body), in the weave that raised it, one deeper than the killing hit, with
    /// <c>body_life_max</c> set. A martyr's burst, a breakable wall's shards.
    /// </summary>
    internal void Died(Simulation sim, Entity body, Minion minion, Vector3 at, int depth)
    {
        var source = !minion.Owner.IsNone && sim.World.IsAlive(minion.Owner) && sim.World.Has<Stats>(minion.Owner) ? minion.Owner : body;
        var weave = minion.SummonWeave;
        CompiledEffect.ExecuteAll(OnDeath, new EffectContext
        {
            Sim = sim,
            Source = source,
            Point = at,
            From = at,
            Weave = weave,
            Tags = weave?.Tags ?? TagSet.Empty,
            Depth = depth + 1,
            Rng = sim.RngFor(body, "on_death"),
            Vars = new Dictionary<string, double>(StringComparer.Ordinal) { [BodyLifeMax] = sim.World.Get<Stats>(body)?.ResourceMax("life") ?? 0 },
        });
    }

    /// <summary>Gives a body a behavior of its own in place of its pattern's, starting from its first state.</summary>
    internal static void SetBehavior(Simulation sim, Entity e, string behavior, Vector3 home)
    {
        if (!sim.Content.Has<BehaviorDef>(behavior))
        {
            sim.Log.Warn($"minion behavior '{behavior}' unknown");
            return;
        }
        if (sim.World.TryGet<Behavior>(e, out var b))
        {
            if (b.BehaviorId != behavior)
            {
                b.BehaviorId = behavior;
                b.State = "";
                b.Entered = false;
            }
            b.Home = home;
            b.Target = Entity.None;
            b.Planned = false;
            return;
        }
        sim.World.Add(e, new Behavior { BehaviorId = behavior, Home = home, NextThinkAt = sim.Clock.Time });
        sim.World.GetOrAdd<Motion>(e);
    }

    /// <summary>A context for births outside a cast (respawns, replenishment): the owner as source, at its feet, with the weave.</summary>
    internal static EffectContext ContextFor(Simulation sim, Entity owner, CompiledWeave? weave, string salt)
    {
        var point = sim.World.Get<Transform>(owner)?.Position ?? default;
        return new EffectContext
        {
            Sim = sim,
            Source = owner,
            Point = point,
            Weave = weave,
            Tags = weave?.Tags ?? TagSet.Empty,
            Rng = sim.RngFor(owner, salt),
        };
    }

    /// <summary>Brings back one body of a contract at its owner's side.</summary>
    internal Entity Respawn(MinionService service, MinionContract contract)
    {
        var sim = service.Sim;
        var ctx = ContextFor(sim, contract.Owner, contract.Weave, "contract:" + contract.Key);
        var ownerStats = sim.World.Get<Stats>(contract.Owner);
        var group = contract.Group;
        service.MakeRoom(contract.Owner, group, PoolGroup, Capacity(ctx, ownerStats), 1);
        var entry = Pick(ctx.Rng);
        var pattern = entry?.Pattern ?? Pattern;
        if (pattern is null)
        {
            return Entity.None;
        }
        var center = Place.Center(ctx);
        return Birth(ctx, service, contract.Owner, ownerStats, pattern, entry, group, Place.Position(ctx, center, 0, 1, sim.Content.Rules.Number("minions.ring", 1.2)), contract, Entity.None);
    }

    /// <summary>Raises one body of a slotted, replenishing summon when its group has room; a full group never evicts.</summary>
    internal Entity ReplenishOne(MinionService service, Entity owner, CompiledWeave weave)
    {
        var sim = service.Sim;
        var ctx = ContextFor(sim, owner, weave, "replenish:" + weave.Key);
        var ownerStats = sim.World.Get<Stats>(owner);
        var group = GroupFor(ctx);
        if (service.CountLiving(owner, group, PoolGroup) >= Capacity(ctx, ownerStats))
        {
            return Entity.None;
        }
        var entry = Pick(ctx.Rng);
        var pattern = entry?.Pattern ?? Pattern;
        if (pattern is null)
        {
            return Entity.None;
        }
        var center = Place.Center(ctx);
        return Birth(ctx, service, owner, ownerStats, pattern, entry, group, Place.Position(ctx, center, 0, 1, sim.Content.Rules.Number("minions.ring", 1.2)), null, Entity.None);
    }
}
