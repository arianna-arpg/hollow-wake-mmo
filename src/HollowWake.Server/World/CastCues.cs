using HollowWake.Core.Components;
using HollowWake.Core.Sim;
using HollowWake.Protocol;
using HollowWake.Server.Sessions;

namespace HollowWake.Server.World;

public sealed partial class ZoneInstance
{
    private long _castSequence;

    private CastCueMsg Cue(CastPresentationEvent e) => new(++_castSequence, e.CastId, e.Phase,
        e.Caster.Key, e.Target.Key, e.Weave, e.Name, e.Mode, e.Tags.Items.ToList(), e.Time,
        e.StartedAt, e.Duration, e.Position.X, e.Position.Y, e.Position.Z,
        e.Aim.X, e.Aim.Y, e.Aim.Z, e.Fired, e.Reason);

    private void SendCastCue(CastPresentationEvent e)
    {
        CastCueMsg? message = null;
        foreach (var s in Sessions)
        {
            if (!s.View.SendCastCues || (s.Entity != e.Caster && !s.Known.Contains(e.Caster.Key))) continue;
            if (e.Phase == "impact" && s.Entity != e.Target && !s.Known.Contains(e.Target.Key)) continue;
            message ??= Cue(e);
            // A hidden target's position/name must not be disclosed through a caster's cosmetic cue.
            var targetKnown = e.Target.IsNone || s.Entity == e.Target || s.Known.Contains(e.Target.Key);
            s.Send(targetKnown ? message : message with { Target = 0, AimX = message.X, AimY = message.Y, AimZ = message.Z });
        }
    }

    /// <summary>Joining interest reconstructs only an ongoing preparation, never past release/contact flourishes.</summary>
    private void SyncCastCue(Session session, EntityState state)
    {
        if (!session.View.SendCastCues) return;
        var entity = Sim.World.Resolve(state.Key);
        if (entity.IsNone || Sim.World.Get<WeaveBook>(entity)?.Casting is not { CastId: > 0 } cast ||
            (cast.Weave ?? cast.Slot.Compiled) is not { } weave) return;
        var point = Sim.World.Get<Transform>(entity)?.Position ?? cast.Point;
        session.Send(Cue(new CastPresentationEvent(cast.CastId, "sync", entity, Core.Ecs.Entity.None,
            weave.Key, weave.Name, cast.Mode.ToString().ToLowerInvariant(), weave.Tags, point, point,
            cast.StartedAt, cast.Duration, Sim.Clock.Time, false, "ongoing")));
    }
}
