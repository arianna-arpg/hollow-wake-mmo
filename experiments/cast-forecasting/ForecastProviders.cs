using System.Numerics;
using HollowWake.Client.UI.Logic;
using HollowWake.Core.Effects;

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

// Thin diagnostic adapter over the production capability. Geometry and purity are owned by core.
public static class ForecastProviders
{
    public static ForecastRegion? Describe(EffectContext context)
    {
        if (context.Weave is null) return null;
        var forecast = DeliveryForecasts.Describe(context.Weave.Effects, context);
        return forecast.PrimaryRegion is { } plan
            ? new ForecastRegion(plan.Shape, plan.Size, plan.Anchor, plan.Direction)
            : null;
    }
}
