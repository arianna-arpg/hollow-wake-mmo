using Godot;
using HollowWake.Client.UI.Logic;
using HollowWake.Protocol;

namespace HollowWake.Client.UI.Core;

/// <summary>What a board view asks of the interface around it.</summary>
public interface IItemHost
{
    Skin Skin { get; }
    Carry? Carrying { get; }
    void HoverItem(ItemInfo? item, Vector2 at, string container);
    void PressItem(ItemInfo item, CarrySource from, Vector2 at);
    void QuickActions(ItemInfo item, string container, Vector2 at);
    void SendMove(long uid, string container, int x, int y, string? slot = null);
}

/// <summary>
/// A board of cells with items on their positions and footprints, as the inventory state sends it (the bag, a
/// stash page, the reliquary). Masked cells are drawn sealed. While an item is carried over it, the cells it
/// would land on are tinted: free, a swap with the one item under it, or blocked. Dropping sends one move
/// intent for this board's container; the server answers with the new state.
/// </summary>
public partial class BoardView : Control, IDropTarget
{
    private readonly IItemHost _host;
    private BoardInfo? _info;
    private Board _board = new(0, 0, Array.Empty<BoardItem>());
    private DropTarget? _target;
    private long _hoverUid;
    private bool _lockedBoard;

    public string Container { get; set; }
    public float Cell { get; private set; }

    public BoardView(IItemHost host, string container)
    {
        _host = host;
        Container = container;
        MouseFilter = MouseFilterEnum.Stop;
    }

    public BoardInfo? Info => _info;

    /// <summary>The size the board draws at with a given cell size.</summary>
    public Vector2 SizeFor(BoardInfo? info, float cell) => info is null ? Vector2.Zero : new Vector2(info.Width * cell, info.Height * cell);

    public void Show(BoardInfo? info, float cell, bool locked = false)
    {
        _info = info;
        Cell = cell;
        _lockedBoard = locked;
        if (info is null)
        {
            _board = new Board(0, 0, Array.Empty<BoardItem>());
        }
        else
        {
            var items = info.Items.Select(i => new BoardItem(i.Uid, i.X ?? 0, i.Y ?? 0, Math.Max(1, i.W ?? 1), Math.Max(1, i.H ?? 1))).ToList();
            _board = new Board(info.Width, info.Height, items, Mask(info));
        }
        CustomMinimumSize = SizeFor(info, cell);
        Size = CustomMinimumSize;
        QueueRedraw();
    }

    private static bool[]? Mask(BoardInfo info)
    {
        if (info.Mask is not { Count: > 0 } rows)
        {
            return null;
        }
        var mask = new bool[info.Width * info.Height];
        for (var y = 0; y < Math.Min(rows.Count, info.Height); y++)
        {
            for (var x = 0; x < Math.Min(rows[y].Length, info.Width); x++)
            {
                mask[y * info.Width + x] = rows[y][x] != 'X';
            }
        }
        return mask;
    }

    private (float X, float Y) CellAt(Vector2 local) => (local.X / Cell, local.Y / Cell);

    private ItemInfo? ItemAt(Vector2 local)
    {
        if (_info is null || Cell <= 0)
        {
            return null;
        }
        var (cx, cy) = CellAt(local);
        var hit = _board.ItemAt((int)Math.Floor(cx), (int)Math.Floor(cy));
        return hit is { } h ? _info.Items.FirstOrDefault(i => i.Uid == h.Uid) : null;
    }

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseMotion motion:
                var item = _host.Carrying is null ? ItemAt(motion.Position) : null;
                var uid = item?.Uid ?? 0;
                if (uid != _hoverUid)
                {
                    _hoverUid = uid;
                    QueueRedraw();
                }
                _host.HoverItem(item, GetGlobalMousePosition(), Container);
                break;
            case InputEventMouseButton { Pressed: true } button when _host.Carrying is null:
                var under = ItemAt(button.Position);
                if (under is null)
                {
                    break;
                }
                if (button.ButtonIndex == MouseButton.Left)
                {
                    _host.PressItem(under, new CarrySource(Container, under.X ?? 0, under.Y ?? 0), GetGlobalMousePosition());
                    AcceptEvent();
                }
                else if (button.ButtonIndex == MouseButton.Right)
                {
                    _host.QuickActions(under, Container, GetGlobalMousePosition());
                    AcceptEvent();
                }
                break;
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationMouseExit)
        {
            _hoverUid = 0;
            _host.HoverItem(null, Vector2.Zero, Container);
            QueueRedraw();
        }
    }

    // ------------------------------------------------------------ drop target

    public bool Covers(Vector2 point) => IsVisibleInTree() && GetGlobalRect().HasPoint(point);

    public void Hover(Carry carry, Vector2 point)
    {
        var local = point - GlobalPosition;
        var (cx, cy) = CellAt(local);
        var target = _board.Target(carry.Item.Uid, carry.W, carry.H, cx, cy);
        if (_lockedBoard)
        {
            target = target with { Fit = DropFit.Blocked };
        }
        if (_target != target)
        {
            _target = target;
            QueueRedraw();
        }
    }

    public void Leave()
    {
        if (_target is not null)
        {
            _target = null;
            QueueRedraw();
        }
    }

    public bool Drop(Carry carry, Vector2 point)
    {
        Hover(carry, point);
        var target = _target;
        Leave();
        if (target is not { } t || t.Fit == DropFit.Outside)
        {
            return false;
        }
        if (t.Fit == DropFit.Blocked)
        {
            // Over the board but nowhere it can land: the drop is spent here, nothing is sent.
            return true;
        }
        if (carry.From.Container == Container && carry.From.X == t.X && carry.From.Y == t.Y && carry.From.Slot is null)
        {
            return true;
        }
        _host.SendMove(carry.Item.Uid, Container, t.X, t.Y);
        return true;
    }

    // ------------------------------------------------------------ drawing

    public override void _Draw()
    {
        var skin = _host.Skin;
        if (_info is null)
        {
            return;
        }
        var inset = skin.S("inset");
        var sealedColor = skin.State("sealed", skin.S("edge_dark"));
        for (var y = 0; y < _info.Height; y++)
        {
            for (var x = 0; x < _info.Width; x++)
            {
                var r = new Rect2(x * Cell, y * Cell, Cell, Cell).Grow(-1);
                if (_board.Blocked(x, y))
                {
                    DrawRect(r, sealedColor with { A = 0.25f });
                    DrawLine(r.Position, r.End, sealedColor with { A = 0.35f }, 1);
                    continue;
                }
                Widgets.Paint.Well(this, skin, r, inset with { A = 0.85f });
            }
        }
        if (_target is { } t && t.Fit != DropFit.Outside && _host.Carrying is { } carry)
        {
            var color = t.Fit switch
            {
                DropFit.Fits => skin.State("fit", new Color(0.4f, 0.8f, 0.4f)),
                DropFit.Swap => skin.State("swap", new Color(0.9f, 0.75f, 0.3f)),
                _ => skin.State("blocked", new Color(0.8f, 0.3f, 0.3f)),
            };
            var r = new Rect2(t.X * Cell, t.Y * Cell, carry.W * Cell, carry.H * Cell);
            DrawRect(r, color with { A = 0.28f });
            DrawRect(r.Grow(-1), color, false, 2);
        }
        foreach (var item in _info.Items)
        {
            if (_host.Carrying is { } c && c.Item.Uid == item.Uid && c.From.Container == Container)
            {
                // the item is in the hand: its place shows only an outline
                var hole = new Rect2((item.X ?? 0) * Cell, (item.Y ?? 0) * Cell, (item.W ?? 1) * Cell, (item.H ?? 1) * Cell).Grow(-2);
                DrawRect(hole, skin.Rarity(item.Rarity) with { A = 0.35f }, false, 1);
                continue;
            }
            var rect = new Rect2((item.X ?? 0) * Cell, (item.Y ?? 0) * Cell, (item.W ?? 1) * Cell, (item.H ?? 1) * Cell).Grow(-2);
            ItemArt.Tile(this, skin, rect, item, 1f, item.Uid == _hoverUid);
        }
        if (_lockedBoard)
        {
            DrawRect(new Rect2(Vector2.Zero, Size), new Color(0, 0, 0, 0.35f));
        }
    }
}
