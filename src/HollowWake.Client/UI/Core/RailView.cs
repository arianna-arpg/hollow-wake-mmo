using Godot;
using HollowWake.Client.UI.Logic;
using HollowWake.Client.UI.Widgets;
using HollowWake.Protocol;

namespace HollowWake.Client.UI.Core;

/// <summary>
/// The ribbon rail along the core pane's edge: a ribbon per ribbon or popout page the hero has, each with its
/// glyph and key, lit while its page is shown and wearing its pips; the wallet's chips below. When the pane is
/// too short the rail drops its badges first and its chips next.
/// </summary>
public partial class RailView : Control
{
    private readonly Skin _skin;
    private readonly List<(TrayRow Row, Rect2 Rect)> _ribbons = new();
    private IReadOnlyList<CurrencyInfo> _wallet = Array.Empty<CurrencyInfo>();
    private bool _badges = true;
    private bool _chips = true;
    private int _hover = -1;

    public event Action<string>? RibbonPressed;

    public RailView(Skin skin)
    {
        _skin = skin;
        MouseFilter = MouseFilterEnum.Stop;
    }

    public static float RibbonHeight(Skin skin) => skin.Metric("rail", 46) * 1.05f;
    public static float BadgeHeight => 12;
    public static float ChipHeight => 38;
    public static float Top => 40;

    public void Show(IEnumerable<TrayRow> rows, IReadOnlyList<CurrencyInfo> wallet, bool badges, bool chips)
    {
        _ribbons.Clear();
        var y = Top;
        var h = RibbonHeight(_skin) + (badges ? BadgeHeight : 0);
        foreach (var row in rows.Where(r => r.Page.OnRibbon && r.State != TrayState.Hidden))
        {
            _ribbons.Add((row, new Rect2(4, y, Size.X - 8, h - 4)));
            y += h;
        }
        _wallet = wallet;
        _badges = badges;
        _chips = chips;
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseMotion m:
                var i = _ribbons.FindIndex(r => r.Rect.HasPoint(m.Position));
                if (i != _hover)
                {
                    _hover = i;
                    TooltipText = i >= 0 ? Tip(_ribbons[i].Row) : WalletTip(m.Position);
                    QueueRedraw();
                }
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } b:
                var hit = _ribbons.FindIndex(r => r.Rect.HasPoint(b.Position));
                if (hit >= 0 && _ribbons[hit].Row.State == TrayState.Open)
                {
                    RibbonPressed?.Invoke(_ribbons[hit].Row.Page.Id);
                }
                AcceptEvent();
                break;
        }
    }

    private static string Tip(TrayRow row)
    {
        var key = row.KeyLabel.Length > 0 ? $" ({row.KeyLabel})" : "";
        return row.State == TrayState.Sealed ? $"{row.Page.Name}{key}: {row.Reason ?? "sealed here"}" : $"{row.Page.Name}{key}\n{row.Page.Description}";
    }

    private string WalletTip(Vector2 p) => string.Join("\n", _wallet.Select(w => $"{w.Name}: {Math.Round(w.Amount)}"));

    public override void _Notification(int what)
    {
        if (what == NotificationMouseExit && _hover != -1)
        {
            _hover = -1;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        var r = new Rect2(Vector2.Zero, Size);
        DrawRect(r, _skin.Brass ? _skin.S("edge").Darkened(0.45f) : _skin.S("pane").Darkened(0.25f));
        Paint.Frame(this, _skin, r);
        var label = _skin.S("label");
        var accent = _skin.S("accent");
        for (var i = 0; i < _ribbons.Count; i++)
        {
            var (row, rect) = _ribbons[i];
            var open = row.State == TrayState.Open;
            var showing = row.Showing;
            Paint.Well(this, _skin, rect, showing ? _skin.S("well").Lerp(accent, 0.25f) : _skin.S("well"), showing);
            if (i == _hover && open)
            {
                DrawRect(rect.Grow(-1), accent with { A = 0.45f }, false, 1);
            }
            var icon = Math.Min(rect.Size.X, RibbonHeight(_skin)) * 0.54f;
            var tint = !open ? _skin.State("sealed", label) : showing ? accent : label;
            DrawTextureRect(_skin.Icon(row.Page.Icon, (int)icon), new Rect2(rect.Position.X + (rect.Size.X - icon) / 2, rect.Position.Y + 5, icon, icon), false, tint);
            if (row.KeyLabel.Length > 0)
            {
                DrawString(_skin.Numerals, new Vector2(rect.Position.X, rect.Position.Y + icon + 5 + _skin.NumeralSize - 2), row.KeyLabel, HorizontalAlignment.Center, rect.Size.X, _skin.NumeralSize - 2, tint with { A = 0.8f });
            }
            if (_badges && row.Pips > 0 && open)
            {
                Paint.Badge(this, _skin, new Vector2(rect.End.X - 6, rect.Position.Y + 5), MenuFold.BadgeText(row.Pips), 7);
            }
        }
        if (!_chips)
        {
            return;
        }
        var y = Size.Y - 10 - _wallet.Count * ChipHeight;
        foreach (var w in _wallet)
        {
            var chip = new Rect2(4, y, Size.X - 8, ChipHeight - 4);
            Paint.Well(this, _skin, chip, _skin.S("well") with { A = 0.8f });
            var icon = 14f;
            DrawTextureRect(_skin.Icon("coin", (int)icon), new Rect2(chip.Position.X + (chip.Size.X - icon) / 2, chip.Position.Y + 3, icon, icon), false, accent);
            var amount = w.Amount >= 10000 ? $"{w.Amount / 1000:0.#}k" : $"{Math.Round(w.Amount)}";
            DrawString(_skin.Numerals, new Vector2(chip.Position.X, chip.End.Y - 5), amount, HorizontalAlignment.Center, chip.Size.X, _skin.NumeralSize - 2, label);
            y += ChipHeight;
        }
    }
}
