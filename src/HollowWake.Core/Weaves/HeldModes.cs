using System.Numerics;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Expressions;
using HollowWake.Core.Sim;

namespace HollowWake.Core.Weaves;

// The parts of the held modes that keep their own state: a guard on its bearer, a channel's brim in its
// slot, and the pulses a released channel leaves behind.
public sealed partial class WeaveRuntime
{
    // ------------------------------------------------------------ guards

    /// <summary>
    /// Raises a guard as its cast begins: a pool (its <c>pool</c> times attribute <c>guard.strength</c>), a
    /// frontal arc, a parry window, the <c>guarding</c> flag, and a hold that ends at <c>max_duration</c>.
    /// </summary>
    private void RaiseGuard(Simulation sim, Entity caster, Stats stats, CastState state, CompiledWeave weave, IExprScope scope, CastingRules rules)
    {
        var spec = weave.Form.Guard;
        var now = state.StartedAt;
        var pool = spec?.Pool is { } p ? Math.Max(0, p.Evaluate(scope)) : 0;
        pool *= Math.Max(0, weave.Attr(stats, "guard.strength"));
        var arc = Math.Clamp(spec?.Arc ?? 120, 0, 360);
        state.MaxHold = spec?.MaxDuration is { } md ? Math.Max(0.05, md.Evaluate(scope)) : rules.MaxHold;
        state.Duration = state.MaxHold;
        var pulseAt = double.PositiveInfinity;
        if (spec?.Pulse is { } pulse)
        {
            pulseAt = now + PulseInterval(pulse, scope);
        }
        sim.World.Add(caster, new Guard
        {
            Slot = state.Slot,
            Cast = state,
            Pool = pool,
            MaxPool = pool,
            ArcCos = Math.Cos(arc / 2 * Math.PI / 180),
            ParryUntil = now + (spec?.Parry is { } parry ? Math.Max(0, parry.Evaluate(scope)) : 0),
            NextPulseAt = pulseAt,
        });
        stats.Flags.Add("guarding");
        stats.Attributes.Touch();
    }

    private static double PulseInterval(GuardPulseSpec pulse, IExprScope scope) => pulse.Interval is { } i ? Math.Max(0.05, i.Evaluate(scope)) : 1;

    private void AdvanceGuard(Simulation sim, Entity caster, WeaveBook book, Stats stats, CastState cast, CompiledWeave weave, double now)
    {
        cast.Held = now - cast.StartedAt;
        if (!sim.World.TryGet<Guard>(caster, out var guard) || !ReferenceEquals(guard.Cast, cast))
        {
            EndHeld(sim, caster, book, stats, cast, weave, "break", release: false);
            return;
        }
        var spec = weave.Form.Guard;
        if (guard.Parried && spec?.EndOnParry == true)
        {
            EndHeld(sim, caster, book, stats, cast, weave, "parry", release: true);
            return;
        }
        if (guard.Broken)
        {
            sim.Events.Publish(new GuardBroken(caster, weave.Key, guard.Absorbed) { Tick = sim.Clock.Tick });
            EndHeld(sim, caster, book, stats, cast, weave, "break", release: true);
            return;
        }
        if (spec?.TurnRate is not null)
        {
            Track(sim, caster, cast, spec.TurnRate, weave, stats);
        }
        if (spec?.Pulse is { } pulse && now >= guard.NextPulseAt - 1e-9)
        {
            var scope = ScopeOf(cast, weave, stats);
            guard.NextPulseAt = now + PulseInterval(pulse, scope);
            var t = sim.World.Get<Transform>(caster);
            CastPreset(sim, caster, pulse.Weave, weave, cast.Slot, cast.Target, t?.Position ?? cast.Point, cast.Direction.LengthSquared() > 1e-6f ? cast.Direction : t?.Forward ?? Vector2.UnitX, 1, "guard:", cast.Pulses++);
            RaiseKind(sim, caster, CastingTriggers.GuardBeat, FocusOf(cast), cast.Pulses, weave.Tags);
            if (!ReferenceEquals(book.Casting, cast))
            {
                return;
            }
        }
        if (cast.Held >= cast.MaxHold - 1e-9)
        {
            EndHeld(sim, caster, book, stats, cast, weave, "max_hold", release: true);
        }
    }

    /// <summary>
    /// A guard's bash as it ends: when released (or held to its end, or parried) after its arm time with at
    /// least its threshold of the pool absorbed, or when it breaks if it bashes on break.
    /// </summary>
    private bool Bash(Simulation sim, Entity caster, Stats stats, CastState cast, CompiledWeave weave, string reason)
    {
        if (weave.Form.Guard?.Bash is not { } bash || weave.Casting.Bash.Count == 0)
        {
            return false;
        }
        if (!sim.World.TryGet<Guard>(caster, out var guard) || !ReferenceEquals(guard.Cast, cast))
        {
            return false;
        }
        bool fires;
        if (reason == "break")
        {
            fires = bash.OnBreak;
        }
        else
        {
            var scope = ScopeOf(cast, weave, stats);
            var arm = bash.ArmTime is { } at ? Math.Max(0, at.Evaluate(scope)) : 0;
            fires = cast.Held >= arm - 1e-9 && guard.Absorbed >= bash.Threshold * guard.MaxPool - 1e-9;
        }
        if (!fires)
        {
            return false;
        }
        Run(sim, caster, stats, cast, weave, weave.Casting.Bash, depth: 0, publishCast: true, new CastVarValues { Held = cast.Held, GuardAbsorbed = guard.Absorbed }, rngPrefix: "bash:");
        return true;
    }

    // ------------------------------------------------------------ brims

    // Gauge names for seats one to sixteen, so a brim's gauge needs no text built per tick.
    private static readonly string[] BrimGauges = Enumerable.Range(1, 16).Select(i => "brim." + i).ToArray();

    private static string BrimGauge(int seat) => seat < BrimGauges.Length ? BrimGauges[seat] : "brim." + (seat + 1);

    /// <summary>A slot's brim now: what it held, decayed since by its decay per second, never below its bank once reached.</summary>
    internal static double BrimNow(WeaveSlot slot, BrimSpec spec, double now)
    {
        var brim = slot.StateOr(SlotState.Brim, 0);
        if (brim <= 0)
        {
            return 0;
        }
        var floor = spec.BankAt > 0 && brim >= spec.BankAt - 1e-9 ? spec.BankAt : 0;
        var since = Math.Max(0, now - slot.StateOr(SlotState.BrimAt, now));
        return Math.Max(floor, brim - spec.Decay * since);
    }

    /// <summary>A channel with a brim fills it while held, over its fill time divided by the speed attribute.</summary>
    private void FillBrim(Simulation sim, Entity caster, WeaveBook book, Stats stats, CastState cast, CompiledWeave weave, BrimSpec brim, double now)
    {
        var scope = ScopeOf(cast, weave, stats);
        var fill = (brim.FillTime is { } f ? Math.Max(0.05, f.Evaluate(scope)) : 2) / Speed(weave, stats);
        cast.Brim = Math.Min(1, cast.Brim + sim.Clock.Dt / fill);
        cast.Fill = cast.Brim;
        SetBrim(caster, book, stats, cast.Slot, cast.Brim, now);
    }

    /// <summary>Stores a slot's brim and mirrors it into the caster's gauge <c>brim.{seat}</c>; a brim at rest decays in upkeep.</summary>
    private void SetBrim(Entity caster, WeaveBook book, Stats stats, WeaveSlot slot, double value, double now)
    {
        slot.State[SlotState.Brim] = value;
        slot.State[SlotState.BrimAt] = now;
        var seat = book.Slots.IndexOf(slot);
        if (seat >= 0)
        {
            var gauge = BrimGauge(seat);
            if (Math.Abs(stats.Gauge(gauge) - value) >= 0.005 || value <= 0)
            {
                stats.SetGauge(gauge, value);
            }
        }
        if (value > 0)
        {
            EnsureUpkeep(caster, book);
        }
    }

    /// <summary>A brim at rest decays; its gauge follows in steps. True while it is above its floor.</summary>
    private static bool DecayBrim(Stats stats, WeaveSlot slot, BrimSpec spec, int seat, double now)
    {
        var stored = slot.StateOr(SlotState.Brim, 0);
        if (stored <= 0)
        {
            return false;
        }
        var value = BrimNow(slot, spec, now);
        var gauge = BrimGauge(seat);
        if (Math.Abs(stats.Gauge(gauge) - value) >= 0.01 || value <= 0)
        {
            stats.SetGauge(gauge, value);
        }
        var floor = spec.BankAt > 0 && stored >= spec.BankAt - 1e-9 ? spec.BankAt : 0;
        return value > floor + 1e-9;
    }

    // ------------------------------------------------------------ persisting pulses

    /// <summary>
    /// The pulses a released channel leaves: its graph keeps running on the channel's beat for
    /// <c>per_held_second</c> seconds per second held (at most <c>max_duration</c>), following the caster,
    /// their damage falling linearly from one to <c>fade</c>.
    /// </summary>
    private void Persist(Simulation sim, Entity caster, CastState cast, CompiledWeave weave, PersistSpec spec, IExprScope scope)
    {
        var duration = (spec.PerHeldSecond is { } per ? Math.Max(0, per.Evaluate(scope)) : 1) * cast.Held;
        if (spec.MaxDuration is { } max)
        {
            duration = Math.Min(duration, Math.Max(0, max.Evaluate(scope)));
        }
        if (duration <= 0)
        {
            return;
        }
        var fade = spec.Fade is { } f ? f.Evaluate(scope) : 1;
        var interval = cast.Interval > 0 ? cast.Interval : RulesOf(sim).ChannelInterval;
        var job = new PersistJob(this, sim, caster, cast, weave, sim.Clock.Time, duration, fade, interval);
        sim.Scheduler.After(interval, job.Fire);
    }

    private void RunPersist(PersistJob job)
    {
        var sim = job.Sim;
        var caster = job.Caster;
        if (!sim.World.IsAlive(caster) || sim.World.Has<Dead>(caster) || !sim.World.TryGet<Stats>(caster, out var stats))
        {
            return;
        }
        var elapsed = sim.Clock.Time - job.Start;
        if (elapsed > job.Duration + 1e-9)
        {
            return;
        }
        var share = 1 + (job.Fade - 1) * Math.Clamp(elapsed / job.Duration, 0, 1);
        Run(sim, caster, stats, job.Cast, job.Weave, job.Weave.Effects, depth: 1, publishCast: false, new CastVarValues { Held = job.Cast.Held, Persist = share }, damageScale: share, rngPrefix: "persist:", rngNumber: job.Count++);
        if (elapsed + job.Interval <= job.Duration + 1e-9)
        {
            sim.Scheduler.After(job.Interval, job.Fire);
        }
    }

    private sealed class PersistJob
    {
        private readonly WeaveRuntime _runtime;

        public PersistJob(WeaveRuntime runtime, Simulation sim, Entity caster, CastState cast, CompiledWeave weave, double start, double duration, double fade, double interval)
        {
            _runtime = runtime;
            Sim = sim;
            Caster = caster;
            Cast = cast;
            Weave = weave;
            Start = start;
            Duration = duration;
            Fade = fade;
            Interval = interval;
            Fire = () => _runtime.RunPersist(this);
        }

        public Simulation Sim { get; }
        public Entity Caster { get; }
        public CastState Cast { get; }
        public CompiledWeave Weave { get; }
        public double Start { get; }
        public double Duration { get; }
        public double Fade { get; }
        public double Interval { get; }
        public long Count;

        /// <summary>The job's beat, one delegate reused for every rescheduling.</summary>
        public Action Fire { get; }
    }
}
