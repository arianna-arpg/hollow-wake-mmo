using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using HollowWake.Core.Attributes;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Expressions;
using HollowWake.Core.Maths;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.StatusEffects;
using HollowWake.Core.Systems;
using HollowWake.Core.Tags;
using HollowWake.Core.Weaves;
using HollowWake.Core.World;
using static HollowWake.Core.Effects.Handlers.EffectHelpers;

namespace HollowWake.Core.Effects.Handlers;

/// <summary>Deliveries: areas, projectiles, chains, fields and auras.</summary>
public static class DeliveryHandlers
{
    public static void Register(EffectRegistry r)
    {
        r.Register(new AreaHandler());
        r.Register(new ProjectileHandler());
        r.Register(new ChainHandler());
        r.Register(new FieldHandler());
        r.Register(new AuraHandler());
    }
}

/// <summary>
/// The hook moments deliveries add to the shared ones in <see cref="EffectHooks"/>. Glyph hooks name them
/// like any other: they append into every node whose type exposes them.
/// </summary>
public static class DeliveryHooks
{
    /// <summary>The first body a projectile strikes.</summary>
    public const string OnFirstHit = "on_first_hit";

    /// <summary>Every so many units a projectile or a dash travels.</summary>
    public const string OnTravel = "on_travel";

    /// <summary>A returning projectile arriving back.</summary>
    public const string OnReturn = "on_return";

    /// <summary>A body stepping into a field.</summary>
    public const string OnEnter = "on_enter";

    /// <summary>A body leaving a field.</summary>
    public const string OnExit = "on_exit";

    /// <summary>A shoved body slamming into something it cannot enter.</summary>
    public const string OnCollide = "on_collide";
}

/// <summary>How a body is chosen among several: the nearest, the farthest, the most or least hurt, or at random.</summary>
internal enum Pick
{
    Random,
    Nearest,
    Farthest,
    LowestLife,
    HighestLife,
    AimedNearest,
}

internal static class Picks
{
    public static readonly string[] Names = { "random", "nearest", "farthest", "lowest_life", "highest_life", "aimed_nearest" };

    public static Pick Of(EffectCompileContext ctx, JsonNode? node, string fallback) => ctx.ChoiceOf(node, "pick", fallback, Names) switch
    {
        "nearest" => Pick.Nearest,
        "farthest" => Pick.Farthest,
        "lowest_life" => Pick.LowestLife,
        "highest_life" => Pick.HighestLife,
        "aimed_nearest" => Pick.AimedNearest,
        _ => Pick.Random,
    };

    /// <summary>The sort key of a body under a pick: smaller is chosen first.</summary>
    public static double Key(Simulation sim, Pick pick, Entity e, Vector3 from)
    {
        switch (pick)
        {
            case Pick.LowestLife:
            case Pick.HighestLife:
            {
                var life = sim.World.Get<Stats>(e)?.ResourceFraction("life") ?? 1;
                return pick == Pick.LowestLife ? life : -life;
            }
            default:
            {
                var p = sim.World.Get<Transform>(e)?.Position ?? from;
                var d = Vector2.Distance(new Vector2(p.X, p.Y), new Vector2(from.X, from.Y));
                return pick == Pick.Farthest ? -d : d;
            }
        }
    }

    /// <summary>
    /// Keeps the <paramref name="max"/> bodies a pick prefers, in that order. Random shuffles with the
    /// context's stream and keeps the first ones; the others sort stably by their key.
    /// </summary>
    public static void Keep(EffectContext ctx, List<Entity> bodies, int max, Pick pick, Vector3 from)
    {
        if (bodies.Count <= max)
        {
            return;
        }
        if (pick == Pick.Random)
        {
            ctx.Rng.Shuffle(bodies);
        }
        else
        {
            // Selection of the best max, stable among equal keys: small lists, no allocation.
            for (var i = 0; i < max; i++)
            {
                var best = i;
                var bestKey = SelectionKey(bodies[i]);
                for (var j = i + 1; j < bodies.Count; j++)
                {
                    var key = SelectionKey(bodies[j]);
                    if (key < bestKey)
                    {
                        best = j;
                        bestKey = key;
                    }
                }
                if (best != i)
                {
                    var chosen = bodies[best];
                    bodies.RemoveAt(best);
                    bodies.Insert(i, chosen);
                }
            }
        }
        bodies.RemoveRange(max, bodies.Count - max);

        double SelectionKey(Entity e) => pick == Pick.AimedNearest && e == ctx.Target
            ? double.NegativeInfinity : Key(ctx.Sim, pick, e, from);
    }
}

/// <summary>
/// <c>area</c>: strikes every body inside a shape at once. Centered shapes sit on the area's center (the
/// focus, the point or the source); anchored shapes (sector, crescent, line, band) extend from the source
/// or the cast's start along the facing. <c>angle</c> is scaled by the weave's <c>area.angle</c>, a line's
/// width defaults to <c>area.width</c>, melee weaves reach <c>melee.reach</c> further with anchored shapes,
/// and <c>reach: edge</c> measures from the source's edge rather than its center. Over <c>max_targets</c> the
/// <c>pick</c> decides who is struck; <c>require</c> is a condition each body must meet. After resolving, the
/// weave's <c>area.scatter</c> places that many smaller aftershocks around it and <c>area.pulses</c> repeats
/// it where it stood.
/// </summary>
internal sealed class AreaHandler : IEffectHandler
{
    public string Type => "area";

    public EffectSignature Signature { get; } = EffectSignature.Of("shape", "radius", "angle", "width", "inner", "filter", "center", "max_targets", "include_center", "pick", "require", "reach", "occlusion", "report_miss")
        .Hook(EffectHooks.OnHit, "effects");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
    {
        Type = Type,
        Shape = ShapeOf(ctx, p["shape"], "shape"),
        Radius = ctx.ExprOf(p["radius"]),
        Angle = ctx.ExprOf(p["angle"]),
        Width = ctx.ExprOf(p["width"]),
        Inner = ctx.ExprOf(p["inner"]),
        Filter = ctx.StringOf(p["filter"]) ?? "enemy",
        CenterMode = p["center"] is null ? null : ctx.ChoiceOf(p["center"], "center", "focus", "focus", "source", "point"),
        MaxTargets = ctx.ExprOf(p["max_targets"]),
        Effects = ctx.CompileList(p["effects"], "effects"),
        IncludeCenter = ctx.BoolOf(p["include_center"], true),
        Pick = Picks.Of(ctx, p["pick"], "random"),
        Require = ctx.ExprOf(p["require"]),
        FromEdge = ctx.ChoiceOf(p["reach"], "reach", "center", "center", "edge") == "edge",
        Free = OcclusionFree(ctx, p["occlusion"]),
        ReportMiss = ctx.BoolOf(p["report_miss"], false),
    };

    private sealed class Effect : CompiledEffect, IDetachedDeliveryPlanSource
    {
        public required Shape Shape;
        public Expr? Radius;
        public Expr? Angle;
        public Expr? Width;
        public Expr? Inner;
        public string Filter = "enemy";
        /// <summary>Where the area sits; null takes the source for anchored shapes and the focus for the rest.</summary>
        public string? CenterMode;
        public Expr? MaxTargets;
        public CompiledEffect[] Effects = Array.Empty<CompiledEffect>();
        public bool IncludeCenter = true;
        public Pick Pick;
        public Expr? Require;
        public bool FromEdge;
        /// <summary>Strikes through walls and rising ground (<c>occlusion: free</c>).</summary>
        public bool Free;
        public bool ReportMiss;

        private ResolvedAreaPlan ResolvePlan(EffectContext ctx)
        {
            var center = Center(ctx, CenterMode ?? (Shape.Anchored ? "source" : "focus"));
            var radius = (float)Math.Max(0.1, ParamOrAttr(ctx, Radius, "area.radius", 2));
            var scale = PlacementScale(ctx);
            if (scale != 1)
            {
                radius = (float)(radius * scale);
            }
            var anchor = center;
            if (Shape.Anchored)
            {
                if (FromEdge)
                {
                    radius += ctx.SourceTransform?.Radius ?? 0;
                }
                if (ctx.Tags.Has("melee"))
                {
                    radius += (float)Math.Max(0, WeaveAttr(ctx, "melee.reach", 0));
                }
            }
            var size = SizeOf(ctx, Shape, radius, Angle, Width, Inner, scaleAngle: true);
            var facing = Facing(ctx);
            var occluded = Occluded(ctx, Free);
            return new ResolvedAreaPlan(Shape, size, anchor, facing, CenterMode ?? (Shape.Anchored ? "source" : "focus"),
                Filter, IncludeCenter ? Entity.None : ctx.Target, occluded, Require is not null, MaxTargets is not null);
        }

        public DeliveryForecast DescribeDelivery(EffectContext ctx)
        {
            // Observation must not initialize the caller's lazy scope and freeze its time/distance.
            // Sample live inputs in a detached context; never advance either shared random stream.
            return DescribeDetachedDelivery(ctx.Fork());
        }

        public DeliveryForecast DescribeDetachedDelivery(EffectContext ctx)
        {
            using var inputs = ForecastInputs.Rent(ctx);
            if (!inputs.ParameterOrAttribute(Radius, "area.radius") || !inputs.Expression(Angle) ||
                Shape.HasAngle && !inputs.WeaveAttribute("area.angle") ||
                Shape.HasWidth && !inputs.ParameterOrAttribute(Width, "area.width") ||
                !inputs.Expression(Inner) || Shape.Anchored && ctx.Tags.Has("melee") && !inputs.WeaveAttribute("melee.reach") ||
                !Free && !inputs.WeaveAttribute("phasing"))
                return DeliveryForecast.Unavailable(inputs.Failure ?? "unavailable_inputs");
            try
            {
                var plan = ResolvePlan(ctx);
                return plan.IsFinite ? new DeliveryForecast(plan, null) : DeliveryForecast.Unavailable("non_finite_geometry");
            }
            catch (ExprEvalException) { return DeliveryForecast.Unavailable("unresolved_expression"); }
            catch (AttributeCycleException) { return DeliveryForecast.Unavailable("cyclic_attribute_dependency"); }
            catch (ArithmeticException) { return DeliveryForecast.Unavailable("invalid_expression"); }
            catch (ArgumentException) { return DeliveryForecast.Unavailable("invalid_expression"); }
        }

        public override void Execute(EffectContext ctx)
        {
            var plan = ResolvePlan(ctx);
            var targets = Rent();
            try
            {
                foreach (var (e, pos, _) in ctx.Sim.Spatial.Within(plan.Anchor, plan.Reach, plan.HeightTolerance))
                {
                    if (!plan.IsCandidate(ctx, e, pos))
                    {
                        continue;
                    }
                    targets.Add(e);
                }
                if (Require is not null)
                {
                    for (var i = targets.Count - 1; i >= 0; i--)
                    {
                        if (ctx.WithTarget(targets[i]).Eval(Require, 1) == 0)
                        {
                            targets.RemoveAt(i);
                        }
                    }
                }
                if (MaxTargets is not null)
                {
                    var max = (int)Math.Round(ctx.Eval(MaxTargets, targets.Count));
                    Picks.Keep(ctx, targets, Math.Max(0, max), Pick, plan.Anchor);
                }
                if (ReportMiss && targets.Count == 0)
                    ReleaseTargets.Miss(ctx, MissReason(ctx, plan), emitted: true);
                var deeper = ctx.Deeper();
                foreach (var t in targets)
                {
                    ExecuteAll(Effects, deeper.WithTarget(t));
                }
            }
            finally
            {
                Return(targets);
            }
            Echoes(ctx, plan.Anchor, plan.Size.Radius);
        }

        private static string MissReason(EffectContext ctx, ResolvedAreaPlan plan)
        {
            if (ctx.Target.IsNone) return "no_candidate";
            if (!ctx.Sim.World.IsAlive(ctx.Target) || ctx.Sim.World.Has<Dead>(ctx.Target)) return "dead";
            if (ctx.TargetTransform is not { } target) return "missing_position";
            var delta = target.Position - plan.Anchor;
            if (new Vector2(delta.X, delta.Y).Length() > plan.Reach || MathF.Abs(delta.Z) > plan.HeightTolerance) return "out_of_reach";
            if (!plan.ContainsCenter(target.Position)) return "outside_area";
            if (plan.RequiresLineOfSight && !LineOfSight.Clear(ctx.Sim.Terrain, plan.Anchor + Eye, target.Position + Eye)) return "line_of_sight";
            return "no_candidate";
        }

        /// <summary>Aftershocks around the area and pulse repeats where it stood, from the weave's area.scatter and area.pulses.</summary>
        private void Echoes(EffectContext ctx, Vector3 center, float radius)
        {
            if (ctx.Weave is null || (ctx.Vars.Count > 0 && ctx.Vars.ContainsKey(DeliveryVars.Echo)))
            {
                return;
            }
            var aftershocks = (int)Math.Round(WeaveAttr(ctx, "area.scatter", 0));
            var pulses = (int)Math.Round(WeaveAttr(ctx, "area.pulses", 0));
            if (aftershocks <= 0 && pulses <= 0)
            {
                return;
            }
            var rules = ctx.Sim.Content.Rules;
            var scale = PlacementScale(ctx);
            var facing = Facing(ctx);
            for (var i = 1; i <= aftershocks; i++)
            {
                var at = InDisc(ctx.Rng, center, radius);
                var vars = VarsWith(ctx, DeliveryVars.Echo, 1);
                vars[DeliveryVars.Scale] = scale * rules.Number("delivery.aftershock_scale", 0.5);
                Later(ctx, At(ctx, Entity.None, at, facing, at, ctx.Depth + 1, vars), rules.Number("delivery.aftershock_delay", 0.15) * i);
            }
            for (var i = 1; i <= pulses; i++)
            {
                Later(ctx, At(ctx, Entity.None, center, facing, center, ctx.Depth + 1, VarsWith(ctx, DeliveryVars.Echo, 1)), rules.Number("delivery.pulse_interval", 0.4) * i);
            }
        }

        private void Later(EffectContext ctx, EffectContext later, double seconds)
        {
            EffectHelpers.Later(ctx.Sim, seconds, () =>
            {
                if (later.Source.IsNone || later.Sim.World.IsAlive(later.Source))
                {
                    Execute(later);
                }
            });
        }
    }
}

/// <summary>
/// <c>projectile</c>: launches flights that the projectile system carries. Firing styles: <c>fan</c> spreads
/// <c>count</c> flights across <c>spread</c> degrees (scaled by <c>projectile.spread</c>), <c>volley</c> fires
/// them side by side <c>volley_spacing</c> apart, <c>ring</c> sends them all around with up to
/// <c>ring_jitter</c> degrees of turn, <c>salvo</c> fires one every <c>salvo_interval</c> seconds re-aimed at
/// the live aim. Trajectory axes (homing, guide, erratic, spiral, orbit, spin, wave, accel, bounce, recurve,
/// converge) and the lifecycle counts (forks, chains, rehit) default to the weave's <c>projectile.*</c>
/// attributes. See docs/design/deliveries.md for every parameter.
/// </summary>
internal sealed class ProjectileHandler : IEffectHandler
{
    private const double Deg = Math.PI / 180.0;

    public string Type => "projectile";

    public EffectSignature Signature { get; } = EffectSignature.Of(
            "count", "speed", "range", "spread", "pierce", "radius", "on_expire", "look", "gravity",
            "shape", "shape_size", "shape_angle", "shape_width",
            "fire", "salvo_interval", "volley_spacing", "ring_jitter", "spread_by_aim", "origin", "origin_range",
            "duration", "return", "forks", "chains", "rehit", "bounce",
            "homing", "guide", "erratic", "spiral", "orbit", "spin", "wave", "accel", "recurve", "converge",
            "amplitude", "orbit_radius", "zigzag", "zigzag_every", "tick", "travel", "occlusion")
        .Hook(EffectHooks.OnHit, "on_hit")
        .Hook(DeliveryHooks.OnFirstHit, "on_first_hit")
        .Hook(EffectHooks.OnEnd, "on_end")
        .Hook(EffectHooks.OnTick, "on_tick")
        .Hook(DeliveryHooks.OnTravel, "on_travel")
        .Hook(EffectHooks.OnLand, "on_land")
        .Hook(DeliveryHooks.OnReturn, "on_return");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx)
    {
        var spec = new ProjectileSpec
        {
            OnHit = ctx.CompileList(p["on_hit"], "on_hit"),
            OnFirstHit = ctx.CompileList(p["on_first_hit"], "on_first_hit"),
            OnExpire = ctx.CompileList(p["on_expire"], "on_expire"),
            OnEnd = ctx.CompileList(p["on_end"], "on_end"),
            OnTick = ctx.CompileList(p["on_tick"], "on_tick"),
            OnTravel = ctx.CompileList(p["on_travel"], "on_travel"),
            OnLand = ctx.CompileList(p["on_land"], "on_land"),
            OnReturn = ctx.CompileList(p["on_return"], "on_return"),
            TickInterval = Math.Max(0.05, ctx.NumberOf(p["tick"], 0.5)),
            TravelEvery = (float)Math.Max(0.25, ctx.NumberOf(p["travel"], 2)),
            Return = ctx.ChoiceOf(p["return"], "return", "none", "none", "origin", "caster") switch
            {
                "origin" => ReturnMode.Origin,
                "caster" => ReturnMode.Caster,
                _ => ReturnMode.None,
            },
            Gravity = ctx.BoolOf(p["gravity"], false),
            Shape = ShapeOf(ctx, p["shape"], "shape"),
        };
        var aim = p["spread_by_aim"] as JsonObject;
        if (p["spread_by_aim"] is not null and not JsonObject)
        {
            throw new InvalidOperationException($"'spread_by_aim' must be {{ near, far, range }} in {ctx.Owner}.");
        }
        return new Effect
        {
            Type = Type,
            Spec = spec,
            Count = ctx.ExprOf(p["count"]),
            Speed = ctx.ExprOf(p["speed"]),
            Range = ctx.ExprOf(p["range"]),
            Spread = ctx.ExprOf(p["spread"], Expr.Constant(15))!,
            Pierce = ctx.ExprOf(p["pierce"]),
            Radius = ctx.NumberOf(p["radius"], 0.5),
            ShapeSize = ctx.ExprOf(p["shape_size"]),
            ShapeAngle = ctx.ExprOf(p["shape_angle"]),
            ShapeWidth = ctx.ExprOf(p["shape_width"]),
            Look = ctx.StringOf(p["look"]),
            Fire = ctx.ChoiceOf(p["fire"], "fire", "fan", "fan", "volley", "ring", "salvo"),
            SalvoInterval = ctx.ExprOf(p["salvo_interval"]),
            VolleySpacing = ctx.NumberOf(p["volley_spacing"], 0.6),
            RingJitter = ctx.NumberOf(p["ring_jitter"], 0) * Deg,
            AimNear = aim is null ? double.NaN : JsonNum.Of(aim["near"], 15),
            AimFar = aim is null ? double.NaN : JsonNum.Of(aim["far"], 15),
            AimRange = aim is null ? double.NaN : Math.Max(0.1, JsonNum.Of(aim["range"], 10)),
            OriginAtPoint = ctx.ChoiceOf(p["origin"], "origin", "caster", "caster", "point") == "point",
            OriginRange = ctx.ExprOf(p["origin_range"]),
            Duration = ctx.ExprOf(p["duration"]),
            Forks = ctx.ExprOf(p["forks"]),
            Chains = ctx.ExprOf(p["chains"]),
            Rehit = ctx.ExprOf(p["rehit"]),
            Bounce = ctx.ExprOf(p["bounce"]),
            Homing = ctx.ExprOf(p["homing"]),
            Guide = ctx.ExprOf(p["guide"]),
            Erratic = ctx.ExprOf(p["erratic"]),
            Spiral = ctx.ExprOf(p["spiral"]),
            Orbit = ctx.ExprOf(p["orbit"]),
            Spin = ctx.ExprOf(p["spin"]),
            Wave = ctx.ExprOf(p["wave"]),
            Accel = ctx.ExprOf(p["accel"]),
            Recurve = ctx.ExprOf(p["recurve"]),
            Converge = ctx.ExprOf(p["converge"]),
            Amplitude = ctx.ExprOf(p["amplitude"]),
            OrbitRadius = ctx.ExprOf(p["orbit_radius"]),
            Zigzag = ctx.ExprOf(p["zigzag"]),
            ZigzagEvery = ctx.ExprOf(p["zigzag_every"]),
            Free = OcclusionFree(ctx, p["occlusion"]),
        };
    }

    /// <summary>The numbers every flight of one launch shares, evaluated once per launch.</summary>
    private struct Flight
    {
        public float Speed;
        public float Range;
        public double Duration;
        public int Pierce;
        public int Forks;
        public int Chains;
        public float Rehit;
        public int Bounces;
        public float Homing;
        public float Guide;
        public float Erratic;
        public float Spiral;
        public float Orbit;
        public float Spin;
        public float Wave;
        public float Amplitude;
        public float Accel;
        public float Recurve;
        public float Converge;
        public float OrbitRadius;
        public float Zigzag;
        public float ZigzagEvery;
        public float Radius;
        public ShapeSize Size;
        public bool Phases;
    }

    private sealed class Effect : CompiledEffect
    {
        public required ProjectileSpec Spec;
        public Expr? Count;
        public Expr? Speed;
        public Expr? Range;
        public required Expr Spread;
        public Expr? Pierce;
        public double Radius = 0.5;
        public Expr? ShapeSize;
        public Expr? ShapeAngle;
        public Expr? ShapeWidth;
        public string? Look;
        public string Fire = "fan";
        public Expr? SalvoInterval;
        public double VolleySpacing = 0.6;
        public double RingJitter;
        public double AimNear = double.NaN;
        public double AimFar = double.NaN;
        public double AimRange = double.NaN;
        public bool OriginAtPoint;
        public Expr? OriginRange;
        public Expr? Duration;
        public Expr? Forks;
        public Expr? Chains;
        public Expr? Rehit;
        public Expr? Bounce;
        public Expr? Homing;
        public Expr? Guide;
        public Expr? Erratic;
        public Expr? Spiral;
        public Expr? Orbit;
        public Expr? Spin;
        public Expr? Wave;
        public Expr? Accel;
        public Expr? Recurve;
        public Expr? Converge;
        public Expr? Amplitude;
        public Expr? OrbitRadius;
        public Expr? Zigzag;
        public Expr? ZigzagEvery;
        /// <summary>Flies through walls (<c>occlusion: free</c>).</summary>
        public bool Free;

        public override void Execute(EffectContext ctx)
        {
            var sim = ctx.Sim;
            var caster = ctx.From ?? ctx.SourceTransform?.Position ?? ctx.Point;
            var origin = caster;
            if (OriginAtPoint)
            {
                origin = ctx.Point;
                if (OriginRange is not null)
                {
                    var limit = (float)Math.Max(0, ctx.Eval(OriginRange, 0));
                    var away = new Vector2(origin.X - caster.X, origin.Y - caster.Y);
                    if (away.Length() > limit && away.LengthSquared() > 1e-6f)
                    {
                        away = Vector2.Normalize(away) * limit;
                        origin = sim.Terrain.SnapToGround(new Vector3(caster.X + away.X, caster.Y + away.Y, caster.Z), 3);
                    }
                }
            }
            var count = Math.Clamp((int)Math.Round(ParamOrAttr(ctx, Count, "projectile.count", 1)), 1, 64);
            var flight = Numbers(ctx);
            var aimPoint = !ctx.Target.IsNone && ctx.TargetTransform is { } aimed ? aimed.Position : ctx.Point;
            var dir = AimFrom(ctx, origin);
            var spreadDegrees = ctx.Eval(Spread, 15);
            if (!double.IsNaN(AimNear))
            {
                var t = Math.Clamp(Vector2.Distance(new Vector2(aimPoint.X, aimPoint.Y), new Vector2(origin.X, origin.Y)) / AimRange, 0, 1);
                spreadDegrees = AimNear + (AimFar - AimNear) * t;
            }
            var spread = (float)(spreadDegrees * WeaveAttr(ctx, "projectile.spread", 1) * Math.PI / 180.0);
            var baseAngle = MathF.Atan2(dir.Y, dir.X);
            var basis = Spec.Emits ? null : ctx.Deeper();
            switch (Fire)
            {
                case "volley":
                {
                    var across = new Vector2(-dir.Y, dir.X);
                    for (var i = 0; i < count; i++)
                    {
                        var offset = (float)((i - (count - 1) / 2.0) * VolleySpacing);
                        var at = origin + new Vector3(across.X * offset, across.Y * offset, 0);
                        Launch(ctx, basis, flight, at, baseAngle, 1, aimPoint);
                    }
                    break;
                }
                case "ring":
                {
                    var phase = RingJitter > 0 ? (float)((ctx.Rng.NextDouble() * 2 - 1) * RingJitter) : 0f;
                    for (var i = 0; i < count; i++)
                    {
                        Launch(ctx, basis, flight, origin, baseAngle + phase + MathF.PI * 2 * i / count, i % 2 == 0 ? 1 : -1, aimPoint);
                    }
                    break;
                }
                case "salvo":
                {
                    Launch(ctx, basis, flight, origin, baseAngle, 1, aimPoint);
                    if (count > 1)
                    {
                        var interval = Math.Max(sim.Clock.Dt, SalvoInterval is null ? sim.Content.Rules.Number("delivery.salvo_interval", 0.15) : ctx.Eval(SalvoInterval, 0.15));
                        var salvo = new Salvo(this, ctx.Fork(), flight, count - 1, interval, dir, aimPoint, sim.Clock.Time);
                        Later(sim, interval, salvo.Beat);
                    }
                    break;
                }
                default:
                {
                    for (var i = 0; i < count; i++)
                    {
                        var offset = count == 1 ? 0 : -spread / 2 + spread * i / (count - 1);
                        Launch(ctx, basis, flight, origin, baseAngle + offset, offset < 0 ? -1 : 1, aimPoint);
                    }
                    break;
                }
            }
        }

        /// <summary>Toward the target, else along the cast's direction, else toward the point, else the source's facing.</summary>
        private static Vector2 AimFrom(EffectContext ctx, Vector3 origin)
        {
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
            return Vector2.Normalize(dir);
        }

        private Flight Numbers(EffectContext ctx)
        {
            var dt = ctx.Sim.Clock.Dt;
            var rules = ctx.Sim.Content.Rules;
            var duration = Duration is null ? double.PositiveInfinity : Math.Max(dt, ctx.Eval(Duration, 2));
            var radius = (float)Radius;
            var shapeSize = ShapeSize is null ? radius : (float)Math.Max(0.05, ctx.Eval(ShapeSize, radius));
            return new Flight
            {
                Speed = (float)Math.Max(0.5, ParamOrAttr(ctx, Speed, "projectile.speed", 12)),
                Range = Range is null && Duration is not null ? float.PositiveInfinity : (float)Math.Max(1, ParamOrAttr(ctx, Range, "range", 12)),
                Duration = duration,
                Pierce = (int)Math.Max(0, ParamOrAttr(ctx, Pierce, "projectile.pierce", 0)),
                Forks = Count0(ParamOrAttr(ctx, Forks, "projectile.forks", 0)),
                Chains = Count0(ParamOrAttr(ctx, Chains, "projectile.chain", 0)),
                Rehit = (float)Math.Max(0, ParamOrAttr(ctx, Rehit, "projectile.rehit", 0)),
                Bounces = Count0(ParamOrAttr(ctx, Bounce, "projectile.bounce", 0)),
                Homing = Rate(ctx, Homing, "projectile.homing"),
                Guide = Rate(ctx, Guide, "projectile.guide"),
                Erratic = Rate(ctx, Erratic, "projectile.erratic"),
                Spiral = Rate(ctx, Spiral, "projectile.spiral"),
                Orbit = Rate(ctx, Orbit, "projectile.orbit"),
                Spin = Rate(ctx, Spin, "projectile.spin"),
                Wave = (float)ParamOrAttr(ctx, Wave, "projectile.wave", 0),
                Accel = (float)ParamOrAttr(ctx, Accel, "projectile.accel", 0),
                Recurve = Rate(ctx, Recurve, "projectile.recurve"),
                Converge = Rate(ctx, Converge, "projectile.converge"),
                Amplitude = (float)(Amplitude is null ? rules.Number("delivery.wave_amplitude", 0.6) : ctx.Eval(Amplitude, 0.6)),
                OrbitRadius = (float)Math.Max(0.5, OrbitRadius is null ? rules.Number("delivery.orbit_radius", 2.5) : ctx.Eval(OrbitRadius, 2.5)),
                Zigzag = Zigzag is null ? 0 : (float)(ctx.Eval(Zigzag, 0) * Deg),
                ZigzagEvery = (float)Math.Max(0.25, ZigzagEvery is null ? 2 : ctx.Eval(ZigzagEvery, 2)),
                Radius = radius,
                Size = SizeOf(ctx, Spec.Shape, shapeSize, ShapeAngle, ShapeWidth, null, scaleAngle: false),
                Phases = !Occluded(ctx, Free),
            };
        }

        private static int Count0(double v) => (int)Math.Max(0, Math.Round(v));

        /// <summary>A rate in degrees per second from a parameter or the weave, as radians per second.</summary>
        private static float Rate(EffectContext ctx, Expr? param, string attribute) => (float)(ParamOrAttr(ctx, param, attribute, 0) * Deg);

        /// <summary>Creates one flight at a point with a heading.</summary>
        public void Launch(EffectContext ctx, EffectContext? basis, in Flight f, Vector3 at, float heading, float side, Vector3 aimPoint)
        {
            var sim = ctx.Sim;
            var start = at + new Vector3(0, 0, 1.0f);
            var speed = f.Speed;
            var velocity = new Vector3(MathF.Cos(heading) * speed, MathF.Sin(heading) * speed, 0);
            if (Spec.Gravity)
            {
                velocity.Z = LobSpeed(sim, start, aimPoint, speed, f.Range);
            }
            var p = new Projectile
            {
                Context = basis ?? ctx.Fork(deeper: 1),
                Spec = Spec,
                Velocity = velocity,
                Speed = speed,
                Heading = heading,
                Carrier = start,
                Origin = start,
                BornAt = sim.Clock.Time,
                Duration = f.Duration,
                Range = f.Range,
                Radius = f.Radius,
                Size = f.Size,
                Pierce = f.Pierce,
                Forks = f.Forks,
                Chains = f.Chains,
                Rehit = f.Rehit,
                Bounces = f.Bounces,
                CastTarget = ctx.Target,
                Homing = f.Homing,
                Guide = f.Guide,
                Erratic = f.Erratic,
                Spiral = f.Spiral,
                Orbit = f.Orbit,
                Spin = f.Spin,
                Wave = f.Wave,
                Amplitude = f.Amplitude,
                Accel = f.Accel,
                Recurve = f.Recurve,
                Converge = f.Converge,
                OrbitRadius = f.OrbitRadius,
                Zigzag = f.Zigzag,
                ZigzagEvery = f.ZigzagEvery,
                ZigzagAt = f.ZigzagEvery,
                AimPoint = aimPoint,
                Converging = f.Converge != 0,
                Phases = f.Phases,
                SpiralAngle = heading,
                Side = side,
                NextTickAt = sim.Clock.Time + Spec.TickInterval,
                NextTravelAt = Spec.TravelEvery,
            };
            var e = ProjectileSystem.Spawn(sim, p, start, ctx.Origin, Look ?? ctx.Weave?.Look);
            if (f.Erratic != 0)
            {
                p.Rng = new Rng(Rng.Combine(ctx.Rng.Seed, (ulong)e.Key));
            }
        }

        /// <summary>The upward speed that lands a lobbed flight at the aim point, or at its range when the point lies beyond.</summary>
        private static float LobSpeed(Simulation sim, Vector3 start, Vector3 aimPoint, float speed, float range)
        {
            var gravity = (float)sim.Content.Rules.Number("move.gravity", 24);
            var flat = new Vector2(aimPoint.X - start.X, aimPoint.Y - start.Y).Length();
            if (!float.IsFinite(range))
            {
                range = flat;
            }
            var distance = Math.Clamp(flat, 0.5f, Math.Max(0.5f, range));
            var time = Math.Max(0.2f, distance / speed);
            var land = sim.Terrain.HeightAt(start.X + (aimPoint.X - start.X) * (distance / Math.Max(flat, 0.001f)), start.Y + (aimPoint.Y - start.Y) * (distance / Math.Max(flat, 0.001f)));
            return (land - start.Z + 0.5f * gravity * time * time) / time;
        }

        /// <summary>
        /// The later shots of a salvo: one per beat, each aimed at the live aim when the caster streams one,
        /// else at the cast's target where it now stands, else along the cast's own aim.
        /// </summary>
        private sealed class Salvo
        {
            private readonly Effect _effect;
            private readonly EffectContext _ctx;
            private readonly Flight _flight;
            private readonly double _interval;
            private readonly Vector2 _direction;
            private readonly Vector3 _point;
            private readonly double _since;
            private int _left;

            public Salvo(Effect effect, EffectContext ctx, Flight flight, int left, double interval, Vector2 direction, Vector3 point, double since)
            {
                _effect = effect;
                _ctx = ctx;
                _flight = flight;
                _left = left;
                _interval = interval;
                _direction = direction;
                _point = point;
                _since = since;
            }

            public void Beat()
            {
                var sim = _ctx.Sim;
                if (!sim.World.IsAlive(_ctx.Source) || sim.World.Has<Dead>(_ctx.Source) || !sim.World.TryGet<Transform>(_ctx.Source, out var st))
                {
                    return;
                }
                var origin = st.Position;
                Vector2 dir;
                var aim = _point;
                if (LiveAim.TryGet(sim, _ctx.Source, _since, out var live, out var liveDir))
                {
                    aim = live;
                    dir = new Vector2(live.X - origin.X, live.Y - origin.Y);
                    if (dir.LengthSquared() < 0.0001f)
                    {
                        dir = liveDir;
                    }
                }
                else if (!_ctx.Target.IsNone && sim.World.IsAlive(_ctx.Target) && !sim.World.Has<Dead>(_ctx.Target) && sim.World.TryGet<Transform>(_ctx.Target, out var tt))
                {
                    aim = tt.Position;
                    dir = new Vector2(aim.X - origin.X, aim.Y - origin.Y);
                }
                else
                {
                    dir = _direction;
                }
                if (dir.LengthSquared() < 0.0001f)
                {
                    dir = st.Forward;
                }
                dir = Vector2.Normalize(dir);
                _effect.Launch(_ctx, _effect.Spec.Emits ? null : _ctx.Deeper(), _flight, origin, MathF.Atan2(dir.Y, dir.X), 1, aim);
                if (--_left > 0)
                {
                    Later(sim, _interval, Beat);
                }
            }
        }
    }
}

/// <summary>
/// <c>chain</c>: from the struck body, leaps <c>count</c> times to another body within <c>range</c>, never
/// the same one twice, choosing by <c>pick</c> (nearest by default; <c>lowest_life</c> finds the most
/// wounded, as heals want).
/// </summary>
internal sealed class ChainHandler : IEffectHandler
{
    public string Type => "chain";

    public EffectSignature Signature { get; } = EffectSignature.Of("count", "range", "filter", "pick")
        .Hook(EffectHooks.OnHit, "effects");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
    {
        Type = Type,
        Count = ctx.ExprOf(p["count"]),
        Range = ctx.ExprOf(p["range"], Expr.Constant(6))!,
        Filter = ctx.StringOf(p["filter"]) ?? "enemy",
        Pick = Picks.Of(ctx, p["pick"], "nearest"),
        Effects = ctx.CompileList(p["effects"], "effects"),
    };

    private sealed class Effect : CompiledEffect
    {
        public Expr? Count;
        public required Expr Range;
        public string Filter = "enemy";
        public Pick Pick = Pick.Nearest;
        public CompiledEffect[] Effects = Array.Empty<CompiledEffect>();

        public override void Execute(EffectContext ctx)
        {
            if (ctx.Target.IsNone)
            {
                return;
            }
            var remaining = (int)Math.Round(ParamOrAttr(ctx, Count, "chain.count", 0));
            if (remaining <= 0)
            {
                return;
            }
            var range = (float)ctx.Eval(Range, 6);
            var struck = Rent();
            try
            {
                struck.Add(ctx.Target);
                var current = ctx.Deeper();
                while (remaining-- > 0)
                {
                    var next = Next(ctx, current.Focus, range, struck);
                    if (next.IsNone)
                    {
                        break;
                    }
                    struck.Add(next);
                    current = current.WithTarget(next).Deeper();
                    ExecuteAll(Effects, current);
                }
            }
            finally
            {
                Return(struck);
            }
        }

        private Entity Next(EffectContext ctx, Vector3 from, float range, List<Entity> struck)
        {
            if (Pick == Pick.Random)
            {
                var pool = Rent();
                try
                {
                    foreach (var (e, _, _) in ctx.Sim.Spatial.Within(from, range))
                    {
                        if (!struck.Contains(e) && Passes(ctx, e, Filter))
                        {
                            pool.Add(e);
                        }
                    }
                    return pool.Count == 0 ? Entity.None : pool[ctx.Rng.Next(pool.Count)];
                }
                finally
                {
                    Return(pool);
                }
            }
            var best = Entity.None;
            var bestKey = double.MaxValue;
            var bestDistance = float.MaxValue;
            foreach (var (e, _, distance) in ctx.Sim.Spatial.Within(from, range))
            {
                if (struck.Contains(e) || !Passes(ctx, e, Filter))
                {
                    continue;
                }
                var key = Pick switch
                {
                    Pick.Nearest => distance,
                    Pick.Farthest => -distance,
                    Pick.AimedNearest when e == ctx.Target => double.NegativeInfinity,
                    _ => Picks.Key(ctx.Sim, Pick, e, from),
                };
                // Ties keep the nearer body, then the one met first.
                if (key < bestKey || (key == bestKey && distance < bestDistance))
                {
                    best = e;
                    bestKey = key;
                    bestDistance = distance;
                }
            }
            return best;
        }
    }
}

/// <summary>
/// <c>field</c>: a standing area that pulses its <c>pulse</c> list on the bodies inside every
/// <c>interval</c> seconds for <c>duration</c> seconds. It can land after a <c>delay</c>, take any shape,
/// demand <c>exposure</c>, strike each body once (<c>hit_once</c>, re-armed by leaving with
/// <c>rearm_on_exit</c>), pull bodies in, drift, seek, turn, follow its caster, grow or shrink along
/// <c>radius_curve</c>, lend a <c>domain</c> of modifiers to the bodies inside, keep one per caster and
/// <c>exclusive</c> key, and lay itself out in <c>segments</c>. See docs/design/deliveries.md.
/// </summary>
internal sealed class FieldHandler : IEffectHandler
{
    public string Type => "field";

    public EffectSignature Signature { get; } = EffectSignature.Of(
            "radius", "duration", "interval", "filter", "look", "follow",
            "shape", "angle", "width", "inner", "delay", "exposure", "pull", "drift", "seek", "rotate", "radius_curve",
            "hit_once", "rearm_on_exit", "domain", "domain_filter", "exclusive", "segments", "pulses", "occlusion")
        .Hook(EffectHooks.OnHit, "pulse")
        .Hook(EffectHooks.OnTick, "on_tick")
        .Hook(EffectHooks.OnLand, "on_land")
        .Hook(DeliveryHooks.OnEnter, "on_enter")
        .Hook(DeliveryHooks.OnExit, "on_exit")
        .Hook(EffectHooks.OnEnd, "on_end");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx)
    {
        var filter = ctx.StringOf(p["filter"]) ?? "enemy";
        var spec = new FieldSpec
        {
            Shape = ShapeOf(ctx, p["shape"], "shape"),
            OnTick = ctx.CompileList(p["on_tick"], "on_tick"),
            OnLand = ctx.CompileList(p["on_land"], "on_land"),
            OnEnter = ctx.CompileList(p["on_enter"], "on_enter"),
            OnExit = ctx.CompileList(p["on_exit"], "on_exit"),
            OnEnd = ctx.CompileList(p["on_end"], "on_end"),
            Domain = p["domain"] is { } domain ? (domain.Deserialize<List<Modifier>>(ContentJson.Options) ?? new List<Modifier>()).ToArray() : Array.Empty<Modifier>(),
            DomainFilter = ctx.StringOf(p["domain_filter"]) ?? filter,
            HitOnce = ctx.BoolOf(p["hit_once"], false),
            RearmOnExit = ctx.BoolOf(p["rearm_on_exit"], false),
        };
        if (p["radius_curve"] is JsonObject curve)
        {
            spec.SizeCurve = ctx.StringOf(curve["curve"]) ?? "linear";
            if (!Curves.Has(spec.SizeCurve))
            {
                throw new InvalidOperationException($"'radius_curve' names unknown curve '{spec.SizeCurve}' in {ctx.Owner}.");
            }
            spec.SizeFrom = (float)JsonNum.Of(curve["from"], 1);
            spec.SizeTo = (float)JsonNum.Of(curve["to"], 1);
        }
        var segments = p["segments"] as JsonObject;
        return new Effect
        {
            Type = Type,
            Spec = spec,
            Radius = ctx.ExprOf(p["radius"]),
            Duration = ctx.ExprOf(p["duration"]),
            Interval = ctx.ExprOf(p["interval"], Expr.Constant(0.5))!,
            Filter = filter,
            Pulse = ctx.CompileList(p["pulse"], "pulse"),
            Look = ctx.StringOf(p["look"]),
            Follow = ctx.BoolOf(p["follow"], false),
            Angle = ctx.ExprOf(p["angle"]),
            Width = ctx.ExprOf(p["width"]),
            Inner = ctx.ExprOf(p["inner"]),
            Delay = ctx.ExprOf(p["delay"]),
            Exposure = ctx.ExprOf(p["exposure"]),
            Pull = ctx.ExprOf(p["pull"]),
            Drift = ctx.ExprOf(p["drift"]),
            Seek = ctx.ExprOf(p["seek"]),
            Rotate = ctx.ExprOf(p["rotate"]),
            Exclusive = ctx.StringOf(p["exclusive"]),
            Pulses = ctx.ExprOf(p["pulses"]),
            SegmentCount = segments is null ? 1 : Math.Max(1, JsonNum.IntOf(segments["count"], 1)),
            SegmentSpacing = segments is null ? 0 : JsonNum.Of(segments["spacing"], 2),
            SegmentsAcross = segments is not null && (JsonNum.StringOf(segments["axis"]) ?? "along") == "across",
            Free = OcclusionFree(ctx, p["occlusion"]),
        };
    }

    private sealed class Effect : CompiledEffect
    {
        public required FieldSpec Spec;
        public Expr? Radius;
        public Expr? Duration;
        public required Expr Interval;
        public string Filter = "enemy";
        public CompiledEffect[] Pulse = Array.Empty<CompiledEffect>();
        public string? Look;
        public bool Follow;
        public Expr? Angle;
        public Expr? Width;
        public Expr? Inner;
        public Expr? Delay;
        public Expr? Exposure;
        public Expr? Pull;
        public Expr? Drift;
        public Expr? Seek;
        public Expr? Rotate;
        public string? Exclusive;
        public Expr? Pulses;
        public int SegmentCount = 1;
        public double SegmentSpacing;
        public bool SegmentsAcross;
        /// <summary>Reaches through walls (<c>occlusion: free</c>).</summary>
        public bool Free;

        public override void Execute(EffectContext ctx)
        {
            var sim = ctx.Sim;
            var now = sim.Clock.Time;
            var center = ctx.Focus;
            var occluded = Occluded(ctx, Free);
            var radius = (float)(ParamOrAttr(ctx, Radius, "area.radius", 2) * PlacementScale(ctx));
            var interval = Math.Max(sim.Clock.Dt, ctx.Eval(Interval, 0.5));
            var duration = Math.Max(sim.Clock.Dt, ParamOrAttr(ctx, Duration, "duration", 4));
            var delay = Delay is null ? 0 : Math.Max(0, ctx.Eval(Delay, 0));
            var facing = Facing(ctx);
            var size = SizeOf(ctx, Spec.Shape, radius, Angle, Width, Inner, scaleAngle: true);
            var pulses = Pulses is null ? int.MaxValue : Math.Max(1, (int)Math.Round(ctx.Eval(Pulses, 1)));
            var exposure = Exposure is null ? 0 : Math.Max(0, ctx.Eval(Exposure, 0));
            var pull = Pull is null ? 0 : (float)ctx.Eval(Pull, 0);
            var drift = Drift is null ? 0 : (float)ctx.Eval(Drift, 0);
            var seek = Seek is null ? 0 : (float)Math.Max(0, ctx.Eval(Seek, 0));
            var rotate = Rotate is null ? 0 : (float)(ctx.Eval(Rotate, 0) * Math.PI / 180.0);
            if (Exclusive is not null)
            {
                FieldSystem.EndExclusive(sim, ctx.Origin, Exclusive);
            }
            var deeper = ctx.Deeper();
            var axis = SegmentsAcross ? new Vector2(-facing.Y, facing.X) : facing;
            for (var i = 0; i < SegmentCount; i++)
            {
                var offset = (float)((i - (SegmentCount - 1) / 2.0) * SegmentSpacing);
                var at = SegmentCount == 1 ? center : sim.Terrain.SnapToGround(center + new Vector3(axis.X * offset, axis.Y * offset, 0), 3);
                var field = new Field
                {
                    Context = deeper,
                    Radius = radius,
                    Interval = interval,
                    NextPulseAt = now + delay,
                    ExpiresAt = now + delay + duration,
                    Filter = Filter,
                    Pulse = Pulse,
                    Follow = Follow ? ctx.Source : Entity.None,
                    Spec = Spec,
                    Size = size,
                    Facing = MathF.Atan2(facing.Y, facing.X),
                    BornAt = now + delay,
                    LandsAt = now + delay,
                    Landed = delay <= 0,
                    PulsesLeft = pulses,
                    Exposure = exposure,
                    Pull = pull,
                    Drift = drift,
                    Seek = seek,
                    Rotate = rotate,
                    Key = Exclusive,
                    Occluded = occluded,
                };
                FieldSystem.Spawn(sim, field, at, ctx.Origin, Look ?? ctx.Weave?.Look);
                if (delay > 0)
                {
                    sim.Events.Publish(new PlacementTelegraphed(ctx.Origin, at, radius, delay) { Tick = sim.Clock.Tick });
                }
            }
        }
    }
}

/// <summary>
/// <c>aura</c>: a field that goes where its source goes. Bodies inside that pass <c>domain_filter</c> carry
/// its <c>domain</c> modifiers while they stay; its <c>pulse</c> (the <c>on_hit</c> hook) strikes the bodies
/// inside that pass <c>filter</c> every <c>interval</c>, and <c>on_tick</c> runs at its center. One aura per
/// source and <c>key</c> (default the weave's): casting it again replaces it, or with <c>toggle</c> turns it
/// off. A toggled aura lasts until turned off; others last <c>duration</c> (the weave's by default).
/// </summary>
internal sealed class AuraHandler : IEffectHandler
{
    public string Type => "aura";

    public EffectSignature Signature { get; } = EffectSignature.Of("radius", "duration", "interval", "filter", "domain", "domain_filter", "key", "toggle", "look", "occlusion")
        .Hook(EffectHooks.OnHit, "pulse")
        .Hook(EffectHooks.OnTick, "on_tick")
        .Hook(DeliveryHooks.OnEnter, "on_enter")
        .Hook(DeliveryHooks.OnExit, "on_exit")
        .Hook(EffectHooks.OnEnd, "on_end");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx)
    {
        var filter = ctx.StringOf(p["filter"]) ?? "enemy";
        return new Effect
        {
            Type = Type,
            Spec = new FieldSpec
            {
                OnTick = ctx.CompileList(p["on_tick"], "on_tick"),
                OnEnter = ctx.CompileList(p["on_enter"], "on_enter"),
                OnExit = ctx.CompileList(p["on_exit"], "on_exit"),
                OnEnd = ctx.CompileList(p["on_end"], "on_end"),
                Domain = p["domain"] is { } domain ? (domain.Deserialize<List<Modifier>>(ContentJson.Options) ?? new List<Modifier>()).ToArray() : Array.Empty<Modifier>(),
                DomainFilter = ctx.StringOf(p["domain_filter"]) ?? "ally_or_self",
            },
            Radius = ctx.ExprOf(p["radius"]),
            Duration = ctx.ExprOf(p["duration"]),
            Interval = ctx.ExprOf(p["interval"], Expr.Constant(1))!,
            Filter = filter,
            Pulse = ctx.CompileList(p["pulse"], "pulse"),
            Key = ctx.StringOf(p["key"]),
            Toggle = ctx.BoolOf(p["toggle"], false),
            Look = ctx.StringOf(p["look"]),
            Free = OcclusionFree(ctx, p["occlusion"]),
        };
    }

    private sealed class Effect : CompiledEffect
    {
        public required FieldSpec Spec;
        public Expr? Radius;
        public Expr? Duration;
        public required Expr Interval;
        public string Filter = "enemy";
        public CompiledEffect[] Pulse = Array.Empty<CompiledEffect>();
        public string? Key;
        public bool Toggle;
        public string? Look;
        public bool Free;

        public override void Execute(EffectContext ctx)
        {
            var sim = ctx.Sim;
            if (!sim.World.TryGet<Transform>(ctx.Source, out var st))
            {
                return;
            }
            var key = Key ?? "aura:" + (ctx.Weave?.Key ?? "aura");
            if (FieldSystem.EndExclusive(sim, ctx.Origin, key) > 0 && Toggle)
            {
                return;
            }
            var now = sim.Clock.Time;
            var radius = (float)(ParamOrAttr(ctx, Radius, "area.radius", 2) * PlacementScale(ctx));
            var duration = Toggle && Duration is null ? double.PositiveInfinity : Math.Max(sim.Clock.Dt, ParamOrAttr(ctx, Duration, "duration", 4));
            var field = new Field
            {
                Context = ctx.Deeper(),
                Radius = radius,
                Interval = Math.Max(sim.Clock.Dt, ctx.Eval(Interval, 1)),
                NextPulseAt = now,
                ExpiresAt = now + duration,
                Filter = Filter,
                Pulse = Pulse,
                Follow = ctx.Source,
                Spec = Spec,
                Size = new ShapeSize(radius, 360, 0, 0),
                Facing = st.Yaw,
                BornAt = now,
                LandsAt = now,
                Key = key,
                Occluded = Occluded(ctx, Free),
            };
            FieldSystem.Spawn(sim, field, st.Position, ctx.Origin, Look ?? ctx.Weave?.Look);
        }
    }
}
