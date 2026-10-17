using System.Numerics;
using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Definitions;
using HollowWake.Core.Sim;

namespace HollowWake.Core.Weaves;

public sealed partial class WeaveRuntime
{
    private long _nextCastId;

    private void Present(Simulation sim, Entity caster, CastState cast, CompiledWeave weave,
        string phase, string reason = "")
    {
        if (cast.CastId == 0) cast.CastId = ++_nextCastId;
        if (phase is "end" or "interrupt")
        {
            if (cast.PresentationEnded) return;
            cast.PresentationEnded = true;
        }
        if (phase == "release") cast.PresentationReleases++;
        var position = sim.World.Get<Transform>(caster)?.Position ?? cast.Point;
        var aim = weave.Targeting == Targeting.Direction ? position + new Vector3(cast.Direction, 0)
            : sim.World.Get<Transform>(cast.Target)?.Position ?? cast.Point;
        sim.Events.Publish(new CastPresentationEvent(cast.CastId, phase, caster, cast.Target,
            weave.Key, weave.Name, cast.Mode.ToString().ToLowerInvariant(), weave.Tags,
            position, aim, cast.StartedAt, cast.Duration, sim.Clock.Time,
            cast.PresentationReleases > 0, reason) { Tick = sim.Clock.Tick });
    }
}
