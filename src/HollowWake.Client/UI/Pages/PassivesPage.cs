using Godot;
using HollowWake.Client.UI.Book;
using HollowWake.Client.UI.Logic;
using HollowWake.Client.UI.Widgets;
using HollowWake.Protocol;

namespace HollowWake.Client.UI.Pages;

/// <summary>
/// The passive constellation, read from passive content and drawn with the passive state: a tab per open
/// realm, the nodes panned with a drag and zoomed with the wheel, allocated nodes lit, choice nodes opening a
/// searchable popup of their options, and a refund mode while a font is in reach. Clicking sends the
/// allocate or refund intent; the server validates every one. Tooltips come from the same content the server
/// validates against.
/// </summary>
public partial class PassivesPage : LeafView
{
    private HBoxContainer _bar = null!;
    private VBoxContainer _grafts = null!;
    private Canvas _canvas = null!;
    private Button _refund = null!;
    private Label _points = null!;
    private ChoicePopup _popup = null!;
    private string _realm = "star";
    private bool _refundMode;
    private int _barVersion = -1;

    public PassivesPage(LeafContext context) : base(context)
    {
        Name = "PassivesPage";
    }

    public override float PreferredWidth => Skin.Metric("page_width", 460) * 1.15f;

    public string Realm => _realm;
    public bool RefundMode => _refundMode;

    public override void _Ready()
    {
        var column = new VBoxContainer();
        AddChild(column);
        column.SetAnchorsPreset(LayoutPreset.FullRect);
        _bar = new HBoxContainer();
        column.AddChild(_bar);
        _canvas = new Canvas(this) { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipContents = true };
        column.AddChild(_canvas);
        _grafts = new VBoxContainer();
        column.AddChild(_grafts);
        _points = MakeLabel("", Skin.S("accent"));
        _refund = MakeButton("Refund", () =>
        {
            _refundMode = !_refundMode;
            _refund.Text = _refundMode ? "Refunding" : "Refund";
            _canvas.QueueRedraw();
        });
        _popup = new ChoicePopup(this) { Visible = false };
        AddChild(_popup);
    }

    public override void Refresh()
    {
        var version = Hero.PassivesVersion * 7919 + Hero.Points("passive") * 31 + Hero.Allocated.Count;
        if (version != _barVersion)
        {
            _barVersion = version;
            RebuildBar();
            _canvas.QueueRedraw();
        }
    }

    private IEnumerable<string> RealmsOpen()
    {
        var realms = Hero.Passives?.Realms ?? new List<string> { "star" };
        return realms.Count == 0 ? new[] { "star" } : realms;
    }

    private void RebuildBar()
    {
        foreach (var child in _bar.GetChildren())
        {
            _bar.RemoveChild(child);
            if (child != _points && child != _refund)
            {
                child.QueueFree();
            }
        }
        var realms = RealmsOpen().ToList();
        if (!realms.Contains(_realm))
        {
            _realm = realms[0];
        }
        if (realms.Count > 1)
        {
            foreach (var realm in realms)
            {
                var id = realm;
                var tab = MakeButton(Skin.Names.RealmName(realm), () =>
                {
                    _realm = id;
                    _canvas.Refit();
                    RebuildBar();
                });
                tab.ToggleMode = true;
                tab.ButtonPressed = realm == _realm;
                _bar.AddChild(tab);
            }
        }
        var points = Hero.Passives?.Points ?? new Dictionary<string, int> { ["passive"] = Hero.Points("passive") };
        _points.Text = string.Join("   ", points.Select(kv => $"{kv.Value} {(kv.Key == "passive" ? "passive" : ContentNames.Humanize(kv.Key))} point{(kv.Value == 1 ? "" : "s")}"));
        _points.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _bar.AddChild(_points);
        var refund = Hero.Passives?.Refund == true;
        if (!refund && _refundMode)
        {
            _refundMode = false;
            _refund.Text = "Refund";
        }
        if (refund)
        {
            _refund.TooltipText = Hero.Passives?.RefundCost is { } cost ? $"Refund single nodes here for {cost}" : "Refund single nodes here";
            _bar.AddChild(_refund);
        }
        RebuildGrafts();
    }

    /// <summary>
    /// The grafts the passives grant: each glyph with its level and the node that grants it, and the weave seat
    /// it is bound to, picked from the seats; the server validates the binding.
    /// </summary>
    private void RebuildGrafts()
    {
        Clear(_grafts);
        var grafts = Hero.Passives?.Grafts ?? new List<GraftInfo>();
        if (grafts.Count == 0)
        {
            return;
        }
        _grafts.AddChild(MakeLabel("Grafts", Skin.S("accent"), font: Skin.Title));
        var seats = Hero.Seats((int)Skin.Metric("slots", 8));
        foreach (var graft in grafts)
        {
            var g = graft;
            var row = new HBoxContainer();
            var node = g.Source.Split(':')[0];
            var from = Skin.Names.Passives.FirstOrDefault(p => p.Id == node)?.Name ?? ContentNames.Humanize(node);
            var label = MakeLabel($"{g.Name} {g.Level}", Skin.S("ink"));
            label.TooltipText = $"Granted by {from}";
            label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(label);
            var picker = new OptionButton { FocusMode = FocusModeEnum.None };
            picker.AddItem("Unbound", 0);
            for (var i = 0; i < seats.Count; i++)
            {
                picker.AddItem($"Seat {i + 1}: {(seats[i].Empty ? "empty" : seats[i].Name)}", i + 1);
            }
            picker.Select(g.Slot is { } bound && bound >= 0 && bound < seats.Count ? bound + 1 : 0);
            picker.ItemSelected += index => Ctx.Send(new BindGraftMsg(g.Source, (int)index - 1));
            row.AddChild(picker);
            _grafts.AddChild(row);
        }
    }

    internal void Click(PassiveNode node)
    {
        var allocated = Hero.Allocated.Contains(node.Id);
        if (_refundMode)
        {
            if (allocated)
            {
                Ctx.Send(new RefundPassiveMsg(node.Id));
            }
            return;
        }
        if (allocated)
        {
            return;
        }
        if (node.Choice is { } group && Skin.Names.Choices.TryGetValue(group, out var choices))
        {
            _popup.Open(node, choices, GetLocalMousePosition());
            return;
        }
        Ctx.Send(new AllocatePassiveMsg(node.Id));
    }

    internal void Pick(PassiveNode node, ChoiceOption option)
    {
        _popup.Visible = false;
        Ctx.Send(new AllocatePassiveMsg(node.Id) { Option = option.Id });
    }

    internal Skin S => Skin;
    internal HeroState H => Hero;

    /// <summary>The constellation itself.</summary>
    private sealed partial class Canvas : Control
    {
        private readonly PassivesPage _page;
        private PanZoom? _view;
        private string _fittedRealm = "";
        private Vector2 _fittedSize;
        private PassiveNode? _hover;
        private bool _dragging;
        private Vector2 _pressAt;

        public Canvas(PassivesPage page)
        {
            _page = page;
            MouseFilter = MouseFilterEnum.Stop;
        }

        private IEnumerable<PassiveNode> Nodes => _page.S.Names.Passives.Where(n => n.Realm == _page._realm);

        public void Refit() => _fittedRealm = "";

        private PanZoom View()
        {
            var skin = _page.S;
            if (_view is null || _fittedRealm != _page._realm || _fittedSize != Size)
            {
                _view ??= new PanZoom(skin.Metric("passive_scale", 26), skin.Metric("zoom_min", 0.4f), skin.Metric("zoom_max", 2.5f));
                var nodes = Nodes.ToList();
                if (nodes.Count > 0 && Size.X > 10)
                {
                    _view.Fit(nodes.Min(n => n.X), nodes.Min(n => n.Y), nodes.Max(n => n.X), nodes.Max(n => n.Y), Size.X, Size.Y);
                }
                _fittedRealm = _page._realm;
                _fittedSize = Size;
            }
            return _view;
        }

        private float Radius(PassiveNode n)
        {
            var b = _page.S.Metric("passive_node", 14) * 0.5f * Math.Clamp(View().Zoom, 0.6f, 1.6f);
            return n.Kind switch
            {
                "keystone" => b * 1.7f,
                "notable" => b * 1.35f,
                "start" => b * 1.5f,
                "attribute" => b * 0.85f,
                _ => b,
            };
        }

        private PassiveNode? NodeAt(Vector2 p)
        {
            var view = View();
            PassiveNode? best = null;
            var bestD = float.MaxValue;
            foreach (var n in Nodes)
            {
                var (x, y) = view.ToScreen(n.X, n.Y);
                var d = new Vector2(x, y).DistanceTo(p);
                if (d <= Radius(n) + 4 && d < bestD)
                {
                    best = n;
                    bestD = d;
                }
            }
            return best;
        }

        public override void _GuiInput(InputEvent @event)
        {
            switch (@event)
            {
                case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true } up:
                    View().ZoomAt(up.Position.X, up.Position.Y, 1.12f);
                    QueueRedraw();
                    AcceptEvent();
                    break;
                case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true } down:
                    View().ZoomAt(down.Position.X, down.Position.Y, 1 / 1.12f);
                    QueueRedraw();
                    AcceptEvent();
                    break;
                case InputEventMouseButton { ButtonIndex: MouseButton.Left } b:
                    if (b.Pressed)
                    {
                        _dragging = true;
                        _pressAt = b.Position;
                    }
                    else
                    {
                        _dragging = false;
                        if (b.Position.DistanceTo(_pressAt) < 5 && NodeAt(b.Position) is { } node)
                        {
                            _page.Click(node);
                        }
                    }
                    AcceptEvent();
                    break;
                case InputEventMouseMotion m:
                    if (_dragging && m.ButtonMask.HasFlag(MouseButtonMask.Left))
                    {
                        View().Pan(m.Relative.X, m.Relative.Y);
                        QueueRedraw();
                    }
                    var hover = NodeAt(m.Position);
                    if (hover != _hover)
                    {
                        _hover = hover;
                        TooltipText = hover is null ? "" : Tip(hover);
                        QueueRedraw();
                    }
                    break;
            }
        }

        private string Tip(PassiveNode n)
        {
            var lines = new List<string> { $"{n.Name}  ({n.Kind})" };
            lines.AddRange(n.Lines);
            if (n.Choice is { } g && _page.S.Names.Choices.TryGetValue(g, out var group))
            {
                lines.Add($"Choose {group.Pick} of {group.Name}");
            }
            if (_page.H.Passives?.Picks.TryGetValue(n.Id, out var picks) == true && picks.Count > 0)
            {
                lines.Add("Picked: " + string.Join(", ", picks.Select(p => _page.S.Names.Choices.Values.SelectMany(c => c.Options).FirstOrDefault(o => o.Id == p)?.Name ?? p)));
            }
            if (n.Cost != 1)
            {
                lines.Add($"Costs {n.Cost} points");
            }
            return string.Join("\n", lines);
        }

        public override void _Draw()
        {
            var skin = _page.S;
            var view = View();
            var allocated = _page.H.Allocated.ToHashSet();
            var nodes = Nodes.ToDictionary(n => n.Id);
            var accent = skin.S("accent");
            var dim = skin.S("ink_muted") with { A = 0.45f };
            DrawRect(new Rect2(Vector2.Zero, Size), skin.S("well") with { A = 0.55f });
            // faint concentric guides in the etched style
            var (ox, oy) = view.ToScreen(0, 0);
            for (var k = 1; k <= 4; k++)
            {
                DrawArc(new Vector2(ox, oy), k * 8 * view.Scale * view.Zoom, 0, Mathf.Tau, 96, skin.S("etch") with { A = 0.06f }, 1, true);
            }
            var reachable = new HashSet<string>();
            foreach (var n in nodes.Values)
            {
                foreach (var link in n.Links)
                {
                    if (allocated.Contains(n.Id) && !allocated.Contains(link))
                    {
                        reachable.Add(link);
                    }
                    if (allocated.Contains(link) && !allocated.Contains(n.Id))
                    {
                        reachable.Add(n.Id);
                    }
                    if (!nodes.TryGetValue(link, out var other) || string.CompareOrdinal(n.Id, link) > 0 && other.Links.Contains(n.Id))
                    {
                        continue;
                    }
                    var (ax, ay) = view.ToScreen(n.X, n.Y);
                    var (bx, by) = view.ToScreen(other.X, other.Y);
                    var both = allocated.Contains(n.Id) && allocated.Contains(link);
                    DrawLine(new Vector2(ax, ay), new Vector2(bx, by), both ? accent : dim, both ? 3 : 1.5f, true);
                }
            }
            foreach (var n in nodes.Values)
            {
                var (x, y) = view.ToScreen(n.X, n.Y);
                var c = new Vector2(x, y);
                if (x < -40 || y < -40 || x > Size.X + 40 || y > Size.Y + 40)
                {
                    continue;
                }
                var r = Radius(n);
                var isAllocated = allocated.Contains(n.Id);
                var fill = isAllocated ? accent : reachable.Contains(n.Id) ? skin.S("pane_light", skin.S("inset")) : skin.S("inset");
                var rim = isAllocated ? accent.Lightened(0.3f) : reachable.Contains(n.Id) ? skin.S("ink") : skin.S("edge");
                if (n.Kind is "keystone")
                {
                    var pts = Enumerable.Range(0, 7).Select(i => c + Vector2.FromAngle(Mathf.Tau * i / 6 - Mathf.Pi / 2) * r).ToArray();
                    DrawColoredPolygon(pts, fill);
                    DrawPolyline(pts, rim, 2, true);
                }
                else if (n.Kind is "attribute")
                {
                    var pts = new[] { c + new Vector2(0, -r), c + new Vector2(r, 0), c + new Vector2(0, r), c + new Vector2(-r, 0), c + new Vector2(0, -r) };
                    DrawColoredPolygon(pts, fill);
                    DrawPolyline(pts, rim, 1.5f, true);
                }
                else
                {
                    DrawCircle(c, r + 1.5f, skin.S("edge_dark"), true, -1, true);
                    DrawCircle(c, r, fill, true, -1, true);
                    DrawArc(c, r, 0, Mathf.Tau, 32, rim, n.Kind is "notable" or "start" ? 2.5f : 1.5f, true);
                    if (n.Kind is "notable" or "start")
                    {
                        DrawArc(c, r + 4, 0, Mathf.Tau, 32, rim with { A = 0.5f }, 1, true);
                    }
                }
                if (n.Choice is not null)
                {
                    DrawArc(c, r + 7, 0, Mathf.Tau, 32, skin.S("accent") with { A = 0.6f }, 1, true);
                }
                if (_page.RefundMode && isAllocated)
                {
                    DrawArc(c, r + 5, 0, Mathf.Tau, 32, skin.S("bad", Colors.Red), 2, true);
                }
                if (n == _hover)
                {
                    DrawArc(c, r + 3, 0, Mathf.Tau, 32, Colors.White with { A = 0.8f }, 1.5f, true);
                }
                if (n.Kind is "notable" or "keystone" or "start" && view.Zoom > 0.7f)
                {
                    Paint.Text(this, skin.Body, new Vector2(c.X - 80, c.Y + r + skin.BodySize), n.Name, Math.Max(10, skin.BodySize - 3), skin.S("label"), HorizontalAlignment.Center, 160, 3);
                }
            }
        }
    }

    /// <summary>The searchable popup of a choice node's options.</summary>
    private sealed partial class ChoicePopup : Control
    {
        private readonly PassivesPage _page;
        private PassiveNode? _node;
        private ChoiceGroup? _group;
        private LineEdit _search = null!;
        private VBoxContainer _list = null!;
        private ColorRect _back = null!;
        private FrameLayer _frame = null!;

        public ChoicePopup(PassivesPage page)
        {
            _page = page;
            MouseFilter = MouseFilterEnum.Stop;
        }

        public override void _Ready()
        {
            _back = new ColorRect { Color = _page.S.S("pane"), MouseFilter = MouseFilterEnum.Ignore };
            AddChild(_back);
            var column = new VBoxContainer { Position = new Vector2(12, 12) };
            AddChild(column);
            _search = new LineEdit { PlaceholderText = "Search the options", CustomMinimumSize = new Vector2(240, 0) };
            _search.TextChanged += _ => Fill();
            column.AddChild(_search);
            var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(260, 220), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            column.AddChild(scroll);
            _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            scroll.AddChild(_list);
            column.AddChild(_page.MakeButton("Cancel", () => Visible = false));
            _frame = new FrameLayer(_page.S);
            AddChild(_frame);
        }

        public void Open(PassiveNode node, ChoiceGroup group, Vector2 at)
        {
            _node = node;
            _group = group;
            _search.Text = "";
            Size = new Vector2(286, 316);
            Position = new Vector2(Math.Clamp(at.X, 0, Math.Max(0, _page.Size.X - Size.X)), Math.Clamp(at.Y, 0, Math.Max(0, _page.Size.Y - Size.Y)));
            _back.Size = Size;
            _frame.Fit(Size);
            Visible = true;
            Fill();
            _search.GrabFocus();
        }

        private void Fill()
        {
            foreach (var child in _list.GetChildren())
            {
                child.QueueFree();
            }
            if (_group is null || _node is null)
            {
                return;
            }
            var query = _search.Text.Trim();
            foreach (var option in _group.Options)
            {
                if (query.Length > 0 && !option.Name.Contains(query, StringComparison.OrdinalIgnoreCase) && !option.Lines.Any(l => l.Contains(query, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
                var o = option;
                var button = _page.MakeButton(option.Name, () => _page.Pick(_node, o), tip: string.Join("\n", option.Lines));
                button.Alignment = HorizontalAlignment.Left;
                _list.AddChild(button);
            }
        }
    }

    internal new Button MakeButton(string text, Action pressed, bool enabled = true, string? tip = null) => base.MakeButton(text, pressed, enabled, tip);
}
