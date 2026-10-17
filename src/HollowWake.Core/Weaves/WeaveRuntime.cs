using System.Numerics;
using System.Runtime.CompilerServices;
using HollowWake.Core.Attributes;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Expressions;
using HollowWake.Core.Maths;
using HollowWake.Core.Sim;
using HollowWake.Core.StatusEffects;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Weaves;

/// <summary>
/// Begins, holds, resolves and interrupts weaves for players and monsters alike. One pipeline: validate the
/// request, pay costs, start the cast or resolve instantly, then run the compiled effect graph with an
/// effect context. Held modes (channels, charge-ups, overcharges, guards) advance each tick through
/// <see cref="Advance"/> and end by release, by running out, or by interruption. Monsters go through exactly
/// the same calls as players. The slot behaviors, triggers, use banks and toggles live in the partial files
/// <c>SlotBehaviors.cs</c>, <c>Triggers.cs</c>, <c>UseCharges.cs</c> and <c>Toggles.cs</c>.
/// </summary>
public sealed partial class WeaveRuntime
{
    private readonly WeaveCompiler _compiler;
    private CastingRules? _rules;

    // Casts begun through the runtime that are still held, so a cast cleared from outside (death, an effect)
    // has its guard, ramps and flags taken off on the next beat.
    private readonly List<LiveCast> _live = new();

    public WeaveRuntime(WeaveCompiler compiler)
    {
        _compiler = compiler;
    }

    public WeaveCompiler Compiler => _compiler;

    /// <summary>The casting rules of the simulation's content, resolved once per ruleset.</summary>
    internal CastingRules RulesOf(Simulation sim)
    {
        var rules = sim.Content.Rules;
        var cached = _rules;
        if (cached is null || !ReferenceEquals(cached.Source, rules))
        {
            cached = new CastingRules(rules);
            _rules = cached;
        }
        return cached;
    }

    private readonly record struct LiveCast(Entity Caster, WeaveBook Book, CastState Cast);

    // ------------------------------------------------------------ resolving slots

    /// <summary>
    /// The slot's compiled weave, compiling it when needed with misfit glyphs dormant (their reasons stay on
    /// the slot) and with the seat's grafted glyphs. Null when the weave cannot compile at all.
    /// </summary>
    public CompiledWeave? Resolve(Simulation sim, WeaveSlot slot, out string? error)
    {
        if (slot.Compiled is not null)
        {
            error = null;
            return slot.Compiled;
        }
        if (_compiler.TryCompile(slot.Spec, out var weave, out error, GlyphFit.Dormant, slot.Grafts))
        {
            slot.Compiled = weave;
            slot.Dormant = weave!.Dormant;
            return weave;
        }
        sim.Log.Warn($"weave {slot.Spec} failed to compile: {error}");
        return null;
    }

    // ------------------------------------------------------------ costs

    /// <summary>Costs after the caster's cost modifiers, added costs and conversions, per resource.</summary>
    public Dictionary<string, double> Costs(CompiledWeave weave, Stats caster)
    {
        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        var plan = PlanFor(weave.Root);
        if (plan.Costs.Length == 0)
        {
            return result;
        }
        var scope = new CasterWeaveScope(weave, caster);
        Span<double> amounts = stackalloc double[plan.Resources.Length];
        ComputeCosts(plan, plan.Costs, caster, scope, weave.Modifiers, amounts, 1);
        foreach (var entry in plan.Costs)
        {
            if (!entry.Implicit)
            {
                result[entry.Resource] = amounts[entry.Index];
            }
        }
        for (var i = 0; i < amounts.Length; i++)
        {
            if (amounts[i] > 0)
            {
                result[plan.Resources[i]] = amounts[i];
            }
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
        var plan = PlanFor(weave.Root);
        if (plan.Costs.Length == 0)
        {
            missing = null;
            return true;
        }
        scope ??= new CasterWeaveScope(weave, caster);
        Span<double> amounts = stackalloc double[plan.Resources.Length];
        ComputeCosts(plan, plan.Costs, caster, scope, weave.Modifiers, amounts, 1);
        return Affords(plan, plan.Costs, caster, amounts, out missing);
    }

    /// <summary>Checks and pays a cost list. False, and nothing paid, when the caster cannot afford it.</summary>
    internal bool TryPay(Simulation sim, CompiledWeave weave, Stats caster, IExprScope scope, CostEntry[] entries, double scale, out string? missing)
    {
        if (entries.Length == 0)
        {
            missing = null;
            return true;
        }
        var plan = PlanFor(weave.Root);
        Span<double> amounts = stackalloc double[plan.Resources.Length];
        ComputeCosts(plan, entries, caster, scope, weave.Modifiers, amounts, scale);
        if (!Affords(plan, entries, caster, amounts, out missing))
        {
            return false;
        }
        Pay(sim, plan, entries, caster, amounts);
        return true;
    }

    private static bool Affords(WeavePlan plan, CostEntry[] entries, Stats caster, Span<double> amounts, out string? missing)
    {
        foreach (var entry in entries)
        {
            if (caster.Resource(entry.Resource) < amounts[entry.Index] - 1e-6)
            {
                missing = entry.Resource;
                return false;
            }
        }
        // Resources only a conversion put a cost on.
        for (var i = 0; i < amounts.Length; i++)
        {
            if (amounts[i] > 0 && caster.Resource(plan.Resources[i]) < amounts[i] - 1e-6)
            {
                missing = plan.Resources[i];
                return false;
            }
        }
        missing = null;
        return true;
    }

    private void Pay(Simulation sim, WeavePlan plan, CostEntry[] entries, Stats caster, Span<double> amounts)
    {
        foreach (var entry in entries)
        {
            if (amounts[entry.Index] > 0)
            {
                caster.AddResource(entry.Resource, -amounts[entry.Index]);
            }
        }
        for (var i = 0; i < amounts.Length; i++)
        {
            if (amounts[i] > 0 && !plan.IsCostResource(i, entries))
            {
                caster.AddResource(plan.Resources[i], -amounts[i]);
            }
        }
        if (plan.StampCosts || RulesOf(sim).StampCosts)
        {
            for (var i = 0; i < amounts.Length; i++)
            {
                caster.SetGauge(plan.LastCostGauges[i], amounts[i]);
            }
        }
    }

    /// <summary>
    /// Each entry's cost: its formula plus <c>cost.added.{resource}</c>, through the caster's <c>cost</c> fold
    /// scoped by the resource and the weave's tags, then moved in part to other resources by
    /// <c>cost.convert.{from}.{to}</c>. Families nothing modifies are not read.
    /// </summary>
    private static void ComputeCosts(WeavePlan plan, CostEntry[] entries, Stats caster, IExprScope scope, IReadOnlyList<Modifier> extras, Span<double> amounts, double scale)
    {
        amounts.Clear();
        var attributes = caster.Attributes;
        foreach (var entry in entries)
        {
            var added = entry.AddedInWeave || attributes.HasModifiers(entry.Added);
            if (entry.Implicit && !added)
            {
                continue;
            }
            var baseCost = Math.Max(0, entry.Expr.Evaluate(scope)) * scale;
            if (added)
            {
                baseCost += attributes.Get(entry.Added, caster, entry.Tags, extras);
            }
            var cost = Math.Max(0, attributes.Apply("cost", Math.Max(0, baseCost), caster, entry.Tags, extras));
            var remaining = cost;
            var converts = entry.Converts;
            for (var t = 0; t < converts.Length; t++)
            {
                var attr = converts[t];
                if (attr is null || !(entry.ConvertInWeave[t] || attributes.HasModifiers(attr)))
                {
                    continue;
                }
                var fraction = Math.Clamp(attributes.Get(attr, caster, entry.Tags, extras), 0, 1);
                var moved = Math.Min(remaining, cost * fraction);
                if (moved > 0)
                {
                    amounts[t] += moved;
                    remaining -= moved;
                }
            }
            amounts[entry.Index] += remaining;
        }
    }

    // A compiled weave never changes, so its cost entries and their tag contexts are built once.
    private readonly ConditionalWeakTable<CompiledWeave, WeavePlan> _plans = new();
    private ConditionalWeakTable<CompiledWeave, WeavePlan>.CreateValueCallback? _newPlan;

    internal WeavePlan PlanFor(CompiledWeave weave) =>
        _plans.TryGetValue(weave, out var plan) ? plan : _plans.GetValue(weave, _newPlan ??= w => new WeavePlan(w, _compiler.Content));

    // ------------------------------------------------------------ timing

    /// <summary>The slot's cached scopes for its compiled weave and caster, rebuilt when either changes.</summary>
    internal static WeaveSlotCache CacheFor(WeaveSlot slot, CompiledWeave weave, Stats caster)
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

    /// <summary>The speed attribute a weave's times divide by: <c>attack.speed</c> for attacks, <c>cast.speed</c> otherwise.</summary>
    private static double Speed(CompiledWeave weave, Stats caster) =>
        Math.Max(0.1, caster.Attributes.Get(weave.Tags.Has("attack") ? "attack.speed" : "cast.speed", caster, weave.Tags, weave.Modifiers));

    public double CastDuration(CompiledWeave weave, Stats caster)
    {
        var time = weave.CastTime(caster);
        if (time <= 0)
        {
            return 0;
        }
        return time / Speed(weave, caster);
    }

    private static double CastDuration(CompiledWeave weave, Stats caster, IExprScope scope)
    {
        var time = weave.Form.CastTime is null ? 0 : Math.Max(0, weave.Form.CastTime.Evaluate(scope));
        return time <= 0 ? 0 : time / Speed(weave, caster);
    }

    /// <summary>
    /// The fraction of movement speed a caster keeps while casting the weave, from the weave alone: one for
    /// weaves tagged <c>mobile</c>, zero (rooted) for the rest. The runtime adds the form's <c>cast_move</c>
    /// (or rule <c>casting.cast_move</c>) and attribute <c>cast.mobility</c> when a cast begins.
    /// </summary>
    public static float MoveFactorOf(CompiledWeave weave) => weave.Tags.Has("mobile") ? 1f : 0f;

    /// <summary>
    /// The fraction of movement speed kept while the cast runs: one for weaves tagged <c>mobile</c>; else the
    /// mode's own factor (a channel's or a guard's <c>move</c>), the form's <c>cast_move</c> or rule
    /// <c>casting.cast_move</c>; plus attribute <c>cast.mobility</c> in the weave's scope; clamped to zero..one.
    /// </summary>
    internal float MoveFactor(CompiledWeave weave, Stats caster, IExprScope scope, CastingRules rules)
    {
        double factor;
        if (weave.Tags.Has("mobile"))
        {
            factor = 1;
        }
        else
        {
            var form = weave.Form;
            var expr = form.CastMode switch
            {
                CastMode.Channel => form.Channel?.Move ?? form.CastMove,
                CastMode.Guard => form.Guard?.Move ?? form.CastMove,
                _ => form.CastMove,
            };
            factor = expr is null ? rules.CastMove : expr.Evaluate(scope);
        }
        factor += weave.AttrOrZero(caster, "cast.mobility");
        return (float)Math.Clamp(factor, 0, 1);
    }

    // ------------------------------------------------------------ beginning

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
        EnsureArmed(sim, caster, book, stats);
        var weave = Resolve(sim, slot, out var error);
        if (weave is null)
        {
            reason = "invalid:" + error;
            return Fail(sim, caster, slot, reason);
        }
        var now = sim.Clock.Time;
        var rules = RulesOf(sim);

        // A toggle that is on turns off; a triggered weave's key arms and disarms it.
        if (weave.IsToggle && IsToggledOn(slot))
        {
            ToggleOff(sim, caster, book, stats, slot, weave, "press");
            return true;
        }
        if (weave.IsTriggered)
        {
            ToggleArmed(sim, caster, slot, weave);
            return true;
        }

        if (stats.HasFlag("stunned") || stats.HasFlag("frozen"))
        {
            reason = "stunned";
            return Fail(sim, caster, slot, reason);
        }
        // The face: a convert or a combo step may put another weave on the slot for this press.
        var face = FaceOf(sim, book, slot, weave, stats, now);
        var forbid = face.ForbidFlags;
        for (var i = 0; i < forbid.Count; i++)
        {
            if (stats.HasFlag(forbid[i]))
            {
                reason = "forbidden:" + face.Tags.Items[i];
                return Fail(sim, caster, slot, reason);
            }
        }
        var cache = CacheFor(slot, weave, stats);
        var faceScope = ReferenceEquals(face, weave) ? cache.Scope : new CasterWeaveScope(face, stats);
        var duration = CastDuration(face, stats, faceScope);
        var resolvesNow = face.CastMode == CastMode.Instant || (duration <= 0 && face.CastMode == CastMode.Cast);
        var reflex = book.Casting is not null && resolvesNow && (face.Form.Reflex || face.AttrOrZero(stats, "reflex") > 0);
        if (book.Casting is not null && !reflex)
        {
            reason = "busy";
            return false;
        }
        if (!book.IsReady(face.Key, now) || book.GlobalReadyAt > now)
        {
            reason = "cooldown";
            return false;
        }
        var casting = face.Casting;
        if (casting.Uses is { } usesSpec && casting.Uses.Empower.Count == 0 && UsesNow(sim, slot, face, usesSpec, stats, now) < 1)
        {
            reason = "uses";
            return Fail(sim, caster, slot, reason);
        }
        for (var i = 0; i < casting.Gates.Count; i++)
        {
            if (!casting.Gates[i].IsTrue(faceScope))
            {
                reason = "gate:" + casting.Gates[i].Source;
                return Fail(sim, caster, slot, reason);
            }
        }
        if (casting.Spend is { Optional: false } spend && !HasCharges(sim, caster, spend))
        {
            reason = "charges:" + spend.Charge;
            return Fail(sim, caster, slot, reason);
        }
        if (!CanAfford(face, stats, faceScope, out var missing))
        {
            reason = "cost:" + missing;
            return Fail(sim, caster, slot, reason);
        }
        var focus = target;
        if (!ValidateTarget(sim, caster, stats, face, ref target, ref point, ref direction, out reason))
        {
            return Fail(sim, caster, slot, reason);
        }

        // Fizzle: the cast fails after paying, and the caster reels.
        var fizzle = face.AttrOrZero(stats, "cast.fizzle_chance");
        if (fizzle > 0 && RngStreams.For(sim, caster, "fizzle:", face.Key, 0).Chance(Math.Clamp(fizzle, 0, 1)))
        {
            TryPay(sim, face, stats, faceScope, PlanFor(face.Root).Costs, 1, out _);
            if (rules.FizzleDuration > 0 && sim.TryService<StatusService>(out var statuses) && sim.Content.Has<StatusDef>(rules.FizzleStatus))
            {
                statuses.Apply(sim, caster, rules.FizzleStatus, caster, new StatusApplication { Duration = rules.FizzleDuration, SourceKey = "fizzle" });
            }
            reason = "fizzled";
            return Fail(sim, caster, slot, reason);
        }

        TryPay(sim, face, stats, faceScope, PlanFor(face.Root).Costs, 1, out _);
        var usesSpent = 0;
        if (casting.Uses is { } bank)
        {
            usesSpent = SpendUses(sim, caster, slot, face, bank, stats, now);
        }
        AdvanceChain(slot, weave, now);

        var cooldownAt = (face.CooldownAt is { } at ? at == "press" : rules.CooldownAtPress) && !(face.CastMode == CastMode.Channel && face.Form.Channel?.CooldownOnEnd == true);
        if (sim.World.TryGet<Transform>(caster, out var t))
        {
            var faceAt = target.IsNone ? point : (sim.World.Get<Transform>(target)?.Position ?? point);
            if (direction.LengthSquared() > 0.001f && target.IsNone && face.Targeting == Targeting.Direction)
            {
                t.Yaw = MathF.Atan2(direction.Y, direction.X);
            }
            else
            {
                t.Face(faceAt);
            }
        }
        var state = new CastState
        {
            Slot = slot,
            Weave = face,
            Mode = face.CastMode,
            Target = target,
            Focus = focus,
            Point = point,
            Direction = direction,
            StartedAt = now,
            Duration = duration,
            Channeling = face.CastMode == CastMode.Channel,
            NextChannelAt = now,
            MoveFactor = MoveFactor(face, stats, faceScope, rules),
            UsesSpent = usesSpent,
            Scope = faceScope,
        };
        if (cooldownAt)
        {
            StartCooldown(sim, book, face, stats);
            state.CooldownStarted = true;
        }
        if (resolvesNow)
        {
            ResolveUse(sim, caster, book, stats, state, face);
            return true;
        }
        BeginHeld(sim, caster, book, stats, state, face, faceScope, rules);
        book.Casting = state;
        _live.Add(new LiveCast(caster, book, state));
        sim.Events.Publish(new WeaveStarted(caster, face.Key, target, point, state.Duration) { Tick = sim.Clock.Tick });
        return true;
    }

    /// <summary>Sets up a cast that does not resolve at once: its bar, or how its hold starts.</summary>
    private void BeginHeld(Simulation sim, Entity caster, WeaveBook book, Stats stats, CastState state, CompiledWeave weave, IExprScope scope, CastingRules rules)
    {
        var now = state.StartedAt;
        var form = weave.Form;
        var speed = Speed(weave, stats);
        switch (weave.CastMode)
        {
            case CastMode.Channel:
            {
                var channel = form.Channel;
                var windup = channel?.Windup is { } w ? Math.Max(0, w.Evaluate(scope)) / speed : 0;
                state.NextChannelAt = now + windup;
                state.MaxHold = channel?.MaxHold is { } max ? Math.Max(0, max.Evaluate(scope)) : state.Duration > 0 ? state.Duration : rules.MaxHold;
                state.Duration = state.MaxHold;
                state.Rng = RngStreams.For(sim, caster, "cast:", weave.Key, 0);
                if (channel?.Brim is not null)
                {
                    state.Brim = BrimNow(state.Slot, channel.Brim, now);
                    state.Fill = state.Brim;
                }
                stats.Flags.Add("channeling");
                stats.Attributes.Touch();
                break;
            }
            case CastMode.Charge:
            {
                var charge = form.Charge;
                var maxTime = (charge?.MaxTime is { } m ? Math.Max(0, m.Evaluate(scope)) : rules.ChargeTime) / speed;
                var fullHold = charge?.FullHold is { } fh ? Math.Max(0, fh.Evaluate(scope)) : rules.FullHold;
                state.Duration = maxTime;
                state.MaxHold = maxTime + fullHold;
                stats.Flags.Add("charging");
                stats.Attributes.Touch();
                break;
            }
            case CastMode.Overcharge:
            {
                var over = form.Overcharge;
                var stageTime = (over?.Time is { } st ? Math.Max(0.05, st.Evaluate(scope)) : rules.StageTime) / speed;
                var stages = Math.Max(1, (int)Math.Round(weave.Attr(stats, "overcharge.stages", over?.Stages ?? 3)));
                var fullHold = over?.FullHold is { } fh ? Math.Max(0, fh.Evaluate(scope)) : rules.FullHold;
                state.Interval = stageTime;
                state.Stages = stages;
                state.StageAt = now;
                state.Duration = stageTime * stages;
                state.MaxHold = stageTime * stages + fullHold + rules.MaxHold;
                stats.Flags.Add("charging");
                stats.Attributes.Touch();
                break;
            }
            case CastMode.Guard:
            {
                // Raising the guard runs the form's own graph once (drawing foes, a brace); the bash is its payload.
                RaiseGuard(sim, caster, stats, state, weave, scope, rules);
                Run(sim, caster, stats, state, weave, weave.Effects, depth: 0, publishCast: true, default, rngPrefix: "guard:");
                break;
            }
            default:
            {
                if (form.Timing is { Kind: TimingKind.Timed })
                {
                    state.Rng = RngStreams.For(sim, caster, "cast:", weave.Key, 0);
                    state.Marker = state.Duration * state.Rng.Range(0.3, 0.8);
                }
                break;
            }
        }
    }

    private static bool Fail(Simulation sim, Entity caster, WeaveSlot slot, string reason)
    {
        sim.Events.Publish(new WeaveFailed(caster, slot.Key, reason) { Tick = sim.Clock.Tick });
        return false;
    }

    private bool ValidateTarget(Simulation sim, Entity caster, Stats stats, CompiledWeave weave, ref Entity target, ref Vector3 point, ref Vector2 direction, out string reason)
    {
        if (weave.Form.Target is { } spec)
        {
            return ValidateKind(sim, caster, stats, weave, spec, ref target, ref point, ref direction, out reason);
        }
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

    /// <summary>
    /// A form's <c>target</c> block: the given body must be one its kind accepts, meet its condition and be in
    /// range; otherwise the kind searches near the aim within the search radius; otherwise the fallback
    /// decides (the caster itself, the bare aim point, or a refusal). Runs before any cost is paid.
    /// </summary>
    private static bool ValidateKind(Simulation sim, Entity caster, Stats stats, CompiledWeave weave, TargetSpec spec, ref Entity target, ref Vector3 point, ref Vector2 direction, out string reason)
    {
        reason = "";
        if (!TargetKinds.TryGet(spec.Kind, out var kind))
        {
            reason = "target_kind:" + spec.Kind;
            return false;
        }
        var ct = sim.World.Get<Transform>(caster);
        var range = weave.Range(stats);
        if (!Fits(sim, caster, stats, kind, spec, target, ct, range))
        {
            var scope = new CasterWeaveScope(weave, stats);
            var radius = (float)(spec.SearchRadius is { } r ? Math.Max(0, r.Evaluate(scope)) : Math.Max(1, range));
            var center = !target.IsNone && sim.World.TryGet<Transform>(target, out var given) ? given.Position : point;
            var found = radius > 0 ? TargetKinds.SearchFor(sim, kind, caster, target, center, radius) : Entity.None;
            if (!found.IsNone && Fits(sim, caster, stats, kind, spec, found, ct, range))
            {
                target = found;
            }
            else
            {
                switch (spec.Fallback)
                {
                    case "self":
                        target = caster;
                        break;
                    case "aim":
                        target = Entity.None;
                        return true;
                    default:
                        reason = "no_target";
                        return false;
                }
            }
        }
        if (sim.World.TryGet<Transform>(target, out var tt))
        {
            point = tt.Position;
            if (ct is not null && weave.Targeting == Targeting.Direction)
            {
                var d = new Vector2(tt.Position.X - ct.Position.X, tt.Position.Y - ct.Position.Y);
                direction = d.LengthSquared() > 1e-6f ? Vector2.Normalize(d) : ct.Forward;
            }
        }
        return true;
    }

    private static bool Fits(Simulation sim, Entity caster, Stats stats, TargetKind kind, TargetSpec spec, Entity candidate, Transform? ct, double range)
    {
        if (candidate.IsNone || !sim.World.IsAlive(candidate) || !kind.Accepts(sim, caster, candidate))
        {
            return false;
        }
        if (ct is not null && range > 0 && sim.World.TryGet<Transform>(candidate, out var tt) && ct.GroundDistanceTo(tt.Position) > range + tt.Radius + ct.Radius)
        {
            return false;
        }
        return spec.Require is null || spec.Require.IsTrue(new CandidateScope(stats, sim.World.Get<Stats>(candidate)));
    }

    /// <summary>A target condition's scope: <c>target.*</c> on the candidate, everything else on the caster.</summary>
    private sealed class CandidateScope : IExprScope
    {
        private readonly Stats _caster;
        private readonly Stats? _candidate;

        public CandidateScope(Stats caster, Stats? candidate)
        {
            _caster = caster;
            _candidate = candidate;
        }

        public bool TryResolve(string name, out double value)
        {
            if (name.Length > 7 && name[6] == '.' && name.StartsWith("target", StringComparison.Ordinal))
            {
                if (_candidate is not null)
                {
                    return _candidate.TryResolve(name.Substring(7), out value);
                }
                value = 0;
                return false;
            }
            return _caster.TryResolve(name, out value);
        }

        public bool HasFlag(string flag) =>
            flag.Length > 7 && flag[6] == '.' && flag.StartsWith("target", StringComparison.Ordinal) ? _candidate?.HasFlag(flag.Substring(7)) ?? false : _caster.HasFlag(flag);

        public double NextRandom() => _caster.NextRandom();
    }

    /// <summary>
    /// Starts a weave's cooldown: its value plus <c>cooldown.added</c>, divided by <c>cooldown.recovery</c>,
    /// keyed by the weave. A weave with a use bank keeps no cooldown between uses: its bank gates it. The
    /// global cooldown applies either way.
    /// </summary>
    internal void StartCooldown(Simulation sim, WeaveBook book, CompiledWeave weave, Stats stats)
    {
        if (weave.Casting.Uses is null)
        {
            var cooldown = CooldownOf(weave, stats);
            if (cooldown > 0)
            {
                var recovery = Math.Max(0.1, stats.Attributes.Get("cooldown.recovery", stats, weave.Tags, weave.Modifiers));
                book.ReadyAt[weave.Key] = sim.Clock.Time + cooldown / recovery;
            }
        }
        var gcd = RulesOf(sim).GlobalCooldown;
        if (gcd > 0)
        {
            book.GlobalReadyAt = Math.Max(book.GlobalReadyAt, sim.Clock.Time + gcd);
        }
    }

    /// <summary>The weave's cooldown in seconds before recovery: its form's value plus <c>cooldown.added</c>.</summary>
    internal static double CooldownOf(CompiledWeave weave, Stats stats)
    {
        var form = weave.Form.Cooldown is { } expr ? (expr.IsConstant ? expr.ConstantValue : expr.Evaluate(new CasterWeaveScope(weave, stats))) : 0;
        return Math.Max(0, form + weave.AttrOrZero(stats, "cooldown.added"));
    }

    // ------------------------------------------------------------ resolution

    /// <summary>Resolves a cast now: runs the effects and emits the event.</summary>
    public void Execute(Simulation sim, Entity caster, CastState state, CompiledWeave weave, bool cooldownAlreadyStarted)
    {
        if (!sim.World.TryGet<Stats>(caster, out var stats))
        {
            return;
        }
        if (!cooldownAlreadyStarted && sim.World.TryGet<WeaveBook>(caster, out var book))
        {
            StartCooldown(sim, book, weave.Root, stats);
            state.CooldownStarted = true;
        }
        Run(sim, caster, stats, state, weave, weave.Effects, depth: 0, publishCast: true, default);
    }

    /// <summary>
    /// A use resolving: an instant press, a timed cast completing, a charge-up or overcharge releasing, a
    /// triggered cast. Spends charges, adds the cast's local modifiers (self stacks, a timed press, an
    /// empowering use), runs the graph, then the slot behaviors: repeats, follow-ups, cycles, stacks, the
    /// recovery lock and toggles turning on.
    /// </summary>
    internal void ResolveUse(Simulation sim, Entity caster, WeaveBook book, Stats stats, CastState state, CompiledWeave weave, int depth = 0, in CastVarValues extraVars = default, bool triggered = false, string rngPrefix = "weave:")
    {
        var now = sim.Clock.Time;
        if (!state.CooldownStarted)
        {
            StartCooldown(sim, book, weave.Root, stats);
            state.CooldownStarted = true;
        }
        var vars = extraVars;
        var casting = weave.Casting;
        var modifiers = state.HasModifiers ? state.Modifiers : null;
        if (casting.Spend is { } spend)
        {
            var spent = SpendCharges(sim, caster, spend);
            vars.ChargesSpent = spent;
            if (spent > 0)
            {
                foreach (var scale in spend.Scales)
                {
                    (modifiers ??= state.Modifiers).Add(new Modifier { Attribute = scale.Attribute, Form = scale.Form, Value = scale.PerCharge * spent, Scope = scale.Scope, Source = "spend" });
                }
            }
        }
        if (casting.Uses is { } uses)
        {
            vars.UsesSpent = state.UsesSpent;
            if (state.UsesSpent > 0 && uses.Empower.Count > 0)
            {
                foreach (var m in uses.Empower)
                {
                    (modifiers ??= state.Modifiers).Add(m.Scaled(state.UsesSpent));
                }
            }
        }
        if (casting.SelfStack is { } stack)
        {
            var stacks = StacksNow(state.Slot, stack, now);
            if (stacks > 0)
            {
                foreach (var m in stack.Modifiers)
                {
                    (modifiers ??= state.Modifiers).Add(m.Scaled(stacks));
                }
            }
        }
        if (state.Flawless && weave.Form.Timing is { } timing && timing.Bonus != 0)
        {
            (modifiers ??= state.Modifiers).Add(new Modifier { Attribute = "damage", Form = ModifierForm.More, Value = timing.Bonus, Source = "timing" });
        }
        var view = state.HasModifiers ? weave.Root.WithModifiers(Combined(weave.Root.Modifiers, state.Modifiers)) : weave;
        var ctx = Run(sim, caster, stats, state, view, weave.Effects, depth, publishCast: true, vars, rngPrefix: rngPrefix);
        AfterUse(sim, caster, book, stats, state, weave, ctx, triggered);
    }

    /// <summary>The weave's own modifiers followed by a cast's local ones, as one list.</summary>
    internal static Modifier[] Combined(IReadOnlyList<Modifier> weave, List<Modifier> local)
    {
        var all = new Modifier[weave.Count + local.Count];
        for (var i = 0; i < weave.Count; i++)
        {
            all[i] = weave[i];
        }
        for (var i = 0; i < local.Count; i++)
        {
            all[weave.Count + i] = local[i];
        }
        return all;
    }

    /// <summary>
    /// Runs a list of a weave's effects for a cast: the context aims at the cast's live target or point, its
    /// random stream comes from the caster, the weave, the tick and the cast's beat, and the cast variables
    /// are set on it. Returns the context, or null when the caster is gone.
    /// </summary>
    internal EffectContext? Run(Simulation sim, Entity caster, Stats stats, CastState state, CompiledWeave weave, IReadOnlyList<CompiledEffect> effects, int depth, bool publishCast, in CastVarValues vars, double damageScale = 1, string rngPrefix = "weave:", long rngNumber = -1)
    {
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
            Rng = RngStreams.For(sim, caster, rngPrefix, weave.Key, rngNumber < 0 ? state.Pulses : rngNumber),
            Potency = 1,
            DamageScale = damageScale,
            Depth = depth,
            Slot = state.Slot,
        };
        vars.WriteTo(ctx.Vars);
        var now = sim.Clock.Time;
        stats.Stamp("cast", now);
        if (weave.Tags.Has("movement"))
        {
            stats.Stamp("moved", now);
        }
        CompiledEffect.ExecuteAll(effects, ctx);
        if (publishCast)
        {
            sim.Events.Publish(new WeaveCast(caster, weave.Key, target, ctx.Point, weave.Tags) { Tick = sim.Clock.Tick });
            if (sim.World.TryGet<WeaveBook>(caster, out var book))
            {
                book.RecordCast(weave.Key, RulesOf(sim).RecentCasts);
            }
        }
        return ctx;
    }

    public void Interrupt(Simulation sim, Entity caster)
    {
        if (sim.World.TryGet<WeaveBook>(caster, out var book) && book.Casting is { } cast)
        {
            var key = cast.Slot.Key;
            book.Casting = null;
            if (sim.World.TryGet<Stats>(caster, out var stats))
            {
                EndHeldState(sim, caster, book, stats, cast);
            }
            sim.Events.Publish(new WeaveInterrupted(caster, key) { Tick = sim.Clock.Tick });
        }
    }

    // ------------------------------------------------------------ the held modes, each tick

    /// <summary>
    /// Advances every cast begun through the runtime by one tick, in the order the books are visited, and
    /// tidies after casts cleared from outside. Called by the casting system for each book with a cast.
    /// </summary>
    internal void Advance(Simulation sim, Entity caster, WeaveBook book, CastState cast)
    {
        if (sim.World.Has<Dead>(caster))
        {
            book.Casting = null;
            if (sim.World.TryGet<Stats>(caster, out var deadStats))
            {
                EndHeldState(sim, caster, book, deadStats, cast);
            }
            return;
        }
        var weave = cast.Weave ?? cast.Slot.Compiled;
        if (weave is null)
        {
            book.Casting = null;
            return;
        }
        if (!sim.World.TryGet<Stats>(caster, out var stats))
        {
            book.Casting = null;
            return;
        }
        if (stats.HasFlag("stunned") || stats.HasFlag("frozen"))
        {
            Interrupt(sim, caster);
            return;
        }
        // A forbidden tag (silence forbids spells) interrupts a running weave that carries it.
        var forbid = weave.ForbidFlags;
        for (var i = 0; i < forbid.Count; i++)
        {
            if (stats.HasFlag(forbid[i]))
            {
                Interrupt(sim, caster);
                return;
            }
        }
        var now = sim.Clock.Time;
        cast.AimPoint = book.AimPoint;
        cast.AimDirection = book.AimDirection;
        cast.AimAt = book.AimAt;
        switch (cast.Mode)
        {
            case CastMode.Channel:
                AdvanceChannel(sim, caster, book, stats, cast, weave, now);
                break;
            case CastMode.Charge:
                AdvanceCharge(sim, caster, book, stats, cast, weave, now);
                break;
            case CastMode.Overcharge:
                AdvanceOvercharge(sim, caster, book, stats, cast, weave, now);
                break;
            case CastMode.Guard:
                AdvanceGuard(sim, caster, book, stats, cast, weave, now);
                break;
            default:
                if (now - cast.StartedAt >= cast.Duration)
                {
                    book.Casting = null;
                    ResolveUse(sim, caster, book, stats, cast, weave);
                }
                break;
        }
    }

    /// <summary>Drops casts that ended, taking off what a cast cleared from outside left behind.</summary>
    internal void SweepLive(Simulation sim)
    {
        var live = _live;
        for (var i = live.Count - 1; i >= 0; i--)
        {
            var entry = live[i];
            if (ReferenceEquals(entry.Book.Casting, entry.Cast) && sim.World.IsAlive(entry.Caster))
            {
                continue;
            }
            live.RemoveAt(i);
            if (sim.World.IsAlive(entry.Caster) && sim.World.TryGet<Stats>(entry.Caster, out var stats))
            {
                EndHeldState(sim, entry.Caster, entry.Book, stats, entry.Cast);
            }
        }
    }

    private void AdvanceChannel(Simulation sim, Entity caster, WeaveBook book, Stats stats, CastState cast, CompiledWeave weave, double now)
    {
        var channel = weave.Form.Channel;
        cast.Held = now - cast.StartedAt;
        if (channel?.Brim is { } brim)
        {
            FillBrim(sim, caster, book, stats, cast, weave, brim, now);
        }
        if (channel is { TrackAim: true })
        {
            Track(sim, caster, cast, channel.TurnRate, weave, stats);
        }
        if (now >= cast.NextChannelAt - 1e-9)
        {
            var scope = ScopeOf(cast, weave, stats);
            if (cast.Pulses > 0 && !TryPay(sim, weave, stats, scope, PlanFor(weave.Root).Costs, 1, out _))
            {
                EndHeld(sim, caster, book, stats, cast, weave, "dry", release: true);
                return;
            }
            BuildRamps(sim, caster, stats, cast, weave, cast.Held);
            var pulses = channel?.Release?.Pulses ?? true;
            if (pulses)
            {
                if (!cast.CooldownStarted && channel?.CooldownOnEnd != true)
                {
                    StartCooldown(sim, book, weave.Root, stats);
                    cast.CooldownStarted = true;
                }
                var view = cast.HasModifiers ? weave.Root.WithModifiers(Combined(weave.Root.Modifiers, cast.Modifiers)) : weave;
                var vars = new CastVarValues { Held = cast.Held };
                if (channel?.Brim is not null)
                {
                    vars.Fill = cast.Brim;
                }
                Run(sim, caster, stats, cast, view, weave.Effects, depth: 0, publishCast: true, vars);
            }
            sim.Events.Publish(new ChannelPulsed(caster, weave.Key, cast.Pulses, cast.Held) { Tick = sim.Clock.Tick });
            RaiseKind(sim, caster, CastingTriggers.ChannelBeat, FocusOf(cast), cast.Pulses + 1, weave.Tags);
            cast.Pulses++;
            if (!ReferenceEquals(book.Casting, cast))
            {
                return;
            }
            var interval = ChannelInterval(weave, stats, scope);
            if (channel?.Jitter is { } jitter && cast.Rng is { } rng)
            {
                var spread = Math.Max(0, jitter.Evaluate(scope));
                interval = Math.Max(sim.Clock.Dt, interval + rng.Range(-spread, spread));
            }
            cast.Interval = interval;
            cast.NextChannelAt = now + interval;
        }
        if (cast.MaxHold > 0 && cast.Held >= cast.MaxHold - 1e-9)
        {
            EndHeld(sim, caster, book, stats, cast, weave, "max_hold", release: true);
            return;
        }
        if (channel?.Brim is { AutoRelease: true } && cast.Brim >= 1 - 1e-9)
        {
            EndHeld(sim, caster, book, stats, cast, weave, "full", release: true);
        }
    }

    /// <summary>The caster's view of a cast's weave: the one the cast began with, else the slot's cached one.</summary>
    private static IExprScope ScopeOf(CastState cast, CompiledWeave weave, Stats stats) =>
        cast.Scope ??= CacheFor(cast.Slot, weave.Root, stats).Scope;

    /// <summary>Who a cast is aimed at for the trigger kinds it raises: its target, else the body it tracks.</summary>
    private static Entity FocusOf(CastState cast) => cast.Target.IsNone ? cast.Focus : cast.Target;

    /// <summary>A channel's beat: <c>channel.interval</c>, else the form's <c>channel_interval</c>, else rule <c>casting.channel_interval</c>; divided by the speed attribute.</summary>
    private double ChannelInterval(CompiledWeave weave, Stats stats, IExprScope scope)
    {
        var form = weave.Form;
        var expr = form.Channel?.Interval ?? form.ChannelInterval;
        var seconds = expr is null ? (_rules?.ChannelInterval ?? 0.5) : expr.Evaluate(scope);
        var speed = form.Channel?.Interval is null ? 1 : Speed(weave, stats);
        return Math.Max(1.0 / 64, seconds / speed);
    }

    private void AdvanceCharge(Simulation sim, Entity caster, WeaveBook book, Stats stats, CastState cast, CompiledWeave weave, double now)
    {
        cast.Held = now - cast.StartedAt;
        cast.Fill = cast.Duration <= 0 ? 1 : Math.Min(1, cast.Held / cast.Duration);
        if (cast.Fill >= 1 && cast.FullAt < 0)
        {
            cast.FullAt = now;
        }
        if (cast.Held >= cast.MaxHold - 1e-9)
        {
            EndHeld(sim, caster, book, stats, cast, weave, "full", release: true);
        }
    }

    private void AdvanceOvercharge(Simulation sim, Entity caster, WeaveBook book, Stats stats, CastState cast, CompiledWeave weave, double now)
    {
        cast.Held = now - cast.StartedAt;
        if (cast.FullAt < 0 && cast.Stage < cast.Stages && now - cast.StageAt >= cast.Interval - 1e-9)
        {
            var over = weave.Form.Overcharge;
            var plan = PlanFor(weave.Root);
            if (plan.StageCosts.Length > 0 && !TryPay(sim, weave, stats, ScopeOf(cast, weave, stats), plan.StageCosts, 1, out _))
            {
                // A stage it cannot pay for is a stage it cannot reach: the overcharge holds where it is.
                cast.FullAt = now;
            }
            else
            {
                cast.Stage++;
                cast.StageAt = now;
                sim.Events.Publish(new OverchargeStaged(caster, weave.Key, cast.Stage, cast.Stages) { Tick = sim.Clock.Tick });
                RaiseKind(sim, caster, CastingTriggers.OverchargeStage, FocusOf(cast), cast.Stage, weave.Tags);
                if (!ReferenceEquals(book.Casting, cast))
                {
                    return;
                }
                if (cast.Stage >= cast.Stages)
                {
                    cast.FullAt = now;
                }
            }
            _ = over;
        }
        cast.Fill = cast.Stages <= 0 ? 1 : (double)cast.Stage / cast.Stages;
        var fullHold = weave.Form.Overcharge?.FullHold is { } fh ? Math.Max(0, fh.Evaluate(ScopeOf(cast, weave, stats))) : RulesOf(sim).FullHold;
        if (cast.FullAt >= 0 && now - cast.FullAt >= fullHold - 1e-9)
        {
            EndHeld(sim, caster, book, stats, cast, weave, "full", release: true);
            return;
        }
        if (cast.Held >= cast.MaxHold - 1e-9)
        {
            EndHeld(sim, caster, book, stats, cast, weave, "max_hold", release: true);
        }
    }

    /// <summary>
    /// Turns a held cast toward its live aim at the turn rate (degrees per second, unlimited without one):
    /// the caster's aim when it sent one recently, else its focus body. The caster faces the same way.
    /// </summary>
    private void Track(Simulation sim, Entity caster, CastState cast, Expr? turnRate, CompiledWeave weave, Stats stats)
    {
        if (!sim.World.TryGet<Transform>(caster, out var t))
        {
            return;
        }
        Vector2 desired;
        if (cast.AimDirection is { } aim && aim.LengthSquared() > 1e-6f)
        {
            desired = aim;
        }
        else if (cast.AimPoint is { } aimPoint)
        {
            desired = new Vector2(aimPoint.X - t.Position.X, aimPoint.Y - t.Position.Y);
        }
        else if (!cast.Focus.IsNone && sim.World.TryGet<Transform>(cast.Focus, out var ft) && !sim.World.Has<Dead>(cast.Focus))
        {
            desired = new Vector2(ft.Position.X - t.Position.X, ft.Position.Y - t.Position.Y);
            cast.Point = ft.Position;
        }
        else
        {
            return;
        }
        if (desired.LengthSquared() < 1e-6f)
        {
            return;
        }
        desired = Vector2.Normalize(desired);
        var current = cast.Direction.LengthSquared() > 1e-6f ? Vector2.Normalize(cast.Direction) : t.Forward;
        var wanted = MathF.Atan2(desired.Y, desired.X);
        var have = MathF.Atan2(current.Y, current.X);
        var delta = wanted - have;
        while (delta > MathF.PI)
        {
            delta -= 2 * MathF.PI;
        }
        while (delta < -MathF.PI)
        {
            delta += 2 * MathF.PI;
        }
        if (turnRate is not null)
        {
            var limit = (float)(Math.Max(0, turnRate.Evaluate(ScopeOf(cast, weave, stats))) * Math.PI / 180.0 * sim.Clock.Dt);
            delta = Math.Clamp(delta, -limit, limit);
        }
        var yaw = have + delta;
        cast.Direction = new Vector2(MathF.Cos(yaw), MathF.Sin(yaw));
        t.Yaw = yaw;
        if (cast.AimPoint is { } p)
        {
            cast.Point = p;
        }
    }

    /// <summary>
    /// Builds a channel's ramps at a held time: weave ramps become the cast's local modifiers, caster ramps
    /// sit on the caster under source <c>cast:{slot}</c> until the cast ends.
    /// </summary>
    private static void BuildRamps(Simulation sim, Entity caster, Stats stats, CastState cast, CompiledWeave weave, double held)
    {
        var ramps = weave.Form.Channel?.Ramps;
        if (ramps is null || ramps.Count == 0)
        {
            return;
        }
        cast.Modifiers.Clear();
        var casterSource = cast.CasterSource;
        if (casterSource is not null)
        {
            stats.Attributes.RemoveSource(casterSource);
        }
        foreach (var ramp in ramps)
        {
            var value = ramp.Per * Curves.Evaluate(ramp.Curve, held);
            if (ramp.Max is { } max)
            {
                value = Math.Min(value, max);
            }
            if (ramp.Min is { } min)
            {
                value = Math.Max(value, min);
            }
            if (ramp.On == "caster")
            {
                casterSource ??= cast.CasterSource = "cast:" + cast.Slot.Key;
                stats.Attributes.Add(new Modifier { Attribute = ramp.Attribute, Form = ramp.Form, Value = value, Scope = ramp.Scope, Source = casterSource });
            }
            else
            {
                cast.Modifiers.Add(new Modifier { Attribute = ramp.Attribute, Form = ramp.Form, Value = value, Scope = ramp.Scope, Source = "ramp" });
            }
        }
        _ = sim;
        _ = caster;
    }

    // ------------------------------------------------------------ ending holds

    /// <summary>
    /// Lets go of a held cast from a slot (a release intent): a channel releases, a charge-up or overcharge
    /// fires, a guard drops and may bash. False when the caster holds nothing from that slot.
    /// </summary>
    public bool Release(Simulation sim, Entity caster, int slotIndex)
    {
        if (!sim.World.TryGet<WeaveBook>(caster, out var book) || book.Casting is not { } cast || !sim.World.TryGet<Stats>(caster, out var stats))
        {
            return false;
        }
        if (slotIndex < 0 || slotIndex >= book.Slots.Count || !ReferenceEquals(book.Slots[slotIndex], cast.Slot))
        {
            return false;
        }
        var weave = cast.Weave ?? cast.Slot.Compiled;
        if (weave is null || cast.Mode is CastMode.Instant or CastMode.Cast)
        {
            return false;
        }
        cast.Held = sim.Clock.Time - cast.StartedAt;
        EndHeld(sim, caster, book, stats, cast, weave, "release", release: true);
        return true;
    }

    /// <summary>
    /// Ends a held cast: fires its release when asked and allowed (a channel's release payload, a charge-up
    /// or overcharge resolving with its fill or stage, a guard's bash), starts a cooldown held for the end,
    /// takes off what the hold put on the caster, and publishes the release.
    /// </summary>
    private void EndHeld(Simulation sim, Entity caster, WeaveBook book, Stats stats, CastState cast, CompiledWeave weave, string reason, bool release)
    {
        if (ReferenceEquals(book.Casting, cast))
        {
            book.Casting = null;
        }
        var now = sim.Clock.Time;
        cast.Held = now - cast.StartedAt;
        var fired = false;
        AimAtRelease(sim, caster, book, cast, weave);
        switch (cast.Mode)
        {
            case CastMode.Channel:
                fired = release && ReleaseChannel(sim, caster, book, stats, cast, weave, ref reason);
                break;
            case CastMode.Charge:
                if (release)
                {
                    cast.Fill = cast.Duration <= 0 ? 1 : Math.Min(1, cast.Held / cast.Duration);
                    cast.Modifiers.Clear();
                    if (weave.Form.Charge is { } charge)
                    {
                        foreach (var scale in charge.Scales)
                        {
                            cast.Modifiers.Add(new Modifier { Attribute = scale.Attribute, Form = scale.Form, Value = scale.AtZero + (scale.AtFull - scale.AtZero) * cast.Fill, Scope = scale.Scope, Source = "charge" });
                        }
                    }
                    EndHeldState(sim, caster, book, stats, cast);
                    ResolveUse(sim, caster, book, stats, cast, weave, 0, new CastVarValues { Held = cast.Held, Fill = cast.Fill });
                    fired = true;
                }
                break;
            case CastMode.Overcharge:
                if (release)
                {
                    cast.Modifiers.Clear();
                    if (weave.Form.Overcharge is { } over)
                    {
                        if (cast.Stage > 0)
                        {
                            foreach (var m in over.PerStage)
                            {
                                cast.Modifiers.Add(m.Scaled(cast.Stage).WithSource("overcharge"));
                            }
                        }
                        if (over.Spark is { } spark && cast.Stage > 0 && now - cast.StageAt <= spark.Window + 1e-9)
                        {
                            cast.Modifiers.Add(new Modifier { Attribute = "damage", Form = ModifierForm.More, Value = spark.Bonus, Source = "spark" });
                        }
                    }
                    EndHeldState(sim, caster, book, stats, cast);
                    ResolveUse(sim, caster, book, stats, cast, weave, 0, new CastVarValues { Held = cast.Held, Stage = cast.Stage, Fill = cast.Fill });
                    fired = true;
                }
                break;
            case CastMode.Guard:
                fired = release && Bash(sim, caster, stats, cast, weave, reason);
                break;
        }
        EndHeldState(sim, caster, book, stats, cast);
        if (!cast.CooldownStarted)
        {
            StartCooldown(sim, book, weave.Root, stats);
            cast.CooldownStarted = true;
        }
        var mode = cast.Mode switch
        {
            CastMode.Channel => "channel",
            CastMode.Charge => "charge",
            CastMode.Overcharge => "overcharge",
            CastMode.Guard => "guard",
            _ => "cast",
        };
        sim.Events.Publish(new WeaveReleased(caster, weave.Key, mode, cast.Held, cast.Fill, cast.Stage, fired, reason) { Tick = sim.Clock.Tick });
        if (cast.Mode == CastMode.Channel)
        {
            RaiseKind(sim, caster, CastingTriggers.ChannelEnd, FocusOf(cast), cast.Held, weave.Tags);
        }
        if (fired)
        {
            RaiseKind(sim, caster, CastingTriggers.Release, FocusOf(cast), cast.Held, weave.Tags);
        }
    }

    /// <summary>A held cast that fires on release points where its caster aims at that moment, when it aims.</summary>
    private static void AimAtRelease(Simulation sim, Entity caster, WeaveBook book, CastState cast, CompiledWeave weave)
    {
        if (book.AimAt < cast.StartedAt)
        {
            return;
        }
        if (book.AimDirection is { } dir && dir.LengthSquared() > 1e-6f && weave.Targeting == Targeting.Direction)
        {
            cast.Direction = Vector2.Normalize(dir);
        }
        if (book.AimPoint is { } point && weave.Targeting is Targeting.Ground or Targeting.Direction)
        {
            cast.Point = point;
            if (weave.Targeting == Targeting.Direction && (book.AimDirection is null) && sim.World.TryGet<Transform>(caster, out var t))
            {
                var d = new Vector2(point.X - t.Position.X, point.Y - t.Position.Y);
                if (d.LengthSquared() > 1e-6f)
                {
                    cast.Direction = Vector2.Normalize(d);
                }
            }
        }
    }

    /// <summary>A channel's release: its payload (or its own graph) when the hold was long and full enough; then its persisting pulses.</summary>
    private bool ReleaseChannel(Simulation sim, Entity caster, WeaveBook book, Stats stats, CastState cast, CompiledWeave weave, ref string reason)
    {
        var channel = weave.Form.Channel;
        var fired = false;
        var scope = ScopeOf(cast, weave, stats);
        var brimOk = true;
        if (channel?.Brim is { } brim)
        {
            brimOk = cast.Brim >= brim.MinRelease - 1e-9;
            if (!brimOk)
            {
                reason = "fizzle";
            }
        }
        if (channel?.Release is { } release && brimOk)
        {
            var minHold = release.MinHold is null ? 0 : Math.Max(0, release.MinHold.Evaluate(scope));
            var full = cast.MaxHold <= 0 || cast.Held >= cast.MaxHold - 1e-9;
            if (cast.Held >= minHold - 1e-9 && (!release.RequireFull || full))
            {
                BuildRamps(sim, caster, stats, cast, weave, cast.Held);
                if (channel.Brim is { } scaleBrim && (scaleBrim.Scale.AtEmpty != 1 || scaleBrim.Scale.AtFull != 1))
                {
                    var t = Curves.Evaluate(scaleBrim.Scale.Curve, cast.Brim);
                    var multiplier = scaleBrim.Scale.AtEmpty + (scaleBrim.Scale.AtFull - scaleBrim.Scale.AtEmpty) * t;
                    cast.Modifiers.Add(new Modifier { Attribute = "damage", Form = ModifierForm.More, Value = multiplier - 1, Source = "brim" });
                }
                var view = cast.HasModifiers ? weave.Root.WithModifiers(Combined(weave.Root.Modifiers, cast.Modifiers)) : weave;
                var effects = weave.Casting.Release.Count > 0 ? weave.Casting.Release : weave.Effects;
                var vars = new CastVarValues { Held = cast.Held };
                if (channel.Brim is not null)
                {
                    vars.Fill = cast.Brim;
                }
                Run(sim, caster, stats, cast, view, effects, depth: 0, publishCast: true, vars, rngPrefix: "release:");
                fired = true;
                if (channel.Persist is { } persist)
                {
                    Persist(sim, caster, cast, weave, persist, scope);
                }
            }
        }
        if (channel?.Brim is { } spent && fired && spent.Spend)
        {
            SetBrim(caster, book, stats, cast.Slot, 0, sim.Clock.Time);
        }
        return fired;
    }

    /// <summary>
    /// Takes off what a held cast put on its caster: ramps on the caster, the guard and its flag, the channel
    /// and charge flags. Safe to call more than once.
    /// </summary>
    private void EndHeldState(Simulation sim, Entity caster, WeaveBook book, Stats stats, CastState cast)
    {
        var touched = false;
        if (cast.CasterSource is { } source)
        {
            stats.Attributes.RemoveSource(source);
            cast.CasterSource = null;
        }
        switch (cast.Mode)
        {
            case CastMode.Channel:
                touched = stats.Flags.Remove("channeling");
                break;
            case CastMode.Charge:
            case CastMode.Overcharge:
                touched = stats.Flags.Remove("charging");
                break;
            case CastMode.Guard:
                if (sim.World.TryGet<Guard>(caster, out var guard) && ReferenceEquals(guard.Cast, cast))
                {
                    sim.World.Remove<Guard>(caster);
                }
                touched = stats.Flags.Remove("guarding");
                break;
        }
        if (touched)
        {
            stats.Attributes.Touch();
        }
        _ = book;
    }

    // ------------------------------------------------------------ input during a cast

    /// <summary>
    /// A second press of the casting slot: a timed strike's press (flawless inside its window), or one more
    /// strike of a multitude. Publishes the press either way, so other mechanics (salvo beats) can read it.
    /// False when the caster is not casting from that slot.
    /// </summary>
    public bool Press(Simulation sim, Entity caster, int slotIndex)
    {
        if (!sim.World.TryGet<WeaveBook>(caster, out var book) || book.Casting is not { } cast || !sim.World.TryGet<Stats>(caster, out var stats))
        {
            return false;
        }
        if (slotIndex < 0 || slotIndex >= book.Slots.Count || !ReferenceEquals(book.Slots[slotIndex], cast.Slot))
        {
            return false;
        }
        var weave = cast.Weave ?? cast.Slot.Compiled;
        if (weave is null)
        {
            return false;
        }
        var now = sim.Clock.Time;
        var elapsed = now - cast.StartedAt;
        if (weave.Form.Timing is { } timing && cast.Mode == CastMode.Cast)
        {
            switch (timing.Kind)
            {
                case TimingKind.Perfect when cast.Presses == 0:
                    cast.Flawless = elapsed >= cast.Duration - timing.Window - 1e-9;
                    break;
                case TimingKind.Timed when cast.Presses == 0:
                    cast.Flawless = cast.Marker >= 0 && Math.Abs(elapsed - cast.Marker) <= timing.Window / 2 + 1e-9;
                    break;
                case TimingKind.Multitude when cast.Presses < Math.Max(1, (int)timing.Window):
                    Run(sim, caster, stats, cast, weave, weave.Effects, depth: 1, publishCast: false, default, rngPrefix: "press:", rngNumber: cast.Presses);
                    break;
            }
        }
        cast.Presses++;
        sim.Events.Publish(new WeavePressed(caster, weave.Key, cast.Presses, cast.Flawless) { Tick = sim.Clock.Tick });
        if (cast.Flawless && cast.Presses == 1)
        {
            RaiseKind(sim, caster, CastingTriggers.Flawless, FocusOf(cast), 1, weave.Tags);
        }
        return true;
    }

    /// <summary>
    /// The live aim from the caster: kept on the weave book (and mirrored on a running cast) when it arrives
    /// no faster than rule <c>casting.max_aim_rate</c> per second, with the point pulled within rule
    /// <c>casting.max_aim_range</c> of the caster and the facing made unit length. False when it was dropped.
    /// </summary>
    public bool Aim(Simulation sim, Entity caster, Vector3 point, Vector2 direction)
    {
        if (!sim.World.TryGet<WeaveBook>(caster, out var book))
        {
            return false;
        }
        var rules = RulesOf(sim);
        var now = sim.Clock.Time;
        if (rules.MaxAimRate > 0 && now - book.AimAt < 1.0 / rules.MaxAimRate - 1e-9)
        {
            return false;
        }
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z) || !float.IsFinite(direction.X) || !float.IsFinite(direction.Y))
        {
            return false;
        }
        if (sim.World.TryGet<Transform>(caster, out var t) && rules.MaxAimRange > 0)
        {
            var d = t.GroundDistanceTo(point);
            if (d > rules.MaxAimRange)
            {
                var k = (float)(rules.MaxAimRange / d);
                point = new Vector3(t.Position.X + (point.X - t.Position.X) * k, t.Position.Y + (point.Y - t.Position.Y) * k, point.Z);
            }
        }
        book.AimPoint = point;
        book.AimDirection = direction.LengthSquared() > 1e-6f ? Vector2.Normalize(direction) : null;
        book.AimAt = now;
        if (book.Casting is { } cast)
        {
            cast.AimPoint = book.AimPoint;
            cast.AimDirection = book.AimDirection;
            cast.AimAt = now;
        }
        return true;
    }

    // ------------------------------------------------------------ AI

    /// <summary>Picks the best ready weave for an AI toward a target, honoring hints and range.</summary>
    public (WeaveSlot Slot, CompiledWeave Weave)? ChooseForAi(Simulation sim, Entity caster, Entity target, IExprScope? perception = null)
    {
        if (!sim.World.TryGet<WeaveBook>(caster, out var book) || !sim.World.TryGet<Stats>(caster, out var stats))
        {
            return null;
        }
        EnsureArmed(sim, caster, book, stats);
        var ct = sim.World.Get<Transform>(caster);
        var tt = sim.World.Get<Transform>(target);
        var distance = ct is not null && tt is not null ? ct.GroundDistanceTo(tt.Position) : 0;
        var best = default((WeaveSlot, CompiledWeave)?);
        double bestScore = double.NegativeInfinity;
        var rng = sim.RngFor(caster, "ai-choose");
        var now = sim.Clock.Time;
        foreach (var slot in book.Slots)
        {
            var weave = Resolve(sim, slot, out _);
            if (weave is null || !book.IsReady(slot, now))
            {
                continue;
            }
            // Triggered weaves cast themselves, a toggle that is on would turn off, an empty bank cannot cast.
            if (weave.IsTriggered || (weave.IsToggle && IsToggledOn(slot)))
            {
                continue;
            }
            var cache = CacheFor(slot, weave, stats);
            if (weave.Casting.Uses is { Empower.Count: 0 } uses && UsesNow(sim, slot, weave, uses, stats, now) < 1)
            {
                continue;
            }
            if (!GatesHold(weave, cache.Scope) || !CanAfford(weave, stats, cache.Scope, out _))
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

    private static bool GatesHold(CompiledWeave weave, IExprScope scope)
    {
        var gates = weave.Casting.Gates;
        for (var i = 0; i < gates.Count; i++)
        {
            if (!gates[i].IsTrue(scope))
            {
                return false;
            }
        }
        return true;
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
            if (weave is null || weave.IsTriggered)
            {
                continue;
            }
            var r = weave.Ai?.PreferredRange ?? Math.Max(1.2, RangeOf(weave, stats, CacheFor(slot, weave, stats)) * 0.8);
            best = Math.Max(best, r);
        }
        return best <= 0 ? 1.5 : best;
    }

    // ------------------------------------------------------------ trigger kinds the cast modes raise

    /// <summary>Raises one of the casting trigger kinds for a caster, when anyone listens to it.</summary>
    internal static void RaiseKind(Simulation sim, Entity caster, TriggerKind kind, Entity target, double amount, TagSet tags, int depth = 0)
    {
        if (sim.TryService<TriggerHub>(out var hub) && hub.HasSubscribers(kind))
        {
            hub.Raise(caster, kind, new TriggerEvent(caster, target, amount, false, tags, depth));
        }
    }
}

/// <summary>The trigger kinds the cast modes raise, registered with the casting content kinds.</summary>
public static class CastingTriggers
{
    public static readonly TriggerKind ChannelBeat = TriggerKinds.Register("channel_beat", "the owner's channel pulses");
    public static readonly TriggerKind ChannelEnd = TriggerKinds.Register("channel_end", "the owner's channel ends by release, a full hold or running dry");
    public static readonly TriggerKind Release = TriggerKinds.Register("release", "the owner's held cast fires on release: a charge-up, an overcharge, a channel's payload or a guard's bash");
    public static readonly TriggerKind OverchargeStage = TriggerKinds.Register("overcharge_stage", "the owner's overcharge reaches a stage, the stage as the amount");
    public static readonly TriggerKind Flawless = TriggerKinds.Register("flawless", "the owner lands a timed press");
    public static readonly TriggerKind GuardBeat = TriggerKinds.Register("guard_beat", "the owner's guard pulses");
    public static readonly TriggerKind Parry = TriggerKinds.Register("parry", "the owner's fresh guard parries a hit whole");

    /// <summary>Touches the kinds so they are registered before content is validated.</summary>
    public static void Register()
    {
        _ = ChannelBeat;
    }
}

/// <summary>The cast variables a run sets on its context, each only when it has a value.</summary>
internal struct CastVarValues
{
    public double? Held;
    public double? Fill;
    public double? Stage;
    public double? ChargesSpent;
    public double? UsesSpent;
    public double? TriggerPower;
    public double? TriggerAmount;
    public double? RepeatIndex;
    public double? GuardAbsorbed;
    public double? Persist;

    public readonly void WriteTo(Dictionary<string, double> vars)
    {
        Write(vars, CastVars.Held, Held);
        Write(vars, CastVars.Fill, Fill);
        Write(vars, CastVars.Stage, Stage);
        Write(vars, CastVars.ChargesSpent, ChargesSpent);
        Write(vars, CastVars.UsesSpent, UsesSpent);
        Write(vars, CastVars.TriggerPower, TriggerPower);
        Write(vars, CastVars.TriggerAmount, TriggerAmount);
        Write(vars, CastVars.RepeatIndex, RepeatIndex);
        Write(vars, CastVars.GuardAbsorbed, GuardAbsorbed);
        Write(vars, CastVars.Persist, Persist);
    }

    private static void Write(Dictionary<string, double> vars, string name, double? value)
    {
        if (value is { } v)
        {
            vars[name] = v;
        }
    }
}

/// <summary>
/// Random streams for casts, derived from the simulation seed, the entity, the tick and a salt, exactly as
/// <see cref="Simulation.RngFor(Entity, string)"/> derives them from <c>prefix + key + ":" + number</c>, but
/// without building the salt text.
/// </summary>
internal static class RngStreams
{
    public static Rng For(Simulation sim, Entity e, string prefix, string key, long number) =>
        new(Rng.Combine(Rng.Combine(sim.Rng.Seed, (ulong)e.Key), Rng.Combine((ulong)sim.Clock.Tick, Salt(prefix, key, number))));

    /// <summary>The FNV-1a hash <see cref="Rng.Hash64"/> gives <c>prefix + key + ":" + number</c>.</summary>
    public static ulong Salt(string prefix, string key, long number)
    {
        const ulong prime = 1099511628211UL;
        var h = 14695981039346656037UL;
        foreach (var c in prefix)
        {
            h ^= c;
            h *= prime;
        }
        foreach (var c in key)
        {
            h ^= c;
            h *= prime;
        }
        h ^= ':';
        h *= prime;
        Span<char> digits = stackalloc char[24];
        number.TryFormat(digits, out var written, provider: System.Globalization.CultureInfo.InvariantCulture);
        for (var i = 0; i < written; i++)
        {
            h ^= digits[i];
            h *= prime;
        }
        return h;
    }
}

/// <summary>The rules the weave runtime reads, resolved once per ruleset.</summary>
internal sealed class CastingRules
{
    public CastingRules(Rules rules)
    {
        Source = rules;
        CooldownAtPress = rules.Text("combat.cooldown_at", "complete") == "press";
        GlobalCooldown = rules.Number("combat.global_cooldown", 0);
        CastMove = rules.Number("casting.cast_move", 0);
        MaxHold = Math.Max(0.1, rules.Number("casting.max_hold", 8));
        FullHold = Math.Max(0, rules.Number("casting.full_hold", 1));
        ChargeTime = Math.Max(0.05, rules.Number("casting.charge_time", 1.5));
        StageTime = Math.Max(0.05, rules.Number("casting.stage_time", 0.6));
        ChannelInterval = Math.Max(0.05, rules.Number("casting.channel_interval", 0.5));
        MaxAimRate = rules.Number("casting.max_aim_rate", 10);
        MaxAimRange = rules.Number("casting.max_aim_range", 40);
        MetaInterval = Math.Max(0, rules.Number("casting.meta_interval", 0.35));
        FollowUpDelay = Math.Max(0, rules.Number("casting.follow_up_delay", 0.3));
        FizzleStatus = rules.Text("casting.fizzle_status", "stunned");
        FizzleDuration = Math.Max(0, rules.Number("casting.fizzle_duration", 0.4));
        RequireOwnership = rules.Flag("casting.require_ownership", false);
        SwapRequires = rules.Has("casting.swap_requires") ? rules.Expr("casting.swap_requires", "1") : null;
        SwapWaivedBy = rules.Text("casting.swap_waived_by", "sanctuary");
        RecentCasts = Math.Max(2, (int)rules.Number("casting.recent_casts", 8));
        StampCosts = rules.Flag("casting.stamp_costs", false);
        TriggerMaxCastTime = rules.Number("triggers.max_cast_time", 1);
        TriggerMaxDepth = (int)rules.Number("triggers.max_depth", 4);
        TriggerChanceCap = Math.Clamp(rules.Number("triggers.chance_cap", 1), 0, 1);
    }

    public Rules Source { get; }
    public bool CooldownAtPress { get; }
    public double GlobalCooldown { get; }
    public double CastMove { get; }
    public double MaxHold { get; }
    public double FullHold { get; }
    public double ChargeTime { get; }
    public double StageTime { get; }
    public double ChannelInterval { get; }
    public double MaxAimRate { get; }
    public double MaxAimRange { get; }
    public double MetaInterval { get; }
    public double FollowUpDelay { get; }
    public string FizzleStatus { get; }
    public double FizzleDuration { get; }
    public bool RequireOwnership { get; }
    public Expr? SwapRequires { get; }
    public string SwapWaivedBy { get; }
    public int RecentCasts { get; }
    public bool StampCosts { get; }
    public double TriggerMaxCastTime { get; }
    public int TriggerMaxDepth { get; }
    public double TriggerChanceCap { get; }
}

/// <summary>One resource a cost list charges: its formula, its tag context, and the attributes that add to or convert it.</summary>
internal sealed class CostEntry
{
    public required string Resource;
    /// <summary>The resource's index in <see cref="WeavePlan.Resources"/>.</summary>
    public required int Index;
    public required Expr Expr;
    public required TagSet Tags;
    /// <summary><c>cost.added.{resource}</c>, and whether the weave's own modifiers touch it.</summary>
    public required string Added;
    public bool AddedInWeave;
    /// <summary><c>cost.convert.{resource}.{to}</c> by target index (null for the resource itself), and whether the weave touches each.</summary>
    public required string?[] Converts;
    public required bool[] ConvertInWeave;
    /// <summary>An entry for a resource the weave does not cost, read only when something adds a cost to it.</summary>
    public bool Implicit;
}

/// <summary>
/// What the runtime keeps per compiled weave: its cost entries (and an overcharge's per-stage costs and a
/// toggle's upkeep) with their tag contexts and the family attributes they read, built once.
/// </summary>
internal sealed class WeavePlan
{
    public WeavePlan(CompiledWeave weave, ContentRegistry content)
    {
        var resources = new List<string>();
        foreach (var r in content.Each<ResourceDef>())
        {
            resources.Add(r.Id);
        }
        foreach (var (resource, _) in weave.Costs)
        {
            if (!resources.Contains(resource))
            {
                resources.Add(resource);
            }
        }
        if (weave.Form.Overcharge is { } over)
        {
            foreach (var (resource, _) in over.CostPerStage)
            {
                if (!resources.Contains(resource))
                {
                    resources.Add(resource);
                }
            }
        }
        if (weave.Form.Upkeep is { } upkeep)
        {
            foreach (var (resource, _) in upkeep.PerSecond)
            {
                if (!resources.Contains(resource))
                {
                    resources.Add(resource);
                }
            }
        }
        Resources = resources.ToArray();
        LastCostGauges = Resources.Select(r => "last_cost." + r).ToArray();
        Costs = Entries(weave, weave.Costs, implicitRest: true);
        StageCosts = weave.Form.Overcharge is { } o ? Entries(weave, o.CostPerStage, implicitRest: false) : Array.Empty<CostEntry>();
        Upkeep = weave.Form.Upkeep is { } u ? Entries(weave, u.PerSecond, implicitRest: false) : Array.Empty<CostEntry>();
        foreach (var m in weave.Modifiers)
        {
            if ((m.Gauge is { } g && g.StartsWith("last_cost.", StringComparison.Ordinal)) || (m.ValueExpr is { } e && e.Identifiers.Any(i => i.StartsWith("last_cost.", StringComparison.Ordinal))))
            {
                StampCosts = true;
            }
            if (m.Attribute is "repeat.count" or "repeat.seals")
            {
                MentionsRepeat = true;
            }
        }
        MentionsRepeat |= weave.Bases.ContainsKey("repeat.count") || weave.Bases.ContainsKey("repeat.seals");
    }

    public string[] Resources { get; }
    public string[] LastCostGauges { get; }
    public CostEntry[] Costs { get; }
    public CostEntry[] StageCosts { get; }
    public CostEntry[] Upkeep { get; }
    /// <summary>True when the weave's modifiers read the <c>last_cost.{resource}</c> gauges a payment stamps.</summary>
    public bool StampCosts { get; }
    /// <summary>True when the weave itself sets or modifies <c>repeat.count</c> or <c>repeat.seals</c>.</summary>
    public bool MentionsRepeat { get; }

    /// <summary>Whether a resource index is one an entry of the list charges directly.</summary>
    public bool IsCostResource(int index, CostEntry[] entries)
    {
        foreach (var e in entries)
        {
            if (e.Index == index)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// The entries of a cost list in its own order; with <paramref name="implicitRest"/>, an entry for every
    /// other resource too, read only when something adds a cost to it (<c>cost.added.{resource}</c>).
    /// </summary>
    private CostEntry[] Entries(CompiledWeave weave, IReadOnlyDictionary<string, Expr> costs, bool implicitRest)
    {
        var entries = new List<CostEntry>();
        foreach (var (resource, expr) in costs)
        {
            entries.Add(Entry(weave, resource, expr, isImplicit: false));
        }
        if (implicitRest)
        {
            foreach (var resource in Resources)
            {
                if (!costs.ContainsKey(resource))
                {
                    entries.Add(Entry(weave, resource, Expr.Constant(0), isImplicit: true));
                }
            }
        }
        return entries.ToArray();
    }

    private CostEntry Entry(CompiledWeave weave, string resource, Expr expr, bool isImplicit)
    {
        var index = Array.IndexOf(Resources, resource);
        var added = "cost.added." + resource;
        var converts = new string?[Resources.Length];
        var convertInWeave = new bool[Resources.Length];
        for (var t = 0; t < Resources.Length; t++)
        {
            if (t != index)
            {
                converts[t] = "cost.convert." + resource + "." + Resources[t];
                convertInWeave[t] = Mentions(weave.Modifiers, converts[t]!);
            }
        }
        return new CostEntry
        {
            Resource = resource,
            Index = index,
            Expr = expr,
            Tags = weave.Tags.With("cost." + resource),
            Added = added,
            AddedInWeave = Mentions(weave.Modifiers, added),
            Converts = converts,
            ConvertInWeave = convertInWeave,
            Implicit = isImplicit,
        };
    }

    private static bool Mentions(IReadOnlyList<Modifier> modifiers, string attribute)
    {
        foreach (var m in modifiers)
        {
            if (m.Attribute == attribute)
            {
                return true;
            }
        }
        return false;
    }
}

/// <summary>
/// What the runtime keeps on a weave slot so AI choice and cost checks build no scopes: the caster's view
/// of the compiled weave, the weave mounted over an AI's perception, the slot's own scope for converts,
/// and the faces a convert or combo chain puts on it. Rebuilt when the slot's compiled weave or its caster
/// changes; holds no state of its own.
/// </summary>
internal sealed class WeaveSlotCache
{
    public readonly CompiledWeave Weave;
    public readonly Stats Caster;
    public readonly CasterWeaveScope Scope;
    public readonly PerceptionWeaveScope Hint;
    private SlotScope? _slotScope;
    private Dictionary<string, CompiledWeave>? _faces;

    public WeaveSlotCache(CompiledWeave weave, Stats caster, CasterWeaveScope scope, PerceptionWeaveScope hint)
    {
        Weave = weave;
        Caster = caster;
        Scope = scope;
        Hint = hint;
    }

    public SlotScope SlotScopeFor(WeaveSlot slot) => _slotScope ??= new SlotScope(slot, Scope);

    /// <summary>A preset compiled as a face of this slot, at the slot weave's level; cached.</summary>
    public CompiledWeave? Face(WeaveCompiler compiler, string preset, int level)
    {
        _faces ??= new Dictionary<string, CompiledWeave>(StringComparer.Ordinal);
        if (_faces.TryGetValue(preset, out var face))
        {
            return face;
        }
        if (!compiler.TryCompile(new WeaveSpec { Preset = preset, Level = level }, out face, out _, GlyphFit.Dormant))
        {
            return null;
        }
        _faces[preset] = face!;
        return face;
    }
}

/// <summary>
/// The scope of a slot's convert conditions: <c>slot.{name}</c> reads the slot's state (<c>slot.uses</c>,
/// <c>slot.stack</c> ...), everything else resolves as the caster's view of the weave.
/// </summary>
internal sealed class SlotScope : IExprScope
{
    private readonly WeaveSlot _slot;
    private readonly IExprScope _caster;

    public SlotScope(WeaveSlot slot, IExprScope caster)
    {
        _slot = slot;
        _caster = caster;
    }

    public bool TryResolve(string name, out double value)
    {
        if (name.Length > 5 && name[4] == '.' && name.StartsWith("slot", StringComparison.Ordinal))
        {
            value = _slot.StateOr(name.Substring(5), 0);
            return true;
        }
        return _caster.TryResolve(name, out value);
    }

    public bool HasFlag(string flag) => _caster.HasFlag(flag);

    public double NextRandom() => _caster.NextRandom();
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
