using Godot;

namespace HollowWake.Client.UI.Overlay;

/// <summary>
/// A whole-screen filter over the world and the interface: the grayscale check of the visual language's
/// readability gate (Accessibility in Options), drawn last so a reviewer sees exactly what a player without
/// colour would. Off by default and free when off.
/// </summary>
public partial class ScreenFilter : CanvasLayer
{
    private ColorRect _rect = null!;

    public ScreenFilter()
    {
        Name = "ScreenFilter";
        Layer = 120;
    }

    public override void _Ready()
    {
        _rect = new ColorRect
        {
            Name = "filter",
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Material = new ShaderMaterial { Shader = new Shader { Code = Code } },
        };
        _rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_rect);
    }

    public bool Grayscale
    {
        get => _rect.Visible;
        set => _rect.Visible = value;
    }

    private const string Code = @"
shader_type canvas_item;
uniform sampler2D screen : hint_screen_texture, filter_nearest;
void fragment() {
    vec4 c = texture(screen, SCREEN_UV);
    float l = dot(c.rgb, vec3(0.2126, 0.7152, 0.0722));
    COLOR = vec4(vec3(l), 1.0);
}
";
}
