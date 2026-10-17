using Godot;

namespace HollowWake.Client.UI.Widgets;

/// <summary>A tab of a pane's strip: a leaf of the book the pane draws.</summary>
public sealed record TabInfo(string Id, string Title, bool Active, bool Fresh, string? Icon = null);

/// <summary>
/// A framed surface in the theme's style: the textured ground, the frame on top, a header with the title or the
/// book's tab strip and a close glyph, and a body for content. The header drags the pane when it is draggable.
/// </summary>
public partial class Pane : Control
{
    private readonly Skin _skin;
    private readonly float _seed;
    private ColorRect _surface = null!;
    private ShaderMaterial _material = null!;
    private PaneHeader _header = null!;
    private FrameOverlay _overlay = null!;

    public MarginContainer Body { get; private set; } = null!;
    public string Title { get; set; }
    public bool Closable { get; set; }
    public bool Draggable { get; set; }
    public bool Emphasised { get; set; }
    public float HeaderHeight { get; }
    public IReadOnlyList<TabInfo> Tabs { get; private set; } = Array.Empty<TabInfo>();

    public event Action? CloseRequested;
    public event Action<string>? TabSelected;
    public event Action<Vector2>? DragMoved;
    public event Action? DragEnded;

    public Pane(Skin skin, string title, bool closable = true, bool draggable = false, float? header = null)
    {
        _skin = skin;
        Title = title;
        Closable = closable;
        Draggable = draggable;
        HeaderHeight = header ?? skin.Metric("tab_height", 30);
        _seed = (float)(Math.Abs(title.GetHashCode()) % 997) / 31f;
        MouseFilter = MouseFilterEnum.Stop;
        Theme = skin.Theme;
    }

    public override void _Ready()
    {
        _material = _skin.SurfaceMaterial(Size, _seed);
        _surface = new ColorRect { Name = "surface", Material = _material, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_surface);
        var inset = _skin.Brass ? _skin.Mat("bevel", 4) + 4 : _skin.Mat("bevel", 3) + 3;
        _header = new PaneHeader(_skin, this) { Name = "header" };
        AddChild(_header);
        Body = new MarginContainer { Name = "body", MouseFilter = MouseFilterEnum.Pass };
        Body.AddThemeConstantOverride("margin_left", (int)inset + 6);
        Body.AddThemeConstantOverride("margin_right", (int)inset + 6);
        Body.AddThemeConstantOverride("margin_top", (int)(inset + HeaderHeight));
        Body.AddThemeConstantOverride("margin_bottom", (int)inset + 6);
        AddChild(Body);
        _overlay = new FrameOverlay(_skin, this) { Name = "frame", MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_overlay);
        Resized += Relayout;
        Relayout();
    }

    private void Relayout()
    {
        if (_surface is null)
        {
            return;
        }
        _surface.Position = Vector2.Zero;
        _surface.Size = Size;
        _skin.UpdateSurface(_material, Size, _seed);
        var inset = _skin.Brass ? _skin.Mat("bevel", 4) + 4 : _skin.Mat("bevel", 3) + 3;
        _header.Position = new Vector2(inset, inset);
        _header.Size = new Vector2(Math.Max(0, Size.X - inset * 2), HeaderHeight);
        Body.Position = Vector2.Zero;
        Body.Size = Size;
        _overlay.Position = Vector2.Zero;
        _overlay.Size = Size;
        _overlay.QueueRedraw();
        _header.QueueRedraw();
    }

    public void SetTabs(IReadOnlyList<TabInfo> tabs)
    {
        Tabs = tabs;
        _header?.QueueRedraw();
    }

    public void Refresh()
    {
        _header?.QueueRedraw();
        _overlay?.QueueRedraw();
    }

    public override void _Draw() => Paint.Shadow(this, new Rect2(Vector2.Zero, Size), _skin.Mat("shadow", 0.5f));

    internal void RaiseClose() => CloseRequested?.Invoke();
    internal void RaiseTab(string id) => TabSelected?.Invoke(id);
    internal void RaiseDrag(Vector2 delta) => DragMoved?.Invoke(delta);
    internal void RaiseDragEnded() => DragEnded?.Invoke();

    private sealed partial class FrameOverlay : Control
    {
        private readonly Skin _skin;
        private readonly Pane _pane;

        public FrameOverlay(Skin skin, Pane pane)
        {
            _skin = skin;
            _pane = pane;
        }

        public override void _Draw() => Paint.Frame(this, _skin, new Rect2(Vector2.Zero, Size), _pane.Emphasised);
    }
}

/// <summary>A pane's header: its title or its book's tabs, the close glyph, and the grip that drags the pane.</summary>
public partial class PaneHeader : Control
{
    private readonly Skin _skin;
    private readonly Pane _pane;
    private readonly List<(Rect2 Rect, string Id)> _tabRects = new();
    private Rect2 _close;
    private bool _dragging;
    private bool _hoverClose;
    private string? _hoverTab;

    public PaneHeader(Skin skin, Pane pane)
    {
        _skin = skin;
        _pane = pane;
        MouseFilter = MouseFilterEnum.Stop;
    }

    public override void _Draw()
    {
        var h = Size.Y;
        var ink = _skin.S("ink");
        var muted = _skin.S("ink_muted");
        var accent = _skin.S("accent");
        // the rule under the header
        DrawLine(new Vector2(4, h - 1), new Vector2(Size.X - 4, h - 1), _skin.S("edge_dark") with { A = 0.6f }, 1);
        DrawLine(new Vector2(4, h), new Vector2(Size.X - 4, h), _skin.S("edge") with { A = 0.25f }, 1);
        _tabRects.Clear();
        var right = Size.X;
        if (_pane.Closable)
        {
            var s = h - 10;
            _close = new Rect2(Size.X - s - 6, 5, s, s);
            DrawTextureRect(_skin.Icon("close", (int)s), _close, false, _hoverClose ? accent : muted);
            right = _close.Position.X - 6;
        }
        var titleSize = _skin.TitleSize;
        if (_pane.Tabs.Count >= 2)
        {
            var x = 6f;
            foreach (var tab in _pane.Tabs)
            {
                var w = Math.Min(170, _skin.Body.GetStringSize(tab.Title, HorizontalAlignment.Left, -1, _skin.BodySize).X + 24);
                var r = new Rect2(x, 3, w, h - 5);
                if (r.End.X > right)
                {
                    break;
                }
                if (tab.Active)
                {
                    DrawRect(r, _skin.S("pane_light", _skin.S("pane")) with { A = 0.9f });
                    DrawRect(new Rect2(r.Position.X, r.End.Y - 2, r.Size.X, 2), accent);
                }
                else
                {
                    DrawRect(r, _skin.S("tab", _skin.S("inset")) with { A = _hoverTab == tab.Id ? 0.95f : 0.7f });
                }
                DrawRect(r, _skin.S("edge_dark") with { A = 0.7f }, false, 1);
                if (tab.Fresh)
                {
                    var pulse = Motion.Reduced ? 1f : 0.55f + 0.45f * Mathf.Sin((float)Time.GetTicksMsec() / 260f);
                    DrawCircle(new Vector2(r.End.X - 7, r.Position.Y + 7), 3.2f, _skin.State("fresh", accent) with { A = pulse }, true, -1, true);
                }
                DrawString(_skin.Body, new Vector2(r.Position.X + 10, r.Position.Y + r.Size.Y * 0.68f), tab.Title, HorizontalAlignment.Left, r.Size.X - 18, _skin.BodySize, tab.Active ? ink : muted);
                _tabRects.Add((r, tab.Id));
                x += w + 3;
            }
            if (_pane.Tabs.Any(t => t.Fresh) && !Motion.Reduced)
            {
                QueueRedraw();
            }
        }
        else
        {
            DrawString(_skin.Title, new Vector2(8, h * 0.7f), _pane.Title, HorizontalAlignment.Left, right - 12, titleSize, ink);
            // two runes flank the title in the etched texture; the brass texture rules a line instead
            var tw = _skin.Title.GetStringSize(_pane.Title, HorizontalAlignment.Left, -1, titleSize).X;
            var etch = _skin.S("etch") with { A = _skin.Mat("etch", 0.5f) * 0.6f };
            if (tw + 40 < right)
            {
                Paint.Rune(this, new Vector2(tw + 22, h * 0.5f), 3.5f, etch);
            }
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseMotion motion:
                var hc = _pane.Closable && _close.HasPoint(motion.Position);
                var ht = _tabRects.FirstOrDefault(t => t.Rect.HasPoint(motion.Position)).Id;
                if (hc != _hoverClose || ht != _hoverTab)
                {
                    _hoverClose = hc;
                    _hoverTab = ht;
                    QueueRedraw();
                }
                if (_dragging)
                {
                    _pane.RaiseDrag(motion.Relative);
                }
                AcceptEvent();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
                if (button.Pressed)
                {
                    if (_pane.Closable && _close.HasPoint(button.Position))
                    {
                        _pane.RaiseClose();
                    }
                    else if (_tabRects.FirstOrDefault(t => t.Rect.HasPoint(button.Position)) is { Id: { } id })
                    {
                        _pane.RaiseTab(id);
                    }
                    else if (_pane.Draggable)
                    {
                        _dragging = true;
                    }
                }
                else if (_dragging)
                {
                    _dragging = false;
                    _pane.RaiseDragEnded();
                }
                AcceptEvent();
                break;
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationMouseExit && (_hoverClose || _hoverTab is not null))
        {
            _hoverClose = false;
            _hoverTab = null;
            QueueRedraw();
        }
    }
}
