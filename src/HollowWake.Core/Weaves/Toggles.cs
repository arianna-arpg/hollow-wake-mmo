using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Sim;

namespace HollowWake.Core.Weaves;

// Toggles: a weave pressed on and off. Turning on reserves part of resources' maximums (flat modifiers on
// {resource}.reserved under the toggle's source, which the usable maximum subtracts) and starts its upkeep; each tick the
// upkeep system pays the upkeep, growing by its ramp, and turns the toggle off when the bearer starves, the
// toggle runs past its longest duration, its slot leaves the rack or the bearer dies. Turning off runs the
// toggle's deactivation effects. The upkeep list also carries still banks and decaying brims.
public sealed partial class WeaveRuntime
{
    private readonly record struct ToggleEntry(Entity Caster, WeaveBook Book, WeaveSlot Slot, CompiledWeave Weave);

    private readonly List<ToggleEntry> _toggles = new();
    private readonly List<(Entity Caster, WeaveBook Book)> _upkeep = new();

    /// <summary>Whether a slot's toggle is on.</summary>
    public static bool IsToggledOn(WeaveSlot slot) => slot.StateOr(SlotState.Toggle, 0) > 0;

    private static string ToggleSource(WeaveSlot slot) => "toggle:" + slot.Key;

    private void ToggleOn(Simulation sim, Entity caster, WeaveBook book, Stats stats, WeaveSlot slot, CompiledWeave weave)
    {
        if (IsToggledOn(slot))
        {
            return;
        }
        var form = weave.Form;
        if (form.Reserve.Count > 0)
        {
            var source = ToggleSource(slot);
            var scope = CacheFor(slot, weave, stats).Scope;
            foreach (var (resource, expr) in form.Reserve)
            {
                var amount = Math.Max(0, expr.Evaluate(scope));
                if (amount <= 0)
                {
                    continue;
                }
                // The usable maximum is the resource's maximum less {resource}.reserved; the maximum itself is untouched.
                stats.Attributes.Add(new Modifier { Attribute = resource + ".reserved", Form = ModifierForm.Flat, Value = amount, Source = source });
                stats.SetResource(resource, stats.Resource(resource));
            }
        }
        slot.State[SlotState.Toggle] = 1;
        slot.State[SlotState.ToggleAt] = sim.Clock.Time;
        stats.Flags.Add("toggle." + form.Id);
        stats.Attributes.Touch();
        _toggles.Add(new ToggleEntry(caster, book, slot, weave));
        sim.Events.Publish(new WeaveToggled(caster, weave.Key, true, "press") { Tick = sim.Clock.Tick });
    }

    private void ToggleOff(Simulation sim, Entity caster, Stats stats, WeaveSlot slot, CompiledWeave weave, string reason)
    {
        if (!IsToggledOn(slot))
        {
            return;
        }
        slot.State[SlotState.Toggle] = 0;
        stats.Attributes.RemoveSource(ToggleSource(slot));
        stats.Flags.Remove("toggle." + weave.Form.Id);
        stats.Attributes.Touch();
        for (var i = _toggles.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(_toggles[i].Slot, slot))
            {
                _toggles.RemoveAt(i);
            }
        }
        if (weave.Casting.Deactivate.Count > 0 && sim.World.IsAlive(caster))
        {
            var t = sim.World.Get<Transform>(caster);
            var state = new CastState { Slot = slot, Weave = weave, Mode = CastMode.Instant, Target = caster, Point = t?.Position ?? default, Direction = t?.Forward ?? default, StartedAt = sim.Clock.Time };
            Run(sim, caster, stats, state, weave, weave.Casting.Deactivate, depth: 0, publishCast: false, default, rngPrefix: "toggle:");
        }
        sim.Events.Publish(new WeaveToggled(caster, weave.Key, false, reason) { Tick = sim.Clock.Tick });
    }

    /// <summary>Puts a book on the upkeep list (still banks, decaying brims) once.</summary>
    private void EnsureUpkeep(Entity caster, WeaveBook book)
    {
        if (!book.InUpkeep)
        {
            book.InUpkeep = true;
            _upkeep.Add((caster, book));
        }
    }

    /// <summary>
    /// One tick of upkeep, from the upkeep system: toggles pay or turn off, still banks recover or bleed,
    /// brims decay. Books that need nothing more leave the list.
    /// </summary>
    internal void Upkeep(Simulation sim)
    {
        var now = sim.Clock.Time;
        var dt = sim.Clock.Dt;
        for (var i = 0; i < _toggles.Count; i++)
        {
            var entry = _toggles[i];
            var before = _toggles.Count;
            UpkeepToggle(sim, entry, now, dt);
            if (_toggles.Count < before)
            {
                i--;
            }
        }
        var kept = 0;
        for (var i = 0; i < _upkeep.Count; i++)
        {
            var (caster, book) = _upkeep[i];
            if (sim.World.IsAlive(caster) && !sim.World.Has<Dead>(caster) && sim.World.TryGet<Stats>(caster, out var stats) && UpkeepBook(sim, caster, book, stats, now, dt))
            {
                _upkeep[kept++] = _upkeep[i];
            }
            else
            {
                book.InUpkeep = false;
            }
        }
        _upkeep.RemoveRange(kept, _upkeep.Count - kept);
    }

    private void UpkeepToggle(Simulation sim, ToggleEntry entry, double now, double dt)
    {
        var (caster, book, slot, weave) = entry;
        if (!sim.World.IsAlive(caster) || !sim.World.TryGet<Stats>(caster, out var stats))
        {
            _toggles.Remove(entry);
            return;
        }
        if (sim.World.Has<Dead>(caster))
        {
            ToggleOff(sim, caster, stats, slot, weave, "death");
            return;
        }
        if (!book.Slots.Contains(slot))
        {
            ToggleOff(sim, caster, stats, slot, weave, "unslotted");
            return;
        }
        var elapsed = now - slot.StateOr(SlotState.ToggleAt, now);
        if (weave.Form.Toggle?.MaxDuration is { } max && elapsed >= max.Evaluate(CacheFor(slot, weave, stats).Scope) - 1e-9)
        {
            ToggleOff(sim, caster, stats, slot, weave, "expired");
            return;
        }
        var plan = PlanFor(weave.Root);
        if (plan.Upkeep.Length > 0)
        {
            var ramp = weave.Form.Upkeep?.Ramp ?? 0;
            if (!TryPay(sim, weave, stats, CacheFor(slot, weave, stats).Scope, plan.Upkeep, dt * (1 + ramp * elapsed), out _))
            {
                ToggleOff(sim, caster, stats, slot, weave, "starved");
            }
        }
    }

    private bool UpkeepBook(Simulation sim, Entity caster, WeaveBook book, Stats stats, double now, double dt)
    {
        var busy = false;
        for (var i = 0; i < book.Slots.Count; i++)
        {
            var slot = book.Slots[i];
            if (slot.Compiled is not { } weave)
            {
                continue;
            }
            if (weave.Casting.Uses is { Manual: false, Still: not null } still)
            {
                busy |= UpkeepStill(sim, caster, slot, weave, still, stats, dt);
            }
            if (weave.Form.Channel?.Brim is { } brim)
            {
                // A brim being held fills instead; the book stays listed so it decays once the hold ends,
                // however it ends.
                if (book.Casting is { } cast && ReferenceEquals(cast.Slot, slot))
                {
                    busy = true;
                }
                else
                {
                    busy |= DecayBrim(stats, slot, brim, i, now);
                }
            }
        }
        return busy;
    }
}
