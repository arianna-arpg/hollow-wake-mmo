using System.Numerics;
using System.Text.Json.Nodes;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Expressions;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.StatusEffects;
using HollowWake.Core.Tags;
using static HollowWake.Core.Effects.Handlers.EffectHelpers;

namespace HollowWake.Core.Effects.Handlers;

// Movement effects: teleport, dash, knockback, pull.
internal sealed class MoveHandler : IEffectHandler
{
    public string Type => "move";

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
    {
        Type = Type,
        Kind = ctx.StringOf(p["kind"]) ?? "teleport",
        Distance = ctx.ExprOf(p["distance"], Expr.Constant(4))!,
        Who = ctx.StringOf(p["who"]) ?? "source",
    };

    private sealed class Effect : CompiledEffect
    {
        public string Kind = "teleport";
        public required Expr Distance;
        public string Who = "source";

        public override void Execute(EffectContext ctx)
        {
            var mover = Who == "target" ? ctx.Target : ctx.Source;
            if (!ctx.Sim.World.TryGet<Transform>(mover, out var t) || !ctx.Sim.World.TryGet<Motion>(mover, out var motion))
            {
                return;
            }
            var dist = (float)ctx.Eval(Distance, 4);
            var source = ctx.SourceTransform?.Position ?? ctx.Point;
            Vector3 destination;
            switch (Kind)
            {
                case "knockback":
                {
                    var away = new Vector2(t.Position.X - source.X, t.Position.Y - source.Y);
                    if (away.LengthSquared() < 0.001f)
                    {
                        away = Vector2.UnitX;
                    }
                    away = Vector2.Normalize(away);
                    destination = t.Position + new Vector3(away.X * dist, away.Y * dist, 0);
                    break;
                }
                case "pull":
                {
                    var toward = new Vector2(source.X - t.Position.X, source.Y - t.Position.Y);
                    var len = toward.Length();
                    if (len < 0.001f)
                    {
                        return;
                    }
                    var step = Math.Min(dist, Math.Max(0, len - 1f));
                    toward /= len;
                    destination = t.Position + new Vector3(toward.X * step, toward.Y * step, 0);
                    break;
                }
                default:
                {
                    var target = ctx.Target.IsNone ? ctx.Point : ctx.Focus;
                    var to = new Vector2(target.X - t.Position.X, target.Y - t.Position.Y);
                    var len = to.Length();
                    if (len < 0.001f)
                    {
                        var fwd = t.Forward;
                        to = fwd;
                        len = 1;
                    }
                    to /= len;
                    var step = Math.Min(dist, len);
                    destination = t.Position + new Vector3(to.X * step, to.Y * step, 0);
                    break;
                }
            }
            destination = Movement.MovementSystem.ResolveDestination(ctx.Sim, t.Position, destination);
            var from = t.Position;
            t.Position = ctx.Sim.Terrain.SnapToGround(destination, 1.5);
            motion.Stop();
            ctx.Sim.Spatial.Update(mover, t.Position);
            ctx.Sim.Events.Publish(new EntityMoved(mover, from, t.Position) { Tick = ctx.Sim.Clock.Tick });
        }
    }
}



