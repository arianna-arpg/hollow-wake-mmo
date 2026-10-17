using Godot;
using HollowWake.Client.UI.Logic;
using HollowWake.Client.UI.Widgets;

namespace HollowWake.Client.UI.Cluster;

/// <summary>
/// The weave bar between the orbs: the weave slots in rack order, each with its glyph tinted by its Thread,
/// the cooldown sweep drawn against the full cooldown the server sent, the cost in its resource's colour, use
/// pips, a converted or meta face when the slot has one, and a greyed, hatched face with its reason when the
/// weave is dormant. A click casts the slot as its key would.
/// </summary>
public partial class WeaveBar : Control
{
    private readonly Skin _skin;
    private readonly List<SlotView> _slots = new();
    private ClusterLayout? _layout;

    public event Action<int>? SlotPressed;

    public WeaveBar(Skin skin)
    {
        _skin = skin;
        MouseFilter = MouseFilterEnum.Stop;
    }

    public void Update(ClusterLayout layout, HeroState hero, double now)
    {
        _layout = layout;
        Position = new Vector2(layout.Bar.X, layout.Bar.Y);
        Size = new Vector2(layout.Bar.W, layout.Bar.H);
        var seats = hero.Seats(layout.Slots.Count);
        var cast = hero.Cast;
        while (_slots.Count < layout.Slots.Count)
        {
            var view = new SlotView(_skin, _slots.Count, this) { Name = $"slot{_slots.Count + 1}" };
            AddChild(view);
            _slots.Add(view);
        }
        for (var i = 0; i < _slots.Count; i++)
        {
            var box = i < layout.Slots.Count ? layout.Slots[i] : Box.Empty;
            _slots[i].Visible = i < layout.Slots.Count;
            _slots[i].Position = new Vector2(box.X - layout.Bar.X, box.Y - layout.Bar.Y);
            _slots[i].Size = new Vector2(box.W, box.H);
            _slots[i].Show(i < seats.Count ? seats[i] : null, now, cast is not null && cast.Slot == i);
        }
        QueueRedraw();
    }

    internal void Press(int slot) => SlotPressed?.Invoke(slot);

    public override void _Draw()
    {
        var r = new Rect2(Vector2.Zero, Size);
        Paint.Shadow(this, r, _skin.Mat("shadow", 0.5f));
        DrawRect(r, _skin.S("pane"));
        if (_skin.Brass)
        {
            // The bar is a brass plate in the brass texture.
            var plate = _skin.S("edge").Darkened(0.35f);
            DrawRect(r.Grow(-2), plate);
        }
        Paint.Frame(this, _skin, r);
    }

    /// <summary>One weave slot.</summary>
    private sealed partial class SlotView : Control
    {
        private readonly Skin _skin;
        private readonly int _index;
        private readonly WeaveBar _bar;
        private HeroState.Seat? _seat;
        private double _now;
        private bool _casting;
        private bool _hover;
        private string _tip = "";

        public SlotView(Skin skin, int index, WeaveBar bar)
        {
            _skin = skin;
            _index = index;
            _bar = bar;
            MouseFilter = MouseFilterEnum.Stop;
        }

        public void Show(HeroState.Seat? seat, double now, bool casting)
        {
            _seat = seat;
            _now = now;
            _casting = casting;
            var tip = Tip(seat);
            if (tip != _tip)
            {
                _tip = tip;
                TooltipText = tip;
            }
            QueueRedraw();
        }

        private string Tip(HeroState.Seat? seat)
        {
            if (seat is null || seat.Empty)
            {
                return $"Seat {_index + 1}: empty";
            }
            var lines = new List<string> { $"{seat.Name}  (key {_index + 1})" };
            if (seat.Info is { } info)
            {
                lines.Add($"Level {info.Level}{(info.CastMode is null ? "" : "  " + info.CastMode)}{(info.Targeting is null ? "" : "  " + info.Targeting)}");
                lines.AddRange(info.Preview.Select(p => $"{p.Label}: {p.Text}"));
                foreach (var g in info.Glyphs)
                {
                    lines.Add($"Glyph {g.Name} {g.Level}{(g.Dormant is null ? "" : "  (dormant: " + g.Dormant + ")")}");
                }
                if (info.Meta is { } meta)
                {
                    lines.Add($"Second ability: {meta.Label}");
                }
            }
            if (seat.Dormant is not null)
            {
                lines.Add($"Dormant: {seat.Dormant}");
            }
            return string.Join("\n", lines);
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
            {
                _bar.Press(_index);
                AcceptEvent();
            }
        }

        public override void _Notification(int what)
        {
            if (what == NotificationMouseEnter || what == NotificationMouseExit)
            {
                _hover = what == NotificationMouseEnter;
                QueueRedraw();
            }
        }

        public override void _Draw()
        {
            var r = new Rect2(Vector2.Zero, Size);
            var seat = _seat;
            var dormant = seat?.Dormant is not null;
            // A slot whose toggle or contract runs (a sustained summon, an aura) shows its face on: the well lit.
            var active = seat?.Info?.Active == true;
            Paint.Well(this, _skin, r, active ? _skin.S("well").Lerp(_skin.S("accent"), 0.22f) : _skin.S("well"), _casting || active);
            var label = _skin.S("label");
            var keySize = Math.Max(9, (int)(Size.Y * 0.22f));
            if (seat is null || seat.Empty)
            {
                Paint.Text(this, _skin.Numerals, new Vector2(4, keySize + 2), (_index + 1).ToString(), keySize, label with { A = 0.35f }, outline: 3);
                return;
            }
            var face = seat.Converted;
            var iconId = face?.Icon ?? seat.Icon;
            var glyph = _skin.IconSet.ForWeave(iconId, seat.Key);
            var tint = seat.Threads.Count > 0 ? _skin.Thread(seat.Threads[0]) : label;
            if (dormant)
            {
                tint = _skin.State("dormant", new Color(0.5f, 0.5f, 0.5f));
            }
            var iconSize = Size.X * 0.62f;
            var iconRect = new Rect2((Size - new Vector2(iconSize, iconSize)) / 2, new Vector2(iconSize, iconSize));
            // a faint halo of the Thread behind the glyph, bright while the slot is on
            DrawCircle(Size / 2, iconSize * 0.46f, tint with { A = dormant ? 0.06f : active ? 0.32f : 0.14f }, true, -1, true);
            DrawTextureRect(_skin.Icon(glyph, (int)iconSize), iconRect, false, tint);
            if (dormant)
            {
                var hatch = new Color(0, 0, 0, 0.35f);
                for (var x = -Size.Y; x < Size.X; x += 7)
                {
                    DrawLine(new Vector2(x, Size.Y), new Vector2(x + Size.Y, 0), hatch, 2);
                }
            }

            // The cooldown sweep against the full cooldown the server sent.
            var left = seat.ReadyIn(_now);
            if (left > 0.05 && seat.Cooldown > 0)
            {
                var frac = (float)Math.Clamp(left / seat.Cooldown, 0, 1);
                Paint.Sweep(this, r.Grow(-2), frac, _skin.State("cooldown", Colors.Black) with { A = 0.62f });
                var text = left >= 10 ? $"{Math.Ceiling(left)}" : $"{left:0.0}";
                Paint.Text(this, _skin.Numerals, new Vector2(0, Size.Y * 0.58f), text, Math.Max(11, (int)(Size.Y * 0.3f)), Colors.White, HorizontalAlignment.Center, Size.X);
            }

            // The key, the cost, the pips and the faces.
            Paint.Text(this, _skin.Numerals, new Vector2(4, keySize + 2), (_index + 1).ToString(), keySize, label, outline: 3);
            var costY = Size.Y - 4;
            foreach (var (resource, amount) in seat.Costs)
            {
                if (amount <= 0)
                {
                    continue;
                }
                Paint.Text(this, _skin.Numerals, new Vector2(0, costY), $"{Math.Round(amount)}", keySize, _skin.Fill(resource).Lightened(0.25f), HorizontalAlignment.Right, Size.X - 4, 3);
                costY -= keySize;
            }
            if (seat.UsesMax is > 1 and var max)
            {
                Paint.Pips(this, new Vector2(Size.X - 3, 3), seat.Uses ?? 0, max, 2.6f, _skin.State("pip", new Color(0.95f, 0.8f, 0.3f)), _skin.S("edge_dark"));
            }
            if (face is not null)
            {
                DrawRect(new Rect2(Size.X - 9, Size.Y - 9, 6, 6), _skin.S("accent"));
            }
            if (seat.Meta is not null)
            {
                Paint.Text(this, _skin.Body, new Vector2(4, Size.Y - 4), "+", keySize, _skin.S("accent"), outline: 3);
            }
            if (seat.Info?.Stacks is > 0 and var stacks)
            {
                // Self stacks the slot holds, as the server counts them.
                Paint.Text(this, _skin.Numerals, new Vector2(0, Size.Y - 4), $"x{stacks}", keySize, label, HorizontalAlignment.Center, Size.X, 3);
            }
            if (active)
            {
                DrawRect(r.Grow(-1.5f), _skin.S("accent") with { A = 0.9f }, false, 2);
            }
            if (_hover)
            {
                DrawRect(r.Grow(-1), _skin.S("accent") with { A = 0.5f }, false, 1);
            }
        }
    }
}
