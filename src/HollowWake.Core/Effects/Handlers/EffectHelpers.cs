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

/// <summary>Shared targeting helpers for effect handlers.</summary>
internal static class EffectHelpers
{
    internal static IEnumerable<Entity> Filter(EffectContext ctx, IEnumerable<Entity> candidates, string filter)
    {
        foreach (var e in candidates)
        {
            if (Passes(ctx, e, filter))
            {
                yield return e;
            }
        }
    }

    /// <summary>
    /// Whether one body passes a filter: alive, not dead, with stats, not flagged <c>untargetable</c> (a
    /// leaper in the air), and <c>enemy</c> (hostile to the origin), <c>ally</c> (not hostile, not the
    /// origin), <c>ally_or_self</c>, <c>self</c> (the source), <c>not_self</c>, or <c>all</c> (any other word
    /// passes everyone).
    /// </summary>
    internal static bool Passes(EffectContext ctx, Entity e, string filter)
    {
        var world = ctx.Sim.World;
        if (!world.IsAlive(e) || world.Has<Dead>(e) || !world.TryGet<Stats>(e, out var stats) || stats.Flags.Contains(Untargetable))
        {
            return false;
        }
        return Filters.Passes(ctx, e, filter);
    }

    internal static Vector3 Center(EffectContext ctx, string centerMode)
    {
        return centerMode switch
        {
            "source" => ctx.From ?? ctx.SourceTransform?.Position ?? ctx.Point,
            "point" => ctx.Point,
            _ => ctx.Focus,
        };
    }

    /// <summary>The facing a delivery takes: the context's direction, else the source's facing, else east.</summary>
    internal static Vector2 Facing(EffectContext ctx) =>
        ctx.Direction.LengthSquared() > 0.001f ? Vector2.Normalize(ctx.Direction) : (ctx.SourceTransform?.Forward ?? Vector2.UnitX);

    /// <summary>A weave attribute as the context's weave folds it on its source, or the fallback when there is no weave.</summary>
    internal static double WeaveAttr(EffectContext ctx, string attribute, double fallback)
    {
        if (ctx.Weave is { } weave && ctx.SourceStats is { } stats)
        {
            return weave.Attr(stats, attribute, ctx.Tags);
        }
        return fallback;
    }

    /// <summary>A parameter when content gave one, else the weave attribute, else the fallback.</summary>
    internal static double ParamOrAttr(EffectContext ctx, Expr? param, string attribute, double fallback) =>
        param is null ? WeaveAttr(ctx, attribute, fallback) : ctx.Eval(param, fallback);

    /// <summary>The size placements under a cascade or an aftershock take, as a factor; one elsewhere.</summary>
    internal static double PlacementScale(EffectContext ctx) =>
        ctx.Vars.Count > 0 && ctx.Vars.TryGetValue(DeliveryVars.Scale, out var scale) ? scale : 1;

    /// <summary>
    /// A context for work a delivery does somewhere along its way: at a point, facing a direction, struck at
    /// a body or at none, starting deliveries from <paramref name="from"/>, and with variables of its own
    /// when given. Everything else is the basis's.
    /// </summary>
    internal static EffectContext At(EffectContext basis, Entity target, Vector3 point, Vector2 direction, Vector3? from, int depth, Dictionary<string, double>? vars = null, double? damageScale = null) => new()
    {
        CastId = basis.CastId,
        Sim = basis.Sim,
        Origin = basis.Origin,
        Source = basis.Source,
        Target = target,
        Point = point,
        Direction = direction,
        Weave = basis.Weave,
        Tags = basis.Tags,
        Depth = depth,
        Rng = basis.Rng,
        Potency = basis.Potency,
        DamageScale = damageScale ?? basis.DamageScale,
        From = from,
        Vars = vars ?? basis.Vars,
        Slot = basis.Slot,
        Host = basis.Host,
    };

    /// <summary>A copy of a context's variables with one more set.</summary>
    internal static Dictionary<string, double> VarsWith(EffectContext ctx, string name, double value) =>
        new(ctx.Vars, StringComparer.Ordinal) { [name] = value };

    /// <summary>
    /// A shape's proportions for one delivery: the angle from the parameter or the shape's default (scaled by
    /// the weave's <c>area.angle</c> when asked and the shape has an angle), the width from the parameter or
    /// the shape's default over the weave's <c>area.width</c> (read only for shapes with a width), and the
    /// inner fraction from the parameter or the shape's default.
    /// </summary>
    internal static ShapeSize SizeOf(EffectContext ctx, Shape shape, float radius, Expr? angle, Expr? width, Expr? inner, bool scaleAngle)
    {
        var a = angle is null ? shape.DefaultAngle : (float)ctx.Eval(angle, 90);
        if (scaleAngle && shape.HasAngle)
        {
            a = (float)(a * WeaveAttr(ctx, "area.angle", 1));
        }
        var w = !shape.HasWidth ? 0f : width is null ? shape.DefaultWidth(radius, (float)WeaveAttr(ctx, "area.width", 2)) : (float)ctx.Eval(width, 2);
        var i = inner is null ? shape.DefaultInner : (float)Math.Clamp(ctx.Eval(inner, 0), 0, 1);
        return new ShapeSize(radius, a, w, i);
    }

    /// <summary>The height above the ground lines of sight are drawn at, between bodies and the points deliveries act from.</summary>
    internal static readonly Vector3 Eye = new(0, 0, 1);

    /// <summary>The flag a body wears while no delivery may strike it.</summary>
    internal const string Untargetable = "untargetable";

    /// <summary>
    /// Whether a delivery's lines are stopped by walls and rising ground: unless its <c>occlusion</c> is
    /// <c>free</c> or its weave phases (<c>phasing</c> above zero).
    /// </summary>
    internal static bool Occluded(EffectContext ctx, bool free) => !free && WeaveAttr(ctx, "phasing", 0) <= 0;

    /// <summary>Reads an <c>occlusion</c> parameter: true for <c>free</c>, false for <c>blocked</c> (the default).</summary>
    internal static bool OcclusionFree(EffectCompileContext ctx, JsonNode? node) => ctx.ChoiceOf(node, "occlusion", "blocked", "blocked", "free") == "free";

    /// <summary>Reads a shape name, failing compilation with the known names when it is not one.</summary>
    internal static Shape ShapeOf(EffectCompileContext ctx, JsonNode? node, string field)
    {
        var name = ctx.StringOf(node) ?? "circle";
        if (!Shapes.TryGet(name, out var shape))
        {
            throw new InvalidOperationException($"'{field}' names unknown shape '{name}' in {ctx.Owner}; known shapes are {string.Join(", ", Shapes.Names)}.");
        }
        return shape;
    }

    /// <summary>
    /// Runs an action a number of seconds from now on the tick those seconds reach, at least the next one.
    /// Sums of short intervals (0.1 three times) land on the tick they name rather than one past it.
    /// </summary>
    internal static void Later(Simulation sim, double seconds, Action action)
    {
        var ticks = (long)Math.Ceiling(seconds * sim.Clock.TicksPerSecond - 1e-6);
        sim.Scheduler.AtTick(sim.Clock.Tick + Math.Max(1, ticks), action);
    }

    /// <summary>A uniformly random point in a disc.</summary>
    internal static Vector3 InDisc(Maths.Rng rng, Vector3 center, double radius)
    {
        var r = radius * Math.Sqrt(rng.NextDouble());
        var a = rng.NextDouble() * Math.PI * 2;
        return new Vector3(center.X + (float)(Math.Cos(a) * r), center.Y + (float)(Math.Sin(a) * r), center.Z);
    }

    // ------------------------------------------------------------ pooled lists

    [ThreadStatic] private static Stack<List<Entity>>? _lists;

    /// <summary>An empty list of bodies to collect into; give it back with <see cref="Return"/>. Nested rents get different lists.</summary>
    internal static List<Entity> Rent()
    {
        var pool = _lists ??= new Stack<List<Entity>>();
        return pool.Count > 0 ? pool.Pop() : new List<Entity>(16);
    }

    internal static void Return(List<Entity> list)
    {
        list.Clear();
        (_lists ??= new Stack<List<Entity>>()).Push(list);
    }
}

/// <summary>
/// Variables deliveries set on the contexts they run effects in. They are ordinary variables: expressions
/// read them where a delivery set them.
/// </summary>
public static class DeliveryVars
{
    /// <summary>Multiplies the size of areas, fields and strikes placed under it: cascades step it, aftershocks shrink it.</summary>
    public const string Scale = "placement.scale";

    /// <summary>Which placement of a cascade or which strike of a scatter this is, from zero.</summary>
    public const string Step = "placement.step";

    /// <summary>Set on aftershocks and pulse repeats, which never spawn more of their own.</summary>
    public const string Echo = "placement.echo";

    /// <summary>Every name above, for validators.</summary>
    public static readonly IReadOnlyList<string> All = new[] { Scale, Step, Echo };
}

/// <summary>
/// The live aim a caster streams while a held, guided or salvo weave is in play: a point on the ground and
/// a facing, kept on the caster's weave book from its aim intents. Deliveries that follow the aim read it
/// here and fall back to the cast's own point, direction or target when the caster sent none.
/// </summary>
internal static class LiveAim
{
    /// <summary>
    /// The caster's latest aim sent at or after <paramref name="since"/>. False when it sent none, in which
    /// case the caller keeps the aim the cast was made with.
    /// </summary>
    public static bool TryGet(Simulation sim, Entity caster, double since, out Vector3 point, out Vector2 direction)
    {
        if (sim.World.TryGet<WeaveBook>(caster, out var book) && book.AimAt >= since && book.AimPoint is { } aimed)
        {
            point = aimed;
            direction = book.AimDirection ?? default;
            return true;
        }
        point = default;
        direction = default;
        return false;
    }
}
