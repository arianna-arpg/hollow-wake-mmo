using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Ecs;
using HollowWake.Core.Expressions;
using HollowWake.Core.Sim;

namespace HollowWake.Core.Movement;

/// <summary>Canonical body mass and content-authored weight/force policy.</summary>
public static class Physicality
{
    /// <summary>Mount geometry as input gauges to the ordinary mass formula; never bypass its base override.</summary>
    public static void Bind(Stats stats, PhysicalBody body, Transform transform)
    {
        var p = body.Profile;
        stats.SetGauge("physical.present", 1);
        stats.SetGauge("physical.density", body.Density);
        stats.SetGauge("physical.fill", p.Fill);
        stats.SetGauge("physical.heft", p.Heft);
        void Resize()
        {
            var horizontal = transform.Radius / body.Radius;
            var vertical = transform.Height / p.Height;
            stats.SetGauge("physical.volume", p.Width * p.Depth * p.Height * horizontal * horizontal * vertical);
        }
        transform.DimensionsChanged += Resize;
        Resize();
    }

    public static double Mass(Simulation sim, Entity entity) => sim.World.Get<Stats>(entity)?.Get("mass")
        ?? sim.Content.Attributes.Get("mass")?.Base ?? sim.Content.Rules.Number("physicality.reference_mass", 80);

    public static double EffectiveWeight(Simulation sim, Entity entity) => PolicyOf(sim).Weight(sim, entity);

    /// <summary>Includes the query's shove authority before the final authored clamp.</summary>
    public static double ShoveScale(Simulation sim, Entity source, Entity target, double? authority = null)
    {
        var policy = PolicyOf(sim);
        var present = !source.IsNone && sim.World.IsAlive(source);
        var sourceWeight = present ? policy.Weight(sim, source) : 1;
        var targetWeight = policy.Weight(sim, target);
        return policy.Scale(sourceWeight, targetWeight, authority ?? sim.World.Get<Stats>(source)?.Get("shove.authority") ?? 1, present);
    }

    private static Policy PolicyOf(Simulation sim)
    {
        if (!sim.TryService<Policy>(out var policy) || !ReferenceEquals(policy.Rules, sim.Content.Rules))
        {
            policy = new Policy(sim);
            sim.AddService(policy);
        }
        return policy;
    }

    // Per simulation, never shared mutable scope between zone worker threads. Queries are sequential
    // and expressions finish before another query mounts its inputs; steady-state evaluation allocates nothing.
    private sealed class Policy
    {
        public readonly Rules Rules;
        private readonly Expr _weight;
        private readonly Expr _shove;
        private readonly DictScope _scope;
        private readonly string _anchorPool;
        private readonly string _brokenFlag;

        public Policy(Simulation sim)
        {
            Rules = sim.Content.Rules;
            _scope = new DictScope(sim.RngFor(Entity.None, "physicality"));
            _weight = Rules.Expr("physicality.weight", "1");
            _shove = Rules.Expr("physicality.shove", "1");
            _anchorPool = Rules.Text("physicality.anchor_pool", "");
            _brokenFlag = Rules.Text("physicality.anchor_broken_flag", "");
            foreach (var key in new[] { "reference_mass", "minimum_weight", "poise_anchor", "authority_power", "authority_min", "authority_max", "shove_scale_min", "shove_scale_max" })
                _scope.Set(key, Rules.Number("physicality." + key, 0));
        }

        public double Weight(Simulation sim, Entity entity)
        {
            var stats = sim.World.Get<Stats>(entity);
            var anchor = stats is not null && !stats.HasFlag(_brokenFlag) ? stats.Resource(_anchorPool) : 0;
            _scope.Set("mass", Mass(sim, entity)).Set("anchor", Math.Max(0, anchor));
            return _weight.Evaluate(_scope);
        }

        public double Scale(double source, double target, double authority, bool present)
        {
            _scope.Set("source_weight", source).Set("target_weight", target)
                .Set("authority", authority).Set("source_present", present ? 1 : 0);
            return _shove.Evaluate(_scope);
        }
    }
}
