using System.Numerics;
using System.Text.Json.Nodes;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Expressions;
using HollowWake.Core.Movement;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.StatusEffects;
using HollowWake.Core.Systems;
using HollowWake.Core.Tags;
using static HollowWake.Core.Effects.Handlers.EffectHelpers;

namespace HollowWake.Core.Effects.Handlers;

/// <summary>Movement effects: teleport, dash, leap, blink, recall, knockback, pull, buffet; and marks to recall to.</summary>
public static class MovementHandlers
{
    public static void Register(EffectRegistry r)
    {
        r.Register(new MoveHandler());
        r.Register(new MarkHandler());
    }
}

/// <summary>
/// <c>move</c>: moves a body. <c>teleport</c> and <c>dash</c> carry the source toward the target or point by
/// up to <c>distance</c>, stopping before the first cell it cannot stand in; a dash with a <c>speed</c>
/// travels there over time, striking bodies within <c>width</c> of its way once each (<c>on_contact</c>,
/// the <c>on_hit</c> hook), running <c>on_travel</c> every <c>travel</c> units and passing placed blockers
/// when it phases. <c>leap</c> arcs there over <c>air_time</c> seconds, untargetable, and lands
/// (<c>on_land</c>); <c>blink</c> arrives after a <c>delay</c>, behind the target with <c>behind</c>;
/// <c>recall</c> returns to the point a <c>mark</c> stored. <c>knockback</c>, <c>pull</c> and <c>buffet</c>
/// shove the target with a decaying impulse; one that slams a body into something it cannot enter runs
/// <c>on_collide</c> and fires the <c>collision</c> trigger kind. See docs/design/deliveries.md.
/// </summary>
internal sealed class MoveHandler : IEffectHandler
{
    private static readonly string[] Kinds = { "teleport", "dash", "leap", "blink", "recall", "knockback", "pull", "buffet" };

    public string Type => "move";

    public EffectSignature Signature { get; } = EffectSignature.Of("kind", "distance", "who", "anchor", "status", "speed", "width", "filter", "phase", "air_time", "arc", "delay", "behind", "mark", "travel", "telegraph")
        .Hook(DeliveryHooks.OnCollide, "on_collide")
        .Hook(EffectHooks.OnHit, "on_contact")
        .Hook(DeliveryHooks.OnTravel, "on_travel")
        .Hook(EffectHooks.OnLand, "on_land")
        .Hook(EffectHooks.OnEnd, "on_end");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx)
    {
        var kind = ctx.ChoiceOf(p["kind"], "kind", "teleport", Kinds);
        var shove = kind is "knockback" or "pull" or "buffet";
        return new Effect
        {
            Type = Type,
            Kind = kind,
            Distance = ctx.ExprOf(p["distance"], Expr.Constant(4))!,
            OnTarget = ctx.ChoiceOf(p["who"], "who", shove ? "target" : "source", "source", "target") == "target",
            Anchor = ctx.ChoiceOf(p["anchor"], "anchor", "from", "from", "caster", "point"),
            Status = ctx.StringOf(p["status"]),
            Speed = ctx.ExprOf(p["speed"]),
            Width = ctx.ExprOf(p["width"], Expr.Constant(1.5))!,
            Filter = ctx.StringOf(p["filter"]) ?? "enemy",
            Phase = ctx.BoolOf(p["phase"], false),
            AirTime = ctx.ExprOf(p["air_time"], Expr.Constant(0.6))!,
            Arc = ctx.ExprOf(p["arc"], Expr.Constant(2))!,
            Delay = ctx.ExprOf(p["delay"]),
            Behind = ctx.BoolOf(p["behind"], false),
            Mark = ctx.StringOf(p["mark"]) ?? "mark",
            TravelEvery = (float)Math.Max(0.25, ctx.NumberOf(p["travel"], 2)),
            Telegraph = ctx.BoolOf(p["telegraph"], false),
            OnCollide = ctx.CompileList(p["on_collide"], "on_collide"),
            OnContact = ctx.CompileList(p["on_contact"], "on_contact"),
            OnTravel = ctx.CompileList(p["on_travel"], "on_travel"),
            OnLand = ctx.CompileList(p["on_land"], "on_land"),
            OnEnd = ctx.CompileList(p["on_end"], "on_end"),
        };
    }

    private sealed class Effect : CompiledEffect
    {
        public string Kind = "teleport";
        public required Expr Distance;
        public bool OnTarget;
        public string Anchor = "from";
        public string? Status;
        public Expr? Speed;
        public required Expr Width;
        public string Filter = "enemy";
        public bool Phase;
        public required Expr AirTime;
        public required Expr Arc;
        public Expr? Delay;
        public bool Behind;
        public string Mark = "mark";
        public float TravelEvery = 2f;
        public bool Telegraph;
        public CompiledEffect[] OnCollide = Array.Empty<CompiledEffect>();
        public CompiledEffect[] OnContact = Array.Empty<CompiledEffect>();
        public CompiledEffect[] OnTravel = Array.Empty<CompiledEffect>();
        public CompiledEffect[] OnLand = Array.Empty<CompiledEffect>();
        public CompiledEffect[] OnEnd = Array.Empty<CompiledEffect>();

        public override void Execute(EffectContext ctx)
        {
            var mover = OnTarget ? ctx.Target : ctx.Source;
            if (!ctx.Sim.World.TryGet<Transform>(mover, out var t) || !ctx.Sim.World.TryGet<Motion>(mover, out var motion))
            {
                return;
            }
            switch (Kind)
            {
                case "knockback":
                case "pull":
                case "buffet":
                    Shove(ctx, mover, t);
                    return;
                case "leap":
                    Leap(ctx, mover, t, motion);
                    return;
                case "blink":
                    Blink(ctx, mover, t, motion);
                    return;
                case "recall":
                    Recall(ctx, mover, t, motion);
                    return;
                case "dash" when Speed is not null:
                    Dash(ctx, mover, t, motion);
                    return;
                default:
                    Relocate(ctx, mover, t, motion);
                    return;
            }
        }

        private void Shove(EffectContext ctx, Entity mover, Transform t)
        {
            var sim = ctx.Sim;
            if (sim.World.Get<Stats>(mover) is { } stats && stats.HasFlag("immovable"))
            {
                return;
            }
            var distance = (float)ctx.Eval(Distance, 4);
            // What the shove measures from: where the context's deliveries start (a flight's hit point; with none
            // given, the caster), the caster's own position (a hook on a flight reeling its catch home), or the point
            // the cast was aimed at, which a hit keeps when it moves the context's point onto the struck body.
            var source = Anchor switch
            {
                "caster" => ctx.SourceTransform?.Position ?? ctx.From ?? ctx.Point,
                "point" => ctx.Aim,
                _ => ctx.From ?? ctx.SourceTransform?.Position ?? ctx.Point,
            };
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

        /// <summary>Where a dash or a leap is headed: toward the target or point by up to the distance, the facing when they coincide.</summary>
        private Vector3 Aim(EffectContext ctx, Transform t, out Vector2 direction)
        {
            var dist = (float)ctx.Eval(Distance, 4);
            var target = ctx.Target.IsNone || ctx.Target == ctx.Source ? ctx.Point : ctx.Focus;
            var to = new Vector2(target.X - t.Position.X, target.Y - t.Position.Y);
            var len = to.Length();
            if (len < 0.001f)
            {
                to = ctx.Direction.LengthSquared() > 0.001f ? Vector2.Normalize(ctx.Direction) : t.Forward;
                len = dist;
            }
            else
            {
                to /= len;
            }
            direction = to;
            var step = Math.Min(dist, len);
            return t.Position + new Vector3(to.X * step, to.Y * step, 0);
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
            destination = MovementSystem.ResolveDestination(ctx.Sim, t.Position, destination);
            Jump(ctx.Sim, mover, t, motion, destination);
        }

        private static void Jump(Simulation sim, Entity mover, Transform t, Motion motion, Vector3 destination)
        {
            var from = t.Position;
            t.Position = sim.Terrain.SnapToGround(destination, 1.5);
            motion.Stop();
            sim.Spatial.Update(mover, t.Position);
            sim.Events.Publish(new EntityMoved(mover, from, t.Position) { Tick = sim.Clock.Tick });
        }

        private void Dash(EffectContext ctx, Entity mover, Transform t, Motion motion)
        {
            var sim = ctx.Sim;
            var aim = Aim(ctx, t, out var direction);
            var to = MovementSystem.ResolveDestination(sim, t.Position, aim, throughPlaced: Phase);
            var length = new Vector2(to.X - t.Position.X, to.Y - t.Position.Y).Length();
            var speed = (float)Math.Max(0.5, ctx.Eval(Speed, 20));
            t.Yaw = MathF.Atan2(direction.Y, direction.X);
            Start(ctx, mover, motion, new Displacement
            {
                Kind = "dash",
                From = t.Position,
                To = to,
                StartedAt = sim.Clock.Time,
                EndsAt = sim.Clock.Time + Math.Max(sim.Clock.Dt, length / speed),
                Width = (float)Math.Max(0, ctx.Eval(Width, 1.5)),
                Phase = Phase,
                Filter = Filter,
                Flags = DashFlags,
            });
        }

        private void Leap(EffectContext ctx, Entity mover, Transform t, Motion motion)
        {
            var sim = ctx.Sim;
            var aim = Aim(ctx, t, out var direction);
            var blocked = sim.TryService<BlockedCells>(out var bc) ? bc : null;
            var arc = (float)Math.Max(0, ctx.Eval(Arc, 2));
            // A leap clears what lies between: it lands where a body may stand, on anything up to the top of its
            // arc, else as far along as it could walk.
            var to = MovementSystem.CanOccupy(sim, blocked, aim.X, aim.Y, t.Position.Z + arc, sim.Content.Rules.Number("move.step_up", 0.6), out var ground)
                ? new Vector3(aim.X, aim.Y, ground.Height)
                : MovementSystem.ResolveDestination(sim, t.Position, aim);
            var air = Math.Max(sim.Clock.Dt, ctx.Eval(AirTime, 0.6));
            t.Yaw = MathF.Atan2(direction.Y, direction.X);
            if (Telegraph)
            {
                PlacementHandlers.Telegraph(ctx, to, t.Radius, air);
            }
            Start(ctx, mover, motion, new Displacement
            {
                Kind = "leap",
                From = t.Position,
                To = to,
                StartedAt = sim.Clock.Time,
                EndsAt = sim.Clock.Time + air,
                Arc = arc,
                Width = (float)Math.Max(0, ctx.Eval(Width, 1.5)),
                Filter = Filter,
                Flags = LeapFlags,
            });
        }

        private void Blink(EffectContext ctx, Entity mover, Transform t, Motion motion)
        {
            var sim = ctx.Sim;
            Vector3 to;
            if (Behind && !ctx.Target.IsNone && ctx.Target != mover && sim.World.TryGet<Transform>(ctx.Target, out var tt))
            {
                var back = tt.Forward;
                var gap = tt.Radius + t.Radius + 0.3f;
                to = tt.Position - new Vector3(back.X * gap, back.Y * gap, 0);
                to = MovementSystem.ResolveDestination(sim, tt.Position, to);
            }
            else
            {
                to = MovementSystem.ResolveDestination(sim, t.Position, Aim(ctx, t, out _));
            }
            var delay = Delay is null ? 0 : Math.Max(0, ctx.Eval(Delay, 0));
            if (delay <= 0)
            {
                Jump(sim, mover, t, motion, to);
                Ended(ctx, t.Position);
                return;
            }
            Start(ctx, mover, motion, new Displacement
            {
                Kind = "blink",
                From = t.Position,
                To = to,
                StartedAt = sim.Clock.Time,
                EndsAt = sim.Clock.Time + delay,
            });
        }

        private void Recall(EffectContext ctx, Entity mover, Transform t, Motion motion)
        {
            if (!ctx.Sim.World.TryGet<Marks>(mover, out var marks) || !marks.Points.Remove(Mark, out var point))
            {
                return;
            }
            Jump(ctx.Sim, mover, t, motion, point);
            Ended(ctx, t.Position);
        }

        /// <summary>An instant move's end lists: it lands where it arrived.</summary>
        private void Ended(EffectContext ctx, Vector3 at)
        {
            if (OnLand.Length == 0 && OnEnd.Length == 0)
            {
                return;
            }
            var end = At(ctx, Entity.None, at, Facing(ctx), at, ctx.Depth + 1);
            CompiledEffect.ExecuteAll(OnLand, end);
            CompiledEffect.ExecuteAll(OnEnd, end);
        }

        private static readonly string[] DashFlags = { "dashing" };
        private static readonly string[] LeapFlags = { "leaping", "untargetable" };

        private void Start(EffectContext ctx, Entity mover, Motion motion, Displacement d)
        {
            d.Context = ctx.Deeper();
            d.OnContact = OnContact;
            d.OnTravel = OnTravel;
            d.OnLand = OnLand;
            d.OnEnd = OnEnd;
            d.TravelEvery = TravelEvery;
            d.NextTravelAt = TravelEvery;
            DisplacementSystem.Carry(ctx.Sim, mover, motion, d);
        }
    }
}

/// <summary>
/// <c>mark {key, at}</c>: remembers a point on the source under a key (default <c>mark</c>): where the source
/// stands (default) or the context's point (<c>at: point</c>). A <c>move</c> of kind <c>recall</c> with the
/// same key returns there and spends the mark.
/// </summary>
internal sealed class MarkHandler : IEffectHandler
{
    public string Type => "mark";

    public EffectSignature Signature { get; } = EffectSignature.Of("key", "at");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
    {
        Type = Type,
        Key = ctx.StringOf(p["key"]) ?? "mark",
        AtPoint = ctx.ChoiceOf(p["at"], "at", "caster", "caster", "point") == "point",
    };

    private sealed class Effect : CompiledEffect
    {
        public string Key = "mark";
        public bool AtPoint;

        public override void Execute(EffectContext ctx)
        {
            if (!ctx.Sim.World.IsAlive(ctx.Source))
            {
                return;
            }
            var point = AtPoint ? ctx.Point : ctx.SourceTransform?.Position ?? ctx.Point;
            ctx.Sim.World.GetOrAdd<Marks>(ctx.Source).Points[Key] = point;
        }
    }
}
