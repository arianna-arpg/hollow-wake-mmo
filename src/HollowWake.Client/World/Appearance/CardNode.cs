using Godot;
using HollowWake.Client.World.Logic;

namespace HollowWake.Client.World.Appearance;

/// <summary>
/// A body as a directional card: one foot-anchored quad the body's size, turned to face the camera about
/// the vertical, showing the atlas cell nearest the camera's bearing from the body's yaw. Depth-tested and
/// depth-written, so it occludes and is occluded like the geometry it stands in for. The animator's root
/// bone still moves it (bob, lean, squash, the fall of death); the rest of the pose is baked.
/// </summary>
public partial class CardNode : Node3D, IBodyView
{
    private readonly CardAtlas _atlas;
    private readonly float _scale;
    private MeshInstance3D _quad = null!;
    private Node3D _pivot = null!;
    private int _cell = -1;
    private float _flash = -1;
    private float _fade = -1;

    public BodyPlan Plan { get; }
    public int Triangles => 2;

    /// <summary>The camera's yaw this frame, set by the world so every card agrees.</summary>
    public static float CameraYaw { get; set; }

    public CardNode(BodyPlan plan, CardAtlas atlas, float scale)
    {
        Plan = plan;
        _atlas = atlas;
        _scale = scale;
        Name = "card";
    }

    public override void _Ready()
    {
        _pivot = new Node3D { Name = "pivot" };
        AddChild(_pivot);
        var h = _atlas.Height * _scale;
        var w = MathF.Max(_atlas.Height, _atlas.Width) * 1.08f * _scale;
        _quad = new MeshInstance3D
        {
            Name = "quad",
            // the atlas cell is square over the larger of height and width, so the quad is too, standing on its base
            Mesh = new QuadMesh { Size = new Vector2(w, w), CenterOffset = new Vector3(0, w / 2 - (w - h) * 0.0f, 0) },
            MaterialOverride = _atlas.Material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _pivot.AddChild(_quad);
        SetFlash(0);
        SetFade(1);
    }

    /// <summary>Faces the camera about the vertical and picks the cell for the body's facing seen from the camera.</summary>
    public void Apply(Pose pose)
    {
        // the body's yaw is the parent's rotation; the card turns back to face the camera and picks a cell for the difference
        var bodyYaw = GetParent<Node3D>().Rotation.Y;
        _pivot.Rotation = new Vector3(0, CameraYaw - bodyYaw + Mathf.Pi, 0);
        var relative = Mathf.PosMod(bodyYaw - CameraYaw + Mathf.Pi * 0.25f, Mathf.Tau);
        var cell = (int)MathF.Round(relative / Mathf.Tau * CardAtlas.Cells) % CardAtlas.Cells;
        if (cell != _cell)
        {
            _cell = cell;
            _quad.SetInstanceShaderParameter("cell", (float)cell);
        }
        var root = pose["root"];
        var turn = root.Turn * (MathF.PI / 180f);
        // the root's pitch lays the card down for a corpse; its roll leans it; its offset lifts it
        _quad.Rotation = new Vector3(turn.X, 0, turn.Y);
        _quad.Position = new Vector3(0, root.Offset.Y, 0);
        _quad.Scale = Vector3.One * MathF.Max(0.05f, root.Scale);
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
        _quad.SetInstanceShaderParameter("flash", flash);
    }

    private void SetFade(float fade)
    {
        if (MathF.Abs(fade - _fade) < 0.002f)
        {
            return;
        }
        _fade = fade;
        _quad.SetInstanceShaderParameter("fade", fade);
    }

    public void SetShadows(bool on)
    {
    }

    public void Release() => QueueFree();
}
