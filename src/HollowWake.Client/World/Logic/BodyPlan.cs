using HollowWake.Client.UI.Logic;

namespace HollowWake.Client.World.Logic;

/// <summary>A point or size in body space: x forward, y up, z to the body's right. Metres, where a cell is one.</summary>
public readonly record struct Vec3(float X, float Y, float Z)
{
    public static readonly Vec3 Zero = new(0, 0, 0);
    public static readonly Vec3 One = new(1, 1, 1);

    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator *(Vec3 a, float s) => new(a.X * s, a.Y * s, a.Z * s);
    public static Vec3 operator *(Vec3 a, Vec3 b) => new(a.X * b.X, a.Y * b.Y, a.Z * b.Z);

    public float Length => MathF.Sqrt(X * X + Y * Y + Z * Z);
}

public enum PartShape
{
    /// <summary>A box of the given size.</summary>
    Box,
    /// <summary>An ellipsoid filling the size.</summary>
    Ellipsoid,
    /// <summary>A cone whose base is the x-z extent and whose apex is at the top of the size.</summary>
    Cone,
    /// <summary>A cylinder along y; Taper scales its top radius.</summary>
    Cylinder,
    /// <summary>A box whose top edge is pinched toward the middle along z, for blades, roofs and fins.</summary>
    Wedge,
}

/// <summary>
/// One painted part of a body: a primitive on a bone, with its size, its centre, its turn (euler degrees:
/// roll about x, pitch about z, yaw about y, applied in that order) and its colour. Glow parts are drawn
/// unlit so they read as light.
/// </summary>
public sealed record BodyPart(string Bone, PartShape Shape, Vec3 Size, Vec3 At, Vec3 Turn, Rgba Color)
{
    /// <summary>For cylinders: the top radius as a fraction of the bottom one.</summary>
    public float Taper { get; init; } = 1;
    public bool Glow { get; init; }
    /// <summary>Facets around a round part; fewer read as hewn.</summary>
    public int Segments { get; init; } = 8;
}

/// <summary>A bone of the rig: a pivot in body space, a parent, and the rest turn the animator adds its own to.</summary>
public sealed record BodyBone(string Name, string? Parent, Vec3 Pivot)
{
    public Vec3 Rest { get; init; } = Vec3.Zero;
}

/// <summary>A place gear attaches: its bone, its point and its frame's turn in body space, and a size to scale the piece by.</summary>
public sealed record BodySocket(string Bone, Vec3 At, Vec3 Turn, float Size);

/// <summary>
/// A built body: the rig, the parts and the sockets, sized to the entity. Families build these from an
/// appearance entry; the renderers merge the parts of each bone into one mesh and move the bones.
/// </summary>
public sealed class BodyPlan
{
    public string Family { get; init; } = "";
    public string Rig { get; set; } = "static";
    public List<BodyBone> Bones { get; } = new();
    public List<BodyPart> Parts { get; } = new();
    public Dictionary<string, BodySocket> Sockets { get; } = new(StringComparer.Ordinal);
    /// <summary>The body's drawn height and width, for cards, labels and picking.</summary>
    public float Height { get; set; }
    public float Width { get; set; }
    /// <summary>How far above the ground the body rests (wisps hover).</summary>
    public float Hover { get; set; }
    /// <summary>A light the body emits, or null.</summary>
    public Rgba? Glow { get; set; }
    /// <summary>Whether the body holds a weapon that swings (else it gathers and casts).</summary>
    public bool Swings { get; set; }

    public BodyBone Bone(string name, string? parent, Vec3 pivot, Vec3? rest = null)
    {
        var bone = new BodyBone(name, parent, pivot) { Rest = rest ?? Vec3.Zero };
        Bones.Add(bone);
        return bone;
    }

    public bool HasBone(string name) => Bones.Any(b => b.Name == name);

    public void Add(string bone, PartShape shape, Vec3 size, Vec3 at, Rgba color, Vec3? turn = null, float taper = 1, bool glow = false, int segments = 8)
    {
        if (!HasBone(bone))
        {
            bone = "root";
        }
        Parts.Add(new BodyPart(bone, shape, size, at, turn ?? Vec3.Zero, color) { Taper = taper, Glow = glow, Segments = segments });
    }

    /// <summary>The bounds of every part's centre and size, ignoring turns: enough for cards and picking.</summary>
    public (Vec3 Min, Vec3 Max) Bounds()
    {
        if (Parts.Count == 0)
        {
            return (Vec3.Zero, Vec3.Zero);
        }
        var min = new Vec3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vec3(float.MinValue, float.MinValue, float.MinValue);
        foreach (var p in Parts)
        {
            // A turned part reaches at most the length of its half diagonal from its centre.
            var reach = p.Turn == Vec3.Zero ? p.Size * 0.5f : Vec3.One * (p.Size.Length * 0.5f);
            min = new Vec3(MathF.Min(min.X, p.At.X - reach.X), MathF.Min(min.Y, p.At.Y - reach.Y), MathF.Min(min.Z, p.At.Z - reach.Z));
            max = new Vec3(MathF.Max(max.X, p.At.X + reach.X), MathF.Max(max.Y, p.At.Y + reach.Y), MathF.Max(max.Z, p.At.Z + reach.Z));
        }
        return (min, max);
    }
}

/// <summary>
/// Triangles for a part, flat shaded so a body reads as painted facets: positions and normals in body space,
/// with the part's turn and centre applied. The web client builds the same triangles in the same order.
/// </summary>
public static class PartGeometry
{
    /// <summary>The part's triangles as positions and normals, three floats each, three vertices per triangle.</summary>
    public static (float[] Positions, float[] Normals) Build(BodyPart part)
    {
        var tris = new List<Vec3>();
        switch (part.Shape)
        {
            case PartShape.Box:
                BoxTris(tris, part.Size, 0);
                break;
            case PartShape.Wedge:
                BoxTris(tris, part.Size, 0.85f);
                break;
            case PartShape.Ellipsoid:
                EllipsoidTris(tris, part.Size, part.Segments);
                break;
            case PartShape.Cone:
                ConeTris(tris, part.Size, part.Segments);
                break;
            case PartShape.Cylinder:
                CylinderTris(tris, part.Size, part.Taper, part.Segments);
                break;
        }
        var (rx, ry, rz) = (Deg(part.Turn.X), Deg(part.Turn.Y), Deg(part.Turn.Z));
        var positions = new float[tris.Count * 3];
        var normals = new float[tris.Count * 3];
        for (var i = 0; i < tris.Count; i += 3)
        {
            var a = Turn(tris[i], rx, ry, rz);
            var b = Turn(tris[i + 1], rx, ry, rz);
            var c = Turn(tris[i + 2], rx, ry, rz);
            var n = Normal(a, b, c);
            Put(positions, normals, i, a + part.At, n);
            Put(positions, normals, i + 1, b + part.At, n);
            Put(positions, normals, i + 2, c + part.At, n);
        }
        return (positions, normals);
    }

    private static float Deg(float degrees) => degrees * MathF.PI / 180f;

    private static void Put(float[] p, float[] n, int i, Vec3 v, Vec3 normal)
    {
        p[i * 3] = v.X;
        p[i * 3 + 1] = v.Y;
        p[i * 3 + 2] = v.Z;
        n[i * 3] = normal.X;
        n[i * 3 + 1] = normal.Y;
        n[i * 3 + 2] = normal.Z;
    }

    /// <summary>Roll about x, then pitch about z, then yaw about y.</summary>
    public static Vec3 Turn(Vec3 v, float roll, float pitch, float yaw)
    {
        if (roll != 0)
        {
            var (s, c) = (MathF.Sin(roll), MathF.Cos(roll));
            v = new Vec3(v.X, v.Y * c - v.Z * s, v.Y * s + v.Z * c);
        }
        if (pitch != 0)
        {
            var (s, c) = (MathF.Sin(pitch), MathF.Cos(pitch));
            v = new Vec3(v.X * c - v.Y * s, v.X * s + v.Y * c, v.Z);
        }
        if (yaw != 0)
        {
            var (s, c) = (MathF.Sin(yaw), MathF.Cos(yaw));
            v = new Vec3(v.X * c + v.Z * s, v.Y, -v.X * s + v.Z * c);
        }
        return v;
    }

    private static Vec3 Normal(Vec3 a, Vec3 b, Vec3 c)
    {
        var u = b - a;
        var v = c - a;
        var n = new Vec3(u.Y * v.Z - u.Z * v.Y, u.Z * v.X - u.X * v.Z, u.X * v.Y - u.Y * v.X);
        var len = n.Length;
        return len < 1e-8f ? new Vec3(0, 1, 0) : n * (1f / len);
    }

    private static void Quad(List<Vec3> tris, Vec3 a, Vec3 b, Vec3 c, Vec3 d)
    {
        // a, b, c, d around the face, counter-clockwise seen from outside.
        tris.Add(a); tris.Add(b); tris.Add(c);
        tris.Add(a); tris.Add(c); tris.Add(d);
    }

    /// <summary>A box; pinch narrows the top face along z (a wedge when near one).</summary>
    private static void BoxTris(List<Vec3> tris, Vec3 size, float pinch)
    {
        var hx = size.X / 2;
        var hy = size.Y / 2;
        var hz = size.Z / 2;
        var tz = hz * (1 - pinch);
        // bottom corners
        var b0 = new Vec3(-hx, -hy, -hz);
        var b1 = new Vec3(hx, -hy, -hz);
        var b2 = new Vec3(hx, -hy, hz);
        var b3 = new Vec3(-hx, -hy, hz);
        // top corners
        var t0 = new Vec3(-hx, hy, -tz);
        var t1 = new Vec3(hx, hy, -tz);
        var t2 = new Vec3(hx, hy, tz);
        var t3 = new Vec3(-hx, hy, tz);
        Quad(tris, b0, b3, b2, b1); // bottom (facing -y)
        Quad(tris, t0, t1, t2, t3); // top
        Quad(tris, b1, b2, t2, t1); // +x front
        Quad(tris, b3, b0, t0, t3); // -x back
        Quad(tris, b2, b3, t3, t2); // +z right
        Quad(tris, b0, b1, t1, t0); // -z left
    }

    private static void EllipsoidTris(List<Vec3> tris, Vec3 size, int segments)
    {
        var rings = Math.Max(3, segments / 2);
        var rx = size.X / 2;
        var ry = size.Y / 2;
        var rz = size.Z / 2;
        Vec3 P(int ring, int seg)
        {
            var phi = MathF.PI * ring / rings;
            var theta = 2 * MathF.PI * seg / segments;
            return new Vec3(rx * MathF.Sin(phi) * MathF.Cos(theta), ry * MathF.Cos(phi), rz * MathF.Sin(phi) * MathF.Sin(theta));
        }
        for (var r = 0; r < rings; r++)
        {
            for (var s = 0; s < segments; s++)
            {
                var a = P(r, s);
                var b = P(r + 1, s);
                var c = P(r + 1, s + 1);
                var d = P(r, s + 1);
                if (r == 0)
                {
                    tris.Add(a); tris.Add(c); tris.Add(b);
                }
                else if (r == rings - 1)
                {
                    tris.Add(a); tris.Add(d); tris.Add(b);
                }
                else
                {
                    tris.Add(a); tris.Add(d); tris.Add(c);
                    tris.Add(a); tris.Add(c); tris.Add(b);
                }
            }
        }
    }

    private static void ConeTris(List<Vec3> tris, Vec3 size, int segments)
    {
        var rx = size.X / 2;
        var rz = size.Z / 2;
        var hy = size.Y / 2;
        var apex = new Vec3(0, hy, 0);
        var centre = new Vec3(0, -hy, 0);
        for (var s = 0; s < segments; s++)
        {
            var t0 = 2 * MathF.PI * s / segments;
            var t1 = 2 * MathF.PI * (s + 1) / segments;
            var a = new Vec3(rx * MathF.Cos(t0), -hy, rz * MathF.Sin(t0));
            var b = new Vec3(rx * MathF.Cos(t1), -hy, rz * MathF.Sin(t1));
            tris.Add(a); tris.Add(apex); tris.Add(b);
            tris.Add(a); tris.Add(b); tris.Add(centre);
        }
    }

    private static void CylinderTris(List<Vec3> tris, Vec3 size, float taper, int segments)
    {
        var rx = size.X / 2;
        var rz = size.Z / 2;
        var hy = size.Y / 2;
        var top = new Vec3(0, hy, 0);
        var bottom = new Vec3(0, -hy, 0);
        for (var s = 0; s < segments; s++)
        {
            var t0 = 2 * MathF.PI * s / segments;
            var t1 = 2 * MathF.PI * (s + 1) / segments;
            var a = new Vec3(rx * MathF.Cos(t0), -hy, rz * MathF.Sin(t0));
            var b = new Vec3(rx * MathF.Cos(t1), -hy, rz * MathF.Sin(t1));
            var c = new Vec3(rx * taper * MathF.Cos(t1), hy, rz * taper * MathF.Sin(t1));
            var d = new Vec3(rx * taper * MathF.Cos(t0), hy, rz * taper * MathF.Sin(t0));
            tris.Add(a); tris.Add(d); tris.Add(c);
            tris.Add(a); tris.Add(c); tris.Add(b);
            tris.Add(d); tris.Add(top); tris.Add(c);
            tris.Add(a); tris.Add(b); tris.Add(bottom);
        }
    }
}
