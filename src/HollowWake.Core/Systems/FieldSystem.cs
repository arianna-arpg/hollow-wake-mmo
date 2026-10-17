using System.Numerics;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Sim;
using HollowWake.Core.StatusEffects;

namespace HollowWake.Core.Systems;

/// <summary>Pulses standing fields on whoever stands inside them until they expire.</summary>
public sealed class FieldSystem : SimSystem
{
    public override string Name => "fields";
    public override SimPhase Phase => SimPhase.Move;
    public override int Order => 11;

    public override void Update(Simulation sim)
    {
        var now = sim.Clock.Time;
        foreach (var (e, t, f) in sim.World.Query<Transform, Field>())
        {
            if (!f.Follow.IsNone && sim.World.TryGet<Transform>(f.Follow, out var ft))
            {
                t.Position = ft.Position;
                sim.Spatial.Update(e, t.Position);
            }
            if (now >= f.ExpiresAt)
            {
                sim.Spatial.Remove(e);
                sim.Events.Publish(new EntityDespawned(e, "field") { Tick = sim.Clock.Tick });
                sim.World.DestroyDeferred(e);
                continue;
            }
            if (now < f.NextPulseAt)
            {
                continue;
            }
            f.NextPulseAt = now + f.Interval;
            foreach (var (other, _, _) in sim.Spatial.Within(t.Position, f.Radius, 3f).ToList())
            {
                if (other == e || !sim.World.Has<Stats>(other) || sim.World.Has<Dead>(other))
                {
                    continue;
                }
                var hostile = Relations.IsHostile(sim, f.Context.Origin, other);
                var pass = f.Filter switch
                {
                    "enemy" => hostile,
                    "ally" => !hostile && other != f.Context.Origin,
                    "ally_or_self" => !hostile,
                    _ => true,
                };
                if (pass)
                {
                    CompiledEffect.ExecuteAll(f.Pulse, f.Context.WithTarget(other));
                }
            }
        }
    }
}
