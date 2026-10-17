using Godot;

namespace HollowWake.Client.UI.Widgets;

/// <summary>A frame drawn over its parent's children (add it last), so the frame sits on the surface it borders.</summary>
public partial class FrameLayer : Control
{
    private readonly Skin _skin;

    public bool Emphasised { get; set; }

    public FrameLayer(Skin skin)
    {
        _skin = skin;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void Fit(Vector2 size)
    {
        Position = Vector2.Zero;
        Size = size;
        QueueRedraw();
    }

    public override void _Draw() => Paint.Frame(this, _skin, new Rect2(Vector2.Zero, Size), Emphasised);
}
