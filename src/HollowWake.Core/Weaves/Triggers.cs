using System.Numerics;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Expressions;
using HollowWake.Core.Maths;
using HollowWake.Core.Sim;

namespace HollowWake.Core.Weaves;

/// <summary>A book's triggered slots grouped by kind, its subscriptions, and its own random stream for the chance rolls.</summary>
internal sealed class BookTriggers
{
    public readonly List<TriggerGroup> Groups = new();
    public readonly List<int> Handles = new();
    public Rng Rng = null!;
    /// <summary>Triggered casts so far: salts their random streams.</summary>
    public long Fired;
}

/// <summary>The triggered slots of one kind, in bar order, and where the round robin goes next.</summary>
internal sealed class TriggerGroup
{
    public required TriggerKind Kind;
    public readonly List<TriggerEntry> Entries = new();
    public int Cursor;
}

internal readonly record struct TriggerEntry(WeaveSlot Slot, WeaveTrigger Trigger);

// Arming books and casting triggered weaves. A book is armed when its slots change: every seat compiles,
// worn modifiers go on, grafts are read, and the triggered seats subscribe to their kinds on the trigger
// hub for their caster. One event casts at most one triggered seat, chosen round-robin down the bar.
public sealed partial class WeaveRuntime
{
    // The depth of the triggered cast running now, so events its own effects raise (casts publish no depth)
    // count as deeper; -1 when none runs.
    private int _triggerDepth = -1;
    private bool _feeds;

    /// <summary>Arms a book whose slots changed since it was last armed.</summary>
    internal void EnsureArmed(Simulation sim, Entity caster, WeaveBook book, Stats? stats = null)
    {
        if (book.ArmedVersion == book.Version && book.ArmedCount == book.Slots.Count)
        {
            return;
        }
        Arm(sim, caster, book, stats ?? sim.World.Get<Stats>(caster));
    }

    /// <summary>
    /// Arms a book: reads the seats' grafts, compiles every seat (misfits dormant), puts on the modifiers
    /// slotted weaves grant their caster (source <c>weave:{seat}</c>), and subscribes the triggered seats.
    /// </summary>
    private void Arm(Simulation sim, Entity caster, WeaveBook book, Stats? stats)
    {
        book.ArmedVersion = book.Version;
        book.ArmedCount = book.Slots.Count;
        EnsureFeeds(sim);
        var hub = sim.TryService<TriggerHub>(out var h) ? h : null;
        var triggers = book.Triggers;
        if (triggers is not null)
        {
            foreach (var handle in triggers.Handles)
            {
                hub?.Unsubscribe(handle);
            }
            triggers.Handles.Clear();
            triggers.Groups.Clear();
        }
        if (stats is null)
        {
            return;
        }
        if (book.WornSources is { Count: > 0 } worn)
        {
            foreach (var source in worn)
            {
                stats.Attributes.RemoveSource(source);
            }
            worn.Clear();
        }
        RefreshGrafts(stats, book);
        for (var i = 0; i < book.Slots.Count; i++)
        {
            var slot = book.Slots[i];
            if (slot.Spec.Preset is null && slot.Spec.Form is null)
            {
                continue;
            }
            var weave = Resolve(sim, slot, out _);
            if (weave is null)
            {
                continue;
            }
            var casting = weave.Casting;
            if (casting.EquipModifiers.Count > 0)
            {
                var source = "weave:" + (i + 1);
                stats.Attributes.AddRange(casting.EquipModifiers, source);
                (book.WornSources ??= new List<string>()).Add(source);
            }
            foreach (var trigger in casting.Triggers)
            {
                triggers ??= book.Triggers = new BookTriggers { Rng = new Rng(Rng.Combine(Rng.Combine(sim.Rng.Seed, (ulong)caster.Key), Rng.Hash64("triggers"))) };
                TriggerGroup? group = null;
                foreach (var g in triggers.Groups)
                {
                    if (g.Kind == trigger.Kind)
                    {
                        group = g;
                        break;
                    }
                }
                if (group is null)
                {
                    group = new TriggerGroup { Kind = trigger.Kind };
                    triggers.Groups.Add(group);
                }
                group.Entries.Add(new TriggerEntry(slot, trigger));
            }
        }
        if (triggers is null || hub is null)
        {
            return;
        }
        foreach (var group in triggers.Groups)
        {
            var g = group;
            triggers.Handles.Add(hub.Subscribe(g.Kind, caster, (in TriggerEvent e) => OnTrigger(sim, caster, book, g, e)));
        }
    }

    /// <summary>Events that change what a book's seats grant: gear and passives re-arm their bearer's book.</summary>
    private void EnsureFeeds(Simulation sim)
    {
        if (_feeds)
        {
            return;
        }
        _feeds = true;
        sim.Events.Subscribe<PassiveAllocated>(p => sim.World.Get<WeaveBook>(p.Entity)?.Changed());
        sim.Events.Subscribe<ItemEquipped>(p => sim.World.Get<WeaveBook>(p.Entity)?.Changed());
    }

    /// <summary>A triggered weave's key arms and disarms it.</summary>
    private static void ToggleArmed(Simulation sim, Entity caster, WeaveSlot slot, CompiledWeave weave)
    {
        var armed = slot.StateOr(SlotState.Armed, 1) <= 0;
        slot.State[SlotState.Armed] = armed ? 1 : 0;
        sim.Events.Publish(new TriggerArmed(caster, weave.Key, armed) { Tick = sim.Clock.Tick });
    }

    /// <summary>
    /// One event of a kind for a caster: damage taken feeds every life-fraction trigger, then the seats are
    /// tried round-robin from the one after the last that fired, and the first that may and does fire casts.
    /// </summary>
    private void OnTrigger(Simulation sim, Entity owner, WeaveBook book, TriggerGroup group, in TriggerEvent e)
    {
        if (!sim.World.IsAlive(owner) || sim.World.Has<Dead>(owner) || !sim.World.TryGet<Stats>(owner, out var stats))
        {
            return;
        }
        var rules = RulesOf(sim);
        var depth = Math.Max(e.Depth, _triggerDepth);
        if (depth >= rules.TriggerMaxDepth)
        {
            return;
        }
        var entries = group.Entries;
        var n = entries.Count;
        if (n == 0)
        {
            return;
        }
        if (e.Amount > 0)
        {
            for (var i = 0; i < n; i++)
            {
                var entry = entries[i];
                if (entry.Trigger.Spec.LifeFraction is not null)
                {
                    var max = Math.Max(1, stats.ResourceMax("life"));
                    entry.Slot.State[entry.Trigger.AccumKey] = entry.Slot.StateOr(entry.Trigger.AccumKey, 0) + e.Amount / max;
                }
            }
        }
        var start = group.Cursor % n;
        for (var k = 0; k < n; k++)
        {
            var i = (start + k) % n;
            if (TryFire(sim, owner, book, stats, group, entries[i], e, depth, rules))
            {
                group.Cursor = (i + 1) % n;
                return;
            }
        }
    }

    private bool TryFire(Simulation sim, Entity owner, WeaveBook book, Stats stats, TriggerGroup group, TriggerEntry entry, in TriggerEvent e, int depth, CastingRules rules)
    {
        var slot = entry.Slot;
        var trigger = entry.Trigger;
        var spec = trigger.Spec;
        var now = sim.Clock.Time;
        if (slot.StateOr(SlotState.Armed, 1) <= 0 || now < slot.StateOr(trigger.IcdKey, double.NegativeInfinity))
        {
            return false;
        }
        if (spec.Status is not null && !string.Equals(spec.Status, e.Status, StringComparison.Ordinal))
        {
            return false;
        }
        if (!spec.Tags.IsEmpty && !e.Tags.HasAny(spec.Tags))
        {
            return false;
        }
        if (spec.LifeFraction is { } fraction && slot.StateOr(trigger.AccumKey, 0) < fraction - 1e-9)
        {
            return false;
        }
        var weave = Resolve(sim, slot, out _);
        if (weave is null || weave.IsHeld || weave.IsToggle || weave.Tags.Has("untriggerable"))
        {
            return false;
        }
        if (stats.HasFlag("stunned") || stats.HasFlag("frozen"))
        {
            return false;
        }
        if (ForbiddenTag(stats, weave) is not null)
        {
            return false;
        }
        var cache = CacheFor(slot, weave, stats);
        // The cast-time gate: only quick weaves trigger, unless a permit rides along.
        var castTime = weave.Form.CastTime is null ? 0 : Math.Max(0, weave.Form.CastTime.Evaluate(cache.Scope));
        if (castTime > (spec.MaxCastTime ?? rules.TriggerMaxCastTime) + 1e-9 && !weave.HasMechanism("trigger_permit"))
        {
            return false;
        }
        if (!book.IsReady(weave.Key, now))
        {
            return false;
        }
        if (weave.Casting.Uses is { Empower.Count: 0 } uses && UsesNow(slot, weave, uses, stats, now) < 1)
        {
            return false;
        }
        if (!GatesHold(weave, cache.Scope) || !CanAfford(weave, stats, cache.Scope, cache, out _))
        {
            return false;
        }
        if (!TriggerTarget(sim, owner, weave, e, out var target, out var point, out var direction))
        {
            return false;
        }
        var chance = Math.Clamp(weave.Attr(stats, "trigger.chance", spec.Chance is null ? 1 : spec.Chance.Evaluate(cache.Scope)), 0, rules.TriggerChanceCap);
        var triggers = book.Triggers!;
        if (!triggers.Rng.Chance(chance))
        {
            return false;
        }

        // It fires: the internal cooldown starts, the accumulated damage is spent, the cast pays honestly.
        slot.State[trigger.IcdKey] = now + spec.Icd;
        if (spec.LifeFraction is { } spent)
        {
            slot.State[trigger.AccumKey] = Math.Max(0, slot.StateOr(trigger.AccumKey, 0) - spent);
        }
        TryPay(sim, weave, stats, cache.Scope, PlanFor(weave.Root).Costs, 1, out _);
        var usesSpent = weave.Casting.Uses is { } bank ? SpendUses(sim, owner, slot, weave, bank, stats, now) : 0;
        double? power = null;
        if (spec.Power is { } powerExpr)
        {
            power = powerExpr.Evaluate(new ScopeOverlay(cache.Scope).Set("amount", e.Amount));
        }
        var state = new CastState
        {
            Slot = slot,
            Weave = weave,
            Mode = CastMode.Instant,
            Target = target,
            Point = point,
            Direction = direction,
            StartedAt = now,
            Pulses = (int)(triggers.Fired++ & int.MaxValue),
            UsesSpent = usesSpent,
        };
        sim.Events.Publish(new WeaveTriggered(owner, weave.Key, group.Kind.Name, target, depth + 1) { Tick = sim.Clock.Tick });
        var previous = _triggerDepth;
        _triggerDepth = depth + 1;
        try
        {
            ResolveUse(sim, owner, book, stats, state, weave, depth + 1, new CastVarValues { TriggerPower = power, TriggerAmount = e.Amount }, triggered: true, rngPrefix: "trigger:");
        }
        finally
        {
            _triggerDepth = previous;
        }
        return true;
    }

    /// <summary>
    /// Where a triggered weave goes: at the other party of the event (the struck body for the owner's hits,
    /// the attacker for hits on the owner) for weaves aimed at enemies, points or directions; at the owner
    /// for self and ally weaves.
    /// </summary>
    private static bool TriggerTarget(Simulation sim, Entity owner, CompiledWeave weave, in TriggerEvent e, out Entity target, out Vector3 point, out Vector2 direction)
    {
        var ot = sim.World.Get<Transform>(owner);
        var other = e.Target == owner || e.Target.IsNone ? e.Source : e.Target;
        if (other == owner || (!other.IsNone && (!sim.World.IsAlive(other) || sim.World.Has<Dead>(other))))
        {
            other = Entity.None;
        }
        var otherAt = !other.IsNone && sim.World.TryGet<Transform>(other, out var tt) ? tt.Position : (Vector3?)null;
        point = otherAt ?? ot?.Position ?? default;
        direction = ot is null ? Vector2.UnitX : ot.Forward;
        if (ot is not null && otherAt is { } at)
        {
            var d = new Vector2(at.X - ot.Position.X, at.Y - ot.Position.Y);
            if (d.LengthSquared() > 1e-6f)
            {
                direction = Vector2.Normalize(d);
            }
        }
        switch (weave.Targeting)
        {
            case Targeting.Self:
            case Targeting.Ally:
                target = owner;
                point = ot?.Position ?? point;
                return true;
            case Targeting.Enemy:
            case Targeting.Any:
                target = other;
                return !other.IsNone && (weave.Targeting == Targeting.Any || Relations.IsHostile(sim, owner, other));
            default:
                target = Entity.None;
                return otherAt is not null || weave.Targeting == Targeting.Direction;
        }
    }
}
