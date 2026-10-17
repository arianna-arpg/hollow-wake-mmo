using System.Numerics;
using System.Text.Json.Nodes;
using HollowWake.Client.UI.Logic;
using HollowWake.Core.Definitions;
using HollowWake.Core.Effects;
using HollowWake.Core.Effects.Handlers;
using HollowWake.Core.Expressions;

namespace ForecastExperiment;

public sealed record ForecastRegion(Shape Shape, ShapeSize Size, Vector3 Anchor, Vector2 Facing)
{
    public bool ContainsCenter(Vector3 point) => Shape.Contains(Size,
        new Vector2(point.X - Anchor.X, point.Y - Anchor.Y), Facing, 0);

    // The diagnostic viewer consumes the existing client's outline, not a separate skill/shape renderer.
    public float[][][] Outline()
    {
        if (!ShapeOutline.Names.Contains(Shape.Name)) throw new InvalidOperationException("No client outline for registered shape: " + Shape.Name);
        var outline = ShapeOutline.RegionOf(new ShapeSpec(Shape.Name, Size.Radius,
            Size.Angle * MathF.PI / 180, Size.Width, Size.Inner * Size.Radius, Shape.Curvature), 48);
        return outline.Holes.Prepend(outline.Outer).Select(loop => loop.Select(p => new[] { p.X, p.Y }).ToArray()).ToArray();
    }
}

// Experiment-only proof of mechanism registration. Production must share the full execution plan.
// No provider, renderer or runtime branch may inspect a weave/form/species ID to select behavior.
public sealed class ForecastProviders
{
    private readonly Dictionary<string, Func<EffectContext, JsonObject, ForecastRegion>> _providers = new(StringComparer.Ordinal);
    public void Register(string effectType, Func<EffectContext, JsonObject, ForecastRegion> provider) => _providers.Add(effectType, provider);

    public ForecastRegion? Describe(EffectContext context)
    {
        var graph = context.Weave!.Specs;
        if (graph.Count != 1 || !_providers.TryGetValue(graph[0].Type, out var provider)) return null;
        return provider(context, graph[0].Params);
    }

    public static ForecastProviders CreateDefault()
    {
        var registry = new ForecastProviders();
        registry.Register("area", DirectArea);
        return registry;
    }

    private static ForecastRegion DirectArea(EffectContext context, JsonObject parameters)
    {
        Expr? Parameter(string name)
        {
            if (parameters[name] is not { } node) return null;
            var expression = Expr.Parse(node.ToString());
            // Bounded to the deterministic shipped fixtures. Attribute dependencies also need a
            // purity contract before this can become a production provider for arbitrary content.
            if (expression.Source.Contains('(') || expression.Identifiers.Any(x => !x.StartsWith("weave.", StringComparison.Ordinal)))
                throw new InvalidOperationException("Unsupported preview expression: " + name);
            return expression;
        }
        var shape = Shapes.Get(parameters["shape"]?.GetValue<string>() ?? "circle");
        var radius = (float)Math.Max(0.1, EffectHelpers.ParamOrAttr(context, Parameter("radius"), "area.radius", 2));
        radius = (float)(radius * EffectHelpers.PlacementScale(context));
        if (shape.Anchored)
        {
            if (parameters["reach"]?.GetValue<string>() == "edge") radius += context.SourceTransform?.Radius ?? 0;
            if (context.Tags.Has("melee")) radius += (float)Math.Max(0, EffectHelpers.WeaveAttr(context, "melee.reach", 0));
        }
        var size = EffectHelpers.SizeOf(context, shape, radius, Parameter("angle"), Parameter("width"), Parameter("inner"), true);
        var center = parameters["center"]?.GetValue<string>() ?? (shape.Anchored ? "source" : "focus");
        return new ForecastRegion(shape, size, EffectHelpers.Center(context, center), EffectHelpers.Facing(context));
    }
}
