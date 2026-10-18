using Godot;
using HollowWake.Client.World.Logic;

namespace HollowWake.Client.World.Appearance;

/// <summary>
/// A body plan as painted geometry: a Node3D per bone in the plan's hierarchy, each with one mesh of its
/// parts (lit facets in one surface, glowing parts in another), posed each frame by the animator. The root
/// sits at the entity's position; body space is x forward, y up, z right, and the view turns it by the yaw.
/// </summary>
public partial class BodyNode : Node3D, IBodyView
{
    private readonly Dictionary<string, Node3D> _bones = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Vector3> _localPivots = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Vector3> _rests = new(StringComparer.Ordinal);
    private readonly List<MeshInstance3D> _meshes = new();
    private float _flash = -1;
    private float _fade = -1;

    public BodyPlan Plan { get; }

    /// <summary>Triangles drawn, for the measurement line.</summary>
    public int Triangles { get; private set; }

    private readonly bool _castsShadows;

    public BodyNode(BodyPlan plan)
    {
        Plan = plan;
        Name = "body";
        // ground flora and small props cast no shadow: too many for the shadow passes and too low to show one
        _castsShadows = plan.Height >= 0.9f && plan.Family is not ("fern" or "reed" or "plant" or "bush" or "knot" or "drop" or "orb" or "trap" or "dome");
    }

    public override void _Ready()
    {
        var byName = Plan.Bones.ToDictionary(b => b.Name, b => b, StringComparer.Ordinal);
        foreach (var bone in Plan.Bones)
        {
            var node = new Node3D { Name = bone.Name };
            var parentPivot = bone.Parent is not null && byName.TryGetValue(bone.Parent, out var parent) ? parent.Pivot : Vec3.Zero;
            var local = ToGodot(bone.Pivot - parentPivot);
            _localPivots[bone.Name] = local;
            _rests[bone.Name] = new Vector3(bone.Rest.X, bone.Rest.Y, bone.Rest.Z);
            node.Position = local;
            if (bone.Parent is not null && _bones.TryGetValue(bone.Parent, out var parentNode))
            {
                parentNode.AddChild(node);
            }
            else
            {
                AddChild(node);
            }
            _bones[bone.Name] = node;
        }
        foreach (var group in Plan.Parts.GroupBy(p => p.Bone))
        {
            if (!_bones.TryGetValue(group.Key, out var boneNode) || !byName.TryGetValue(group.Key, out var bone))
            {
                continue;
            }
            var mesh = BuildMesh(group, bone.Pivot);
            if (mesh is null)
            {
                continue;
            }
            var instance = new MeshInstance3D { Name = "parts", Mesh = mesh, CastShadow = _castsShadows ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off };
            boneNode.AddChild(instance);
            _meshes.Add(instance);
        }
        SetFlash(0);
        SetFade(1);
    }

    private static Vector3 ToGodot(Vec3 v) => new(v.X, v.Y, v.Z);

    /// <summary>One mesh for a bone's parts, positioned relative to the bone's pivot, lit and glowing surfaces apart.</summary>
    private ArrayMesh? BuildMesh(IEnumerable<BodyPart> parts, Vec3 pivot)
    {
        var lit = new Buffers();
        var glow = new Buffers();
        foreach (var part in parts)
        {
            var (positions, normals) = PartGeometry.Build(part);
            var color = new Color(part.Color.R, part.Color.G, part.Color.B, 1f).SrgbToLinear();
            var target = part.Glow ? glow : lit;
            for (var i = 0; i < positions.Length; i += 3)
            {
                target.Vertices.Add(new Vector3(positions[i] - pivot.X, positions[i + 1] - pivot.Y, positions[i + 2] - pivot.Z));
                target.Normals.Add(new Vector3(normals[i], normals[i + 1], normals[i + 2]));
                target.Colors.Add(color);
            }
        }
        if (lit.Vertices.Count == 0 && glow.Vertices.Count == 0)
        {
            return null;
        }
        var mesh = new ArrayMesh();
        var surface = 0;
        foreach (var (buffers, material) in new[] { (lit, BodyShaders.Painted), (glow, BodyShaders.Glow) })
        {
            if (buffers.Vertices.Count == 0)
            {
                continue;
            }
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            // Godot takes clockwise triangles as front faces; the plan's are counter-clockwise, so reverse each.
            var vertices = new Vector3[buffers.Vertices.Count];
            var normals = new Vector3[buffers.Vertices.Count];
            var colors = new Color[buffers.Vertices.Count];
            for (var i = 0; i < buffers.Vertices.Count; i += 3)
            {
                vertices[i] = buffers.Vertices[i];
                vertices[i + 1] = buffers.Vertices[i + 2];
                vertices[i + 2] = buffers.Vertices[i + 1];
                normals[i] = buffers.Normals[i];
                normals[i + 1] = buffers.Normals[i + 2];
                normals[i + 2] = buffers.Normals[i + 1];
                colors[i] = buffers.Colors[i];
                colors[i + 1] = buffers.Colors[i + 2];
                colors[i + 2] = buffers.Colors[i + 1];
            }
            arrays[(int)Mesh.ArrayType.Vertex] = vertices;
            arrays[(int)Mesh.ArrayType.Normal] = normals;
            arrays[(int)Mesh.ArrayType.Color] = colors;
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            mesh.SurfaceSetMaterial(surface++, material);
            Triangles += buffers.Vertices.Count / 3;
        }
        return mesh;
    }

    private sealed class Buffers
    {
        public readonly List<Vector3> Vertices = new();
        public readonly List<Vector3> Normals = new();
        public readonly List<Color> Colors = new();
    }

    /// <summary>Moves every bone to its pose: the rest turn plus the animator's, about the bone's pivot.</summary>
    public void Apply(Pose pose)
    {
        foreach (var (name, node) in _bones)
        {
            var p = pose[name];
            var rest = _rests[name];
            var turn = new Vector3(p.Turn.X + rest.X, p.Turn.Y + rest.Y, p.Turn.Z + rest.Z) * (MathF.PI / 180f);
            // roll about x, then pitch about z, then yaw about y: the plan's convention, applied right to left
            var basis = new Basis(Vector3.Up, turn.Z) * new Basis(Vector3.Back, turn.Y) * new Basis(Vector3.Right, turn.X);
            if (p.Scale != 1)
            {
                basis = basis.Scaled(Vector3.One * p.Scale);
            }
            node.Transform = new Transform3D(basis, _localPivots[name] + new Vector3(p.Offset.X, p.Offset.Y, p.Offset.Z));
        }
        SetFlash(pose.Flash);
        SetFade(pose.Opacity);
    }

    private void SetFlash(float flash)
    {
        if (MathF.Abs(flash - _flash) < 0.002f)
        {
            return;
        }
        _flash = flash;
        foreach (var m in _meshes)
        {
            m.SetInstanceShaderParameter("flash", flash);
        }
    }

    private void SetFade(float fade)
    {
        if (MathF.Abs(fade - _fade) < 0.002f)
        {
            return;
        }
        _fade = fade;
        foreach (var m in _meshes)
        {
            m.SetInstanceShaderParameter("fade", fade);
            m.Transparency = 1 - fade;
        }
    }

    public void SetShadows(bool on)
    {
        foreach (var m in _meshes)
        {
            m.CastShadow = on && _castsShadows ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off;
        }
    }

    public void Release() => QueueFree();
}

/// <summary>What the entity view asks of whichever representation draws the body.</summary>
public interface IBodyView
{
    BodyPlan Plan { get; }
    int Triangles { get; }
    void Apply(Pose pose);
    void SetShadows(bool on);
    void Release();
}
