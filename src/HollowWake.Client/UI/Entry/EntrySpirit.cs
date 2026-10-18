using Godot;
using HollowWake.Client.UI.Loading;
using HollowWake.Client.UI.Logic;

namespace HollowWake.Client.UI.Entry;

public partial class EntrySpirit : Control
{
    public SpatialSelection Model { get; init; } = null!;
    public Camera3D Camera { get; init; } = null!;
    public EntrySceneSpec Spec { get; init; } = null!;
    public WispLook Look { get; init; } = null!;
    public Func<bool> ReducedMotion { get; init; } = () => false;
    public double Time { get; set; }
    private GradientTexture2D? _glow;
    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;
    private void Glow(Vector2 at, float radius, Color color)
    {
        _glow ??= new GradientTexture2D { Width = 128, Height = 128, Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new(.5f, .5f), FillTo = new(1, .5f), Gradient = new Gradient { Colors = [Colors.White, Colors.Transparent] } };
        DrawTextureRect(_glow, new Rect2(at - Vector2.One * radius, Vector2.One * radius * 2), false, color);
    }
    public override void _Draw()
    {
        SpiritCanvas.Abyss(this, Size, ReducedMotion() ? 0 : Time, ReducedMotion() ? 0 : Model.X, ReducedMotion() ? 0 : Model.Y, new Color(Spec.Palette["ink"]), Glow);
        var at = Camera.UnprojectPosition(new((float)(Model.X / Spec.Units), .1f, (float)(Model.Y / Spec.Units)));
        SpiritCanvas.Draw(this, Look, at, -Mathf.Pi / 2, (float)(Size.Y / Spec.CameraSpan), ReducedMotion() ? 0 : Time, Glow);
    }
    public override void _ExitTree() { _glow?.Gradient?.Dispose(); _glow?.Dispose(); }
}
