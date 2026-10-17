using Godot;
using HollowWake.Client.UI.Widgets;
using HollowWake.Protocol;

namespace HollowWake.Client.UI.Overlay;

/// <summary>
/// The target line and the target's plate at the top centre: the name in its rarity's colour, its level and
/// what it can be used for, its life with the pools before it, and its statuses, all from its entity state.
/// </summary>
public partial class TargetPlate : Control
{
    private readonly Skin _skin;
    private EntityState? _target;

    public TargetPlate(Skin skin)
    {
        _skin = skin;
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
    }

    public EntityState? Target => _target;

    /// <summary>The target line as text, for scripted checks.</summary>
    public string Line => _target is null ? "" : $"{_target.Name}{(_target.Level > 0 ? $" [{_target.Level}]" : "")}{(_target.Interactable is not null ? $"  ({_target.Interactable})" : "")}";

    public void Show(EntityState? target, Vector2 viewport, float scale)
    {
        _target = target;
        Visible = target is not null;
        if (target is null)
        {
            return;
        }
        var width = _skin.Metric("plate_width", 340) * scale;
        var hasLife = target.ResourceMax.GetValueOrDefault("life") > 0;
        var height = (hasLife ? 58 : 38) + (Shown(target).Count > 0 ? 22 : 0);
        Position = new Vector2((viewport.X - width) / 2, 10);
        Size = new Vector2(width, height * scale);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_target is not { } t)
        {
            return;
        }
        var r = new Rect2(Vector2.Zero, Size);
        Paint.Shadow(this, r, _skin.Mat("shadow", 0.5f), 5);
        DrawRect(r, _skin.S("pane") with { A = 0.94f });
        Paint.Frame(this, _skin, r, t.Rarity is "rare" or "unique" or "champion");
        var nameColor = t.Dead ? _skin.State("dormant", _skin.S("ink_muted")) : t.IsPlayer ? _skin.S("accent") : t.Rarity is not null ? _skin.Rarity(t.Rarity) : _skin.S("ink");
        var title = $"{t.Name}{(t.Level > 0 ? $"  {t.Level}" : "")}";
        DrawString(_skin.Title, new Vector2(0, 24), title, HorizontalAlignment.Center, Size.X, _skin.TitleSize, nameColor);
        if (t.Interactable is { } use)
        {
            DrawString(_skin.Body, new Vector2(0, 24 + _skin.BodySize + 2), use.Replace('_', ' '), HorizontalAlignment.Center, Size.X, _skin.BodySize - 2, _skin.S("ink_muted"));
        }
        var y = 34f;
        if (t.ResourceMax.TryGetValue("life", out var max) && max > 0)
        {
            var cur = t.Resources.GetValueOrDefault("life");
            var bar = new Rect2(14, y, Size.X - 28, 14);
            Paint.Well(this, _skin, bar, _skin.S("well"));
            var inner = bar.Grow(-2);
            DrawRect(new Rect2(inner.Position, new Vector2(inner.Size.X * Math.Clamp(cur / max, 0, 1), inner.Size.Y)), _skin.Fill("life"));
            DrawRect(new Rect2(inner.Position, new Vector2(inner.Size.X * Math.Clamp(cur / max, 0, 1), inner.Size.Y * 0.4f)), Colors.White with { A = 0.15f });
            if (t.ResourceMax.TryGetValue("ward", out var wmax) && wmax > 0)
            {
                var w = t.Resources.GetValueOrDefault("ward");
                DrawRect(new Rect2(inner.Position.X, inner.Position.Y, inner.Size.X * Math.Clamp(w / wmax, 0, 1), 3), _skin.Fill("ward"));
            }
            Paint.Text(this, _skin.Numerals, new Vector2(bar.Position.X, bar.End.Y - 2), $"{Math.Round(cur)} / {Math.Round(max)}", _skin.NumeralSize - 2, Colors.White, HorizontalAlignment.Center, bar.Size.X, 3);
            y = bar.End.Y + 4;
        }
        var shown = Shown(t);
        if (shown.Count > 0)
        {
            var size = 18f;
            var x = (Size.X - shown.Count * (size + 3)) / 2;
            foreach (var s in shown)
            {
                var (glyph, tint) = _skin.StatusFace(s);
                DrawTextureRect(_skin.Icon(glyph, (int)size), new Rect2(x, y, size, size), false, tint);
                x += size + 3;
            }
        }
    }

    /// <summary>The target's statuses content does not hide.</summary>
    private List<string> Shown(EntityState target) => target.Statuses.Where(s => !_skin.Names.LookOf(s).Hidden).ToList();
}
