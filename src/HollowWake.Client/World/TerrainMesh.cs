using Godot;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.World;

namespace HollowWake.Client.World;

/// <summary>
/// Builds a mesh for one chunk: one quad per cell, vertex colors from biome and surface tags, smooth
/// normals from the height field, vertical skirts where flattened cells step, and extra quads for
/// layered surfaces (bridges, floors). Water is a translucent plane at water level. Neighbouring chunks
/// are consulted for corner heights so chunk borders meet without cracks.
/// Placeholder geometry by design; the renderer's job here is to make the simulation legible.
/// </summary>
public static class TerrainMesh
{
    /// <summary>Looks up a chunk other than the one being built, or null when it is not available yet.</summary>
    public delegate ChunkData? ChunkLookup(ChunkCoord coord);

    private static StandardMaterial3D? _ground;
    private static StandardMaterial3D? _water;

    private static StandardMaterial3D Ground => _ground ??= new StandardMaterial3D
    {
        VertexColorUseAsAlbedo = true,
        Roughness = 1f,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
    };

    private static StandardMaterial3D Water => _water ??= new StandardMaterial3D
    {
        AlbedoColor = new Color(0.2f, 0.45f, 0.7f, 0.55f),
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        Roughness = 0.15f,
    };

    public static Node3D Build(ChunkData chunk, WorldLayout layout, ContentRegistry content, ChunkLookup? neighbours = null)
    {
        var root = new Node3D { Name = $"chunk_{chunk.Coord.X}_{chunk.Coord.Y}" };
        var origin = layout.ChunkOrigin(chunk.Coord);
        var size = chunk.Size;

        var vertices = new List<Vector3>(size * size * 4 + 256);
        var normals = new List<Vector3>(size * size * 4 + 256);
        var colors = new List<Color>(size * size * 4 + 256);
        var indices = new List<int>(size * size * 6 + 384);
        var biomeColors = new Dictionary<string, Color>(StringComparer.Ordinal);

        // A cell by chunk-local coordinates that may run past the edges of this chunk into a neighbour.
        bool TryCell(int lx, int ly, out float height, out CellFlags flags)
        {
            if (lx >= 0 && ly >= 0 && lx < size && ly < size)
            {
                var i = chunk.Index(lx, ly);
                height = chunk.Height[i];
                flags = chunk.Flags[i];
                return true;
            }
            var cell = new CellCoord(origin.X + lx, origin.Y + ly);
            var other = neighbours?.Invoke(layout.ChunkOf(cell));
            if (other is null)
            {
                height = 0;
                flags = CellFlags.None;
                return false;
            }
            var o = layout.ChunkOrigin(other.Coord);
            var j = other.Index(cell.X - o.X, cell.Y - o.Y);
            height = other.Height[j];
            flags = other.Flags[j];
            return true;
        }

        static bool IsFlat(CellFlags f) => (f & (CellFlags.Structure | CellFlags.Flattened)) != 0;

        // Height at a cell corner: the average of the up-to-four cells around it, so slopes are smooth.
        // Corner (i, j) is the south-west corner of cell (i, j).
        float Corner(int i, int j)
        {
            float sum = 0;
            var n = 0;
            for (var dy = -1; dy <= 0; dy++)
            {
                for (var dx = -1; dx <= 0; dx++)
                {
                    if (TryCell(i + dx, j + dy, out var h, out _))
                    {
                        sum += h;
                        n++;
                    }
                }
            }
            return n == 0 ? 0 : sum / n;
        }

        // Corner heights for -1..size+1 on both axes, so normals at the edges see across the border.
        var stride = size + 3;
        var corners = new float[stride * stride];
        for (var j = -1; j <= size + 1; j++)
        {
            for (var i = -1; i <= size + 1; i++)
            {
                corners[(j + 1) * stride + (i + 1)] = Corner(i, j);
            }
        }
        float CornerHeight(int i, int j) => corners[(j + 1) * stride + (i + 1)];
        Vector3 CornerNormal(int i, int j)
        {
            // Central differences over the neighbouring corners; cells are one unit wide.
            var dzdx = (CornerHeight(i + 1, j) - CornerHeight(i - 1, j)) * 0.5f;
            var dzdy = (CornerHeight(i, j + 1) - CornerHeight(i, j - 1)) * 0.5f;
            return Coords.ToGodot(-dzdx, -dzdy, 1f).Normalized();
        }

        Color ColorFor(int index)
        {
            var biome = chunk.BiomeAt(index);
            if (!biomeColors.TryGetValue(biome, out var c))
            {
                var def = content.Get<BiomeDef>(biome);
                c = def is not null && !string.IsNullOrEmpty(def.Color) && Color.HtmlIsValid(def.Color) ? Color.FromHtml(def.Color) : new Color(0.45f, 0.55f, 0.35f);
                biomeColors[biome] = c;
            }
            var tags = chunk.TagsAt(index);
            var flags = chunk.Flags[index];
            if ((flags & CellFlags.Structure) != 0)
            {
                c = tags.Has("road") ? new Color(0.62f, 0.56f, 0.46f) : tags.Has("wall") || tags.Has("house") || tags.Has("pillar") ? new Color(0.5f, 0.48f, 0.5f) : new Color(0.6f, 0.58f, 0.54f);
            }
            else if ((flags & CellFlags.Road) != 0)
            {
                c = c.Lerp(new Color(0.62f, 0.56f, 0.46f), 0.7f);
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

        // A vertical quad along one cell edge, covering the gap between the heights the two sides use.
        void Skirt(int x0, int y0, int x1, int y1, float bottom, float top, Color color)
        {
            if (top - bottom < 0.02f)
            {
                return;
            }
            AddQuad(vertices, normals, colors, indices,
                Coords.ToGodot(x0, y0, bottom), Coords.ToGodot(x1, y1, bottom), Coords.ToGodot(x1, y1, top), Coords.ToGodot(x0, y0, top), color.Darkened(0.3f), doubleSided: true);
        }

        for (var ly = 0; ly < size; ly++)
        {
            for (var lx = 0; lx < size; lx++)
            {
                var i = chunk.Index(lx, ly);
                var h = chunk.Height[i];
                var flat = IsFlat(chunk.Flags[i]);
                var color = ColorFor(i);
                var wx = origin.X + lx;
                var wy = origin.Y + ly;
                if (flat)
                {
                    AddQuad(vertices, normals, colors, indices,
                        Coords.ToGodot(wx, wy, h), Coords.ToGodot(wx + 1, wy, h), Coords.ToGodot(wx + 1, wy + 1, h), Coords.ToGodot(wx, wy + 1, h), color);
                }
                else
                {
                    AddQuad(vertices, normals, colors, indices,
                        Coords.ToGodot(wx, wy, CornerHeight(lx, ly)), Coords.ToGodot(wx + 1, wy, CornerHeight(lx + 1, ly)),
                        Coords.ToGodot(wx + 1, wy + 1, CornerHeight(lx + 1, ly + 1)), Coords.ToGodot(wx, wy + 1, CornerHeight(lx, ly + 1)), color,
                        CornerNormal(lx, ly), CornerNormal(lx + 1, ly), CornerNormal(lx + 1, ly + 1), CornerNormal(lx, ly + 1));
                }
                // Flattened cells keep their own height on every corner, so an edge shared with any other
                // cell can step; a skirt closes the gap and makes terraces and foundations read as steps.
                if (TryCell(lx + 1, ly, out var he, out var fe) && (flat || IsFlat(fe)))
                {
                    var c0 = CornerHeight(lx + 1, ly);
                    var c1 = CornerHeight(lx + 1, ly + 1);
                    var mineLow = flat ? h : Mathf.Min(c0, c1);
                    var mineHigh = flat ? h : Mathf.Max(c0, c1);
                    var theirsLow = IsFlat(fe) ? he : Mathf.Min(c0, c1);
                    var theirsHigh = IsFlat(fe) ? he : Mathf.Max(c0, c1);
                    Skirt(wx + 1, wy, wx + 1, wy + 1, Mathf.Min(mineLow, theirsLow), Mathf.Max(mineHigh, theirsHigh), color);
                }
                if (TryCell(lx, ly + 1, out var hn, out var fn) && (flat || IsFlat(fn)))
                {
                    var c0 = CornerHeight(lx, ly + 1);
                    var c1 = CornerHeight(lx + 1, ly + 1);
                    var mineLow = flat ? h : Mathf.Min(c0, c1);
                    var mineHigh = flat ? h : Mathf.Max(c0, c1);
                    var theirsLow = IsFlat(fn) ? hn : Mathf.Min(c0, c1);
                    var theirsHigh = IsFlat(fn) ? hn : Mathf.Max(c0, c1);
                    Skirt(wx, wy + 1, wx + 1, wy + 1, Mathf.Min(mineLow, theirsLow), Mathf.Max(mineHigh, theirsHigh), color);
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
        mesh.SurfaceSetMaterial(0, Ground);
        root.AddChild(new MeshInstance3D { Name = "ground", Mesh = mesh });

        if (!float.IsNegativeInfinity(chunk.WaterLevel) && chunk.Flags.Any(f => (f & CellFlags.Water) != 0))
        {
            var water = new MeshInstance3D
            {
                Name = "water",
                Mesh = new PlaneMesh { Size = new Vector2(size, size) },
                Position = Coords.ToGodot(origin.X + size / 2f, origin.Y + size / 2f, chunk.WaterLevel),
                MaterialOverride = Water,
            };
            root.AddChild(water);
        }
        return root;
    }

    private static void AddQuad(List<Vector3> v, List<Vector3> n, List<Color> c, List<int> idx, Vector3 a, Vector3 b, Vector3 cPt, Vector3 d, Color color, bool doubleSided = false)
    {
        var normal = (b - a).Cross(d - a).Normalized();
        if (normal.LengthSquared() < 0.0001f)
        {
            normal = Vector3.Up;
        }
        AddQuad(v, n, c, idx, a, b, cPt, d, color, normal, normal, normal, normal, doubleSided);
    }

    /// <summary>
    /// One quad with corners a, b, c, d going around it and the front on the side of (b - a) x (d - a).
    /// Godot treats clockwise triangles as front faces; simulation Y north maps to Godot -Z, which mirrors
    /// the winding, so the corners are emitted a, d, c, b to put the front face on the side of the normal.
    /// </summary>
    private static void AddQuad(List<Vector3> v, List<Vector3> n, List<Color> c, List<int> idx, Vector3 a, Vector3 b, Vector3 cPt, Vector3 d, Color color, Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd, bool doubleSided = false)
    {
        var start = v.Count;
        v.Add(a); v.Add(d); v.Add(cPt); v.Add(b);
        n.Add(na); n.Add(nd); n.Add(nc); n.Add(nb);
        for (var i = 0; i < 4; i++)
        {
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
