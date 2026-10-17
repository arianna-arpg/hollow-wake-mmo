using Godot;
using HollowWake.Client.UI.Logic;
using HollowWake.Client.UI.Widgets;
using HollowWake.Protocol;

namespace HollowWake.Client.UI.Cluster;

/// <summary>
/// The bar above the weave bar while a cast, channel or charge-up runs: the progress the server sent (run on
/// with the duration it named until the next state), overcharge stages as segments, and the brim marked from
/// where it begins. Hidden when nothing is cast.
/// </summary>
public partial class CastBar : Control
{
    private readonly Skin _skin;
    private CastInfo? _cast;
    private double _progress;
    private string? _thread;

    public CastBar(Skin skin)
    {
        _skin = skin;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void Update(ClusterLayout layout, HeroState hero, double now)
    {
        Position = new Vector2(layout.Cast.X, layout.Cast.Y);
        Size = new Vector2(layout.Cast.W, layout.Cast.H);
        _cast = hero.Cast;
        Visible = _cast is not null;
        if (_cast is null)
        {
            return;
        }
        var progress = _cast.Progress;
        if (_cast.Duration is > 0 and var duration && _cast.Mode != "channel")
        {
            progress = Math.Min(1, progress + (now - hero.SelfAt) / duration);
        }
        _progress = Math.Clamp(progress, 0, 1);
        _thread = _cast.Slot >= 0 ? hero.Seats(Math.Max(8, _cast.Slot + 1)).ElementAtOrDefault(_cast.Slot)?.Threads.FirstOrDefault() : null;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_cast is null)
        {
            return;
        }
        var r = new Rect2(Vector2.Zero, Size);
        Paint.Well(this, _skin, r, _skin.S("well") with { A = 0.9f });
        var fill = _thread is not null ? _skin.Thread(_thread) : _skin.S("accent");
        var inner = r.Grow(-2);
        if (_cast.Brim is { } brim and > 0 and < 1)
        {
            DrawRect(new Rect2(inner.Position.X + inner.Size.X * (float)brim, inner.Position.Y, inner.Size.X * (1 - (float)brim), inner.Size.Y), fill with { A = 0.18f });
        }
        DrawRect(new Rect2(inner.Position, new Vector2(inner.Size.X * (float)_progress, inner.Size.Y)), fill);
        DrawRect(new Rect2(inner.Position, new Vector2(inner.Size.X * (float)_progress, inner.Size.Y * 0.4f)), Colors.White with { A = 0.18f });
        if (_cast.Stages > 1)
        {
            for (var i = 1; i < _cast.Stages; i++)
            {
                var x = inner.Position.X + inner.Size.X * i / _cast.Stages;
                DrawLine(new Vector2(x, inner.Position.Y), new Vector2(x, inner.End.Y), _skin.S("edge_dark"), 2);
            }
            for (var i = 0; i < _cast.Stages; i++)
            {
                var on = i < _cast.Stage;
                DrawCircle(new Vector2(inner.Position.X + inner.Size.X * (i + 0.5f) / _cast.Stages, -5), 2.5f, on ? _skin.State("pip", fill) : _skin.S("edge_dark"), true, -1, true);
            }
        }
        if (_cast.Brim is { } b and > 0 and < 1)
        {
            var x = inner.Position.X + inner.Size.X * (float)b;
            DrawLine(new Vector2(x, r.Position.Y - 2), new Vector2(x, r.End.Y + 2), _skin.S("accent"), 2);
        }
        var name = _cast.Name ?? "";
        if (name.Length > 0)
        {
            var size = Math.Max(10, (int)(Size.Y * 0.85f));
            Paint.Text(this, _skin.Body, new Vector2(0, Size.Y * 0.5f + size * 0.36f), name, size, Colors.White, HorizontalAlignment.Center, Size.X, 4);
        }
    }
}
