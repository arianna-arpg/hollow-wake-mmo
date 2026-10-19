using Godot;
using HollowWake.Client.UI.Logic;

namespace HollowWake.Client.World;

/// <summary>
/// A delivery shape's region as a mesh on the ground: the shape's x along its facing becomes Godot's x, its y to
/// the left becomes Godot's -z, so turning the body by the shape's facing angle about Y lays it the right way.
/// A thickness extrudes it upward into a slab (a projectile); without one it is a flat mark (an area, a field, a
/// telegraph). Meant for materials that draw both sides.
/// </summary>
public static class ShapeMesh
{
    public static ArrayMesh Build(ShapeRegion region, float thickness)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        st.SetNormal(Vector3.Up);
        var top = thickness;
        foreach (var (a, b, c) in Triangles(region))
        {
            st.AddVertex(new Vector3(a.X, top, -a.Y));
            st.AddVertex(new Vector3(b.X, top, -b.Y));
            st.AddVertex(new Vector3(c.X, top, -c.Y));
            if (thickness > 0)
            {
                st.AddVertex(new Vector3(c.X, 0, -c.Y));
                st.AddVertex(new Vector3(b.X, 0, -b.Y));
                st.AddVertex(new Vector3(a.X, 0, -a.Y));
            }
        }
        if (thickness > 0)
        {
            foreach (var loop in region.Holes.Prepend(region.Outer))
            {
                for (var i = 0; i < loop.Count; i++)
                {
                    var p = loop[i];
                    var q = loop[(i + 1) % loop.Count];
                    var p0 = new Vector3(p.X, 0, -p.Y);
                    var q0 = new Vector3(q.X, 0, -q.Y);
                    var p1 = new Vector3(p.X, top, -p.Y);
                    var q1 = new Vector3(q.X, top, -q.Y);
                    st.AddVertex(p0);
                    st.AddVertex(q0);
                    st.AddVertex(q1);
                    st.AddVertex(p0);
                    st.AddVertex(q1);
                    st.AddVertex(p1);
                }
            }
        }
        st.GenerateNormals();
        return st.Commit();
    }

    /// <summary>
    /// The region's triangles: an outline without holes is triangulated as a polygon; a ring (an outline with one
    /// hole of as many points, as rings and hollow squares are) is stitched as a strip between the two loops.
    /// </summary>
    private static IEnumerable<((float X, float Y) A, (float X, float Y) B, (float X, float Y) C)> Triangles(ShapeRegion region)
    {
        var outer = region.Outer;
        if (region.Holes.Count == 1 && region.Holes[0].Count == outer.Count)
        {
            // The hole runs the other way round; walk it in the outer loop's direction.
            var hole = region.Holes[0].Reverse().ToList();
            // Match the hole's start to the outer loop's by the nearest angle.
            var start = 0;
            var best = float.MaxValue;
            // Bowed full-turn regions center behind the anchor. Match ring vertices about the
            // actual loop center, otherwise an offset hole stitches to unrelated outer angles.
            var centerX = outer.Average(p => p.X);
            var centerY = outer.Average(p => p.Y);
            var a0 = MathF.Atan2(outer[0].Y - centerY, outer[0].X - centerX);
            for (var i = 0; i < hole.Count; i++)
            {
                var d = MathF.Abs(Mathf.AngleDifference(a0, MathF.Atan2(hole[i].Y - centerY, hole[i].X - centerX)));
                if (d < best)
                {
                    best = d;
                    start = i;
                }
            }
            for (var i = 0; i < outer.Count; i++)
            {
                var o0 = outer[i];
                var o1 = outer[(i + 1) % outer.Count];
                var h0 = hole[(start + i) % hole.Count];
                var h1 = hole[(start + i + 1) % hole.Count];
                yield return (o0, o1, h1);
                yield return (o0, h1, h0);
            }
            yield break;
        }
        var points = outer.Select(p => new Vector2(p.X, p.Y)).ToArray();
        var indices = Geometry2D.TriangulatePolygon(points);
        for (var i = 0; i + 2 < indices.Length; i += 3)
        {
            yield return (outer[indices[i]], outer[indices[i + 1]], outer[indices[i + 2]]);
        }
    }
}
