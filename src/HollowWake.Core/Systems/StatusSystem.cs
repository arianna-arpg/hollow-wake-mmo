using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Sim;
using HollowWake.Core.StatusEffects;

namespace HollowWake.Core.Systems;

/// <summary>Ticks statuses, regenerates resources, maintains condition flags, clears expired temporaries.</summary>
public sealed class StatusSystem : SimSystem
{
    public override string Name => "statuses";
    public override SimPhase Phase => SimPhase.Status;

    // The resources that regenerate, read once per content set rather than every tick.
    private ContentRegistry? _content;
    private ResourceDef[] _regenerating = Array.Empty<ResourceDef>();

    public override void Update(Simulation sim)
    {
        var dt = sim.Clock.Dt;
        var now = sim.Clock.Time;
        var service = sim.Service<StatusService>();
        if (!ReferenceEquals(_content, sim.Content))
        {
            _content = sim.Content;
            _regenerating = sim.Content.Each<ResourceDef>().Where(r => r.Regen is not null).ToArray();
        }
        // The loop stays small and the per-body work lives in its own method, so the runtime can optimize a
        // long-running update without recompiling the whole body in the middle of a tick.
        foreach (var (e, stats) in sim.World.Query<Stats>())
        {
            stats.Now = now;
            if (sim.World.Has<Dead>(e))
            {
                continue;
            }
            Advance(sim, service, e, stats, now, dt);
        }
    }

    private void Advance(Simulation sim, StatusService service, Entity e, Stats stats, double now, double dt)
    {
        if (sim.World.TryGet<Statuses>(e, out var statuses))
        {
            service.Tick(sim, e, stats, statuses, dt);
            if (!sim.World.IsAlive(e) || sim.World.Has<Dead>(e))
            {
                return;
            }
        }
        var regenerating = _regenerating;
        for (var i = 0; i < regenerating.Length; i++)
        {
            var r = regenerating[i];
            var regen = stats.Get(r.Regen!);
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
