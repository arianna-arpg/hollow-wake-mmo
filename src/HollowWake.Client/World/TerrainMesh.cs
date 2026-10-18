using Godot;
using HollowWake.Client.UI.Logic;
using HollowWake.Client.World.Logic;
using HollowWake.Core.World;

namespace HollowWake.Client.World;

/// <summary>
/// Builds a mesh for one chunk: one quad per cell coloured by the surfaces content from its surface tags and
/// cell flags (with a seeded value jitter in patches so a field is not one flat colour), smooth normals from
/// the height field, vertical skirts where flattened cells step (in the structure's side colour), a pitched
/// roof over roof cells, extra quads for layered surfaces (bridges, floors), and water as a lit, rippling
/// plane at water level. Neighbouring chunks are consulted for corner heights so chunk borders meet without
/// cracks. Terrain generation is untouched: this is only how the land is drawn.
/// </summary>
public static class TerrainMesh
{
    /// <summary>Looks up a chunk other than the one being built, or null when it is not available yet.</summary>
    public delegate ChunkData? ChunkLookup(ChunkCoord coord);

    private static StandardMaterial3D? _ground;
    private static ShaderMaterial? _water;

    private static StandardMaterial3D Ground => _ground ??= new StandardMaterial3D
    {
        VertexColorUseAsAlbedo = true,
        VertexColorIsSrgb = true,
        Roughness = 1f,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
    };

    /// <summary>The water's look follows the surfaces content; set once the content is known.</summary>
    public static void ConfigureWater(SurfaceLook look)
    {
        var water = Water;
        water.SetShaderParameter("shallow_color", new Color(look.Water.R, look.Water.G, look.Water.B));
        water.SetShaderParameter("deep_color", new Color(look.WaterDeep.R, look.WaterDeep.G, look.WaterDeep.B));
        water.SetShaderParameter("light_color", new Color(look.WaterLight.R, look.WaterLight.G, look.WaterLight.B));
        water.SetShaderParameter("opacity", look.WaterOpacity);
        water.SetShaderParameter("ripple", look.WaterRipple);
        water.SetShaderParameter("speed", look.WaterSpeed);
    }

    /// <summary>Whether the water ripples and lights (the water detail option); off, it is a still tinted plane.</summary>
    public static void SetWaterDetail(bool on) => Water.SetShaderParameter("detail", on ? 1f : 0f);

    private static ShaderMaterial Water => _water ??= new ShaderMaterial { Shader = new Shader { Code = WaterCode } };

    private const string WaterCode = @"
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_disabled, specular_schlick_ggx;
uniform vec4 shallow_color : source_color = vec4(0.18, 0.42, 0.54, 1.0);
uniform vec4 deep_color : source_color = vec4(0.12, 0.25, 0.36, 1.0);
uniform vec4 light_color : source_color = vec4(0.6, 0.84, 0.85, 1.0);
uniform float opacity = 0.62;
uniform float ripple = 0.5;
uniform float speed = 0.35;
uniform float detail = 1.0;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}
varying vec3 world_pos;
void vertex() {
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}
void fragment() {
    vec2 p = world_pos.xz;
    float t = TIME * speed * detail;
    float n1 = noise(p * 0.9 + vec2(t * 0.6, t * 0.4));
    float n2 = noise(p * 2.3 - vec2(t * 0.3, t * 0.7));
    float wave = (n1 * 0.65 + n2 * 0.35);
    // a small light held beneath the surface: brighter where the ripples crest
    float crest = smoothstep(0.55, 0.8, wave) * ripple * detail;
    vec3 base = mix(deep_color.rgb, shallow_color.rgb, wave * 0.6 + 0.2);
    ALBEDO = mix(base, light_color.rgb, crest * 0.5);
    EMISSION = light_color.rgb * crest * 0.12;
    vec3 n = normalize(vec3((n1 - 0.5) * 0.35 * ripple * detail, 1.0, (n2 - 0.5) * 0.35 * ripple * detail));
    NORMAL = normalize((VIEW_MATRIX * vec4(n, 0.0)).xyz);
    ROUGHNESS = 0.2;
    SPECULAR = 0.6;
    ALPHA = opacity + crest * 0.2;
}
";

    public static Node3D Build(ChunkData chunk, WorldLayout layout, SurfaceLook look, ChunkLookup? neighbours = null)
    {
        var root = new Node3D { Name = $"chunk_{chunk.Coord.X}_{chunk.Coord.Y}" };
        var origin = layout.ChunkOrigin(chunk.Coord);
        var size = chunk.Size;

        var vertices = new List<Vector3>(size * size * 4 + 256);
        var normals = new List<Vector3>(size * size * 4 + 256);
        var colors = new List<Color>(size * size * 4 + 256);
        var indices = new List<int>(size * size * 6 + 384);
        var colorCache = new Dictionary<int, (Rgba Top, Rgba Side, Rgba? Roof)>();

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

        static Color ToColor(Rgba c) => new(c.R, c.G, c.B);

        // The colours of a cell: its top, the side its skirts take, and a roof when the content raises one.
        (Rgba Top, Rgba Side, Rgba? Roof) Colors(int index)
        {
            var key = chunk.SurfaceTag[index] * 256 + (int)chunk.Flags[index];
            if (!colorCache.TryGetValue(key, out var c))
            {
                var tags = chunk.TagsAt(index);
                var flags = chunk.Flags[index];
                var structure = look.Structure(tags, flags);
                var top = structure?.Top ?? look.Cell(tags, flags);
                c = (top, structure?.Side ?? look.Skirt(top), structure?.Roof);
                colorCache[key] = c;
            }
            return c;
        }

        // A vertical quad along one cell edge, covering the gap between the heights the two sides use.
        void Skirt(int x0, int y0, int x1, int y1, float bottom, float top, Rgba color)
        {
            if (top - bottom < 0.02f)
            {
                return;
            }
            AddQuad(vertices, normals, colors, indices,
                Coords.ToGodot(x0, y0, bottom), Coords.ToGodot(x1, y1, bottom), Coords.ToGodot(x1, y1, top), Coords.ToGodot(x0, y0, top), ToColor(color), doubleSided: true);
        }

        for (var ly = 0; ly < size; ly++)
        {
            for (var lx = 0; lx < size; lx++)
            {
                var i = chunk.Index(lx, ly);
                var h = chunk.Height[i];
                var flags = chunk.Flags[i];
                var flat = IsFlat(flags);
                var (top, side, roof) = Colors(i);
                var wx = origin.X + lx;
                var wy = origin.Y + ly;
                var jitter = look.Jitter(wx, wy, flags);
                var color = ToColor(jitter == 1 ? top : new Rgba(Math.Clamp(top.R * jitter, 0, 1), Math.Clamp(top.G * jitter, 0, 1), Math.Clamp(top.B * jitter, 0, 1)));
                if (flat)
                {
                    AddQuad(vertices, normals, colors, indices,
                        Coords.ToGodot(wx, wy, h), Coords.ToGodot(wx + 1, wy, h), Coords.ToGodot(wx + 1, wy + 1, h), Coords.ToGodot(wx, wy + 1, h), color);
                    if (roof is { } roofColor)
                    {
                        Roof(vertices, normals, colors, indices, wx, wy, h, ToColor(roofColor), ToColor(roofColor.Darkened(0.25f)));
                    }
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
                // The skirt takes the structure's side colour, so walls read as stone or wood below their tops.
                var skirtColor = (flags & CellFlags.Structure) != 0 ? side : look.Skirt(top);
                if (TryCell(lx + 1, ly, out var he, out var fe) && (flat || IsFlat(fe)))
                {
                    var c0 = CornerHeight(lx + 1, ly);
                    var c1 = CornerHeight(lx + 1, ly + 1);
                    var mineLow = flat ? h : Mathf.Min(c0, c1);
                    var mineHigh = flat ? h : Mathf.Max(c0, c1);
                    var theirsLow = IsFlat(fe) ? he : Mathf.Min(c0, c1);
                    var theirsHigh = IsFlat(fe) ? he : Mathf.Max(c0, c1);
                    Skirt(wx + 1, wy, wx + 1, wy + 1, Mathf.Min(mineLow, theirsLow), Mathf.Max(mineHigh, theirsHigh), h >= he ? skirtColor : look.Skirt(top));
                }
                if (TryCell(lx, ly + 1, out var hn, out var fn) && (flat || IsFlat(fn)))
                {
                    var c0 = CornerHeight(lx, ly + 1);
                    var c1 = CornerHeight(lx + 1, ly + 1);
                    var mineLow = flat ? h : Mathf.Min(c0, c1);
                    var mineHigh = flat ? h : Mathf.Max(c0, c1);
                    var theirsLow = IsFlat(fn) ? hn : Mathf.Min(c0, c1);
                    var theirsHigh = IsFlat(fn) ? hn : Mathf.Max(c0, c1);
                    Skirt(wx, wy + 1, wx + 1, wy + 1, Mathf.Min(mineLow, theirsLow), Mathf.Max(mineHigh, theirsHigh), h >= hn ? skirtColor : look.Skirt(top));
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
                var c = ToColor(look.Layer(s.Tags));
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
                Mesh = new PlaneMesh { Size = new Vector2(size, size), SubdivideDepth = 3, SubdivideWidth = 3 },
                Position = Coords.ToGodot(origin.X + size / 2f, origin.Y + size / 2f, chunk.WaterLevel),
                MaterialOverride = Water,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            root.AddChild(water);
        }
        return root;
    }

    /// <summary>
    /// A pitched cap over a roof cell: a ridge along the cell's longer run of roof neighbours would need the
    /// whole house; each cell takes a low hip to its centre, which reads as a roof at gameplay distance.
    /// </summary>
    private static void Roof(List<Vector3> v, List<Vector3> n, List<Color> c, List<int> idx, int wx, int wy, float h, Color top, Color shade)
    {
        const float rise = 0.55f;
        var apex = Coords.ToGodot(wx + 0.5f, wy + 0.5f, h + rise);
        var a = Coords.ToGodot(wx, wy, h);
        var b = Coords.ToGodot(wx + 1, wy, h);
        var cc = Coords.ToGodot(wx + 1, wy + 1, h);
        var d = Coords.ToGodot(wx, wy + 1, h);
        // south and west faces catch the light's side, the others its shade
        AddTri(v, n, c, idx, a, b, apex, top);
        AddTri(v, n, c, idx, b, cc, apex, shade);
        AddTri(v, n, c, idx, cc, d, apex, top);
        AddTri(v, n, c, idx, d, a, apex, shade);
    }

    private static void AddTri(List<Vector3> v, List<Vector3> n, List<Color> c, List<int> idx, Vector3 a, Vector3 b, Vector3 cPt, Color color)
    {
        var normal = (b - a).Cross(cPt - a).Normalized();
        if (normal.LengthSquared() < 0.0001f)
        {
            normal = Vector3.Up;
        }
        var start = v.Count;
        // the plan's corners run counter-clockwise seen from the normal's side; Godot wants clockwise
        v.Add(a); v.Add(cPt); v.Add(b);
        n.Add(normal); n.Add(normal); n.Add(normal);
        c.Add(color); c.Add(color); c.Add(color);
        idx.Add(start); idx.Add(start + 1); idx.Add(start + 2);
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
