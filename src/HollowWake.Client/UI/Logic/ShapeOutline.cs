namespace HollowWake.Client.UI.Logic;

/// <summary>A shape's full proportions: radius, full angle in radians, full width, and the distance from the anchor left empty.</summary>
public readonly record struct ShapeSpec(string Name, float Radius, float Angle, float Width, float Inner);

/// <summary>A filled region in a shape's own frame: its outer loop and any holes, each counter-clockwise.</summary>
public sealed record ShapeRegion(IReadOnlyList<(float X, float Y)> Outer, IReadOnlyList<IReadOnlyList<(float X, float Y)>> Holes);

/// <summary>
/// The outlines of the delivery shapes the server names (docs/design/deliveries.md), for drawing projectiles,
/// areas, fields and telegraph marks by shape: circle, square, triangle, sector (cone), crescent, line, band, bar,
/// arc and wave. Outlines are in the shape's own frame on the ground: x along its facing, y to its left, the
/// anchor at the origin. Centered shapes sit on the anchor; anchored ones (sector, crescent, line, band) extend
/// from it. Sizes are as the wire sends them (angle in radians, inner as a distance); a size the wire leaves out
/// takes the shape's default, as Effects/Shapes.cs gives it. The browser client's world/shapes.ts.
/// </summary>
public static class ShapeOutline
{
    private const float Deg = MathF.PI / 180f;

    /// <summary>Shapes the clients draw; any other name falls back to a circle.</summary>
    public static readonly IReadOnlyList<string> Names = new[] { "circle", "square", "triangle", "sector", "cone", "crescent", "line", "band", "bar", "arc", "wave" };

    /// <summary>A shape's specification from what the wire sent, with the shape's defaults for what it left out.</summary>
    public static ShapeSpec SpecOf(string name, float? radius, float? angle, float? width, float? inner, float fallbackRadius)
    {
        var known = Names.Contains(name) ? name : "circle";
        var r = MathF.Max(0.05f, radius ?? fallbackRadius);
        var thin = MathF.Max(0.3f, r * 0.25f);
        var (da, dw, di) = known switch
        {
            "sector" or "cone" => (90 * Deg, 0f, 0f),
            "crescent" => (180 * Deg, 0f, r * 0.5f),
            "line" => (0f, thin, 0f),
            "band" => (0f, r, 0f),
            "bar" => (0f, thin, 0f),
            "arc" => (90 * Deg, thin, 0f),
            "wave" => (120 * Deg, thin, 0f),
            _ => (0f, 0f, 0f),
        };
        return new ShapeSpec(
            known,
            r,
            Math.Clamp(angle ?? da, 0.01f, MathF.PI * 2),
            MathF.Max(0.02f, width ?? dw),
            Math.Clamp(inner ?? di, 0, r * 0.98f));
    }

    private static List<(float X, float Y)> Circle(float r, int segments, float cx = 0)
    {
        var pts = new List<(float, float)>(segments);
        for (var i = 0; i < segments; i++)
        {
            var a = i * MathF.PI * 2 / segments;
            pts.Add((cx + MathF.Cos(a) * r, MathF.Sin(a) * r));
        }
        return pts;
    }

    /// <summary>A ring sector between two radii and two angles about a center, counter-clockwise.</summary>
    private static List<(float X, float Y)> Annulus(float cx, float r0, float r1, float a0, float a1, int segments)
    {
        var pts = new List<(float, float)>((segments + 1) * 2);
        for (var i = 0; i <= segments; i++)
        {
            var a = a0 + (a1 - a0) * i / segments;
            pts.Add((cx + MathF.Cos(a) * r1, MathF.Sin(a) * r1));
        }
        for (var i = segments; i >= 0; i--)
        {
            var a = a0 + (a1 - a0) * i / segments;
            pts.Add((cx + MathF.Cos(a) * r0, MathF.Sin(a) * r0));
        }
        return pts;
    }

    /// <summary>The region a shape covers, in its own frame (x along the facing, y to the left).</summary>
    public static ShapeRegion RegionOf(ShapeSpec spec, int segments = 32)
    {
        var r = spec.Radius;
        var none = Array.Empty<IReadOnlyList<(float, float)>>();
        switch (spec.Name)
        {
            case "square":
            {
                var outer = new List<(float, float)> { (-r, -r), (r, -r), (r, r), (-r, r) };
                var i = spec.Inner;
                return i > 0 ? new ShapeRegion(outer, new[] { new List<(float, float)> { (-i, -i), (-i, i), (i, i), (i, -i) } }) : new ShapeRegion(outer, none);
            }
            case "triangle":
                // Centered, its apex along the facing, inscribed in a circle of the radius.
                return new ShapeRegion(new List<(float, float)> { (r, 0), (r * MathF.Cos(120 * Deg), r * MathF.Sin(120 * Deg)), (r * MathF.Cos(240 * Deg), r * MathF.Sin(240 * Deg)) }, none);
            case "sector":
            case "cone":
            case "crescent":
            {
                var half = spec.Angle / 2;
                if (spec.Inner > 0)
                {
                    return new ShapeRegion(Annulus(0, spec.Inner, r, -half, half, segments), none);
                }
                var pts = new List<(float, float)> { (0, 0) };
                for (var i = 0; i <= segments; i++)
                {
                    var a = -half + spec.Angle * i / segments;
                    pts.Add((MathF.Cos(a) * r, MathF.Sin(a) * r));
                }
                return new ShapeRegion(pts, none);
            }
            case "line":
            case "band":
            {
                var w = spec.Width / 2;
                return new ShapeRegion(new List<(float, float)> { (spec.Inner, -w), (r, -w), (r, w), (spec.Inner, w) }, none);
            }
            case "bar":
            {
                // Centered and laid across the facing: the radius to each side, the width thick.
                var w = spec.Width / 2;
                return new ShapeRegion(new List<(float, float)> { (-w, -r), (w, -r), (w, r), (-w, r) }, none);
            }
            case "arc":
            case "wave":
            {
                // A blade bowed forward: part of a circle whose center lies behind the anchor by curvature times the radius.
                var bend = r * (spec.Name == "wave" ? 2 : 1);
                var half = spec.Angle / 2;
                return new ShapeRegion(Annulus(-bend, MathF.Max(0.01f, bend - spec.Width / 2), bend + spec.Width / 2, -half, half, segments), none);
            }
            default:
            {
                var outer = Circle(r, segments * 2);
                if (spec.Inner <= 0)
                {
                    return new ShapeRegion(outer, none);
                }
                var hole = Circle(spec.Inner, segments * 2);
                hole.Reverse();
                return new ShapeRegion(outer, new[] { hole });
            }
        }
    }

    /// <summary>The farthest any point of a region lies from the anchor.</summary>
    public static float Reach(ShapeRegion region) => region.Outer.Max(p => MathF.Sqrt(p.X * p.X + p.Y * p.Y));

    /// <summary>Whether a point in the shape's frame lies inside the region: inside its outer loop and outside its holes.</summary>
    public static bool Inside(ShapeRegion region, float x, float y)
    {
        static bool Within(IReadOnlyList<(float X, float Y)> loop, float x, float y)
        {
            var hit = false;
            for (int i = 0, j = loop.Count - 1; i < loop.Count; j = i++)
            {
                var (xi, yi) = loop[i];
                var (xj, yj) = loop[j];
                if (yi > y != yj > y && x < (xj - xi) * (y - yi) / (yj - yi) + xi)
                {
                    hit = !hit;
                }
            }
            return hit;
        }
        return Within(region.Outer, x, y) && !region.Holes.Any(h => Within(h, x, y));
    }
}
