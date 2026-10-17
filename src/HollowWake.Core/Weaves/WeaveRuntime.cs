using System.Numerics;
using System.Runtime.CompilerServices;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Expressions;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Weaves;

/// <summary>
/// Begins, resolves and interrupts weaves for players and monsters alike. One pipeline: validate the
/// request, pay costs, start the cast or resolve instantly, then run the compiled effect graph with
/// an effect context. Monsters go through exactly the same calls as players.
/// </summary>
public sealed class WeaveRuntime
{
    private readonly WeaveCompiler _compiler;

    public WeaveRuntime(WeaveCompiler compiler)
    {
        _compiler = compiler;
    }

    public WeaveCompiler Compiler => _compiler;

    public CompiledWeave? Resolve(Simulation sim, WeaveSlot slot, out string? error)
    {
        if (slot.Compiled is not null)
        {
            error = null;
            return slot.Compiled;
        }
        if (_compiler.TryCompile(slot.Spec, out var weave, out error))
        {
            slot.Compiled = weave;
            return weave;
        }
        sim.Log.Warn($"weave {slot.Spec} failed to compile: {error}");
        return null;
    }

    /// <summary>Costs after the caster's cost multipliers, per resource.</summary>
    public Dictionary<string, double> Costs(CompiledWeave weave, Stats caster)
    {
        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        var plan = CostPlanFor(weave);
        if (plan.Length == 0)
        {
            return result;
        }
        var scope = new CasterWeaveScope(weave, caster);
        foreach (var entry in plan)
        {
            result[entry.Resource] = CostOf(weave, caster, scope, entry);
        }
        return result;
    }

    public bool CanAfford(CompiledWeave weave, Stats caster, out string? missing) => CanAfford(weave, caster, null, out missing);

    /// <summary>
    /// <see cref="CanAfford(CompiledWeave, Stats, out string?)"/> without allocating: every cost is computed
    /// first, in the weave's cost order, then each is checked in the same order, exactly as reading
    /// <see cref="Costs"/> would.
    /// </summary>
    private bool CanAfford(CompiledWeave weave, Stats caster, IExprScope? scope, out string? missing)
    {
        var plan = CostPlanFor(weave);
        if (plan.Length == 0)
        {
            missing = null;
            return true;
        }
        scope ??= new CasterWeaveScope(weave, caster);
        Span<double> costs = plan.Length <= 16 ? stackalloc double[plan.Length] : new double[plan.Length];
        for (var i = 0; i < plan.Length; i++)
        {
            costs[i] = CostOf(weave, caster, scope, plan[i]);
        }
        for (var i = 0; i < plan.Length; i++)
        {
            if (caster.Resource(plan[i].Resource) < costs[i] - 1e-6)
            {
                missing = plan[i].Resource;
                return false;
            }
        }
        missing = null;
        return true;
    }

    private static double CostOf(CompiledWeave weave, Stats caster, IExprScope scope, CostEntry entry)
    {
        var baseCost = Math.Max(0, entry.Expr.Evaluate(scope));
        var cost = caster.Attributes.Apply("cost", baseCost, caster, entry.Tags, weave.Modifiers);
        return Math.Max(0, cost);
    }

    /// <summary>One resource a weave costs: its formula and the tag context the caster's cost stack sees.</summary>
    private readonly record struct CostEntry(string Resource, Expr Expr, TagSet Tags);

    // A compiled weave never changes, so its cost entries and their tag contexts are built once.
    private readonly ConditionalWeakTable<CompiledWeave, CostEntry[]> _costPlans = new();

    private CostEntry[] CostPlanFor(CompiledWeave weave) => _costPlans.GetValue(weave, static w =>
    {
        var entries = new List<CostEntry>();
        foreach (var (resource, expr) in w.Costs)
        {
            entries.Add(new CostEntry(resource, expr, w.Tags.With("cost." + resource)));
        }
        return entries.ToArray();
    });

    /// <summary>The slot's cached scopes for its compiled weave and caster, rebuilt when either changes.</summary>
    private static WeaveSlotCache CacheFor(WeaveSlot slot, CompiledWeave weave, Stats caster)
    {
        var cache = slot.RuntimeCache;
        if (cache is null || !ReferenceEquals(cache.Weave, weave) || !ReferenceEquals(cache.Caster, caster))
        {
            var scope = new CasterWeaveScope(weave, caster);
            cache = new WeaveSlotCache(weave, caster, scope, new PerceptionWeaveScope(scope.Weave));
            slot.RuntimeCache = cache;
        }
        return cache;
    }

    /// <summary><see cref="CompiledWeave.Range"/> reading the slot's cached scope instead of building one.</summary>
    private static double RangeOf(CompiledWeave weave, Stats caster, WeaveSlotCache cache) =>
        weave.Form.Range is null ? weave.Range(caster) : weave.Form.Range.Evaluate(cache.Scope);

    public double CastDuration(CompiledWeave weave, Stats caster)
    {
        var time = weave.CastTime(caster);
        if (time <= 0)
        {
            return 0;
        }
        var speedAttr = weave.Tags.Has("attack") ? "attack.speed" : "cast.speed";
        var speed = Math.Max(0.1, caster.Attributes.Get(speedAttr, caster, weave.Tags, weave.Modifiers));
        return time / speed;
    }

    public bool TryBegin(Simulation sim, Entity caster, WeaveSlot slot, Entity target, Vector3 point, Vector2 direction, out string reason)
    {
        reason = "";
        if (!sim.World.TryGet<Stats>(caster, out var stats) || sim.World.Has<Dead>(caster))
        {
            reason = "dead";
            return false;
        }
        var book = sim.World.Get<WeaveBook>(caster);
        if (book is null)
        {
            reason = "no_weaves";
            return false;
        }
        var weave = Resolve(sim, slot, out var error);
        if (weave is null)
        {
            reason = "invalid:" + error;
            return Fail(sim, caster, slot, reason);
        }
        var now = sim.Clock.Time;
        if (stats.HasFlag("stunned") || stats.HasFlag("frozen"))
        {
            reason = "stunned";
            return Fail(sim, caster, slot, reason);
        }
        var forbid = weave.ForbidFlags;
        for (var i = 0; i < forbid.Count; i++)
        {
            if (stats.HasFlag(forbid[i]))
            {
                reason = "forbidden:" + weave.Tags.Items[i];
                return Fail(sim, caster, slot, reason);
            }
        }
        if (book.Casting is not null)
        {
            reason = "busy";
            return false;
        }
        if (!book.IsReady(slot, now) || book.GlobalReadyAt > now)
        {
            reason = "cooldown";
            return false;
        }
        if (!CanAfford(weave, stats, out var missing))
        {
            reason = "cost:" + missing;
            return Fail(sim, caster, slot, reason);
        }
        if (!ValidateTarget(sim, caster, stats, weave, ref target, ref point, ref direction, out reason))
        {
            return Fail(sim, caster, slot, reason);
        }

        foreach (var (resource, cost) in Costs(weave, stats))
        {
            stats.AddResource(resource, -cost);
        }
        var duration = CastDuration(weave, stats);
        var cooldownAtPress = sim.Content.Rules.Text("combat.cooldown_at", "complete") == "press";
        if (cooldownAtPress)
        {
            StartCooldown(sim, book, slot, weave, stats);
        }
        if (sim.World.TryGet<Transform>(caster, out var t))
        {
            var face = target.IsNone ? point : (sim.World.Get<Transform>(target)?.Position ?? point);
            if (direction.LengthSquared() > 0.001f && target.IsNone && weave.Targeting == Targeting.Direction)
            {
                t.Yaw = MathF.Atan2(direction.Y, direction.X);
            }
            else
            {
                t.Face(face);
            }
        }
        var state = new CastState
        {
            Slot = slot,
            Target = target,
            Point = point,
            Direction = direction,
            StartedAt = now,
            Duration = duration,
            Channeling = weave.CastMode == CastMode.Channel,
            NextChannelAt = now,
            MoveFactor = MoveFactorOf(weave),
        };
        if (weave.CastMode == CastMode.Instant || (duration <= 0 && weave.CastMode != CastMode.Channel))
        {
            Execute(sim, caster, state, weave, cooldownAtPress);
            return true;
        }
        book.Casting = state;
        sim.Events.Publish(new WeaveStarted(caster, weave.Key, target, point, duration) { Tick = sim.Clock.Tick });
        return true;
    }

    /// <summary>
    /// The fraction of movement speed a caster keeps while casting the weave: one for weaves tagged
    /// <c>mobile</c>, zero (rooted) for the rest. The movement system reads it from the cast state.
    /// </summary>
    public static float MoveFactorOf(CompiledWeave weave) => weave.Tags.Has("mobile") ? 1f : 0f;

    private static bool Fail(Simulation sim, Entity caster, WeaveSlot slot, string reason)
    {
        sim.Events.Publish(new WeaveFailed(caster, slot.Key, reason) { Tick = sim.Clock.Tick });
        return false;
    }

    private bool ValidateTarget(Simulation sim, Entity caster, Stats stats, CompiledWeave weave, ref Entity target, ref Vector3 point, ref Vector2 direction, out string reason)
    {
        reason = "";
        var ct = sim.World.Get<Transform>(caster);
        var range = weave.Range(stats);
        switch (weave.Targeting)
        {
            case Targeting.Self:
                target = caster;
                point = ct?.Position ?? point;
                return true;
            case Targeting.Enemy:
            case Targeting.Ally:
            case Targeting.Any:
            {
                if (target.IsNone || !sim.World.IsAlive(target) || sim.World.Has<Dead>(target))
                {
                    reason = "no_target";
                    return false;
                }
                if (weave.Targeting == Targeting.Enemy && !Relations.IsHostile(sim, caster, target))
                {
                    reason = "not_hostile";
                    return false;
                }
                if (weave.Targeting == Targeting.Ally && Relations.IsHostile(sim, caster, target))
                {
                    reason = "not_ally";
                    return false;
                }
                var tt = sim.World.Get<Transform>(target);
                if (ct is not null && tt is not null && range > 0 && ct.GroundDistanceTo(tt.Position) > range + tt.Radius + ct.Radius)
                {
                    reason = "out_of_range";
                    return false;
                }
                point = tt?.Position ?? point;
                return true;
            }
            case Targeting.Ground:
            {
                target = Entity.None;
                if (ct is not null && range > 0)
                {
                    var d = ct.GroundDistanceTo(point);
                    if (d > range)
                    {
                        // Clamp to range instead of refusing: feels better for click casting.
                        var dir = new Vector2(point.X - ct.Position.X, point.Y - ct.Position.Y) / d;
                        point = new Vector3(ct.Position.X + dir.X * (float)range, ct.Position.Y + dir.Y * (float)range, point.Z);
                        point = sim.Terrain.SnapToGround(point, 3);
                    }
                }
                return true;
            }
            case Targeting.Direction:
            {
                target = Entity.None;
                if (direction.LengthSquared() < 0.0001f && ct is not null)
                {
                    direction = new Vector2(point.X - ct.Position.X, point.Y - ct.Position.Y);
                    if (direction.LengthSquared() < 0.0001f)
                    {
                        direction = ct.Forward;
                    }
                }
                direction = Vector2.Normalize(direction);
                return true;
            }
        }
        return true;
    }

    private void StartCooldown(Simulation sim, WeaveBook book, WeaveSlot slot, CompiledWeave weave, Stats stats)
    {
        var cooldown = weave.Cooldown(stats);
        if (cooldown > 0)
        {
            var recovery = Math.Max(0.1, stats.Attributes.Get("cooldown.recovery", stats, weave.Tags, weave.Modifiers));
            book.ReadyAt[slot.Key] = sim.Clock.Time + cooldown / recovery;
        }
        var gcd = sim.Content.Rules.Number("combat.global_cooldown", 0);
        if (gcd > 0)
        {
            book.GlobalReadyAt = sim.Clock.Time + gcd;
        }
    }

    /// <summary>Resolves a cast now: runs the effects and emits the event.</summary>
    public void Execute(Simulation sim, Entity caster, CastState state, CompiledWeave weave, bool cooldownAlreadyStarted)
    {
        if (!sim.World.TryGet<Stats>(caster, out var stats))
        {
            return;
        }
        var book = sim.World.Get<WeaveBook>(caster);
        if (book is not null && !cooldownAlreadyStarted)
        {
            StartCooldown(sim, book, state.Slot, weave, stats);
        }
        var ct = sim.World.Get<Transform>(caster);
        var target = state.Target;
        if (!target.IsNone && (!sim.World.IsAlive(target) || sim.World.Has<Dead>(target)))
        {
            target = Entity.None;
        }
        var ctx = new EffectContext
        {
            Sim = sim,
            Source = caster,
            Target = target,
            Point = target.IsNone ? state.Point : (sim.World.Get<Transform>(target)?.Position ?? state.Point),
            Direction = state.Direction,
            Weave = weave,
            Tags = weave.Tags,
            Rng = sim.RngFor(caster, "weave:" + weave.Key + ":" + state.Pulses),
            Potency = 1,
        };
        stats.Stamp("cast", sim.Clock.Time);
        if (weave.Tags.Has("movement"))
        {
            stats.Stamp("moved", sim.Clock.Time);
        }
        CompiledEffect.ExecuteAll(weave.Effects, ctx);
        sim.Events.Publish(new WeaveCast(caster, weave.Key, target, ctx.Point, weave.Tags) { Tick = sim.Clock.Tick });
        _ = ct;
    }

    public void Interrupt(Simulation sim, Entity caster)
    {
        if (sim.World.TryGet<WeaveBook>(caster, out var book) && book.Casting is not null)
        {
            var key = book.Casting.Slot.Key;
            book.Casting = null;
            sim.Events.Publish(new WeaveInterrupted(caster, key) { Tick = sim.Clock.Tick });
        }
    }

    /// <summary>Picks the best ready weave for an AI toward a target, honoring hints and range.</summary>
    public (WeaveSlot Slot, CompiledWeave Weave)? ChooseForAi(Simulation sim, Entity caster, Entity target, IExprScope? perception = null)
    {
        if (!sim.World.TryGet<WeaveBook>(caster, out var book) || !sim.World.TryGet<Stats>(caster, out var stats))
        {
            return null;
        }
        var ct = sim.World.Get<Transform>(caster);
        var tt = sim.World.Get<Transform>(target);
        var distance = ct is not null && tt is not null ? ct.GroundDistanceTo(tt.Position) : 0;
        var best = default((WeaveSlot, CompiledWeave)?);
        double bestScore = double.NegativeInfinity;
        var rng = sim.RngFor(caster, "ai-choose");
        foreach (var slot in book.Slots)
        {
            var weave = Resolve(sim, slot, out _);
            if (weave is null || !book.IsReady(slot, sim.Clock.Time))
            {
                continue;
            }
            var cache = CacheFor(slot, weave, stats);
            if (!CanAfford(weave, stats, cache.Scope, out _))
            {
                continue;
            }
            var hint = weave.Ai;
            var range = RangeOf(weave, stats, cache);
            if (weave.Targeting is Targeting.Enemy or Targeting.Ground or Targeting.Direction && range > 0 && distance > range + (tt?.Radius ?? 0) + (ct?.Radius ?? 0))
            {
                continue;
            }
            if (hint?.When is not null)
            {
                // Hints read the AI's perception (nearest_enemy_distance, life_pct ...) with the weave mounted.
                bool holds;
                if (perception is null)
                {
                    holds = hint.When.IsTrue(cache.Scope);
                }
                else
                {
                    cache.Hint.Perception = perception;
                    try
                    {
                        holds = hint.When.IsTrue(cache.Hint);
                    }
                    finally
                    {
                        cache.Hint.Perception = EmptyScope.Instance;
                    }
                }
                if (!holds)
                {
                    continue;
                }
            }
            var score = (hint?.Weight ?? 1) * (0.75 + rng.NextDouble() * 0.5);
            if (score > bestScore)
            {
                bestScore = score;
                best = (slot, weave);
            }
        }
        return best;
    }

    /// <summary>The longest range among an entity's weaves, for AI positioning.</summary>
    public double PreferredRange(Simulation sim, Entity caster)
    {
        if (!sim.World.TryGet<WeaveBook>(caster, out var book) || !sim.World.TryGet<Stats>(caster, out var stats))
        {
            return 1.5;
        }
        double best = 0;
        foreach (var slot in book.Slots)
        {
            var weave = Resolve(sim, slot, out _);
            if (weave is null)
            {
                continue;
            }
            var r = weave.Ai?.PreferredRange ?? Math.Max(1.2, RangeOf(weave, stats, CacheFor(slot, weave, stats)) * 0.8);
            best = Math.Max(best, r);
        }
        return best <= 0 ? 1.5 : best;
    }
}

/// <summary>
/// What the runtime keeps on a weave slot so AI choice and cost checks build no scopes: the caster's view
/// of the compiled weave and the weave mounted over an AI's perception. Rebuilt when the slot's compiled
/// weave or its caster changes; holds no state of its own.
/// </summary>
internal sealed class WeaveSlotCache
{
    public readonly CompiledWeave Weave;
    public readonly Stats Caster;
    public readonly CasterWeaveScope Scope;
    public readonly PerceptionWeaveScope Hint;

    public WeaveSlotCache(CompiledWeave weave, Stats caster, CasterWeaveScope scope, PerceptionWeaveScope hint)
    {
        Weave = weave;
        Caster = caster;
        Scope = scope;
        Hint = hint;
    }
}

/// <summary>
/// The scope <see cref="CompiledWeave.ScopeFor"/> builds, without its dictionaries: <c>level</c> is the
/// caster's level, <c>weave.*</c> resolves through the weave, everything else through the caster.
/// </summary>
internal sealed class CasterWeaveScope : IExprScope
{
    private readonly Stats _caster;

    public CasterWeaveScope(CompiledWeave weave, Stats caster)
    {
        _caster = caster;
        Weave = new WeaveScope(weave, caster, weave.Tags);
    }

    public WeaveScope Weave { get; }

    public bool TryResolve(string name, out double value)
    {
        if (name == "level")
        {
            value = _caster.Level;
            return true;
        }
        return TryWeave(name, out var rest) ? Weave.TryResolve(rest, out value) : _caster.TryResolve(name, out value);
    }

    public bool HasFlag(string flag) => TryWeave(flag, out var rest) ? Weave.HasFlag(rest) : _caster.HasFlag(flag);

    public double NextRandom() => _caster.NextRandom();

    /// <summary>Whether a name is mounted under <c>weave</c>: the text before its first dot, as an overlay splits it.</summary>
    internal static bool TryWeave(string name, out string rest)
    {
        if (name.Length >= 6 && name[5] == '.' && name.StartsWith("weave", StringComparison.Ordinal))
        {
            rest = name.Substring(6);
            return true;
        }
        rest = "";
        return false;
    }
}

/// <summary>
/// An AI's perception with a weave mounted as <c>weave.</c>, for hint conditions: what an overlay of the
/// perception with that mount resolves, reused across thinks with the perception swapped in.
/// </summary>
internal sealed class PerceptionWeaveScope : IExprScope
{
    private readonly WeaveScope _weave;

    public PerceptionWeaveScope(WeaveScope weave)
    {
        _weave = weave;
    }

    public IExprScope Perception { get; set; } = EmptyScope.Instance;

    public bool TryResolve(string name, out double value) =>
        CasterWeaveScope.TryWeave(name, out var rest) ? _weave.TryResolve(rest, out value) : Perception.TryResolve(name, out value);

    public bool HasFlag(string flag) => CasterWeaveScope.TryWeave(flag, out var rest) ? _weave.HasFlag(rest) : Perception.HasFlag(flag);

    public double NextRandom() => Perception.NextRandom();
}
