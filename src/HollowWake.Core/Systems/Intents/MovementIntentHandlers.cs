using System.Numerics;
using HollowWake.Core.Intents;
using HollowWake.Core.World;

namespace HollowWake.Core.Systems.Intents;

/// <summary>Walking to a point, steering by direction, stopping and jumping.</summary>
public static class MovementIntentHandlers
{
    public static void Register(IntentHandlers handlers)
    {
        handlers.Register<MoveToIntent>(MoveTo);
        handlers.Register<MoveDirIntent>(MoveDir);
        handlers.Register<StopIntent>(Stop);
        handlers.Register<JumpIntent>(Jump);
    }

    private static void MoveTo(in IntentContext ctx, MoveToIntent m)
    {
        var motion = ctx.Motion;
        var transform = ctx.Transform;
        if (motion is null || transform is null)
        {
            ctx.Reject("unknown_intent");
            return;
        }
        var target = ctx.Sim.Terrain.SnapToGround(m.Point, 3);
        var path = ctx.Sim.Service<Pathfinder>().FindPath(transform.Position, target);
        motion.MoveDir = Vector2.Zero;
        motion.Path.Clear();
        if (path.Count > 0)
        {
            motion.Path.AddRange(path);
            motion.StopDistance = 0.3f;
        }
        else
        {
            motion.MoveTarget = target;
        }
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
