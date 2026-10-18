using System.Runtime.CompilerServices;
using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Sim;

namespace HollowWake.Core.Combat;

/// <summary>
/// Counts, gains, spends, decays and drains named charges. Every change mirrors the count into the bearer's
/// gauge <c>charge.{id}</c>, so modifiers scale by charges and formulas read them without knowing this
/// service. The most a body holds is its attribute <c>charge.{id}.max</c>; a charge kind's definition (kind
/// <c>charges</c>) says how long charges last after the last gain, how fast they decay once that time runs
/// out, and what each charge held grants: the kind's modifiers join the bearer with its first charge and leave
/// with its last, scaled by the gauge in between. A drain burns charges down at a rate and refuses gains while
/// it lasts, its rate read as the gauge <c>charge.{id}.drain</c>. Kinds without a declared maximum hold any
/// amount, and kinds without a definition never decay and grant nothing.
/// </summary>
public sealed class ChargeService
{
    /// <summary>The charges an entity holds of one kind.</summary>
    public double Count(Simulation sim, Entity entity, string id) =>
        sim.World.TryGet<Charges>(entity, out var charges) ? charges.Count(id) : 0;

    /// <summary>The most an entity can hold of a kind: its <c>charge.{id}.max</c>; unbounded where nothing declares it.</summary>
    public double Max(Simulation sim, Entity entity, string id)
    {
        var attribute = ChargeNames.MaxOf(id);
        if (!sim.World.TryGet<Stats>(entity, out var stats) || !(sim.Content.Attributes.Contains(attribute) || stats.Attributes.HasModifiers(attribute)))
        {
            return double.PositiveInfinity;
        }
        return Math.Max(0, stats.Attributes.Get(attribute, stats));
    }

    /// <summary>
    /// Adds charges up to <paramref name="max"/> (the kind's own maximum when left out); returns how many were
    /// gained. A drain in progress refuses the gain. Any gain restarts the kind's duration.
    /// </summary>
    public double Gain(Simulation sim, Entity entity, string id, double amount, double max = double.PositiveInfinity, int depth = 0)
    {
        if (amount <= 0 || !sim.World.IsAlive(entity))
        {
            return 0;
        }
        var charges = sim.World.GetOrAdd<Charges>(entity);
        var clock = charges.ClockOf(id);
        if (clock.Drain > 0)
        {
            if (charges.Count(id) > 0)
            {
                return 0;
            }
            // A drain ends with the last charge, however that charge went.
            clock.Drain = 0;
        }
        var cap = double.IsPositiveInfinity(max) ? Max(sim, entity, id) : max;
        var before = charges.Count(id);
        // A count already above the cap (the cap fell) is kept, never cut by a gain.
        var after = Math.Max(before, Math.Min(cap, before + amount));
        clock.LastGain = sim.Clock.Time;
        clock.Owed = 0;
        if (after > before)
        {
            Set(sim, entity, charges, id, after);
            sim.Events.Publish(new ChargeGained(entity, id, after - before, after) { Tick = sim.Clock.Tick, Depth = depth });
            CombatTriggers.Raise(sim, entity, CombatTriggers.ChargeGained, entity, Entity.None, id, depth, after - before);
        }
        return after - before;
    }

    /// <summary>Spends exactly <paramref name="amount"/> charges if the entity has them; false (and nothing spent) otherwise.</summary>
    public bool Spend(Simulation sim, Entity entity, string id, double amount, int depth = 0)
    {
        if (amount <= 0)
        {
            return true;
        }
        if (!sim.World.TryGet<Charges>(entity, out var charges) || charges.Count(id) < amount - 1e-9)
        {
            return false;
        }
        var after = charges.Count(id) - amount;
        Set(sim, entity, charges, id, after);
        sim.Events.Publish(new ChargeSpent(entity, id, amount, Math.Max(0, after)) { Tick = sim.Clock.Tick, Depth = depth });
        return true;
    }

    /// <summary>Spends every charge of a kind and returns how many there were.</summary>
    public double SpendAll(Simulation sim, Entity entity, string id, int depth = 0)
    {
        if (!sim.World.TryGet<Charges>(entity, out var charges))
        {
            return 0;
        }
        var count = charges.Count(id);
        if (count > 0)
        {
            Set(sim, entity, charges, id, 0);
            sim.Events.Publish(new ChargeSpent(entity, id, count, 0) { Tick = sim.Clock.Tick, Depth = depth });
        }
        return count;
    }

    /// <summary>
    /// Burns a kind down at <paramref name="rate"/> charges per second until none are left, refusing gains
    /// meanwhile; a rate of zero stops a drain. Nothing happens to a body holding none. The rate reads as the
    /// gauge <c>charge.{id}.drain</c> while the drain lasts, so a modifier can hold only while its kind drains.
    /// </summary>
    public void Drain(Simulation sim, Entity entity, string id, double rate)
    {
        if (!sim.World.TryGet<Charges>(entity, out var charges) || (charges.Count(id) <= 0 && rate > 0))
        {
            return;
        }
        var clock = charges.ClockOf(id);
        clock.Drain = Math.Max(0, rate);
        clock.Owed = 0;
        sim.World.Get<Stats>(entity)?.SetGauge(ChargeNames.DrainOf(id), clock.Drain);
    }

    /// <summary>Advances decay and drains on one body by <paramref name="dt"/> seconds. The status system calls it.</summary>
    public void Tick(Simulation sim, Entity entity, Charges charges, double dt)
    {
        if (charges.Counts.Count == 0)
        {
            return;
        }
        var now = sim.Clock.Time;
        foreach (var (id, clock) in charges.Clocks)
        {
            var count = charges.Count(id);
            if (count <= 0)
            {
                clock.Drain = 0;
                clock.Owed = 0;
                continue;
            }
            double rate;
            string reason;
            if (clock.Drain > 0)
            {
                rate = clock.Drain;
                reason = "drained";
            }
            else
            {
                if (!sim.Content.TryGet<ChargeDef>(id, out var def) || def?.Duration is null)
                {
                    continue;
                }
                var stats = sim.World.Get<Stats>(entity);
                var duration = stats is null ? def.Duration.Evaluate(Expressions.EmptyScope.Instance) : def.Duration.Evaluate(stats);
                if (now - clock.LastGain < duration)
                {
                    continue;
                }
                if (def.Decay <= 0)
                {
                    LoseCharges(sim, entity, charges, clock, id, count, "decayed");
                    continue;
                }
                rate = def.Decay;
                reason = "decayed";
            }
            clock.Owed += rate * dt;
            if (clock.Owed >= 1)
            {
                var lost = Math.Min(count, Math.Floor(clock.Owed));
                clock.Owed -= lost;
                LoseCharges(sim, entity, charges, clock, id, lost, reason);
            }
        }
    }

    private void LoseCharges(Simulation sim, Entity entity, Charges charges, ChargeClock clock, string id, double lost, string reason)
    {
        var after = Math.Max(0, charges.Count(id) - lost);
        Set(sim, entity, charges, id, after);
        if (after <= 0)
        {
            clock.Drain = 0;
            clock.Owed = 0;
        }
        sim.Events.Publish(new ChargeLost(entity, id, lost, after, reason) { Tick = sim.Clock.Tick });
    }

    private static void Set(Simulation sim, Entity entity, Charges charges, string id, double value)
    {
        var held = charges.Count(id) > 0;
        if (value <= 0)
        {
            charges.Counts.Remove(id);
        }
        else
        {
            charges.Counts[id] = value;
        }
        if (sim.World.Get<Stats>(entity) is not { } stats)
        {
            return;
        }
        stats.SetGauge(ChargeNames.GaugeOf(id), value);
        if (value > 0)
        {
            // The kind's modifiers join with the first charge; from then on the gauge scales them by the count.
            if (!held && sim.Content.TryGet<ChargeDef>(id, out var def) && def is { Modifiers.Count: > 0 })
            {
                foreach (var m in ChargeModifiers.Of(def))
                {
                    stats.Attributes.Add(m);
                }
            }
        }
        else if (held)
        {
            // They leave with the last charge, and a drain ends with it.
            stats.Attributes.RemoveSource(ChargeNames.SourceOf(id));
            if (stats.Gauge(ChargeNames.DrainOf(id)) > 0)
            {
                stats.SetGauge(ChargeNames.DrainOf(id), 0);
            }
        }
    }
}

/// <summary>The names a charge kind is read by, built once per id.</summary>
public static class ChargeNames
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string Gauge, string Max, string Drain, string Source)> Names = new(StringComparer.Ordinal);

    private static (string Gauge, string Max, string Drain, string Source) Of(string id) =>
        Names.GetOrAdd(id, static i => ("charge." + i, "charge." + i + ".max", "charge." + i + ".drain", "charge:" + i));

    /// <summary>The gauge holding the count: <c>charge.{id}</c>.</summary>
    public static string GaugeOf(string id) => Of(id).Gauge;

    /// <summary>The attribute holding the most a body holds: <c>charge.{id}.max</c>, which the interface reads beside the count.</summary>
    public static string MaxOf(string id) => Of(id).Max;

    /// <summary>The gauge holding a drain's rate while it burns: <c>charge.{id}.drain</c>.</summary>
    public static string DrainOf(string id) => Of(id).Drain;

    /// <summary>The source of the modifiers the kind grants its holder: <c>charge:{id}</c>.</summary>
    public static string SourceOf(string id) => Of(id).Source;
}

/// <summary>
/// The modifiers a charge kind grants per charge held (<c>modifiers</c> on its definition), as its holders carry
/// them: each scaled by the count through the gauge <c>charge.{id}</c>, under the source <c>charge:{id}</c>. Built
/// once per definition, so tooltips and the fold read the same data.
/// </summary>
public static class ChargeModifiers
{
    private static readonly ConditionalWeakTable<ChargeDef, Modifier[]> Cache = new();

    public static Modifier[] Of(ChargeDef def) => Cache.GetValue(def, static d =>
    {
        var gauge = ChargeNames.GaugeOf(d.Id);
        var source = ChargeNames.SourceOf(d.Id);
        var granted = new Modifier[d.Modifiers.Count];
        for (var i = 0; i < granted.Length; i++)
        {
            granted[i] = d.Modifiers[i] with { Gauge = gauge, GaugeAt = null, Source = source };
        }
        return granted;
    });
}
