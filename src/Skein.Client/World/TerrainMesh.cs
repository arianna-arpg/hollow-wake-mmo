using Godot;
using Skein.Core.Content;
using Skein.Core.Definitions;
using Skein.Core.World;

namespace Skein.Client.World;

/// <summary>
/// Builds a mesh for one chunk: one quad per cell, vertex colors from biome and surface tags, and
/// extra quads for layered surfaces (bridges, floors). Water is a translucent plane at water level.
/// Placeholder geometry by design; the renderer's job here is to make the simulation legible.
/// </summary>
public static class TerrainMesh
{
    public static Node3D Build(ChunkData chunk, WorldLayout layout, ContentRegistry content)
    {
        var root = new Node3D { Name = $"chunk_{chunk.Coord.X}_{chunk.Coord.Y}" };
        var origin = layout.ChunkOrigin(chunk.Coord);
        var size = chunk.Size;

        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var colors = new List<Color>();
        var indices = new List<int>();
        var biomeColors = new Dictionary<string, Color>(StringComparer.Ordinal);

        Color ColorFor(int index)
        {
            var biome = chunk.BiomeAt(index);
            if (!biomeColors.TryGetValue(biome, out var c))
            {
                var def = content.Get<BiomeDef>(biome);
                c = def is null ? new Color(0.45f, 0.55f, 0.35f) : Color.FromHtml(def.Color);
                biomeColors[biome] = c;
            }
            var tags = chunk.TagsAt(index);
            var flags = chunk.Flags[index];
            if ((flags & CellFlags.Structure) != 0)
            {
                c = tags.Has("road") ? new Color(0.62f, 0.56f, 0.46f) : tags.Has("wall") || tags.Has("house") || tags.Has("pillar") ? new Color(0.5f, 0.48f, 0.5f) : new Color(0.6f, 0.58f, 0.54f);
            }
            else if (tags.Has("cliff"))
            {
                c = c.Darkened(0.35f);
            }
            else if ((flags & CellFlags.Water) != 0)
            {
                c = c.Lerp(new Color(0.35f, 0.5f, 0.6f), 0.5f);
            }
            if ((flags & CellFlags.Blocked) != 0 && (flags & CellFlags.Structure) == 0)
            {
                c = c.Darkened(0.2f);
            }
            return c;
        }

        // Height at a cell corner: average of the up-to-four cells around it, so slopes are smooth
        // and terraces keep their steps because neighbors share the same quantized height.
        float Corner(int lx, int ly)
        {
            float sum = 0;
            var n = 0;
            for (var dy = -1; dy <= 0; dy++)
            {
                for (var dx = -1; dx <= 0; dx++)
                {
                    var cx = lx + dx;
                    var cy = ly + dy;
                    if (cx < 0 || cy < 0 || cx >= size || cy >= size)
                    {
                        continue;
                    }
                    sum += chunk.Height[chunk.Index(cx, cy)];
                    n++;
                }
            }
            return n == 0 ? 0 : sum / n;
        }

        for (var ly = 0; ly < size; ly++)
        {
            for (var lx = 0; lx < size; lx++)
            {
                var i = chunk.Index(lx, ly);
                var h = chunk.Height[i];
                var flat = (chunk.Flags[i] & CellFlags.Structure) != 0 || (chunk.Flags[i] & CellFlags.Flattened) != 0;
                var h00 = flat ? h : Corner(lx, ly);
                var h10 = flat ? h : Corner(lx + 1, ly);
                var h01 = flat ? h : Corner(lx, ly + 1);
                var h11 = flat ? h : Corner(lx + 1, ly + 1);
                var color = ColorFor(i);
                var wx = origin.X + lx;
                var wy = origin.Y + ly;
                AddQuad(vertices, normals, colors, indices,
                    Coords.ToGodot(wx, wy, h00), Coords.ToGodot(wx + 1, wy, h10), Coords.ToGodot(wx + 1, wy + 1, h11), Coords.ToGodot(wx, wy + 1, h01), color);
                // Vertical skirts where a cell is much higher than its east or north neighbor, so terraces read as steps.
                if (lx + 1 < size)
                {
                    var hn = chunk.Height[chunk.Index(lx + 1, ly)];
                    if (flat && Mathf.Abs(h - hn) > 0.4f)
                    {
                        var top = Mathf.Max(h, hn);
                        var bottom = Mathf.Min(h, hn);
                        AddQuad(vertices, normals, colors, indices,
                            Coords.ToGodot(wx + 1, wy, bottom), Coords.ToGodot(wx + 1, wy + 1, bottom), Coords.ToGodot(wx + 1, wy + 1, top), Coords.ToGodot(wx + 1, wy, top), color.Darkened(0.3f), doubleSided: true);
                    }
                }
                if (ly + 1 < size)
                {
                    var hn = chunk.Height[chunk.Index(lx, ly + 1)];
                    if (flat && Mathf.Abs(h - hn) > 0.4f)
                    {
                        var top = Mathf.Max(h, hn);
                        var bottom = Mathf.Min(h, hn);
                        AddQuad(vertices, normals, colors, indices,
                            Coords.ToGodot(wx, wy + 1, bottom), Coords.ToGodot(wx + 1, wy + 1, bottom), Coords.ToGodot(wx + 1, wy + 1, top), Coords.ToGodot(wx, wy + 1, top), color.Darkened(0.3f), doubleSided: true);
                    }
                }
            }
        }
        foreach (var (index, layers) in chunk.Layers)
        {
            var lx = index % size;
            var ly = index / size;
            var wx = origin.X + lx;
            var wy = origin.Y + ly;
            foreach (var s in layers)
            {
                var c = s.Tags.Has("bridge") ? new Color(0.55f, 0.42f, 0.3f) : new Color(0.6f, 0.58f, 0.55f);
                AddQuad(vertices, normals, colors, indices,
                    Coords.ToGodot(wx, wy, s.Height), Coords.ToGodot(wx + 1, wy, s.Height), Coords.ToGodot(wx + 1, wy + 1, s.Height), Coords.ToGodot(wx, wy + 1, s.Height), c, doubleSided: true);
            }
        }

        var mesh = new ArrayMesh();
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        var material = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 1f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        mesh.SurfaceSetMaterial(0, material);
        root.AddChild(new MeshInstance3D { Name = "ground", Mesh = mesh });

        if (!float.IsNegativeInfinity(chunk.WaterLevel) && chunk.Flags.Any(f => (f & CellFlags.Water) != 0))
        {
            var water = new MeshInstance3D
            {
                Name = "water",
                Mesh = new PlaneMesh { Size = new Vector2(size, size) },
                Position = Coords.ToGodot(origin.X + size / 2f, origin.Y + size / 2f, chunk.WaterLevel),
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.2f, 0.45f, 0.7f, 0.55f),
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    Roughness = 0.15f,
                },
            };
            root.AddChild(water);
        }
        return root;
    }

    private static void AddQuad(List<Vector3> v, List<Vector3> n, List<Color> c, List<int> idx, Vector3 a, Vector3 b, Vector3 cPt, Vector3 d, Color color, bool doubleSided = false)
    {
        var start = v.Count;
        var normal = (b - a).Cross(d - a).Normalized();
        if (normal.LengthSquared() < 0.0001f)
        {
            normal = Vector3.Up;
        }
        // Simulation Y north maps to Godot -Z, which flips winding; order so the face points up/out.
        v.Add(a); v.Add(d); v.Add(cPt); v.Add(b);
        for (var i = 0; i < 4; i++)
        {
            n.Add(-normal);
            c.Add(color);
        }
        idx.Add(start); idx.Add(start + 1); idx.Add(start + 2);
        idx.Add(start); idx.Add(start + 2); idx.Add(start + 3);
        if (doubleSided)
        {
            idx.Add(start); idx.Add(start + 2); idx.Add(start + 1);
            idx.Add(start); idx.Add(start + 3); idx.Add(start + 2);
        }
    }
}
