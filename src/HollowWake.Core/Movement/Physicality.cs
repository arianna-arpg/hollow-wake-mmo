using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Sim;

namespace HollowWake.Core.Movement;

/// <summary>Mass through the common modifier engine, then poise and shove authority.</summary>
public static class Physicality
{
    public static double Mass(Simulation sim, Entity entity)
    {
        var profiled = sim.World.Get<PhysicalBody>(entity) is not null;
        var mass = sim.Content.Rules.Number("physicality.reference_mass", 80);
        if (sim.World.Get<PhysicalBody>(entity) is { } body && sim.World.Get<Transform>(entity) is { } t)
        {
            var p = body.Profile;
            var horizontal = t.Radius / body.Radius;
            var vertical = t.Height / p.Height;
            mass = p.Width * p.Depth * p.Height * p.Fill * p.Heft * body.Density * horizontal * horizontal * vertical;
        }
        if (sim.World.Get<Stats>(entity) is { } stats)
        {
            // External geometric base, exactly the same fold as an externally supplied damage amount.
            mass = profiled ? stats.Attributes.Get("mass", stats, stats.Tags, null, mass) : stats.Get("mass");
        }
        return Math.Max(sim.Content.Rules.Number("physicality.minimum_mass", 0.1), mass);
    }

    public static double EffectiveWeight(Simulation sim, Entity entity)
    {
        var weight = Mass(sim, entity) / sim.Content.Rules.Number("physicality.reference_mass", 80);
        var stats = sim.World.Get<Stats>(entity);
        if (stats is not null && !stats.HasFlag("pool.poise.broken"))
        {
            weight *= 1 + Math.Max(0, stats.Resource("poise")) * sim.Content.Rules.Number("physicality.poise_anchor", 0.012);
        }
        return Math.Max(sim.Content.Rules.Number("physicality.minimum_weight", 0.05), weight);
    }

    public static double ShoveScale(Simulation sim, Entity source, Entity target)
    {
        var rules = sim.Content.Rules;
        var authority = source.IsNone || !sim.World.IsAlive(source) ? 1 : Math.Clamp(
            Math.Pow(EffectiveWeight(sim, source), rules.Number("physicality.authority_power", 0.55)),
            rules.Number("physicality.authority_min", 0.4), rules.Number("physicality.authority_max", 2.75));
        return authority / EffectiveWeight(sim, target);
    }
}
