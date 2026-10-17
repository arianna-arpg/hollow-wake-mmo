using Godot;
using HollowWake.Client.UI.Logic;
using HollowWake.Protocol;

namespace HollowWake.Client.UI.Core;

/// <summary>
/// The equipment doll: every equipment slot from content, placed on the cells the layout gives it (slots it
/// does not place follow in slot order), each showing what is worn there or its own glyph faintly, and a slot
/// filled by another slot's item (a two-handed weapon's off hand) drawn as taken. Worn items lift like bag
/// items; dropping an item on a slot sends the move for that slot.
/// </summary>
public partial class DollView : Control, IDropTarget
{
    private readonly IItemHost _host;
    private readonly List<(EquipSlotInfo Slot, Rect2 Rect)> _cells = new();
    private IReadOnlyDictionary<string, ItemInfo> _worn = new Dictionary<string, ItemInfo>();
    private string? _hoverSlot;
    private string? _dropSlot;
    private bool _dropFits;

    public DollView(IItemHost host)
    {
        _host = host;
        MouseFilter = MouseFilterEnum.Stop;
    }

    /// <summary>Lays out the slots on the doll's grid and returns the size it needs.</summary>
    public Vector2 Show(IReadOnlyList<EquipSlotInfo> slots, IReadOnlyDictionary<string, ItemInfo> worn, UiLayout layout, float cell)
    {
        _worn = worn;
        _cells.Clear();
        var gap = Math.Max(4, cell * 0.12f);
        var placed = new HashSet<(int, int)>();
        var columns = Math.Max(3, layout.Doll.Count == 0 ? 3 : layout.Doll.Values.Max(c => c.Column) + 1);
        var maxRow = layout.Doll.Count == 0 ? -1 : layout.Doll.Values.Max(c => c.Row);
        foreach (var slot in slots.OrderBy(s => s.Order))
        {
            if (layout.Doll.TryGetValue(slot.Id, out var at))
            {
                placed.Add(at);
                _cells.Add((slot, new Rect2(at.Column * (cell + gap), at.Row * (cell + gap), cell, cell)));
            }
        }
        // Slots the layout does not place follow below, in slot order.
        var next = 0;
        foreach (var slot in slots.OrderBy(s => s.Order))
        {
            if (layout.Doll.ContainsKey(slot.Id))
            {
                continue;
            }
            int col, row;
            do
            {
                col = next % columns;
                row = maxRow + 1 + next / columns;
                next++;
            }
            while (placed.Contains((col, row)));
            _cells.Add((slot, new Rect2(col * (cell + gap), row * (cell + gap), cell, cell)));
        }
        var size = _cells.Count == 0 ? Vector2.Zero : new Vector2(_cells.Max(c => c.Rect.End.X), _cells.Max(c => c.Rect.End.Y));
        CustomMinimumSize = size;
        Size = size;
        QueueRedraw();
        return size;
    }

    private (EquipSlotInfo Slot, Rect2 Rect)? CellAt(Vector2 local)
    {
        foreach (var c in _cells)
        {
            if (c.Rect.HasPoint(local))
            {
                return c;
            }
        }
        return null;
    }

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseMotion motion:
                var cell = _host.Carrying is null ? CellAt(motion.Position) : null;
                var slot = cell?.Slot.Id;
                if (slot != _hoverSlot)
                {
                    _hoverSlot = slot;
                    QueueRedraw();
                }
                _host.HoverItem(slot is not null && _worn.TryGetValue(slot, out var worn) ? worn : null, GetGlobalMousePosition(), "equipment");
                TooltipText = cell is { } c && !_worn.ContainsKey(c.Slot.Id) ? c.Slot.OccupiedBy is { } by ? $"{c.Slot.Name}: held by {by}" : c.Slot.Name : "";
                break;
            case InputEventMouseButton { Pressed: true } button when _host.Carrying is null:
                if (CellAt(button.Position) is not { } hit || !_worn.TryGetValue(hit.Slot.Id, out var item))
                {
                    break;
                }
                if (button.ButtonIndex == MouseButton.Left)
                {
                    _host.PressItem(item, new CarrySource("equipment", 0, 0, hit.Slot.Id), GetGlobalMousePosition());
                }
                else if (button.ButtonIndex == MouseButton.Right)
                {
                    _host.QuickActions(item, "equipment", GetGlobalMousePosition());
                }
                AcceptEvent();
                break;
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationMouseExit)
        {
            _hoverSlot = null;
            _host.HoverItem(null, Vector2.Zero, "equipment");
            QueueRedraw();
        }
    }

    public bool Covers(Vector2 point) => IsVisibleInTree() && CellAt(point - GlobalPosition) is not null;

    public void Hover(Carry carry, Vector2 point)
    {
        var cell = CellAt(point - GlobalPosition);
        var slot = cell?.Slot.Id;
        // A slot takes the item when the item names it (or names no slot and is gear); the server decides.
        var fits = slot is not null && (carry.Item.Slot is null ? carry.Item.Class?.StartsWith("weapon", StringComparison.Ordinal) == true || carry.Item.Class?.StartsWith("armor", StringComparison.Ordinal) == true : carry.Item.Slot == slot);
        if (slot != _dropSlot || fits != _dropFits)
        {
            _dropSlot = slot;
            _dropFits = fits;
            QueueRedraw();
        }
    }

    public void Leave()
    {
        if (_dropSlot is not null)
        {
            _dropSlot = null;
            QueueRedraw();
        }
    }

    public bool Drop(Carry carry, Vector2 point)
    {
        var cell = CellAt(point - GlobalPosition);
        Leave();
        if (cell is not { } c)
        {
            return false;
        }
        if (carry.From.Slot == c.Slot.Id)
        {
            return true;
        }
        _host.SendMove(carry.Item.Uid, "equipment", 0, 0, c.Slot.Id);
        return true;
    }

    public override void _Draw()
    {
        var skin = _host.Skin;
        foreach (var (slot, rect) in _cells)
        {
            Widgets.Paint.Well(this, skin, rect, skin.S("inset") with { A = 0.9f });
            if (_worn.TryGetValue(slot.Id, out var item) && !(_host.Carrying is { } c && c.Item.Uid == item.Uid))
            {
                ItemArt.Tile(this, skin, rect.Grow(-2), item, 1f, slot.Id == _hoverSlot);
            }
            else
            {
                var glyph = skin.IconSet.ForSlot(slot.Id);
                var side = rect.Size.X * 0.56f;
                var alpha = slot.OccupiedBy is not null ? 0.5f : 0.22f;
                DrawTextureRect(skin.Icon(glyph, (int)side), new Rect2(rect.GetCenter() - new Vector2(side, side) / 2, new Vector2(side, side)), false, skin.S("ink_muted") with { A = alpha });
            }
            if (slot.Id == _dropSlot)
            {
                var color = _dropFits ? skin.State("fit", Colors.Green) : skin.State("swap", Colors.Yellow);
                DrawRect(rect.Grow(-1), color, false, 2);
            }
        }
    }
}
