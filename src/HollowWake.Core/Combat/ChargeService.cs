using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Sim;

namespace HollowWake.Core.Combat;

/// <summary>
/// Counts, gains and spends named charges. Every change mirrors the count into the bearer's gauge
/// <c>charge.{id}</c>, so modifiers scale by charges and formulas read them without knowing this service.
/// Decay, drains and caps from charge definitions build on these three operations.
/// </summary>
public sealed class ChargeService
{
    /// <summary>The charges an entity holds of one kind.</summary>
    public double Count(Simulation sim, Entity entity, string id) =>
        sim.World.TryGet<Charges>(entity, out var charges) ? charges.Count(id) : 0;

    /// <summary>Adds charges up to <paramref name="max"/>; returns how many were gained.</summary>
    public double Gain(Simulation sim, Entity entity, string id, double amount, double max = double.PositiveInfinity)
    {
        if (amount <= 0 || !sim.World.IsAlive(entity))
        {
            return 0;
        }
        var charges = sim.World.GetOrAdd<Charges>(entity);
        var before = charges.Count(id);
        // A count already above the cap (the cap fell) is kept, never cut by a gain.
        var after = Math.Max(before, Math.Min(max, before + amount));
        Set(sim, entity, charges, id, after);
        return after - before;
    }

    /// <summary>Spends exactly <paramref name="amount"/> charges if the entity has them; false (and nothing spent) otherwise.</summary>
    public bool Spend(Simulation sim, Entity entity, string id, double amount)
    {
        if (amount <= 0)
        {
            return true;
        }
        if (!sim.World.TryGet<Charges>(entity, out var charges) || charges.Count(id) < amount - 1e-9)
        {
            return false;
        }
        Set(sim, entity, charges, id, charges.Count(id) - amount);
        return true;
    }

    /// <summary>Spends every charge of a kind and returns how many there were.</summary>
    public double SpendAll(Simulation sim, Entity entity, string id)
    {
        if (!sim.World.TryGet<Charges>(entity, out var charges))
        {
            return 0;
        }
        var count = charges.Count(id);
        if (count > 0)
        {
            Set(sim, entity, charges, id, 0);
        }
        return count;
    }

    private static void Set(Simulation sim, Entity entity, Charges charges, string id, double value)
    {
        if (value <= 0)
        {
            charges.Counts.Remove(id);
        }
        else
        {
            charges.Counts[id] = value;
        }
        sim.World.Get<Stats>(entity)?.SetGauge("charge." + id, value);
    }
}
