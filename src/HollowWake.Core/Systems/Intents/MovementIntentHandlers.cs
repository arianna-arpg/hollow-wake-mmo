using System.Numerics;
using HollowWake.Core.Intents;
using HollowWake.Core.Movement;

namespace HollowWake.Core.Systems.Intents;

/// <summary>Walking to a point, steering by direction, stopping and jumping: any mover may send them.</summary>
public static class MovementIntentHandlers
{
    public static void Register(IntentHandlers handlers)
    {
        handlers.Register<MoveToIntent>(MoveTo);
        handlers.Register<MoveDirIntent>(MoveDir);
        handlers.Register<StopIntent>(Stop);
        handlers.Register<JumpIntent>(Jump);
    }

    /// <summary>
    /// Plans the walk under the shared policy of <see cref="MovePlanner"/>, for a click and a chase alike. The
    /// search budget goes by the sender's kind, a player's or any other actor's, never by what the intent says.
    /// </summary>
    private static void MoveTo(in IntentContext ctx, MoveToIntent m)
    {
        var motion = ctx.Motion;
        var transform = ctx.Transform;
        if (motion is null || transform is null)
        {
            ctx.Reject("unknown_intent");
            return;
        }
        if (!float.IsFinite(m.Point.X) || !float.IsFinite(m.Point.Y) || !float.IsFinite(m.Point.Z))
        {
            ctx.Reject("bad_point");
            return;
        }
        var stop = m.Stop is { } s && float.IsFinite(s) ? Math.Max(0f, s) : MovePlanner.DefaultStop;
        MovePlanner.Of(ctx.Sim).Plan(ctx.Sim, transform, motion, m.Point, stop, m.KeepPlan, player: ctx.Player is not null);
    }

    private static void MoveDir(in IntentContext ctx, MoveDirIntent d)
    {
        if (ctx.Motion is not { } motion)
        {
            ctx.Reject("unknown_intent");
            return;
        }
        motion.Path.Clear();
        motion.MoveTarget = null;
        motion.MoveDir = d.Direction.LengthSquared() > 1 ? Vector2.Normalize(d.Direction) : d.Direction;
    }

    private static void Stop(in IntentContext ctx, StopIntent _)
    {
        if (ctx.Motion is not { } motion)
        {
            ctx.Reject("unknown_intent");
            return;
        }
        motion.Stop();
    }

    private static void Jump(in IntentContext ctx, JumpIntent _)
    {
        if (ctx.Motion is not { } motion)
        {
            ctx.Reject("unknown_intent");
            return;
        }
        motion.JumpRequested = true;
    }
}
