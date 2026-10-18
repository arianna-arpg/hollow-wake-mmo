using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Sim;

namespace HollowWake.Core.Weaves;

// Banks of use charges kept in a slot's state. A bank starts full; a trickle bank recovers one use per
// recharge, a magazine refills when emptied (all at once, or one at a time with drip), a still bank
// recovers only while its caster stands still and bleeds while it moves, a manual bank never recovers on its
// own (only restore_uses and converts refill it). Reading a bank changes nothing:
// what it holds now is computed from what it held when it last changed, so checks, AI and the slot message
// can read it freely. A bank is brought up to date only as it changes (a spend, a restore, a still bank's
// beat), and only still banks need the upkeep system.
public sealed partial class WeaveRuntime
{
    /// <summary>The bank's size: attribute <c>uses.max</c> in the weave's scope, its base the spec's <c>max</c>.</summary>
    internal static int UsesMax(CompiledWeave weave, UsesSpec spec, Stats stats) =>
        Math.Max(0, (int)Math.Round(weave.Attr(stats, "uses.max", spec.Max)));

    /// <summary>Seconds a used charge takes to come back: the spec's <c>recharge</c> or the weave's cooldown, divided by <c>uses.rate</c>.</summary>
    private static double RechargeSeconds(CompiledWeave weave, UsesSpec spec, Stats stats)
    {
        var seconds = spec.Recharge is { } r ? Math.Max(0, r.IsConstant ? r.ConstantValue : r.Evaluate(new CasterWeaveScope(weave, stats))) : CooldownOf(weave, stats);
        return seconds / Math.Max(0.1, weave.Attr(stats, "uses.rate", 1));
    }

    /// <summary>Seconds an emptied magazine takes to refill: its <c>refill</c> or the weave's cooldown, divided by <c>uses.rate</c>.</summary>
    private static double RefillSeconds(CompiledWeave weave, UsesSpec spec, Stats stats)
    {
        var seconds = spec.Magazine?.Refill is { } r ? Math.Max(0, r.IsConstant ? r.ConstantValue : r.Evaluate(new CasterWeaveScope(weave, stats))) : CooldownOf(weave, stats);
        return seconds / Math.Max(0.1, weave.Attr(stats, "uses.rate", 1));
    }

    /// <summary>
    /// The charges in a slot's bank now (fractional while recovering), from what it held when it last
    /// changed. Pure: the slot is not touched, so a snapshot or a check never moves the simulation.
    /// </summary>
    internal static double UsesNow(WeaveSlot slot, CompiledWeave weave, UsesSpec spec, Stats stats, double now)
    {
        var max = UsesMax(weave, spec, stats);
        var uses = slot.StateOr(SlotState.Uses, double.NaN);
        if (double.IsNaN(uses))
        {
            return max;
        }
        var elapsed = Math.Max(0, now - slot.StateOr(SlotState.UsesAt, now));
        if (spec.Magazine is { } magazine)
        {
            var reload = slot.StateOr(SlotState.UsesReload, 0);
            if (reload > 0)
            {
                if (magazine.Drip)
                {
                    var refill = RefillSeconds(weave, spec, stats);
                    uses = refill <= 0 ? max : Math.Min(max, uses + elapsed * max / refill);
                }
                else if (now >= reload - 1e-9)
                {
                    uses = max;
                }
            }
        }
        else if (spec.Still is null && !spec.Manual && uses < max)
        {
            var recharge = RechargeSeconds(weave, spec, stats);
            uses = recharge <= 0 ? max : Math.Min(max, uses + elapsed / recharge);
        }
        return Math.Min(uses, max);
    }

    /// <summary>
    /// Brings a slot's bank up to date before it changes: stores what <see cref="UsesNow"/> reads as of now,
    /// and ends a magazine's reload once it is full again.
    /// </summary>
    private static double SettleUses(WeaveSlot slot, CompiledWeave weave, UsesSpec spec, Stats stats, double now)
    {
        var uses = UsesNow(slot, weave, spec, stats, now);
        var state = slot.State;
        state[SlotState.Uses] = uses;
        state[SlotState.UsesAt] = now;
        if (uses >= UsesMax(weave, spec, stats) - 1e-9 && slot.StateOr(SlotState.UsesReload, 0) > 0)
        {
            state[SlotState.UsesReload] = 0;
        }
        return uses;
    }

    /// <summary>
    /// Spends a press's charges: one, or the whole bank with <c>vent_all</c>; none when the bank is empty (an
    /// empower bank then casts unempowered). An emptied magazine starts refilling. Returns how many were spent.
    /// </summary>
    private int SpendUses(Simulation sim, Entity caster, WeaveSlot slot, CompiledWeave weave, UsesSpec spec, Stats stats, double now)
    {
        var uses = SettleUses(slot, weave, spec, stats, now);
        var whole = (int)Math.Floor(uses + 1e-9);
        if (whole < 1)
        {
            return 0;
        }
        var spent = spec.VentAll ? whole : 1;
        uses -= spent;
        slot.State[SlotState.Uses] = uses;
        if (spec.Magazine is not null && uses < 1 - 1e-9 && slot.StateOr(SlotState.UsesReload, 0) <= 0)
        {
            slot.State[SlotState.UsesReload] = now + RefillSeconds(weave, spec, stats);
        }
        if (spec.Still is not null && sim.World.TryGet<WeaveBook>(caster, out var book))
        {
            EnsureUpkeep(caster, book);
        }
        sim.Events.Publish(new UsesChanged(caster, weave.Key, uses, UsesMax(weave, spec, stats)) { Tick = sim.Clock.Tick });
        return spent;
    }

    /// <summary>Puts charges back into a slot's bank, up to its size; a full magazine stops refilling.</summary>
    internal void RestoreUses(Simulation sim, Entity caster, WeaveSlot slot, CompiledWeave weave, UsesSpec spec, Stats stats, double amount)
    {
        var now = sim.Clock.Time;
        var max = UsesMax(weave, spec, stats);
        var uses = Math.Min(max, SettleUses(slot, weave, spec, stats, now) + Math.Max(0, amount));
        slot.State[SlotState.Uses] = uses;
        if (uses >= max - 1e-9)
        {
            slot.State[SlotState.UsesReload] = 0;
        }
        sim.Events.Publish(new UsesChanged(caster, weave.Key, uses, max) { Tick = sim.Clock.Tick });
    }

    /// <summary>
    /// The whole charges in a slot's bank now and its size, for the slot message; -1 (and a size of zero) for a
    /// weave without a bank. A pure read: the bank is not brought up to date by reading it.
    /// </summary>
    public int Uses(Simulation sim, Entity caster, WeaveSlot slot, out int max)
    {
        max = 0;
        if (!sim.World.TryGet<Stats>(caster, out var stats) || Resolve(sim, slot, out _) is not { } weave || weave.Casting.Uses is not { } spec)
        {
            return -1;
        }
        max = UsesMax(weave, spec, stats);
        return (int)Math.Floor(UsesNow(slot, weave, spec, stats, sim.Clock.Time) + 1e-9);
    }

    /// <summary>A still bank's beat: it recovers while the caster stands still and bleeds while it moves. True while it is not full.</summary>
    private bool UpkeepStill(Simulation sim, Entity caster, WeaveSlot slot, CompiledWeave weave, UsesSpec spec, Stats stats, double dt)
    {
        var max = UsesMax(weave, spec, stats);
        var uses = SettleUses(slot, weave, spec, stats, sim.Clock.Time);
        var before = uses;
        if (stats.Flags.Contains("moving"))
        {
            uses = Math.Max(0, uses - spec.Still!.Bleed * dt);
        }
        else
        {
            var recharge = RechargeSeconds(weave, spec, stats);
            uses = recharge <= 0 ? max : Math.Min(max, uses + dt / recharge);
        }
        slot.State[SlotState.Uses] = uses;
        if (Math.Floor(uses + 1e-9) != Math.Floor(before + 1e-9))
        {
            sim.Events.Publish(new UsesChanged(caster, weave.Key, uses, max) { Tick = sim.Clock.Tick });
        }
        return uses < max - 1e-9 || stats.Flags.Contains("moving");
    }
}
