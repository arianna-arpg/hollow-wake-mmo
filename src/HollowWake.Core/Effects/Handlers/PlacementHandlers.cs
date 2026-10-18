using System.Numerics;
using System.Text.Json.Nodes;
using HollowWake.Core.Components;
using HollowWake.Core.Combat;
using HollowWake.Core.Content;
using HollowWake.Core.Ecs;
using HollowWake.Core.Expressions;
using HollowWake.Core.Maths;
using HollowWake.Core.Sim;
using HollowWake.Core.Systems;
using HollowWake.Core.Tags;
using static HollowWake.Core.Effects.Handlers.EffectHelpers;

namespace HollowWake.Core.Effects.Handlers;

/// <summary>
/// Placements that put a delivery at many points or join points: scattered strikes, cascades, tethers and
/// detonations (<c>scatter</c>, <c>cascade</c>, <c>tether</c>, <c>detonate</c>). Wrappers among them
/// declare the list they wrap so glyph patches can wrap a form's node in them.
/// </summary>
public static class PlacementHandlers
{
    public static void Register(EffectRegistry r)
    {
        r.Register(new ScatterHandler());
        r.Register(new CascadeHandler());
        r.Register(new TetherHandler());
        r.Register(new AimHandler());
        r.Register(new DetonateHandler());
    }

    private static readonly TagSet TelegraphTags = TagSet.Of("telegraph");

    /// <summary>
    /// Marks a point where something will land: a short-lived entity clients draw (look <c>telegraph</c>)
    /// and a <see cref="PlacementTelegraphed"/> event.
    /// </summary>
    internal static void Telegraph(EffectContext ctx, Vector3 at, float radius, double seconds)
    {
        var sim = ctx.Sim;
        var e = sim.World.Create();
        sim.World.Add(e, new Transform { Position = at, Radius = radius });
        sim.World.Add(e, new Identity { Name = "telegraph", Tags = TelegraphTags, Faction = Relations.FactionOf(sim, ctx.Origin), Owner = ctx.Origin, Look = "telegraph" });
        sim.World.Add(e, new Lifetime { ExpiresAt = sim.Clock.Time + seconds });
        sim.Spatial.Update(e, at);
        sim.Events.Publish(new EntitySpawned(e, "telegraph", at) { Tick = sim.Clock.Tick });
        sim.Events.Publish(new PlacementTelegraphed(ctx.Origin, at, radius, seconds) { Tick = sim.Clock.Tick });
    }

    /// <summary>Runs a list at a context now, or later through the scheduler while its source still exists.</summary>
    internal static void RunAt(CompiledEffect[] effects, EffectContext at, double seconds)
    {
        if (seconds <= 0)
        {
            CompiledEffect.ExecuteAll(effects, at);
            return;
        }
        Later(at.Sim, seconds, () =>
        {
            if (at.Source.IsNone || at.Sim.World.IsAlive(at.Source))
            {
                CompiledEffect.ExecuteAll(effects, at);
            }
        });
    }
}

/// <summary>
/// <c>scatter</c>: a storm of strikes in a disc around the focus. <c>count</c> strikes (the weave's
/// <c>area.strikes</c> by default) land within <c>radius</c>, one every <c>interval</c> seconds or all at
/// once; <c>at: enemies</c> puts the first ones under the nearest enemies (within <c>jitter</c> of where they
/// stood) and the rest at random. With <c>telegraph</c> each strike is marked that many seconds before it
/// lands. Each strike runs <c>effects</c> at its point, one level deeper, with <c>placement.step</c> its index.
/// </summary>
internal sealed class ScatterHandler : IEffectHandler
{
    public string Type => "scatter";

    public EffectSignature Signature { get; } = EffectSignature.Of("count", "radius", "interval", "at", "jitter", "telegraph", "telegraph_radius", "filter", "effects");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
    {
        Type = Type,
        Count = ctx.ExprOf(p["count"]),
        Radius = ctx.ExprOf(p["radius"]),
        Interval = ctx.ExprOf(p["interval"], Expr.Constant(0))!,
        AtEnemies = ctx.ChoiceOf(p["at"], "at", "random", "random", "enemies") == "enemies",
        Jitter = ctx.ExprOf(p["jitter"], Expr.Constant(0))!,
        Telegraph = ctx.ExprOf(p["telegraph"], Expr.Constant(0))!,
        TelegraphRadius = ctx.ExprOf(p["telegraph_radius"]),
        Filter = ctx.StringOf(p["filter"]) ?? "enemy",
        Effects = ctx.CompileList(p["effects"], "effects"),
    };

    private sealed class Effect : CompiledEffect
    {
        public Expr? Count;
        public Expr? Radius;
        public required Expr Interval;
        public bool AtEnemies;
        public required Expr Jitter;
        public required Expr Telegraph;
        public Expr? TelegraphRadius;
        public string Filter = "enemy";
        public CompiledEffect[] Effects = Array.Empty<CompiledEffect>();

        public override void Execute(EffectContext ctx)
        {
            var count = (int)Math.Round(ParamOrAttr(ctx, Count, "area.strikes", 1));
            if (count <= 0 || Effects.Length == 0)
            {
                return;
            }
            var scale = PlacementScale(ctx);
            var radius = Math.Max(0.1, Radius is null ? WeaveAttr(ctx, "area.radius", 2) * 2 : ctx.Eval(Radius, 4)) * scale;
            var interval = Math.Max(0, ctx.Eval(Interval, 0));
            var jitter = Math.Max(0, ctx.Eval(Jitter, 0));
            var telegraph = Math.Max(0, ctx.Eval(Telegraph, 0));
            var markRadius = (float)(TelegraphRadius is null ? WeaveAttr(ctx, "area.radius", 2) * scale : ctx.Eval(TelegraphRadius, 2));
            var center = ctx.Focus;
            var rng = new Rng(Rng.Combine(ctx.Rng.NextUInt64(), Rng.Hash64("scatter")));
            var facing = Facing(ctx);
            var points = Points(ctx, center, count, radius, jitter, rng);
            for (var i = 0; i < points.Count; i++)
            {
                var at = ctx.Sim.Terrain.SnapToGround(points[i], 3);
                var delay = interval * i;
                if (telegraph > 0)
                {
                    if (delay <= 0)
                    {
                        PlacementHandlers.Telegraph(ctx, at, markRadius, telegraph);
                    }
                    else
                    {
                        var mark = at;
                        Later(ctx.Sim, delay, () =>
                        {
                            if (ctx.Source.IsNone || ctx.Sim.World.IsAlive(ctx.Source))
                            {
                                PlacementHandlers.Telegraph(ctx, mark, markRadius, telegraph);
                            }
                        });
                    }
                }
                var strike = At(ctx, Entity.None, at, facing, at, ctx.Depth + 1, VarsWith(ctx, DeliveryVars.Step, i));
                PlacementHandlers.RunAt(Effects, strike, delay + telegraph);
            }
        }

        /// <summary>Strike points: under the nearest enemies first when asked, then uniformly in the disc.</summary>
        private List<Vector3> Points(EffectContext ctx, Vector3 center, int count, double radius, double jitter, Rng rng)
        {
            var points = new List<Vector3>(count);
            if (AtEnemies)
            {
                var enemies = Rent();
                try
                {
                    foreach (var (e, _, _) in ctx.Sim.Spatial.Within(center, (float)radius))
                    {
                        if (Passes(ctx, e, Filter))
                        {
                            enemies.Add(e);
                        }
                    }
                    Picks.Keep(ctx, enemies, Math.Min(count, enemies.Count), Pick.Nearest, center);
                    foreach (var e in enemies)
                    {
                        var p = ctx.Sim.World.Get<Transform>(e)!.Position;
                        points.Add(jitter > 0 ? InDisc(rng, p, jitter) : p);
                    }
                    // Keep the nearest first even when there were fewer enemies than strikes.
                    SortByDistance(points, center);
                }
                finally
                {
                    Return(enemies);
                }
            }
            while (points.Count < count)
            {
                points.Add(InDisc(rng, center, radius));
            }
            return points;
        }

        private static void SortByDistance(List<Vector3> points, Vector3 center)
        {
            for (var i = 1; i < points.Count; i++)
            {
                var p = points[i];
                var d = Vector2.DistanceSquared(new Vector2(p.X, p.Y), new Vector2(center.X, center.Y));
                var j = i - 1;
                while (j >= 0 && Vector2.DistanceSquared(new Vector2(points[j].X, points[j].Y), new Vector2(center.X, center.Y)) > d)
                {
                    points[j + 1] = points[j];
                    j--;
                }
                points[j + 1] = p;
            }
        }
    }
}

/// <summary>
/// <c>cascade</c>: runs its <c>effects</c> where it is, then again <c>count</c> more times (the weave's
/// <c>area.cascade</c> by default) at points stepping <c>step</c> units along <c>dir</c>: <c>forward</c>
/// along the facing, <c>backward</c>, <c>axis</c> (both ways in turn), <c>out</c> (away from the caster) or
/// <c>random</c>. Each later placement waits <c>interval</c> seconds more than the one before (growing by
/// <c>interval_step</c>), is <c>scale_step</c> larger and deals <c>damage_step</c> more. A wrapper: glyphs wrap a
/// form's placement in it with a patch.
/// </summary>
internal sealed class CascadeHandler : IEffectHandler
{
    public string Type => "cascade";

    public EffectSignature Signature { get; } = EffectSignature.Of("count", "dir", "step", "scale_step", "damage_step", "interval", "interval_step")
        .Wrapping("effects");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
    {
        Type = Type,
        Count = ctx.ExprOf(p["count"]),
        Dir = ctx.ChoiceOf(p["dir"], "dir", "forward", "forward", "backward", "axis", "out", "random"),
        Step = ctx.ExprOf(p["step"]),
        ScaleStep = ctx.ExprOf(p["scale_step"], Expr.Constant(0))!,
        DamageStep = ctx.ExprOf(p["damage_step"], Expr.Constant(0))!,
        Interval = ctx.ExprOf(p["interval"], Expr.Constant(0.15))!,
        IntervalStep = ctx.ExprOf(p["interval_step"], Expr.Constant(0))!,
        Effects = ctx.CompileList(p["effects"], "effects"),
    };

    private sealed class Effect : CompiledEffect
    {
        public Expr? Count;
        public string Dir = "forward";
        public Expr? Step;
        public required Expr ScaleStep;
        public required Expr DamageStep;
        public required Expr Interval;
        public required Expr IntervalStep;
        public CompiledEffect[] Effects = Array.Empty<CompiledEffect>();

        public override void Execute(EffectContext ctx)
        {
            ExecuteAll(Effects, ctx);
            var extra = (int)Math.Round(ParamOrAttr(ctx, Count, "area.cascade", 0));
            if (extra <= 0 || Effects.Length == 0)
            {
                return;
            }
            var basePoint = ctx.From ?? (ctx.Target.IsNone ? ctx.Point : ctx.Focus);
            var step = Step is null ? WeaveAttr(ctx, "area.radius", 2) * 1.5 * PlacementScale(ctx) : ctx.Eval(Step, 3);
            var scaleStep = ctx.Eval(ScaleStep, 0);
            var damageStep = ctx.Eval(DamageStep, 0);
            var interval = ctx.Eval(Interval, 0.15);
            var intervalStep = ctx.Eval(IntervalStep, 0);
            var facing = Facing(ctx);
            var outward = facing;
            if (Dir == "out" && ctx.SourceTransform is { } st)
            {
                var away = new Vector2(basePoint.X - st.Position.X, basePoint.Y - st.Position.Y);
                if (away.LengthSquared() > 0.0001f)
                {
                    outward = Vector2.Normalize(away);
                }
            }
            var rng = Dir == "random" ? new Rng(Rng.Combine(ctx.Rng.NextUInt64(), Rng.Hash64("cascade"))) : null;
            var delay = 0.0;
            for (var i = 1; i <= extra; i++)
            {
                delay += Math.Max(0, interval + intervalStep * (i - 1));
                Vector2 offset;
                switch (Dir)
                {
                    case "backward":
                        offset = -facing * (float)(step * i);
                        break;
                    case "axis":
                    {
                        var rank = (i + 1) / 2;
                        offset = facing * (float)(step * rank * (i % 2 == 1 ? 1 : -1));
                        break;
                    }
                    case "random":
                    {
                        var angle = rng!.NextDouble() * Math.PI * 2;
                        offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * (float)(step * i);
                        break;
                    }
                    case "out":
                        offset = outward * (float)(step * i);
                        break;
                    default:
                        offset = facing * (float)(step * i);
                        break;
                }
                var at = ctx.Sim.Terrain.SnapToGround(basePoint + new Vector3(offset.X, offset.Y, 0), 3);
                var vars = VarsWith(ctx, DeliveryVars.Step, i);
                vars[DeliveryVars.Scale] = PlacementScale(ctx) * Math.Max(0.1, 1 + scaleStep * i);
                var placement = At(ctx, Entity.None, at, facing, at, ctx.Depth + 1, vars, ctx.DamageScale * Math.Max(0, 1 + damageStep * i));
                PlacementHandlers.RunAt(Effects, placement, delay);
            }
        }
    }
}

/// <summary>
/// <c>tether</c>: a live line from the source to its target (<c>link: target</c>, the default), to the point
/// (<c>point</c>) or to the source's owner (<c>owner</c>, for constructs and minions). Every <c>interval</c> it
/// strikes the bodies passing <c>filter</c> that it crosses within half its <c>width</c> (<c>pulse</c>, the
/// <c>on_hit</c> hook); it lasts <c>duration</c> seconds (the weave's by default) and snaps when stretched past
/// <c>max_length</c> or when an end it is bound to is gone, running <c>on_end</c>.
/// </summary>
internal sealed class TetherHandler : IEffectHandler
{
    public string Type => "tether";

    public EffectSignature Signature { get; } = EffectSignature.Of("link", "width", "duration", "interval", "filter", "max_length", "look")
        .Hook(EffectHooks.OnHit, "pulse")
        .Hook(EffectHooks.OnEnd, "on_end");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
    {
        Type = Type,
        Link = ctx.ChoiceOf(p["link"], "link", "target", "target", "point", "owner"),
        Width = ctx.ExprOf(p["width"], Expr.Constant(1))!,
        Duration = ctx.ExprOf(p["duration"]),
        Interval = ctx.ExprOf(p["interval"], Expr.Constant(0.5))!,
        Filter = ctx.StringOf(p["filter"]) ?? "enemy",
        MaxLength = ctx.ExprOf(p["max_length"]),
        Look = ctx.StringOf(p["look"]),
        Pulse = ctx.CompileList(p["pulse"], "pulse"),
        OnEnd = ctx.CompileList(p["on_end"], "on_end"),
    };

    private sealed class Effect : CompiledEffect
    {
        public string Link = "target";
        public required Expr Width;
        public Expr? Duration;
        public required Expr Interval;
        public string Filter = "enemy";
        public Expr? MaxLength;
        public string? Look;
        public CompiledEffect[] Pulse = Array.Empty<CompiledEffect>();
        public CompiledEffect[] OnEnd = Array.Empty<CompiledEffect>();

        public override void Execute(EffectContext ctx)
        {
            var sim = ctx.Sim;
            var to = Entity.None;
            switch (Link)
            {
                case "target":
                    if (ctx.Target.IsNone || ctx.Target == ctx.Source)
                    {
                        return;
                    }
                    to = ctx.Target;
                    break;
                case "owner":
                    to = sim.World.Get<Identity>(ctx.Source)?.Owner ?? Entity.None;
                    if (to.IsNone)
                    {
                        return;
                    }
                    break;
            }
            var now = sim.Clock.Time;
            var tether = new Tether
            {
                Context = ctx.Deeper(),
                From = ctx.Source,
                To = to,
                Point = ctx.Point,
                Width = (float)Math.Max(0.05, ctx.Eval(Width, 1)),
                MaxLength = MaxLength is null ? float.PositiveInfinity : (float)ctx.Eval(MaxLength, 10),
                Interval = Math.Max(sim.Clock.Dt, ctx.Eval(Interval, 0.5)),
                NextPulseAt = now,
                ExpiresAt = now + Math.Max(sim.Clock.Dt, ParamOrAttr(ctx, Duration, "duration", 4)),
                Filter = Filter,
                Pulse = Pulse,
                OnEnd = OnEnd,
            };
            TetherSystem.Spawn(sim, tether, ctx.Origin, Look ?? ctx.Weave?.Look);
        }
    }
}

/// <summary>
/// <c>aim</c>: a wrapper that turns the bearing its <c>effects</c> are delivered along: by <c>offset</c>
/// degrees, by the next of a <c>sequence</c> of bearings each time the same source runs it, and by a random
/// turn within <c>spread</c> degrees (scaled by the weave's <c>aim.spread</c>) rolled on the cast's stream. The
/// point turns about the source with it, and the deliveries go along the bearing rather than at a target.
/// Glyphs graft it onto a form's delivery with a wrap patch.
/// </summary>
internal sealed class AimHandler : IEffectHandler
{
    public string Type => "aim";

    public EffectSignature Signature { get; } = EffectSignature.Of("offset", "spread", "sequence")
        .Wrapping("effects");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx)
    {
        var sequence = Array.Empty<double>();
        if (p["sequence"] is JsonArray steps)
        {
            sequence = new double[steps.Count];
            for (var i = 0; i < steps.Count; i++)
            {
                sequence[i] = JsonNum.Of(steps[i], 0);
            }
        }
        else if (p["sequence"] is not null)
        {
            throw new InvalidOperationException($"'sequence' must be a list of bearings in degrees in {ctx.Owner}.");
        }
        return new Effect
        {
            Type = Type,
            Offset = ctx.ExprOf(p["offset"], Expr.Constant(0))!,
            Spread = ctx.ExprOf(p["spread"], Expr.Constant(0))!,
            Sequence = sequence,
            Effects = ctx.CompileList(p["effects"], "effects"),
        };
    }

    private sealed class Effect : CompiledEffect
    {
        public required Expr Offset;
        public required Expr Spread;
        public double[] Sequence = Array.Empty<double>();
        public CompiledEffect[] Effects = Array.Empty<CompiledEffect>();

        public override void Execute(EffectContext ctx)
        {
            var degrees = ctx.Eval(Offset, 0);
            if (Sequence.Length > 0 && ctx.Sim.World.IsAlive(ctx.Source))
            {
                // Each source keeps its own place in the sequence, on itself, so it goes with the body.
                var cursors = ctx.Sim.World.GetOrAdd<AimCursors>(ctx.Source).ByAim;
                if (!cursors.TryGetValue(this, out var at) && cursors.Count >= AimCursors.Max)
                {
                    // Recompiled weaves leave their old aims behind; past a handful, every sequence starts over.
                    cursors.Clear();
                }
                degrees += Sequence[at % Sequence.Length];
                cursors[this] = (at + 1) % Sequence.Length;
            }
            var spread = ctx.Eval(Spread, 0) * WeaveAttr(ctx, "aim.spread", 1);
            if (spread > 0)
            {
                degrees += (ctx.Rng.NextDouble() - 0.5) * spread;
            }
            var facing = Facing(ctx);
            var origin = ctx.From ?? ctx.SourceTransform?.Position ?? ctx.Point;
            if (!ctx.Target.IsNone && ctx.TargetTransform is { } tt && ctx.Target != ctx.Source)
            {
                var toTarget = new Vector2(tt.Position.X - origin.X, tt.Position.Y - origin.Y);
                if (toTarget.LengthSquared() > 1e-6f)
                {
                    facing = Vector2.Normalize(toTarget);
                }
            }
            var angle = MathF.Atan2(facing.Y, facing.X) + (float)(degrees * Math.PI / 180.0);
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var reach = new Vector2(ctx.Point.X - origin.X, ctx.Point.Y - origin.Y).Length();
            var point = new Vector3(origin.X + direction.X * reach, origin.Y + direction.Y * reach, ctx.Point.Z);
            var target = ctx.Target == ctx.Source ? ctx.Target : Entity.None;
            ExecuteAll(Effects, At(ctx, target, point, direction, ctx.From, ctx.Depth));
        }
    }
}

/// <summary>
/// <c>detonate</c>: sets off what the source owns: its projectiles in flight, its fields, and anything else it
/// owns that carries one of the <c>tags</c> (default <c>projectile</c> and <c>field</c>), all of them or only
/// the one nearest the point (<c>pick: nearest</c>), within <c>radius</c> of the point when given. Each runs
/// <c>effects</c> where it is, then ends: a projectile with its <c>on_end</c> as if its flight ran out there, a
/// field with its <c>on_end</c>, anything else gone.
/// </summary>
internal sealed class DetonateHandler : IEffectHandler
{
    public string Type => "detonate";

    public EffectSignature Signature { get; } = EffectSignature.Of("tags", "pick", "radius", "effects");

    private static readonly TagSet DefaultTags = TagSet.Of("projectile", "field");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx)
    {
        var tags = p["tags"] is null ? DefaultTags : ctx.TagsOf(p["tags"]);
        return new Effect
        {
            Type = Type,
            Tags = tags,
            Nearest = ctx.ChoiceOf(p["pick"], "pick", "all", "all", "nearest") == "nearest",
            Radius = ctx.ExprOf(p["radius"]),
            Effects = ctx.CompileList(p["effects"], "effects"),
            OnlyDeliveries = tags.Items.All(t => t is "projectile" or "field"),
        };
    }

    private sealed class Effect : CompiledEffect
    {
        public TagSet Tags = TagSet.Empty;
        public bool Nearest;
        public Expr? Radius;
        public CompiledEffect[] Effects = Array.Empty<CompiledEffect>();
        public bool OnlyDeliveries;

        public override void Execute(EffectContext ctx)
        {
            var sim = ctx.Sim;
            var radius = Radius is null ? float.PositiveInfinity : (float)ctx.Eval(Radius, 10);
            var owned = Rent();
            try
            {
                if (Tags.Has("projectile"))
                {
                    foreach (var (e, p) in sim.World.Query<Projectile>())
                    {
                        if (!p.Ended && p.Context.Origin == ctx.Origin)
                        {
                            owned.Add(e);
                        }
                    }
                }
                if (Tags.Has("field"))
                {
                    foreach (var (e, f) in sim.World.Query<Field>())
                    {
                        if (!f.Ended && f.Context.Origin == ctx.Origin)
                        {
                            owned.Add(e);
                        }
                    }
                }
                if (!OnlyDeliveries)
                {
                    foreach (var (e, id) in sim.World.Query<Identity>())
                    {
                        if (id.Owner == ctx.Origin && id.Tags.HasAny(Tags) && !sim.World.Has<Projectile>(e) && !sim.World.Has<Field>(e))
                        {
                            owned.Add(e);
                        }
                    }
                }
                // Keep those within reach; for the nearest, only the nearest of them.
                var best = Entity.None;
                var bestDistance = float.MaxValue;
                for (var i = owned.Count - 1; i >= 0; i--)
                {
                    var at = sim.World.Get<Transform>(owned[i])?.Position ?? ctx.Point;
                    var d = Vector2.Distance(new Vector2(at.X, at.Y), new Vector2(ctx.Point.X, ctx.Point.Y));
                    if (d > radius)
                    {
                        owned.RemoveAt(i);
                    }
                    else if (d <= bestDistance)
                    {
                        best = owned[i];
                        bestDistance = d;
                    }
                }
                if (Nearest)
                {
                    owned.Clear();
                    if (!best.IsNone)
                    {
                        owned.Add(best);
                    }
                }
                foreach (var e in owned)
                {
                    Detonate(ctx, e);
                }
            }
            finally
            {
                Return(owned);
            }
        }

        private void Detonate(EffectContext ctx, Entity e)
        {
            var sim = ctx.Sim;
            if (!sim.World.IsAlive(e) || !sim.World.TryGet<Transform>(e, out var t))
            {
                return;
            }
            var at = t.Position;
            if (Effects.Length > 0)
            {
                ExecuteAll(Effects, At(ctx, Entity.None, at, t.Forward, at, ctx.Depth + 1));
            }
            if (ProjectileSystem.Detonate(sim, e) || FieldSystem.End(sim, e) || TetherSystem.End(sim, e))
            {
                return;
            }
            LifetimeSystem.Despawn(sim, e);
        }
    }
}
