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
using HollowWake.Core.Systems;
using HollowWake.Core.Tags;
using static HollowWake.Core.Effects.Handlers.EffectHelpers;

namespace HollowWake.Core.Effects.Handlers;

/// <summary>Movement effects: teleport, dash, knockback, pull, buffet.</summary>
public static class MovementHandlers
{
    public static void Register(EffectRegistry r)
    {
        r.Register(new MoveHandler());
    }
}

/// <summary>
/// <c>move</c>: moves a body. <c>teleport</c> and <c>dash</c> carry the source toward the target or point by up
/// to <c>distance</c>, stopping at the first cell it cannot stand in. <c>knockback</c> (away from the source),
/// <c>pull</c> (toward it, stopping a unit short) and <c>buffet</c> (a random way) shove the target with an
/// impulse that carries it <c>distance</c> units as it decays; knockback and buffet scale by the weave's
/// <c>knockback</c>. A shove that slams a body into something it cannot enter collides: <c>on_collide</c>
/// runs on it and the <c>collision</c> trigger kind fires. <c>status</c> names a status the shoved body wears
/// until the shove settles. Bodies flagged <c>immovable</c> are not shoved.
/// </summary>
internal sealed class MoveHandler : IEffectHandler
{
    public string Type => "move";

    public EffectSignature Signature { get; } = EffectSignature.Of("kind", "distance", "who", "status")
        .Hook(DeliveryHooks.OnCollide, "on_collide");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx)
    {
        var kind = ctx.ChoiceOf(p["kind"], "kind", "teleport", "teleport", "dash", "knockback", "pull", "buffet");
        var shove = kind is "knockback" or "pull" or "buffet";
        return new Effect
        {
            Type = Type,
            Kind = kind,
            Distance = ctx.ExprOf(p["distance"], Expr.Constant(4))!,
            OnTarget = ctx.ChoiceOf(p["who"], "who", shove ? "target" : "source", "source", "target") == "target",
            Status = ctx.StringOf(p["status"]),
            OnCollide = ctx.CompileList(p["on_collide"], "on_collide"),
        };
    }

    private sealed class Effect : CompiledEffect
    {
        public string Kind = "teleport";
        public required Expr Distance;
        public bool OnTarget;
        public string? Status;
        public CompiledEffect[] OnCollide = Array.Empty<CompiledEffect>();

        public override void Execute(EffectContext ctx)
        {
            var mover = OnTarget ? ctx.Target : ctx.Source;
            if (!ctx.Sim.World.TryGet<Transform>(mover, out var t) || !ctx.Sim.World.TryGet<Motion>(mover, out var motion))
            {
                return;
            }
            if (Kind is "knockback" or "pull" or "buffet")
            {
                Shove(ctx, mover, t);
                return;
            }
            Relocate(ctx, mover, t, motion);
        }

        private void Shove(EffectContext ctx, Entity mover, Transform t)
        {
            var sim = ctx.Sim;
            if (sim.World.Get<Stats>(mover) is { } stats && stats.HasFlag("immovable"))
            {
                return;
            }
            var distance = (float)ctx.Eval(Distance, 4);
            var source = ctx.From ?? ctx.SourceTransform?.Position ?? ctx.Point;
            Vector2 direction;
            switch (Kind)
            {
                case "pull":
                {
                    var toward = new Vector2(source.X - t.Position.X, source.Y - t.Position.Y);
                    var length = toward.Length();
                    if (length < 0.001f)
                    {
                        return;
                    }
                    distance = Math.Min(distance, Math.Max(0, length - 1f));
                    direction = toward / length;
                    break;
                }
                case "buffet":
                {
                    var angle = ctx.Rng.NextDouble() * Math.PI * 2;
                    direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
                    distance *= (float)WeaveAttr(ctx, "knockback", 1);
                    break;
                }
                default:
                {
                    var away = new Vector2(t.Position.X - source.X, t.Position.Y - source.Y);
                    direction = away.LengthSquared() < 0.001f ? Facing(ctx) : Vector2.Normalize(away);
                    distance *= (float)WeaveAttr(ctx, "knockback", 1);
                    break;
                }
            }
            if (distance <= 0.01f)
            {
                return;
            }
            var speed = DisplacementSystem.SpeedFor(sim, distance);
            DisplacementSystem.Shove(sim, mover, direction * speed, new ShoveRider { Source = ctx.Source, Context = ctx, OnCollide = OnCollide });
            if (Status is not null && sim.TryService<StatusService>(out var statuses))
            {
                statuses.Apply(sim, mover, Status, ctx.Source, new StatusApplication
                {
                    Duration = DisplacementSystem.SettleSeconds(sim, speed),
                    Potency = ctx.Potency,
                    SourceKey = ctx.Weave?.Key ?? "effect",
                    Tags = ctx.Tags,
                    Depth = ctx.Depth,
                });
            }
        }

        /// <summary>Teleports and instant dashes: straight toward the target or point, stopping before what blocks.</summary>
        private void Relocate(EffectContext ctx, Entity mover, Transform t, Motion motion)
        {
            var dist = (float)ctx.Eval(Distance, 4);
            var target = ctx.Target.IsNone ? ctx.Point : ctx.Focus;
            var to = new Vector2(target.X - t.Position.X, target.Y - t.Position.Y);
            var len = to.Length();
            if (len < 0.001f)
            {
                to = t.Forward;
                len = 1;
            }
            to /= len;
            var step = Math.Min(dist, len);
            var destination = t.Position + new Vector3(to.X * step, to.Y * step, 0);
            destination = Movement.MovementSystem.ResolveDestination(ctx.Sim, t.Position, destination);
            var from = t.Position;
            t.Position = ctx.Sim.Terrain.SnapToGround(destination, 1.5);
            motion.Stop();
            ctx.Sim.Spatial.Update(mover, t.Position);
            ctx.Sim.Events.Publish(new EntityMoved(mover, from, t.Position) { Tick = ctx.Sim.Clock.Tick });
        }
    }
}
