using System.Numerics;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Sim;
using HollowWake.Core.StatusEffects;

namespace HollowWake.Core.Systems;

/// <summary>Ticks statuses, regenerates resources, maintains condition flags, clears expired temporaries.</summary>
public sealed class StatusSystem : SimSystem
{
    public override string Name => "statuses";
    public override SimPhase Phase => SimPhase.Status;

    public override void Update(Simulation sim)
    {
        var dt = sim.Clock.Dt;
        var now = sim.Clock.Time;
        var service = sim.Service<StatusService>();
        var resources = sim.Content.Each<ResourceDef>().ToList();
        foreach (var (e, stats) in sim.World.Query<Stats>())
        {
            stats.Now = now;
            if (sim.World.Has<Dead>(e))
            {
                continue;
            }
            if (sim.World.TryGet<Statuses>(e, out var statuses))
            {
                service.Tick(sim, e, stats, statuses, dt);
                if (!sim.World.IsAlive(e) || sim.World.Has<Dead>(e))
                {
                    continue;
                }
            }
            foreach (var r in resources)
            {
                if (r.Regen is null)
                {
                    continue;
                }
                var regen = stats.Get(r.Regen);
                if (regen != 0)
                {
                    stats.AddResource(r.Id, regen * dt);
                }
            }
            if (sim.World.TryGet<Motion>(e, out var m))
            {
                var moving = now - m.LastMoveTime <= 0.15;
                var stationary = now - m.LastMoveTime >= 0.6;
                Set(stats, "moving", moving);
                Set(stats, "stationary", stationary);
                Set(stats, "airborne", !m.Grounded && !m.Flying);
            }
        }
    }

    private static void Set(Stats stats, string flag, bool on)
    {
        if (on)
        {
            stats.Flags.Add(flag);
        }
        else
        {
            stats.Flags.Remove(flag);
        }
    }
}
