using Godot;
using HollowWake.Client.UI.Logic;
using HollowWake.Client.UI.Widgets;
using HollowWake.Protocol;

namespace HollowWake.Client.UI.Cluster;

/// <summary>
/// The bar above the weave bar while a cast runs, drawn by its mode: a cast or a charge-up fills with the
/// progress the server sent (run on with the duration it named until the next state); a channel shows its
/// progress and its brim from where it begins; an overcharge shows its stages as segments with a pip per stage
/// banked; a guard shows the pool it has left over the time it can still be held. Hidden when nothing is cast.
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

    /// <summary>Modes whose progress is time over the named duration, so it runs on between states.</summary>
    private static bool Timed(string mode) => mode is "cast" or "charge" or "guard";

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
        if (_cast.Duration is > 0 and var duration && Timed(_cast.Mode))
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
        if (_cast.Mode == "guard")
        {
            DrawGuard(inner);
        }
        else
        {
            if (_cast.Mode == "channel" && _cast.Brim is { } brim and > 0 and < 1)
            {
                DrawRect(new Rect2(inner.Position.X + inner.Size.X * (float)brim, inner.Position.Y, inner.Size.X * (1 - (float)brim), inner.Size.Y), fill with { A = 0.18f });
            }
            DrawRect(new Rect2(inner.Position, new Vector2(inner.Size.X * (float)_progress, inner.Size.Y)), fill);
            DrawRect(new Rect2(inner.Position, new Vector2(inner.Size.X * (float)_progress, inner.Size.Y * 0.4f)), Colors.White with { A = 0.18f });
            if (_cast.Stages > 1)
            {
                DrawStages(inner, fill);
            }
            if (_cast.Mode == "channel" && _cast.Brim is { } b and > 0 and < 1)
            {
                var x = inner.Position.X + inner.Size.X * (float)b;
                DrawLine(new Vector2(x, r.Position.Y - 2), new Vector2(x, r.End.Y + 2), _skin.S("accent"), 2);
            }
        }
        var name = _cast.Name ?? "";
        if (_cast.Mode == "overcharge" && _cast.Stages > 0)
        {
            name = name.Length > 0 ? $"{name}  {_cast.Stage}/{_cast.Stages}" : $"{_cast.Stage}/{_cast.Stages}";
        }
        if (name.Length > 0)
        {
            var size = Math.Max(10, (int)(Size.Y * 0.85f));
            Paint.Text(this, _skin.Body, new Vector2(0, Size.Y * 0.5f + size * 0.36f), name, size, Colors.White, HorizontalAlignment.Center, Size.X, 4);
        }
    }

    /// <summary>Segments between stages and a pip above each, lit when banked.</summary>
    private void DrawStages(Rect2 inner, Color fill)
    {
        for (var i = 1; i < _cast!.Stages; i++)
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

    /// <summary>A guard: the pool it has left (the brim the server sends for a guard) above a strip of the hold that remains.</summary>
    private void DrawGuard(Rect2 inner)
    {
        var guard = _skin.State("guard", _skin.Fill("ward"));
        var pool = (float)Math.Clamp(_cast!.Brim ?? 1, 0, 1);
        var strip = Math.Max(2, inner.Size.Y * 0.22f);
        var top = new Rect2(inner.Position, new Vector2(inner.Size.X, inner.Size.Y - strip));
        DrawRect(new Rect2(top.Position, new Vector2(top.Size.X * pool, top.Size.Y)), guard);
        DrawRect(new Rect2(top.Position, new Vector2(top.Size.X * pool, top.Size.Y * 0.4f)), Colors.White with { A = 0.2f });
        var left = 1 - (float)_progress;
        DrawRect(new Rect2(inner.Position.X, inner.End.Y - strip, inner.Size.X * left, strip), _skin.S("ink_muted") with { A = 0.8f });
    }
}
