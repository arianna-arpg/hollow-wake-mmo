using Godot;
using HollowWake.Client.UI.Logic;
using HollowWake.Client.World.Logic;
using HollowWake.Core.Definitions;
using HollowWake.Core.World;

namespace HollowWake.Client.World;

/// <summary>
/// Decorative tufts scattered over a chunk by its cells' surface tags, as the world content's foliage rules
/// say, seeded from the cell so both clients and every visit agree. One MultiMesh per chunk with a shared
/// two-colour tuft mesh; the density option scales the count. Nothing here exists in the simulation.
/// </summary>
public static class Foliage
{
    private static ArrayMesh? _tuft;
    private static StandardMaterial3D? _material;

    private static StandardMaterial3D Material => _material ??= new StandardMaterial3D
    {
        VertexColorUseAsAlbedo = true,
        VertexColorIsSrgb = true,
        Roughness = 1f,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
    };

    /// <summary>A tuft: three crossed blades, white so the instance colour paints it, darker at the base through vertex alpha mix.</summary>
    private static ArrayMesh Tuft => _tuft ??= BuildTuft();

    private static ArrayMesh BuildTuft()
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var colors = new List<Color>();
        var indices = new List<int>();
        for (var i = 0; i < 3; i++)
        {
            var a = Mathf.Pi * i / 3f;
            var dx = MathF.Cos(a) * 0.12f;
            var dz = MathF.Sin(a) * 0.12f;
            var start = vertices.Count;
            vertices.Add(new Vector3(-dx, 0, -dz));
            vertices.Add(new Vector3(dx, 0, dz));
            vertices.Add(new Vector3(dx * 0.4f + dz * 0.3f, 1f, dz * 0.4f - dx * 0.3f));
            vertices.Add(new Vector3(-dx * 0.4f + dz * 0.3f, 1f, -dz * 0.4f - dx * 0.3f));
            var normal = new Vector3(-dz, 0.3f, dx).Normalized();
            for (var k = 0; k < 4; k++)
            {
                normals.Add(normal);
                // the base is the tuft's own colour, the tip is lightened by the instance's custom data through the shader-less mix below
                colors.Add(k < 2 ? new Color(0.72f, 0.72f, 0.72f) : Colors.White);
            }
            indices.Add(start); indices.Add(start + 2); indices.Add(start + 1);
            indices.Add(start); indices.Add(start + 3); indices.Add(start + 2);
        }
        var mesh = new ArrayMesh();
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, Material);
        return mesh;
    }

    /// <summary>The tufts of a chunk at a density, or null when the chunk grows none.</summary>
    public static MultiMeshInstance3D? Build(ChunkData chunk, WorldLayout layout, IReadOnlyList<FoliageRule> rules, float density)
    {
        if (density <= 0 || rules.Count == 0)
        {
            return null;
        }
        var origin = layout.ChunkOrigin(chunk.Coord);
        var size = chunk.Size;
        var transforms = new List<Transform3D>();
        var colors = new List<Color>();
        var compiled = rules.Select(r => (r.Tags, Density: (float)r.Density, Color: Rgba.Parse(r.Color, new Rgba(0.4f, 0.6f, 0.25f)), Tip: Rgba.Parse(r.Tip, new Rgba(0.6f, 0.75f, 0.4f)), Height: (float)r.Height)).ToList();
        for (var ly = 0; ly < size; ly++)
        {
            for (var lx = 0; lx < size; lx++)
            {
                var i = chunk.Index(lx, ly);
                var flags = chunk.Flags[i];
                if ((flags & (CellFlags.Structure | CellFlags.Road | CellFlags.Water | CellFlags.Blocked)) != 0)
                {
                    continue;
                }
                var tags = chunk.TagsAt(i);
                var rule = compiled.FirstOrDefault(r => r.Tags.All(tags.Has));
                if (rule.Tags is null)
                {
                    continue;
                }
                var wx = origin.X + lx;
                var wy = origin.Y + ly;
                var want = rule.Density * density;
                var count = (int)MathF.Floor(want);
                if (SurfaceLook.Hash(wx * 3 + 1, wy * 5 + 2) < want - count)
                {
                    count++;
                }
                for (var k = 0; k < count && k < 4; k++)
                {
                    var ox = SurfaceLook.Hash(wx * 7 + k * 13, wy * 11 + 3);
                    var oy = SurfaceLook.Hash(wx * 17 + 5, wy * 19 + k * 7);
                    var scale = 0.7f + 0.6f * SurfaceLook.Hash(wx * 23 + k, wy * 29 + 1);
                    var turn = SurfaceLook.Hash(wx * 31 + k * 3, wy * 37 + 2) * MathF.PI * 2;
                    var cellX = wx + ox;
                    var cellY = wy + oy;
                    var h = HeightAt(chunk, lx, ly, ox, oy);
                    var basis = new Basis(Vector3.Up, turn).Scaled(new Vector3(scale, rule.Height * scale, scale));
                    transforms.Add(new Transform3D(basis, Coords.ToGodot(cellX, cellY, h)));
                    var mix = SurfaceLook.Hash(wx * 41 + k, wy * 43 + 9);
                    var c = rule.Color.Lerp(rule.Tip, mix * 0.6f);
                    colors.Add(new Color(c.R, c.G, c.B));
                }
            }
        }
        if (transforms.Count == 0)
        {
            return null;
        }
        var multi = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = Tuft,
            InstanceCount = transforms.Count,
        };
        for (var i = 0; i < transforms.Count; i++)
        {
            multi.SetInstanceTransform(i, transforms[i]);
            multi.SetInstanceColor(i, colors[i]);
        }
        return new MultiMeshInstance3D { Name = "foliage", Multimesh = multi, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
    }

    /// <summary>The ground height within a cell, by the cell's own height (flat enough for a tuft).</summary>
    private static float HeightAt(ChunkData chunk, int lx, int ly, float ox, float oy)
    {
        var h = chunk.Height[chunk.Index(lx, ly)];
        // lean toward the neighbour the tuft is nearer, so tufts on a slope sit on it rather than float
        var nx = ox > 0.5f ? Math.Min(chunk.Size - 1, lx + 1) : Math.Max(0, lx - 1);
        var ny = oy > 0.5f ? Math.Min(chunk.Size - 1, ly + 1) : Math.Max(0, ly - 1);
        var hx = chunk.Height[chunk.Index(nx, ly)];
        var hy = chunk.Height[chunk.Index(lx, ny)];
        var tx = MathF.Abs(ox - 0.5f);
        var ty = MathF.Abs(oy - 0.5f);
        return h + (hx - h) * tx * 0.5f + (hy - h) * ty * 0.5f - 0.02f;
    }
}
