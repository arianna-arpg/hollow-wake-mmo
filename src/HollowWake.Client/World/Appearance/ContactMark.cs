using Godot;

namespace HollowWake.Client.World.Appearance;

/// <summary>
/// The mark where a body meets the ground: a soft disc that gives every body its contact and, for creatures,
/// an edge that says the relation without colour (inward teeth for a hostile, spaced ticks for a friend, a
/// plain rim for the rest), as the visual language asks. Unlit, never written to depth, drawn just above the
/// ground under the body's root so it follows the body and not the pose.
/// </summary>
public partial class ContactMark : Node3D
{
    private MeshInstance3D _quad = null!;
    private readonly float _size;
    private readonly int _style;
    private float _fade = -1;

    /// <summary>0 plain, 1 teeth, 2 ticks.</summary>
    public static int StyleOf(string style) => style switch { "teeth" => 1, "ticks" => 2, _ => 0 };

    public ContactMark(float size, int style)
    {
        _size = size;
        _style = style;
        Name = "contact";
    }

    public override void _Ready()
    {
        _quad = new MeshInstance3D
        {
            Name = "mark",
            Mesh = new QuadMesh { Size = new Vector2(_size, _size) },
            MaterialOverride = BodyShaders.Mark,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = new Vector3(0, 0.04f, 0),
        };
        _quad.RotationDegrees = new Vector3(-90, 0, 0);
        AddChild(_quad);
        _quad.SetInstanceShaderParameter("style", (float)_style);
        SetFade(1);
    }

    public void SetFade(float fade)
    {
        if (MathF.Abs(fade - _fade) < 0.002f || _quad is null)
        {
            return;
        }
        _fade = fade;
        _quad.SetInstanceShaderParameter("fade", fade);
    }
}
