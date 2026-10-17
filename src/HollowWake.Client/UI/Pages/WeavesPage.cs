using Godot;
using HollowWake.Client.UI.Book;
using HollowWake.Client.UI.Core;
using HollowWake.Client.UI.Widgets;
using HollowWake.Protocol;

namespace HollowWake.Client.UI.Pages;

/// <summary>
/// The rack: every weave seat drawn, in bar order. A learned form sits in its seat with its glyph sockets as
/// pips; a stone carried onto a pip sockets it and a form item onto a seat learns it; right-click takes a stone
/// or a form back out. A misfit glyph stays seated, greyed, with the reason in its tooltip. Seats reorder by
/// dragging one onto another. The preview lines are the server's.
/// </summary>
public partial class WeavesPage : LeafView
{
    private readonly List<SeatRow> _rows = new();
    private VBoxContainer _list = null!;
    private int _version = -1;

    public WeavesPage(LeafContext context) : base(context)
    {
        Name = "WeavesPage";
    }

    public override void _Ready()
    {
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(scroll);
        scroll.SetAnchorsPreset(LayoutPreset.FullRect);
        _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_list);
    }

    public override IEnumerable<IDropTarget> DropTargets => _rows;

    public int SeatCount => _rows.Count;

    public override void Refresh()
    {
        var count = Math.Max(Hero.Slots?.Seats.Count ?? 0, (int)Skin.Metric("slots", 8));
        var version = Hero.SlotsVersion * 1000 + (Hero.Self is null ? 0 : 1);
        if (version != _version)
        {
            _version = version;
            var seats = Hero.Seats(count);
            while (_rows.Count < seats.Count)
            {
                var row = new SeatRow(this, _rows.Count);
                _list.AddChild(row);
                _rows.Add(row);
            }
            for (var i = 0; i < _rows.Count; i++)
            {
                _rows[i].Visible = i < seats.Count;
                if (i < seats.Count)
                {
                    _rows[i].Show(seats[i]);
                }
            }
        }
        foreach (var row in _rows)
        {
            row.Tick(Ctx.Now);
        }
    }

    internal LeafContext Context => Ctx;
    internal Skin S => Skin;

    /// <summary>One seat of the rack.</summary>
    private sealed partial class SeatRow : Control, IDropTarget
    {
        private readonly WeavesPage _page;
        private readonly int _index;
        private HeroState.Seat? _seat;
        private double _now;
        private int _dropSocket = -2;
        private string _dropHint = "";

        public SeatRow(WeavesPage page, int index)
        {
            _page = page;
            _index = index;
            MouseFilter = MouseFilterEnum.Stop;
            CustomMinimumSize = new Vector2(0, 78);
        }

        private Skin Skin => _page.S;
        private float Glyph => 52;

        public void Show(HeroState.Seat seat)
        {
            _seat = seat;
            TooltipText = Tip(seat);
            QueueRedraw();
        }

        public void Tick(double now)
        {
            if (_seat is { } s && (s.ReadyIn(now) > 0 || s.ReadyIn(_now) > 0))
            {
                QueueRedraw();
            }
            _now = now;
        }

        private static string Tip(HeroState.Seat seat)
        {
            if (seat.Empty)
            {
                return "An empty seat: drag a form item here to learn it.";
            }
            var lines = new List<string> { seat.Name };
            if (seat.Dormant is not null)
            {
                lines.Add("Dormant: " + seat.Dormant);
            }
            foreach (var g in seat.Info?.Glyphs ?? new List<SeatGlyphInfo>())
            {
                lines.Add($"{g.Name} {g.Level}{(g.Dormant is null ? "" : "  (dormant: " + g.Dormant + ")")}");
            }
            return string.Join("\n", lines);
        }

        private Rect2 GlyphRect => new(28, (Size.Y - Glyph) / 2, Glyph, Glyph);

        private Rect2 SocketRect(int i) => new(GlyphRect.End.X + 12 + i * 22, Size.Y - 26, 18, 18);

        private int SocketAt(Vector2 local)
        {
            var sockets = _seat?.Info?.Sockets ?? 0;
            for (var i = 0; i < sockets; i++)
            {
                if (SocketRect(i).Grow(3).HasPoint(local))
                {
                    return i;
                }
            }
            return -1;
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } b && _seat is { Empty: false } seat)
            {
                var socket = SocketAt(b.Position);
                var actions = new List<(string, Action)>();
                var glyph = seat.Info?.Glyphs.FirstOrDefault(g => g.Socket == socket);
                if (socket >= 0 && glyph is not null)
                {
                    actions.Add(($"Take out {glyph.Name}", () => _page.Context.Send(new UnsocketStoneMsg(_index, socket))));
                }
                else if (GlyphRect.HasPoint(b.Position))
                {
                    actions.Add(($"Unlearn {seat.Name}", () => _page.Context.Send(new UnlearnFormMsg(_index))));
                }
                if (actions.Count > 0)
                {
                    _page.Context.Menu(GetGlobalMousePosition(), actions);
                }
                AcceptEvent();
            }
        }

        // Seats reorder with the engine's own drag and drop: drag a seat by its glyph onto another seat.
        public override Variant _GetDragData(Vector2 atPosition)
        {
            if (_seat is null || _seat.Empty || !GlyphRect.HasPoint(atPosition))
            {
                return default;
            }
            var preview = new SeatPreview(Skin, _seat);
            SetDragPreview(preview);
            return _index;
        }

        public override bool _CanDropData(Vector2 atPosition, Variant data) => data.VariantType == Variant.Type.Int && data.AsInt32() != _index;

        public override void _DropData(Vector2 atPosition, Variant data) => _page.Context.Send(new SwapWeaveSlotsMsg(data.AsInt32(), _index));

        // ---- carried stones and forms

        public bool Covers(Vector2 point) => IsVisibleInTree() && GetGlobalRect().HasPoint(point);

        public void Hover(Carry carry, Vector2 point)
        {
            var local = point - GlobalPosition;
            var socket = carry.Item.Glyph is not null ? SocketAt(local) : -1;
            var hint = carry.Item.Glyph is not null
                ? FitHint(carry.Item)
                : carry.Item.Form is not null ? "learn" : "";
            if (socket != _dropSocket || hint != _dropHint)
            {
                _dropSocket = socket;
                _dropHint = hint;
                QueueRedraw();
            }
        }

        /// <summary>The fit the server sent on the stone's card for this seat.</summary>
        private string FitHint(ItemInfo stone) => stone.Fits?.FirstOrDefault(f => f.Slot == _index)?.Fit ?? "unknown";

        public void Leave()
        {
            if (_dropSocket != -2 || _dropHint.Length > 0)
            {
                _dropSocket = -2;
                _dropHint = "";
                QueueRedraw();
            }
        }

        public bool Drop(Carry carry, Vector2 point)
        {
            var local = point - GlobalPosition;
            Leave();
            if (carry.Item.Form is not null)
            {
                _page.Context.Send(new LearnFormMsg(carry.Item.Uid, _index));
                return true;
            }
            if (carry.Item.Glyph is not null)
            {
                var socket = SocketAt(local);
                if (socket < 0)
                {
                    // Dropped on the seat itself: the first free socket.
                    var taken = _seat?.Info?.Glyphs.Select(g => g.Socket).ToHashSet() ?? new HashSet<int>();
                    var sockets = _seat?.Info?.Sockets ?? 0;
                    socket = Enumerable.Range(0, sockets).FirstOrDefault(i => !taken.Contains(i), -1);
                    if (socket < 0)
                    {
                        socket = 0;
                    }
                }
                _page.Context.Send(new SocketStoneMsg(carry.Item.Uid, _index, socket));
                return true;
            }
            return false;
        }

        public override void _Draw()
        {
            var r = new Rect2(Vector2.Zero, Size);
            var seat = _seat;
            var ink = Skin.S("ink");
            var muted = Skin.S("ink_muted");
            var dormant = seat?.Dormant is not null;
            DrawRect(r, Skin.S("inset") with { A = 0.45f });
            DrawRect(r, Skin.S("edge_dark") with { A = 0.5f }, false, 1);
            if (_dropHint.Length > 0)
            {
                var color = _dropHint switch
                {
                    "fits" or "learn" => Skin.State("fit", Colors.Green),
                    "misfit" => Skin.State("swap", Colors.Yellow),
                    _ => Skin.State("blocked", Colors.Red),
                };
                DrawRect(r.Grow(-1), color, false, 2);
            }
            DrawString(Skin.Numerals, new Vector2(8, Size.Y / 2 + 6), (_index + 1).ToString(), HorizontalAlignment.Left, -1, Skin.NumeralSize + 2, muted);
            var glyphRect = GlyphRect;
            Paint.Well(this, Skin, glyphRect, Skin.S("well"));
            if (seat is null || seat.Empty)
            {
                DrawString(Skin.Body, new Vector2(glyphRect.End.X + 12, Size.Y / 2 + 5), "Empty seat", HorizontalAlignment.Left, -1, Skin.BodySize, muted);
                return;
            }
            var tint = dormant ? Skin.State("dormant", muted) : seat.Threads.Count > 0 ? Skin.Thread(seat.Threads[0]) : Skin.S("label");
            var icon = Skin.IconSet.ForWeave(seat.Converted?.Icon ?? seat.Icon, seat.Key);
            DrawTextureRect(Skin.Icon(icon, (int)(Glyph * 0.7f)), glyphRect.Grow(-Glyph * 0.15f), false, tint);
            var left = seat.ReadyIn(_now);
            if (left > 0.05 && seat.Cooldown > 0)
            {
                Paint.Sweep(this, glyphRect.Grow(-2), (float)Math.Clamp(left / seat.Cooldown, 0, 1), Skin.State("cooldown", Colors.Black) with { A = 0.6f });
            }
            var x = glyphRect.End.X + 12;
            var nameColor = dormant ? Skin.State("dormant", muted) : seat.Threads.Count > 0 ? Skin.Thread(seat.Threads[0]) : ink;
            DrawString(Skin.Title, new Vector2(x, 24), seat.Name, HorizontalAlignment.Left, Size.X * 0.5f, Skin.TitleSize - 1, nameColor);
            var info = seat.Info;
            var meta = info is null ? "" : string.Join("  ", new[]
            {
                $"Level {info.Level}",
                info.Form?.Replace('_', ' '),
                info.CastMode,
                info.Active == true ? "on" : null,
                info.UsesMax is > 1 ? $"{info.Uses ?? 0}/{info.UsesMax} uses" : null,
                info.Stacks is > 0 ? $"{info.Stacks} stacks" : null,
                info.Meta is { } m ? "second: " + m.Label : null,
            }.Where(t => !string.IsNullOrEmpty(t)));
            if (dormant)
            {
                meta = "Dormant: " + seat.Dormant;
            }
            DrawString(Skin.Body, new Vector2(x, 42), meta, HorizontalAlignment.Left, Size.X * 0.55f, Skin.BodySize - 2, dormant ? Skin.S("bad", muted) : muted);
            // the sockets as pips
            var sockets = info?.Sockets ?? 0;
            for (var i = 0; i < sockets; i++)
            {
                var sr = SocketRect(i);
                var glyph = info!.Glyphs.FirstOrDefault(g => g.Socket == i);
                var c = sr.GetCenter();
                DrawCircle(c, 8.5f, Skin.S("edge_dark"), true, -1, true);
                DrawCircle(c, 7f, Skin.S("well"), true, -1, true);
                if (glyph is not null)
                {
                    var color = glyph.Dormant is null ? Skin.S("accent") : Skin.State("dormant", muted);
                    DrawCircle(c, 5.5f, color, true, -1, true);
                    DrawString(Skin.Numerals, new Vector2(sr.Position.X, c.Y + 4), glyph.Name.Length > 0 ? glyph.Name[..1] : "?", HorizontalAlignment.Center, sr.Size.X, 10, Skin.S("well"));
                }
                if (i == _dropSocket)
                {
                    DrawArc(c, 10, 0, Mathf.Tau, 24, Skin.State("fit", Colors.Green), 2, true);
                }
            }
            // the server's preview lines in the right column
            var px = Size.X * 0.6f;
            var py = 16f;
            foreach (var line in (info?.Preview ?? new List<PreviewLineInfo>()).Take(4))
            {
                DrawString(Skin.Body, new Vector2(px, py), line.Label, HorizontalAlignment.Left, Size.X * 0.2f, Skin.BodySize - 3, muted);
                DrawString(Skin.Numerals, new Vector2(px, py), line.Text, HorizontalAlignment.Right, Size.X - px - 8, Skin.NumeralSize - 1, ink);
                py += Skin.BodySize + 1;
            }
        }
    }

    /// <summary>What a dragged seat looks like under the pointer.</summary>
    private sealed partial class SeatPreview : Control
    {
        private readonly Skin _skin;
        private readonly HeroState.Seat _seat;

        public SeatPreview(Skin skin, HeroState.Seat seat)
        {
            _skin = skin;
            _seat = seat;
            Size = new Vector2(52, 52);
        }

        public override void _Draw()
        {
            var r = new Rect2(-26, -26, 52, 52);
            Paint.Well(this, _skin, r, _skin.S("well") with { A = 0.9f }, true);
            var tint = _seat.Threads.Count > 0 ? _skin.Thread(_seat.Threads[0]) : _skin.S("label");
            DrawTextureRect(_skin.Icon(_skin.IconSet.ForWeave(_seat.Icon, _seat.Key), 36), r.Grow(-8), false, tint);
        }
    }
}
