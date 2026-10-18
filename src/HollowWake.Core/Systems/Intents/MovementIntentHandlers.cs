using System.Numerics;
using HollowWake.Core.Intents;
using HollowWake.Core.Movement;

namespace HollowWake.Core.Systems.Intents;

/// <summary>Walking to a point, steering by direction, stopping, jumping and facing: any mover may send them.</summary>
public static class MovementIntentHandlers
{
    public static void Register(IntentHandlers handlers)
    {
        handlers.Register<MoveToIntent>(MoveTo);
        handlers.Register<MoveDirIntent>(MoveDir);
        handlers.Register<StopIntent>(Stop);
        handlers.Register<JumpIntent>(Jump);
        handlers.Register<FaceIntent>(Face);
    }

    /// <summary>Plans the walk under the shared policy of <see cref="MovePlanner"/>, for a click and a chase alike.</summary>
    private static void MoveTo(in IntentContext ctx, MoveToIntent m)
    {
        var motion = ctx.Motion;
        var transform = ctx.Transform;
        if (motion is null || transform is null)
        {
            ctx.Reject("unknown_intent");
            return;
        }
        if (!Finite(m.Point))
        {
            ctx.Reject("bad_point");
            return;
        }
        var stop = m.Stop is { } s && float.IsFinite(s) ? Math.Max(0f, s) : MovePlanner.DefaultStop;
        MovePlanner.Of(ctx.Sim).Plan(ctx.Sim, transform, motion, m.Point, stop, m.KeepPlan);
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

    /// <summary>
    /// Turns the sender toward a point at once. Nothing refuses it while a cast runs, as nothing refused the AI's
    /// facing before it was an intent, so a raised guard's arc turns with the body; a turn rate for held casts
    /// would be a rule checked here.
    /// </summary>
    private static void Face(in IntentContext ctx, FaceIntent f)
    {
        if (ctx.Transform is not { } transform)
        {
            ctx.Reject("unknown_intent");
            return;
        }
        if (!Finite(f.Point))
        {
            ctx.Reject("bad_point");
            return;
        }
        transform.Face(f.Point);
    }

    private static bool Finite(Vector3 p) => float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z);
}
