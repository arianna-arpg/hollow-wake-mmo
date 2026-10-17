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

/// <summary>Deliveries: areas, projectiles, chains and fields.</summary>
public static class DeliveryHandlers
{
    public static void Register(EffectRegistry r)
    {
        r.Register(new AreaHandler());
        r.Register(new ProjectileHandler());
        r.Register(new ChainHandler());
        r.Register(new FieldHandler());
    }
}

internal sealed class AreaHandler : IEffectHandler
{
    public string Type => "area";

    public EffectSignature Signature { get; } = EffectSignature.Of("shape", "radius", "angle", "filter", "center", "max_targets", "include_center")
        .Hook(EffectHooks.OnHit, "effects");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
    {
        Type = Type,
        Shape = ctx.StringOf(p["shape"]) ?? "circle",
        Radius = ctx.ExprOf(p["radius"], Expr.Parse("weave.area.radius"))!,
        Angle = ctx.ExprOf(p["angle"], Expr.Constant(90))!,
        Filter = ctx.StringOf(p["filter"]) ?? "enemy",
        CenterMode = ctx.StringOf(p["center"]) ?? "focus",
        MaxTargets = ctx.ExprOf(p["max_targets"]),
        Effects = ctx.CompileList(p["effects"], "effects"),
        IncludeCenter = ctx.BoolOf(p["include_center"], true),
    };

    private sealed class Effect : CompiledEffect
    {
        public string Shape = "circle";
        public required Expr Radius;
        public required Expr Angle;
        public string Filter = "enemy";
        public string CenterMode = "focus";
        public Expr? MaxTargets;
        public CompiledEffect[] Effects = Array.Empty<CompiledEffect>();
        public bool IncludeCenter = true;

        public override void Execute(EffectContext ctx)
        {
            var center = Center(ctx, CenterMode);
            var radius = (float)Math.Max(0.1, ctx.Eval(Radius, 2));
            var candidates = ctx.Sim.Spatial.Within(center, radius).Select(w => w.Entity);
            if (Shape == "cone")
            {
                var origin = ctx.From ?? ctx.SourceTransform?.Position ?? center;
                var dir = ctx.Direction.LengthSquared() > 0.001f ? Vector2.Normalize(ctx.Direction) : (ctx.SourceTransform?.Forward ?? Vector2.UnitX);
                var half = MathF.PI * (float)ctx.Eval(Angle, 90) / 360f;
                candidates = ctx.Sim.Spatial.Within(origin, radius).Where(w =>
                {
                    var to = new Vector2(w.Position.X - origin.X, w.Position.Y - origin.Y);
                    if (to.LengthSquared() < 0.01f)
                    {
                        return true;
                    }
                    var ang = MathF.Acos(Math.Clamp(Vector2.Dot(Vector2.Normalize(to), dir), -1f, 1f));
                    return ang <= half;
                }).Select(w => w.Entity);
            }
            else if (Shape == "line")
            {
                var origin = ctx.From ?? ctx.SourceTransform?.Position ?? center;
                var dir = ctx.Direction.LengthSquared() > 0.001f ? Vector2.Normalize(ctx.Direction) : (ctx.SourceTransform?.Forward ?? Vector2.UnitX);
                var width = 1.0f;
                candidates = ctx.Sim.Spatial.Within(origin, radius).Where(w =>
                {
                    var to = new Vector2(w.Position.X - origin.X, w.Position.Y - origin.Y);
                    var along = Vector2.Dot(to, dir);
                    if (along < 0 || along > radius)
                    {
                        return false;
                    }
                    var perp = MathF.Abs(to.X * dir.Y - to.Y * dir.X);
                    return perp <= width;
                }).Select(w => w.Entity);
            }
            var targets = Filter(ctx, candidates, Filter).Where(e => IncludeCenter || e != ctx.Target).ToList();
            if (MaxTargets is not null)
            {
                var max = (int)Math.Round(ctx.Eval(MaxTargets, targets.Count));
                if (targets.Count > max)
                {
                    ctx.Rng.Shuffle(targets);
                    targets = targets.Take(max).ToList();
                }
            }
            var deeper = ctx.Deeper();
            foreach (var t in targets)
            {
                ExecuteAll(Effects, deeper.WithTarget(t));
            }
        }
    }
}

internal sealed class ProjectileHandler : IEffectHandler
{
    public string Type => "projectile";

    public EffectSignature Signature { get; } = EffectSignature.Of("count", "speed", "range", "spread", "pierce", "radius", "on_expire", "look", "gravity")
        .Hook(EffectHooks.OnHit, "on_hit");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
    {
        Type = Type,
        Count = ctx.ExprOf(p["count"], Expr.Parse("weave.projectile.count"))!,
        Speed = ctx.ExprOf(p["speed"], Expr.Parse("weave.projectile.speed"))!,
        Range = ctx.ExprOf(p["range"], Expr.Parse("weave.range"))!,
        Spread = ctx.ExprOf(p["spread"], Expr.Constant(15))!,
        Pierce = ctx.ExprOf(p["pierce"], Expr.Parse("weave.projectile.pierce"))!,
        Radius = ctx.NumberOf(p["radius"], 0.5),
        OnHit = ctx.CompileList(p["on_hit"], "on_hit"),
        OnExpire = ctx.CompileList(p["on_expire"], "on_expire"),
        Look = ctx.StringOf(p["look"]),
        Gravity = ctx.BoolOf(p["gravity"], false),
    };

    private sealed class Effect : CompiledEffect
    {
        public required Expr Count;
        public required Expr Speed;
        public required Expr Range;
        public required Expr Spread;
        public required Expr Pierce;
        public double Radius = 0.5;
        public CompiledEffect[] OnHit = Array.Empty<CompiledEffect>();
        public CompiledEffect[] OnExpire = Array.Empty<CompiledEffect>();
        public string? Look;
        public bool Gravity;

        public override void Execute(EffectContext ctx)
        {
            var origin = ctx.From ?? ctx.SourceTransform?.Position ?? ctx.Point;
            var count = Math.Clamp((int)Math.Round(ctx.Eval(Count, 1)), 1, 64);
            var speed = (float)Math.Max(0.5, ctx.Eval(Speed, 12));
            var range = (float)Math.Max(1, ctx.Eval(Range, 12));
            var pierce = (int)Math.Max(0, ctx.Eval(Pierce, 0));
            var spread = (float)(ctx.Eval(Spread, 15) * Math.PI / 180.0);
            Vector2 dir;
            if (!ctx.Target.IsNone && ctx.TargetTransform is { } tt)
            {
                dir = new Vector2(tt.Position.X - origin.X, tt.Position.Y - origin.Y);
            }
            else if (ctx.Direction.LengthSquared() > 0.001f)
            {
                dir = ctx.Direction;
            }
            else
            {
                dir = new Vector2(ctx.Point.X - origin.X, ctx.Point.Y - origin.Y);
            }
            if (dir.LengthSquared() < 0.0001f)
            {
                dir = ctx.SourceTransform?.Forward ?? Vector2.UnitX;
            }
            dir = Vector2.Normalize(dir);
            var baseAngle = MathF.Atan2(dir.Y, dir.X);
            var start = origin + new Vector3(0, 0, 1.0f);
            for (var i = 0; i < count; i++)
            {
                var offset = count == 1 ? 0 : -spread / 2 + spread * i / (count - 1);
                var ang = baseAngle + offset;
                var vel = new Vector3(MathF.Cos(ang) * speed, MathF.Sin(ang) * speed, 0);
                var e = ctx.Sim.World.Create();
                ctx.Sim.World.Add(e, new Transform { Position = start, Yaw = ang, Radius = (float)Radius, Height = 0.5f });
                ctx.Sim.World.Add(e, new Identity { Name = "projectile", Pattern = null, Tags = TagSet.Of("projectile"), Faction = ctx.Sim.World.Get<Identity>(ctx.Origin)?.Faction, Owner = ctx.Origin, Look = Look ?? ctx.Weave?.Look });
                ctx.Sim.World.Add(e, new Projectile
                {
                    Context = ctx.Deeper(),
                    Velocity = vel,
                    Range = range,
                    Pierce = pierce,
                    OnHit = OnHit,
                    OnExpire = OnExpire,
                    Radius = (float)Radius,
                    Gravity = Gravity,
                });
                ctx.Sim.Spatial.Update(e, start);
                ctx.Sim.Events.Publish(new EntitySpawned(e, "projectile", start) { Tick = ctx.Sim.Clock.Tick });
            }
        }
    }
}

internal sealed class ChainHandler : IEffectHandler
{
    public string Type => "chain";

    public EffectSignature Signature { get; } = EffectSignature.Of("count", "range", "filter")
        .Hook(EffectHooks.OnHit, "effects");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
    {
        Type = Type,
        Count = ctx.ExprOf(p["count"], Expr.Parse("weave.chain.count"))!,
        Range = ctx.ExprOf(p["range"], Expr.Constant(6))!,
        Filter = ctx.StringOf(p["filter"]) ?? "enemy",
        Effects = ctx.CompileList(p["effects"], "effects"),
    };

    private sealed class Effect : CompiledEffect
    {
        public required Expr Count;
        public required Expr Range;
        public string Filter = "enemy";
        public CompiledEffect[] Effects = Array.Empty<CompiledEffect>();

        public override void Execute(EffectContext ctx)
        {
            if (ctx.Target.IsNone)
            {
                return;
            }
            var remaining = (int)Math.Round(ctx.Eval(Count, 0));
            var range = (float)ctx.Eval(Range, 6);
            var hit = new HashSet<Entity> { ctx.Target };
            var current = ctx.Deeper();
            while (remaining-- > 0)
            {
                var from = current.Focus;
                var next = Filter(ctx, ctx.Sim.Spatial.Within(from, range).OrderBy(w => w.Distance).Select(w => w.Entity), Filter).FirstOrDefault(e => !hit.Contains(e));
                if (next.IsNone)
                {
                    break;
                }
                hit.Add(next);
                current = current.WithTarget(next).Deeper();
                ExecuteAll(Effects, current);
            }
        }
    }
}

internal sealed class FieldHandler : IEffectHandler
{
    public string Type => "field";

    public EffectSignature Signature { get; } = EffectSignature.Of("radius", "duration", "interval", "filter", "look", "follow")
        .Hook(EffectHooks.OnHit, "pulse");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
    {
        Type = Type,
        Radius = ctx.ExprOf(p["radius"], Expr.Parse("weave.area.radius"))!,
        Duration = ctx.ExprOf(p["duration"], Expr.Parse("weave.duration"))!,
        Interval = ctx.ExprOf(p["interval"], Expr.Constant(0.5))!,
        Filter = ctx.StringOf(p["filter"]) ?? "enemy",
        Pulse = ctx.CompileList(p["pulse"], "pulse"),
        Look = ctx.StringOf(p["look"]),
        Follow = ctx.BoolOf(p["follow"], false),
    };

    private sealed class Effect : CompiledEffect
    {
        public required Expr Radius;
        public required Expr Duration;
        public required Expr Interval;
        public string Filter = "enemy";
        public CompiledEffect[] Pulse = Array.Empty<CompiledEffect>();
        public string? Look;
        public bool Follow;

        public override void Execute(EffectContext ctx)
        {
            var center = ctx.Focus;
            var e = ctx.Sim.World.Create();
            ctx.Sim.World.Add(e, new Transform { Position = center, Radius = (float)ctx.Eval(Radius, 2) });
            ctx.Sim.World.Add(e, new Identity { Name = "field", Tags = TagSet.Of("field"), Faction = ctx.Sim.World.Get<Identity>(ctx.Origin)?.Faction, Owner = ctx.Origin, Look = Look ?? ctx.Weave?.Look });
            ctx.Sim.World.Add(e, new Field
            {
                Context = ctx.Deeper(),
                Radius = (float)ctx.Eval(Radius, 2),
                Interval = Math.Max(ctx.Sim.Clock.Dt, ctx.Eval(Interval, 0.5)),
                ExpiresAt = ctx.Sim.Clock.Time + Math.Max(ctx.Sim.Clock.Dt, ctx.Eval(Duration, 4)),
                NextPulseAt = ctx.Sim.Clock.Time,
                Filter = Filter,
                Pulse = Pulse,
                Follow = Follow ? ctx.Source : Entity.None,
            });
            ctx.Sim.Spatial.Update(e, center);
            ctx.Sim.Events.Publish(new EntitySpawned(e, "field", center) { Tick = ctx.Sim.Clock.Tick });
        }
    }
}



