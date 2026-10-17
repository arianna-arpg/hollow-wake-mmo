using System.Numerics;
using HollowWake.Core.Attributes;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Weaves;

// What a slot does around its uses: the face a convert or combo chain puts on it, charges a use spends,
// self stacks, cycles, follow-ups, repeats, metas, the recovery lock, and the glyphs grafted onto a seat.
public sealed partial class WeaveRuntime
{
    // Presets cast by slot behaviors (follow-ups, metas, guard pulses), compiled once per level.
    private readonly Dictionary<(string Preset, int Level), CompiledWeave?> _presets = new();

    // ------------------------------------------------------------ faces

    /// <summary>
    /// The weave pressing a slot casts now: a convert's preset while its condition holds over the caster and
    /// the slot's state (<c>slot.uses</c>, <c>slot.stack</c> ...), else the next step of a combo chain inside
    /// its window, else the slot's own weave. The slot message shows this face. A pure read: it changes no
    /// slot state, so a snapshot asking never moves the simulation.
    /// </summary>
    public CompiledWeave? FaceOf(Simulation sim, Entity caster, WeaveSlot slot)
    {
        if (!sim.World.TryGet<WeaveBook>(caster, out var book) || !sim.World.TryGet<Stats>(caster, out var stats))
        {
            return null;
        }
        var weave = Resolve(sim, slot, out _);
        return weave is null ? null : FaceOf(sim, slot, weave, stats, sim.Clock.Time);
    }

    internal CompiledWeave FaceOf(Simulation sim, WeaveSlot slot, CompiledWeave weave, Stats stats, double now)
    {
        var casting = weave.Casting;
        if (casting.Convert is null && casting.Chain is null)
        {
            return weave;
        }
        var cache = CacheFor(slot, weave, stats);
        if (casting.Convert is { } convert)
        {
            // slot.uses, slot.stack and slot.brim read as they stand now, without bringing the slot up to date.
            var scope = cache.SlotScopeFor(slot);
            scope.Now = now;
            if (convert.When.IsTrue(scope) && cache.Face(_compiler, convert.Weave, weave.Level) is { } converted)
            {
                return converted;
            }
        }
        if (casting.Chain is { Steps.Count: > 0 } chain)
        {
            // A chain whose window lapsed starts over; the press that walks it (AdvanceChain) stores that.
            var step = (int)slot.StateOr(SlotState.Chain, 0);
            if (step > 0 && now - slot.StateOr(SlotState.ChainAt, double.NegativeInfinity) > chain.Window + 1e-9)
            {
                step = 0;
            }
            if (step > 0 && step <= chain.Steps.Count && cache.Face(_compiler, chain.Steps[step - 1], weave.Level) is { } next)
            {
                return next;
            }
        }
        return weave;
    }

    /// <summary>A press walks the combo chain: within the window it moves to the next step, wrapping after the last.</summary>
    private static void AdvanceChain(WeaveSlot slot, CompiledWeave weave, double now)
    {
        if (weave.Casting.Chain is not { Steps.Count: > 0 } chain)
        {
            return;
        }
        var step = (int)slot.StateOr(SlotState.Chain, 0);
        if (now - slot.StateOr(SlotState.ChainAt, double.NegativeInfinity) > chain.Window + 1e-9)
        {
            step = 0;
        }
        slot.State[SlotState.Chain] = (step + 1) % (chain.Steps.Count + 1);
        slot.State[SlotState.ChainAt] = now;
    }

    // ------------------------------------------------------------ charges

    /// <summary>Whether the caster holds the charges a spend requires: its amount, or one for <c>all</c>, and at least its minimum.</summary>
    private static bool HasCharges(Simulation sim, Entity caster, SpendSpec spend)
    {
        var count = sim.Service<ChargeService>().Count(sim, caster, spend.Charge);
        var need = Math.Max(spend.Minimum, spend.All ? 1 : spend.Amount);
        return count >= need - 1e-9;
    }

    /// <summary>Spends a use's charges when it resolves; returns how many were spent (none when too few remain).</summary>
    private static double SpendCharges(Simulation sim, Entity caster, SpendSpec spend)
    {
        if (!HasCharges(sim, caster, spend))
        {
            return 0;
        }
        var charges = sim.Service<ChargeService>();
        if (spend.All)
        {
            return charges.SpendAll(sim, caster, spend.Charge);
        }
        return charges.Spend(sim, caster, spend.Charge, spend.Amount) ? spend.Amount : 0;
    }

    // ------------------------------------------------------------ self stacks

    /// <summary>Stacks held now: a pile drops after its duration without a use, or peels one per decay interval.</summary>
    internal static int StacksNow(WeaveSlot slot, SelfStackSpec spec, double now)
    {
        var stacks = (int)slot.StateOr(SlotState.Stack, 0);
        if (stacks <= 0)
        {
            return 0;
        }
        var since = Math.Max(0, now - slot.StateOr(SlotState.StackAt, now));
        if (spec.Decay > 0)
        {
            return Math.Max(0, stacks - (int)Math.Floor(since / spec.Decay));
        }
        return spec.Duration > 0 && since > spec.Duration ? 0 : stacks;
    }

    /// <summary>The self stacks a slot holds now, for the slot message; zero without a pile.</summary>
    public int Stacks(Simulation sim, WeaveSlot slot) =>
        slot.Compiled?.Casting.SelfStack is { } spec ? StacksNow(slot, spec, sim.Clock.Time) : 0;

    // ------------------------------------------------------------ after a use

    /// <summary>
    /// What follows a use: the self stack grows, a cycle counts (and pays out on its count), follow-ups are
    /// scheduled, repeats are scheduled, the recovery lock holds the caster, and a toggle turns on.
    /// </summary>
    private void AfterUse(Simulation sim, Entity caster, WeaveBook book, Stats stats, CastState state, CompiledWeave weave, EffectContext? ctx, bool triggered)
    {
        var now = sim.Clock.Time;
        var slot = state.Slot;
        var casting = weave.Casting;
        if (casting.SelfStack is { } stack)
        {
            slot.State[SlotState.Stack] = Math.Min(stack.Max, StacksNow(slot, stack, now) + 1);
            slot.State[SlotState.StackAt] = now;
        }
        if (casting.Cycle is { } cycle && casting.CycleEffects.Count > 0)
        {
            var count = slot.StateOr(SlotState.Cycle, 0) + 1;
            if (count >= Math.Max(1, cycle.Count))
            {
                count = 0;
                Run(sim, caster, stats, state, weave.Root, casting.CycleEffects, depth: 1, publishCast: false, default, rngPrefix: "cycle:");
            }
            slot.State[SlotState.Cycle] = count;
        }
        for (var i = 0; i < casting.FollowUps.Count; i++)
        {
            ScheduleFollowUp(sim, caster, stats, state, weave, casting.FollowUps[i], ctx, i);
        }
        ScheduleRepeats(sim, caster, stats, state, weave, now);
        if (!triggered && weave.Form.Recovery is { } recovery)
        {
            var seconds = Math.Max(0, recovery.Evaluate(CacheFor(slot, weave.Root, stats).Scope));
            if (seconds > 0)
            {
                book.GlobalReadyAt = Math.Max(book.GlobalReadyAt, now + seconds);
            }
        }
        if (!triggered && weave.IsToggle && ReferenceEquals(weave.Root, slot.Compiled))
        {
            ToggleOn(sim, caster, book, stats, slot, weave.Root);
        }
    }

    private void ScheduleFollowUp(Simulation sim, Entity caster, Stats stats, CastState state, CompiledWeave weave, FollowUpSpec spec, EffectContext? ctx, int index)
    {
        var scope = CacheFor(state.Slot, weave.Root, stats).Scope;
        var chance = spec.Chance is null ? 1 : spec.Chance.Evaluate(scope);
        if (chance < 1 && !(ctx?.Rng ?? RngStreams.For(sim, caster, "follow:", weave.Key, index)).Chance(chance))
        {
            return;
        }
        var delay = spec.Delay is null ? RulesOf(sim).FollowUpDelay : Math.Max(0, spec.Delay.Evaluate(scope));
        var job = new PresetJob(this, sim, caster, spec.Weave, weave, state.Slot, state.Target, state.Point, state.Direction, 1, "follow:", index);
        sim.Scheduler.After(delay, job.Fire);
    }

    // ------------------------------------------------------------ repeats

    /// <summary>
    /// Repeats of a use: attribute <c>repeat.count</c> (fractions carry to the next use) plus seals of unleash
    /// banked while the slot rested (<c>repeat.seals</c>, one per <c>repeat.seal_time</c>), plus one per extra
    /// use a bank vented with <c>steps_from_bank</c>. Each repeat runs the graph again after
    /// <c>repeat.interval</c>, one deeper, larger by <c>repeat.scale</c> per repeat, retargeted to the nearest
    /// enemy with <c>repeat.retarget</c>; <c>repeat.lock</c> holds the caster until the flurry ends.
    /// </summary>
    private void ScheduleRepeats(Simulation sim, Entity caster, Stats stats, CastState state, CompiledWeave weave, double now)
    {
        var plan = PlanFor(weave.Root);
        var attributes = stats.Attributes;
        var counted = plan.MentionsRepeat || attributes.HasModifiers("repeat.count") || attributes.HasModifiers("repeat.seals");
        var steps = weave.Casting.Uses is { StepsFromBank: true } ? Math.Max(0, state.UsesSpent - 1) : 0;
        if (!counted && steps == 0)
        {
            return;
        }
        var slot = state.Slot;
        double count = 0;
        if (counted)
        {
            count = Math.Max(0, weave.Attr(stats, "repeat.count"));
            var seals = Math.Floor(weave.Attr(stats, "repeat.seals"));
            if (seals >= 1)
            {
                var sealTime = Math.Max(0.05, weave.Attr(stats, "repeat.seal_time"));
                var rested = now - slot.StateOr(SlotState.LastUse, double.NegativeInfinity);
                count += Math.Min(seals, Math.Floor(rested / sealTime));
                slot.State[SlotState.LastUse] = now;
            }
            count += slot.StateOr(SlotState.RepeatCarry, 0);
        }
        var repeats = (int)Math.Floor(count + 1e-9);
        if (counted)
        {
            slot.State[SlotState.RepeatCarry] = Math.Max(0, count - repeats);
        }
        repeats += steps;
        if (repeats <= 0)
        {
            return;
        }
        repeats = Math.Min(repeats, RulesOf(sim).MaxRepeats);
        var interval = Math.Max(sim.Clock.Dt, weave.Attr(stats, "repeat.interval"));
        var scale = weave.Attr(stats, "repeat.scale");
        var retarget = weave.Attr(stats, "repeat.retarget") > 0;
        if (weave.Attr(stats, "repeat.lock") > 0 && sim.World.TryGet<WeaveBook>(caster, out var book))
        {
            book.GlobalReadyAt = Math.Max(book.GlobalReadyAt, now + repeats * interval);
        }
        var range = (float)Math.Max(1, weave.Range(stats));
        for (var i = 1; i <= repeats; i++)
        {
            var job = new RepeatJob(this, sim, caster, slot, weave.Root, state.Target, state.Point, state.Direction, i, scale, retarget, range);
            sim.Scheduler.After(interval * i, job.Fire);
        }
    }

    private void RunRepeat(RepeatJob job)
    {
        var sim = job.Sim;
        var caster = job.Caster;
        if (!sim.World.IsAlive(caster) || sim.World.Has<Dead>(caster) || !sim.World.TryGet<Stats>(caster, out var stats))
        {
            return;
        }
        var target = job.Target;
        var point = job.Point;
        var direction = job.Direction;
        if (job.Retarget && sim.World.TryGet<Transform>(caster, out var ct))
        {
            var nearest = Entity.None;
            var best = float.MaxValue;
            foreach (var (e, pos, dist) in sim.Spatial.Within(ct.Position, job.Range))
            {
                if (e != caster && dist < best && sim.World.Has<Stats>(e) && !sim.World.Has<Dead>(e) && Relations.IsHostile(sim, caster, e))
                {
                    best = dist;
                    nearest = e;
                    point = pos;
                }
            }
            if (!nearest.IsNone)
            {
                target = nearest;
                var d = new Vector2(point.X - ct.Position.X, point.Y - ct.Position.Y);
                if (d.LengthSquared() > 1e-6f)
                {
                    direction = Vector2.Normalize(d);
                }
            }
        }
        var state = new CastState { Slot = job.Slot, Weave = job.Weave, Mode = CastMode.Instant, Target = target, Point = point, Direction = direction, StartedAt = sim.Clock.Time };
        var weave = job.Weave;
        if (job.Scale != 0)
        {
            state.Modifiers.Add(new Modifier { Attribute = "area.radius", Form = ModifierForm.Increased, Value = job.Scale * job.Index, Source = "repeat" });
            weave = weave.WithModifiers(Combined(weave.Modifiers, state.Modifiers));
        }
        Run(sim, caster, stats, state, weave, job.Weave.Effects, depth: 1, publishCast: false, new CastVarValues { RepeatIndex = job.Index }, rngPrefix: "repeat:", rngNumber: job.Index);
        sim.Events.Publish(new WeaveRepeated(caster, job.Weave.Key, job.Index, target) { Tick = sim.Clock.Tick });
    }

    private sealed class RepeatJob
    {
        private readonly WeaveRuntime _runtime;

        public RepeatJob(WeaveRuntime runtime, Simulation sim, Entity caster, WeaveSlot slot, CompiledWeave weave, Entity target, Vector3 point, Vector2 direction, int index, double scale, bool retarget, float range)
        {
            _runtime = runtime;
            Sim = sim;
            Caster = caster;
            Slot = slot;
            Weave = weave;
            Target = target;
            Point = point;
            Direction = direction;
            Index = index;
            Scale = scale;
            Retarget = retarget;
            Range = range;
        }

        public Simulation Sim { get; }
        public Entity Caster { get; }
        public WeaveSlot Slot { get; }
        public CompiledWeave Weave { get; }
        public Entity Target { get; }
        public Vector3 Point { get; }
        public Vector2 Direction { get; }
        public int Index { get; }
        public double Scale { get; }
        public bool Retarget { get; }
        public float Range { get; }

        public void Fire() => _runtime.RunRepeat(this);
    }

    // ------------------------------------------------------------ casting presets from slot behaviors

    /// <summary>A preset compiled for slot behaviors at a level, cached; null when it does not compile.</summary>
    private CompiledWeave? Preset(Simulation sim, string preset, int level)
    {
        var key = (preset, level);
        if (_presets.TryGetValue(key, out var weave))
        {
            return weave;
        }
        if (!_compiler.TryCompile(new WeaveSpec { Preset = preset, Level = level }, out weave, out var error, GlyphFit.Dormant))
        {
            sim.Log.Warn($"preset '{preset}' cast by a slot behavior failed to compile: {error}");
        }
        _presets[key] = weave;
        return weave;
    }

    /// <summary>
    /// Casts a preset as the <c>cast</c> effect does: free of cost and cooldown, at the host's level with the
    /// host's tags joined, one deeper, at the host's target when it lives or else its point, with the host
    /// slot as the context's slot and the host weave as its host.
    /// </summary>
    internal void CastPreset(Simulation sim, Entity caster, string preset, CompiledWeave host, WeaveSlot? slot, Entity target, Vector3 point, Vector2 direction, int depth, string rngPrefix, long number)
    {
        if (!sim.World.IsAlive(caster) || sim.World.Has<Dead>(caster) || Preset(sim, preset, host.Level) is not { } weave)
        {
            return;
        }
        var hostTags = host.Tags;
        if (!target.IsNone && (!sim.World.IsAlive(target) || sim.World.Has<Dead>(target)))
        {
            target = Entity.None;
        }
        if (!target.IsNone && sim.World.TryGet<Transform>(target, out var tt))
        {
            point = tt.Position;
        }
        var ctx = new EffectContext
        {
            Sim = sim,
            Source = caster,
            Target = target,
            Point = point,
            Direction = direction,
            Weave = weave,
            Tags = weave.Tags.Union(hostTags),
            Depth = depth,
            Rng = RngStreams.For(sim, caster, rngPrefix, weave.Key, number),
            Slot = slot,
            Host = host.Root,
        };
        CompiledEffect.ExecuteAll(weave.Effects, ctx);
    }

    private sealed class PresetJob
    {
        private readonly WeaveRuntime _runtime;
        private readonly Simulation _sim;
        private readonly Entity _caster;
        private readonly string _preset;
        private readonly CompiledWeave _host;
        private readonly WeaveSlot? _slot;
        private readonly Entity _target;
        private readonly Vector3 _point;
        private readonly Vector2 _direction;
        private readonly int _depth;
        private readonly string _prefix;
        private readonly long _number;

        public PresetJob(WeaveRuntime runtime, Simulation sim, Entity caster, string preset, CompiledWeave host, WeaveSlot? slot, Entity target, Vector3 point, Vector2 direction, int depth, string prefix, long number)
        {
            _runtime = runtime;
            _sim = sim;
            _caster = caster;
            _preset = preset;
            _host = host;
            _slot = slot;
            _target = target;
            _point = point;
            _direction = direction;
            _depth = depth;
            _prefix = prefix;
            _number = number;
        }

        /// <summary>Set by a meta chain: published as the meta runs.</summary>
        public MetaUsed? Meta { get; init; }

        public void Fire()
        {
            _runtime.CastPreset(_sim, _caster, _preset, _host, _slot, _target, _point, _direction, _depth, _prefix, _number);
            if (Meta is { } meta && _sim.World.IsAlive(_caster))
            {
                _sim.Events.Publish(meta with { Tick = _sim.Clock.Tick });
            }
        }
    }

    // ------------------------------------------------------------ metas

    /// <summary>
    /// Runs a slot's meta actions: the form's meta first, then each active glyph's in socket order, one
    /// beat apart (rule <c>casting.meta_interval</c>), each cast like the <c>cast</c> effect with the slot as
    /// the host, aimed where the caster last aimed or else ahead of it. Refused while the slot's last chain
    /// still runs, while the caster cannot act, or when the slot has no meta.
    /// </summary>
    public bool UseMeta(Simulation sim, Entity caster, int slotIndex, out string reason)
    {
        reason = "";
        if (!sim.World.TryGet<WeaveBook>(caster, out var book) || slotIndex < 0 || slotIndex >= book.Slots.Count)
        {
            reason = "no_slot";
            return false;
        }
        if (!sim.World.TryGet<Stats>(caster, out var stats) || sim.World.Has<Dead>(caster))
        {
            reason = "dead";
            return false;
        }
        var slot = book.Slots[slotIndex];
        var weave = Resolve(sim, slot, out var error);
        if (weave is null)
        {
            reason = "invalid:" + error;
            return false;
        }
        var metas = weave.Casting.Metas;
        if (metas.Count == 0)
        {
            reason = "no_meta";
            return false;
        }
        if (stats.HasFlag("stunned") || stats.HasFlag("frozen"))
        {
            reason = "stunned";
            return false;
        }
        var now = sim.Clock.Time;
        if (slot.StateOr(SlotState.MetaUntil, double.NegativeInfinity) > now)
        {
            reason = "busy";
            return false;
        }
        var interval = RulesOf(sim).MetaInterval;
        var t = sim.World.Get<Transform>(caster);
        var point = book.AimPoint ?? (t is null ? default : t.Position + new Vector3(t.Forward.X, t.Forward.Y, 0));
        var direction = book.AimDirection ?? t?.Forward ?? Vector2.UnitX;
        if (book.AimDirection is null && book.AimPoint is { } aimed && t is not null)
        {
            var d = new Vector2(aimed.X - t.Position.X, aimed.Y - t.Position.Y);
            if (d.LengthSquared() > 1e-6f)
            {
                direction = Vector2.Normalize(d);
            }
        }
        slot.State[SlotState.MetaUntil] = now + interval * (metas.Count - 1) + sim.Clock.Dt;
        for (var i = 0; i < metas.Count; i++)
        {
            var meta = metas[i];
            var job = new PresetJob(this, sim, caster, meta.Weave, weave, slot, Entity.None, point, direction, 1, "meta:", i)
            {
                Meta = new MetaUsed(caster, weave.Key, meta.Weave, i),
            };
            if (i == 0)
            {
                job.Fire();
            }
            else
            {
                sim.Scheduler.After(interval * i, job.Fire);
            }
        }
        return true;
    }

    /// <summary>The meta faces a slot shows: the form's, then each active glyph's, in socket order.</summary>
    public IReadOnlyList<MetaSpec> MetasOf(WeaveSlot slot) => slot.Compiled?.Casting.Metas ?? Array.Empty<MetaSpec>();

    // ------------------------------------------------------------ the rack

    /// <summary>Swaps two seats of the rack. Free: re-ordering costs nothing, even mid-fight.</summary>
    public bool Swap(Simulation sim, Entity caster, int a, int b, out string reason)
    {
        reason = "";
        if (!sim.World.TryGet<WeaveBook>(caster, out var book) || a < 0 || b < 0 || a >= book.Slots.Count || b >= book.Slots.Count || a == b)
        {
            reason = "bad_slot";
            return false;
        }
        book.Swap(a, b);
        sim.Events.Publish(new WeaveSlotsSwapped(caster, a, b) { Tick = sim.Clock.Tick });
        return true;
    }

    // ------------------------------------------------------------ trees

    /// <summary>
    /// The points a seat's weave has for its form's tree (rule <c>casting.tree_points</c> of its level) and how
    /// many of those it spent on nodes that count: what a reset gives back, and what the slot shows. (0, 0)
    /// for a weave whose form has no tree. A pure read.
    /// </summary>
    public (int Spent, int Points) TreePoints(Simulation sim, WeaveSlot slot)
    {
        var weave = Resolve(sim, slot, out _);
        return weave?.Form.Tree is null ? (0, 0) : (weave.TreeSpent, weave.TreePoints);
    }

    /// <summary>
    /// Clears a seat's tree, giving back every point spent in it: what a station's reset service calls. The
    /// seat keeps its weave, glyphs, cooldown and state and recompiles without its nodes; a cast running from
    /// it is interrupted. False with a reason when the seat is empty or nothing was spent in its tree.
    /// </summary>
    public bool ResetTree(Simulation sim, Entity caster, int slotIndex, out string reason)
    {
        reason = "";
        if (!sim.World.TryGet<WeaveBook>(caster, out var book) || slotIndex < 0 || slotIndex >= book.Slots.Count)
        {
            reason = "no_slot";
            return false;
        }
        var slot = book.Slots[slotIndex];
        if (slot.Spec.Tree.Count == 0)
        {
            reason = "no_tree";
            return false;
        }
        var refunded = Resolve(sim, slot, out _)?.TreeSpent ?? 0;
        if (book.Casting is { } cast && ReferenceEquals(cast.Slot, slot))
        {
            Interrupt(sim, caster);
        }
        slot.Spec = slot.Spec with { Tree = new List<string>() };
        slot.Compiled = null;
        book.Changed();
        sim.Events.Publish(new WeaveTreeReset(caster, slotIndex, slot.Key, refunded) { Tick = sim.Clock.Tick });
        return true;
    }

    /// <summary>
    /// Whether the caster may change its loadout now: rule <c>casting.swap_requires</c> (an expression over the
    /// caster, such as no recent hits), waived where the ground carries the tag rule <c>casting.swap_waived_by</c>
    /// names. Re-ordering the rack is always free.
    /// </summary>
    public bool CanChangeLoadout(Simulation sim, Entity caster, out string reason)
    {
        reason = "";
        var rules = RulesOf(sim);
        if (rules.SwapRequires is not { } requires || !sim.World.TryGet<Stats>(caster, out var stats))
        {
            return true;
        }
        if (rules.SwapWaivedBy.Length > 0 && sim.World.TryGet<Transform>(caster, out var t)
            && sim.Terrain.TagsAt(World.CellCoord.Of(t.Position.X, t.Position.Y)).Has(rules.SwapWaivedBy))
        {
            return true;
        }
        if (requires.IsTrue(stats))
        {
            return true;
        }
        reason = "in_combat";
        return false;
    }

    // ------------------------------------------------------------ grafts

    /// <summary>
    /// Reads the glyphs the caster's attribute family <c>glyph.slot{n}.{glyph}</c> grafts onto each seat (the
    /// value is the glyph's level), and recompiles a seat whose grafts changed. A socketed copy of a glyph
    /// wins over its graft; a graft that misfits goes dormant.
    /// </summary>
    private static void RefreshGrafts(Stats stats, WeaveBook book)
    {
        var found = FindGrafts(stats, book.Slots.Count);
        var any = false;
        for (var i = 0; i < book.Slots.Count; i++)
        {
            var slot = book.Slots[i];
            var grafts = found?[i];
            any |= grafts is { Count: > 0 };
            if (SameGrafts(slot.Grafts, grafts))
            {
                continue;
            }
            slot.Grafts = grafts;
            slot.Compiled = null;
        }
        book.HasGrafts = any;
    }

    /// <summary>The glyphs the caster's <c>glyph.slot{n}.{glyph}</c> attributes graft onto each seat, sorted by id; null when none.</summary>
    private static List<GlyphRef>[]? FindGrafts(Stats stats, int seats)
    {
        List<GlyphRef>[]? found = null;
        foreach (var attribute in stats.Attributes.ModifiedAttributes)
        {
            if (!attribute.StartsWith("glyph.slot", StringComparison.Ordinal))
            {
                continue;
            }
            var dot = attribute.IndexOf('.', 10);
            if (dot < 0 || !int.TryParse(attribute.AsSpan(10, dot - 10), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var seat) || seat < 1 || seat > seats)
            {
                continue;
            }
            var level = (int)Math.Floor(stats.Get(attribute));
            if (level < 1)
            {
                continue;
            }
            found ??= new List<GlyphRef>[seats];
            (found[seat - 1] ??= new List<GlyphRef>()).Add(new GlyphRef { Id = attribute[(dot + 1)..], Level = level });
        }
        if (found is not null)
        {
            foreach (var grafts in found)
            {
                grafts?.Sort(static (x, y) => string.CompareOrdinal(x.Id, y.Id));
            }
        }
        return found;
    }

    private static bool SameGrafts(IReadOnlyList<GlyphRef>? a, List<GlyphRef>? b)
    {
        var na = a?.Count ?? 0;
        var nb = b?.Count ?? 0;
        if (na != nb)
        {
            return false;
        }
        for (var i = 0; i < na; i++)
        {
            if (a![i].Id != b![i].Id || a[i].Level != b[i].Level)
            {
                return false;
            }
        }
        return true;
    }
}
