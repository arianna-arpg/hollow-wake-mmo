using System.Collections.Concurrent;
using System.Numerics;

namespace HollowWake.Core.Effects;

/// <summary>
/// The proportions a shape is drawn at, in world units and degrees. <see cref="Radius"/> is the shape's
/// main extent (a circle's radius, a sector's reach, a line's length, a square's half side, a bar's half
/// length); <see cref="Angle"/> is the full angle of sectors, crescents, arcs and waves; <see cref="Width"/>
/// is the full width of lines, bands and bars and the thickness of arcs and waves; <see cref="Inner"/> is
/// the fraction of the radius left empty at the anchor (edge-only rings, far-edge cones).
/// </summary>
public readonly record struct ShapeSize(float Radius, float Angle, float Width, float Inner);

/// <summary>
/// A hit shape: where a delivery collides, tested in the delivery's own frame. Centered shapes sit on
/// their anchor (a projectile's position, an area's center); anchored shapes extend from it along the
/// facing (a cone from the caster, a line from the hand). Projectiles, areas and fields share the
/// registry, and the client draws a delivery by the same name.
/// </summary>
public abstract class Shape
{
    public abstract string Name { get; }

    /// <summary>True when the shape extends from its anchor along the facing instead of sitting centered on it.</summary>
    public virtual bool Anchored => false;

    /// <summary>True when <see cref="ShapeSize.Angle"/> shapes it (sectors, crescents, arcs, waves), so the weave's <c>area.angle</c> scales it.</summary>
    public virtual bool HasAngle => false;

    /// <summary>True when <see cref="ShapeSize.Width"/> shapes it (lines, bands, bars, arcs, waves).</summary>
    public virtual bool HasWidth => false;

    /// <summary>False when the shape is the same whichever way it faces (a circle), so its facing need not be worked out.</summary>
    public virtual bool Turns => true;

    /// <summary>The full angle in degrees the shape takes when content names none.</summary>
    public virtual float DefaultAngle => 90f;

    /// <summary>The fraction of the radius left empty when content names none.</summary>
    public virtual float DefaultInner => 0f;

    /// <summary>The width the shape takes when content names none: lines take the weave's area width.</summary>
    public virtual float DefaultWidth(float radius, float areaWidth) => areaWidth;

    /// <summary>The radius around the anchor a query must search to find everything the shape can hold.</summary>
    public abstract float Reach(in ShapeSize size);

    /// <summary>
    /// Whether a body touches the shape. <paramref name="to"/> runs from the anchor to the body on the
    /// ground plane, <paramref name="facing"/> is the shape's unit facing, and <paramref name="pad"/> is
    /// the body's radius (zero tests the body's center only).
    /// </summary>
    public abstract bool Contains(in ShapeSize size, Vector2 to, Vector2 facing, float pad);

    /// <summary>Distance along the facing.</summary>
    protected static float Along(Vector2 to, Vector2 facing) => Vector2.Dot(to, facing);

    /// <summary>Signed distance across the facing, positive to the left.</summary>
    protected static float Across(Vector2 to, Vector2 facing) => facing.X * to.Y - facing.Y * to.X;

    public override string ToString() => Name;
}

/// <summary>
/// The open registry of hit shapes by name. Built in: <c>circle</c>, <c>square</c>, <c>triangle</c>,
/// <c>sector</c> (alias <c>cone</c>), <c>crescent</c>, <c>line</c>, <c>band</c>, <c>bar</c>, <c>arc</c> and
/// <c>wave</c>. Mods register more before content that names them loads.
/// </summary>
public static class Shapes
{
    private static readonly ConcurrentDictionary<string, Shape> Registry = new(StringComparer.Ordinal);

    static Shapes()
    {
        Register(new CircleShape());
        Register(new SquareShape());
        Register(new TriangleShape());
        var sector = new SectorShape("sector");
        Register(sector);
        Register(new SectorShape("cone"));
        Register(new CrescentShape());
        Register(new LineShape("line"));
        Register(new BandShape());
        Register(new BarShape());
        Register(new ArcShape("arc", 1f, 90f));
        Register(new ArcShape("wave", 2f, 120f));
    }

    /// <summary>The circle, the shape of every delivery that names none.</summary>
    public static Shape Circle => Registry["circle"];

    public static void Register(Shape shape) => Registry[shape.Name] = shape;

    public static bool TryGet(string name, out Shape shape) => Registry.TryGetValue(name, out shape!);

    public static bool Has(string name) => Registry.ContainsKey(name);

    public static Shape Get(string name) =>
        TryGet(name, out var shape) ? shape : throw new InvalidOperationException($"Unknown shape '{name}'; known shapes are {string.Join(", ", Names)}.");

    public static IEnumerable<string> Names => Registry.Keys.OrderBy(k => k, StringComparer.Ordinal);

    // ------------------------------------------------------------ the built-in shapes

    private sealed class CircleShape : Shape
    {
        public override string Name => "circle";

        public override bool Turns => false;

        public override float Reach(in ShapeSize size) => size.Radius;

        public override bool Contains(in ShapeSize size, Vector2 to, Vector2 facing, float pad)
        {
            var d2 = to.X * to.X + to.Y * to.Y;
            var outer = size.Radius + pad;
            if (d2 > outer * outer)
            {
                return false;
            }
            var inner = size.Inner * size.Radius - pad;
            return inner <= 0 || d2 >= inner * inner;
        }
    }

    /// <summary>Centered, turned with the facing; <see cref="ShapeSize.Radius"/> is half its side.</summary>
    private sealed class SquareShape : Shape
    {
        public override string Name => "square";

        public override float Reach(in ShapeSize size) => size.Radius * 1.4142136f;

        public override bool Contains(in ShapeSize size, Vector2 to, Vector2 facing, float pad)
        {
            var x = MathF.Abs(Along(to, facing));
            var y = MathF.Abs(Across(to, facing));
            var outer = size.Radius + pad;
            if (x > outer || y > outer)
            {
                return false;
            }
            var inner = size.Inner * size.Radius - pad;
            return inner <= 0 || MathF.Max(x, y) >= inner;
        }
    }

    /// <summary>Centered, its apex along the facing, inscribed in a circle of <see cref="ShapeSize.Radius"/>.</summary>
    private sealed class TriangleShape : Shape
    {
        private const float HalfRoot3 = 0.8660254f;

        public override string Name => "triangle";

        public override float Reach(in ShapeSize size) => size.Radius;

        public override bool Contains(in ShapeSize size, Vector2 to, Vector2 facing, float pad)
        {
            var x = Along(to, facing);
            var y = Across(to, facing);
            // The three edges lie half the radius from the center, their outward normals at 180, 60 and -60 degrees.
            var limit = size.Radius * 0.5f + pad;
            return -x <= limit && 0.5f * x + HalfRoot3 * y <= limit && 0.5f * x - HalfRoot3 * y <= limit;
        }
    }

    /// <summary>
    /// From the anchor along the facing, <see cref="ShapeSize.Angle"/> wide, out to <see cref="ShapeSize.Radius"/>.
    /// A body at the anchor itself is inside.
    /// </summary>
    private class SectorShape : Shape
    {
        public SectorShape(string name)
        {
            Name = name;
        }

        public override string Name { get; }

        public override bool Anchored => true;

        public override bool HasAngle => true;

        public override float Reach(in ShapeSize size) => size.Radius;

        public override bool Contains(in ShapeSize size, Vector2 to, Vector2 facing, float pad)
        {
            var d2 = to.LengthSquared();
            var outer = size.Radius + pad;
            if (d2 > outer * outer)
            {
                return false;
            }
            var inner = size.Inner * size.Radius - pad;
            if (inner > 0 && d2 < inner * inner)
            {
                return false;
            }
            if (d2 < 0.01f)
            {
                return true;
            }
            var half = MathF.PI * size.Angle / 360f;
            var angle = MathF.Acos(Math.Clamp(Vector2.Dot(Vector2.Normalize(to), facing), -1f, 1f));
            if (angle <= half)
            {
                return true;
            }
            // A body beside the edge still touches it when its radius reaches across.
            var excess = angle - half;
            return pad > 0 && excess < MathF.PI / 2 && MathF.Sqrt(d2) * MathF.Sin(excess) <= pad;
        }
    }

    /// <summary>A sector's far band: half a turn wide and hollow to half its reach unless told otherwise.</summary>
    private sealed class CrescentShape : SectorShape
    {
        public CrescentShape() : base("crescent")
        {
        }

        public override float DefaultAngle => 180f;

        public override float DefaultInner => 0.5f;
    }

    /// <summary>From the anchor along the facing for <see cref="ShapeSize.Radius"/>, <see cref="ShapeSize.Width"/> across.</summary>
    private class LineShape : Shape
    {
        public LineShape(string name)
        {
            Name = name;
        }

        public override string Name { get; }

        public override bool Anchored => true;

        public override bool HasWidth => true;

        public override float Reach(in ShapeSize size)
        {
            var half = size.Width * 0.5f;
            return MathF.Sqrt(size.Radius * size.Radius + half * half);
        }

        public override bool Contains(in ShapeSize size, Vector2 to, Vector2 facing, float pad)
        {
            var along = Along(to, facing);
            if (along < size.Inner * size.Radius - pad || along > size.Radius + pad)
            {
                return false;
            }
            return MathF.Abs(Across(to, facing)) <= size.Width * 0.5f + pad;
        }
    }

    /// <summary>A line as wide as it is long unless told otherwise: a sweep in front of the anchor.</summary>
    private sealed class BandShape : LineShape
    {
        public BandShape() : base("band")
        {
        }

        public override float DefaultWidth(float radius, float areaWidth) => radius;
    }

    /// <summary>Centered and laid across the facing: <see cref="ShapeSize.Radius"/> to each side, <see cref="ShapeSize.Width"/> thick.</summary>
    private sealed class BarShape : Shape
    {
        public override string Name => "bar";

        public override bool HasWidth => true;

        public override float DefaultWidth(float radius, float areaWidth) => areaWidth * 0.25f;

        public override float Reach(in ShapeSize size)
        {
            var half = size.Width * 0.5f;
            return MathF.Sqrt(size.Radius * size.Radius + half * half);
        }

        public override bool Contains(in ShapeSize size, Vector2 to, Vector2 facing, float pad) =>
            MathF.Abs(Along(to, facing)) <= size.Width * 0.5f + pad && MathF.Abs(Across(to, facing)) <= size.Radius + pad;
    }

    /// <summary>
    /// A blade bowed forward, centered on the anchor: part of a circle whose center lies behind the anchor
    /// by <c>curvature</c> times the radius, <see cref="ShapeSize.Angle"/> wide as seen from that center and
    /// <see cref="ShapeSize.Width"/> thick. An arc bends tightly; a wave is flatter and wider.
    /// </summary>
    private sealed class ArcShape : Shape
    {
        private readonly float _curvature;
        private readonly float _angle;

        public ArcShape(string name, float curvature, float angle)
        {
            Name = name;
            _curvature = curvature;
            _angle = angle;
        }

        public override string Name { get; }

        public override bool HasAngle => true;

        public override bool HasWidth => true;

        public override float DefaultAngle => _angle;

        public override float DefaultWidth(float radius, float areaWidth) => areaWidth * 0.25f;

        public override float Reach(in ShapeSize size)
        {
            var bend = size.Radius * _curvature;
            var quarter = MathF.PI * Math.Min(size.Angle, 360f) / 720f;
            return 2 * bend * MathF.Sin(quarter) + size.Width * 0.5f;
        }

        public override bool Contains(in ShapeSize size, Vector2 to, Vector2 facing, float pad)
        {
            var bend = size.Radius * _curvature;
            var x = Along(to, facing) + bend;
            var y = Across(to, facing);
            var d = MathF.Sqrt(x * x + y * y);
            if (MathF.Abs(d - bend) > size.Width * 0.5f + pad)
            {
                return false;
            }
            var angle = MathF.Atan2(MathF.Abs(y), x);
            var excess = angle - MathF.PI * size.Angle / 360f;
            return excess <= 0 || excess * bend <= pad;
        }
    }
}
