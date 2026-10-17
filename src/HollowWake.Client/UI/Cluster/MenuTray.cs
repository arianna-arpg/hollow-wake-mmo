using Godot;
using HollowWake.Client.UI.Logic;
using HollowWake.Client.UI.Widgets;

namespace HollowWake.Client.UI.Cluster;

/// <summary>
/// The menu button beside the mana orb and the tray it fans: every page the hero has, in order, each with its
/// glyph, name and key, greyed with the reason where it cannot be used from here, and wearing a pip count when
/// something on it is unspent. While the tray is closed the button wears the sum of the pips: the button is
/// the tell. The tray rebuilds only when its rows change.
/// </summary>
public partial class MenuButton : Control
{
    private readonly Skin _skin;
    private int _badge;
    private bool _open;
    private bool _hover;

    public event Action? Pressed;

    public MenuButton(Skin skin)
    {
        _skin = skin;
        MouseFilter = MouseFilterEnum.Stop;
        TooltipText = "Menu";
    }

    public void Update(ClusterLayout layout, int badge, bool open, string keyLabel)
    {
        Position = new Vector2(layout.Menu.X, layout.Menu.Y);
        Size = new Vector2(layout.Menu.W, layout.Menu.H);
        TooltipText = keyLabel.Length > 0 ? $"Menu ({keyLabel})" : "Menu";
        if (badge != _badge || open != _open)
        {
            _badge = badge;
            _open = open;
            QueueRedraw();
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            Pressed?.Invoke();
            AcceptEvent();
        }
    }

    public override void _Notification(int what)
    {
        if (what is (int)NotificationMouseEnter or (int)NotificationMouseExit)
        {
            _hover = what == NotificationMouseEnter;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        var r = new Rect2(Vector2.Zero, Size);
        Paint.Shadow(this, r, _skin.Mat("shadow", 0.5f), 5);
        DrawRect(r, _skin.Brass ? _skin.S("edge").Darkened(0.35f) : _skin.S("pane"));
        Paint.Frame(this, _skin, r, _open || _hover);
        var icon = Size.X * 0.56f;
        var tint = _open ? _skin.S("accent") : _skin.S("label");
        DrawTextureRect(_skin.Icon("menu", (int)icon), new Rect2((Size - new Vector2(icon, icon)) / 2, new Vector2(icon, icon)), false, tint);
        if (!_open && _badge > 0)
        {
            Paint.Badge(this, _skin, new Vector2(Size.X - 4, 4), MenuFold.BadgeText(_badge));
        }
    }
}

/// <summary>The tray of pages the menu button fans upward.</summary>
public partial class MenuTray : Control
{
    private readonly Skin _skin;
    private List<TrayRow> _rows = new();
    private string _signature = "";
    private int _hover = -1;
    private float _rowH;

    public event Action<string>? Picked;

    public MenuTray(Skin skin)
    {
        _skin = skin;
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
    }

    public IReadOnlyList<TrayRow> Rows => _rows;

    /// <summary>Seats the tray above the button and inside the viewport, and takes new rows when they changed.</summary>
    public void Update(ClusterLayout layout, Vector2 viewport, List<TrayRow> rows)
    {
        var signature = MenuFold.Signature(rows);
        var visible = rows.Where(r => r.State != TrayState.Hidden).ToList();
        if (signature != _signature)
        {
            _signature = signature;
            _rows = visible;
            QueueRedraw();
        }
        _rowH = _skin.Metric("tray_row", 34) * layout.Scale;
        var width = _skin.Metric("tray_width", 270) * layout.Scale;
        var height = _rows.Count * _rowH + 20;
        var x = Math.Clamp(layout.Menu.Right - width, 8, Math.Max(8, viewport.X - width - 8));
        var y = layout.Menu.Y - height - 8;
        Position = new Vector2(x, Math.Max(8, y));
        Size = new Vector2(width, height);
    }

    private int RowAt(Vector2 p)
    {
        var i = (int)((p.Y - 10) / _rowH);
        return p.Y < 10 || i < 0 || i >= _rows.Count ? -1 : i;
    }

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseMotion m:
                var hover = RowAt(m.Position);
                if (hover != _hover)
                {
                    _hover = hover;
                    TooltipText = hover >= 0 ? RowTip(_rows[hover]) : "";
                    QueueRedraw();
                }
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } b:
                var i = RowAt(b.Position);
                if (i >= 0 && _rows[i].State == TrayState.Open)
                {
                    Picked?.Invoke(_rows[i].Page.Id);
                }
                AcceptEvent();
                break;
        }
    }

    private static string RowTip(TrayRow row) =>
        row.State == TrayState.Sealed ? $"{row.Page.Name}: {row.Reason ?? "sealed here"}" : row.Page.Description;

    public override void _Notification(int what)
    {
        if (what == (int)NotificationMouseExit && _hover != -1)
        {
            _hover = -1;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        var r = new Rect2(Vector2.Zero, Size);
        Paint.Shadow(this, r, _skin.Mat("shadow", 0.5f));
        DrawRect(r, _skin.S("pane"));
        Paint.Frame(this, _skin, r);
        var ink = _skin.S("ink");
        var muted = _skin.S("ink_muted");
        var sealedTint = _skin.State("sealed", muted);
        for (var i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            var rr = new Rect2(8, 10 + i * _rowH, Size.X - 16, _rowH - 2);
            if (i == _hover && row.State == TrayState.Open)
            {
                DrawRect(rr, _skin.S("hover", _skin.S("pane_light")));
            }
            if (row.Showing)
            {
                DrawRect(new Rect2(rr.Position, new Vector2(3, rr.Size.Y)), _skin.S("accent"));
            }
            var open = row.State == TrayState.Open;
            var icon = rr.Size.Y * 0.66f;
            DrawTextureRect(_skin.Icon(row.Page.Icon, (int)icon), new Rect2(rr.Position.X + 8, rr.Position.Y + (rr.Size.Y - icon) / 2, icon, icon), false, open ? _skin.S("accent") : sealedTint);
            var text = (int)Math.Max(11, _skin.BodySize * Math.Min(1f, _rowH / 34f));
            DrawString(_skin.Body, new Vector2(rr.Position.X + icon + 18, rr.Position.Y + rr.Size.Y * 0.66f), row.Page.Name, HorizontalAlignment.Left, rr.Size.X - icon - 80, text, open ? ink : sealedTint);
            if (row.KeyLabel.Length > 0)
            {
                DrawString(_skin.Numerals, new Vector2(rr.Position.X, rr.Position.Y + rr.Size.Y * 0.66f), row.KeyLabel, HorizontalAlignment.Right, rr.Size.X - 8, text - 1, muted);
            }
            if (row.Pips > 0 && open)
            {
                Paint.Badge(this, _skin, new Vector2(rr.End.X - 46, rr.Position.Y + rr.Size.Y / 2), MenuFold.BadgeText(row.Pips), 8);
            }
            if (!open)
            {
                DrawTextureRect(_skin.Icon("lock", (int)(icon * 0.7f)), new Rect2(rr.End.X - 64, rr.Position.Y + (rr.Size.Y - icon * 0.7f) / 2, icon * 0.7f, icon * 0.7f), false, sealedTint);
            }
        }
    }
}
