using HollowWake.Core.Ecs;
using HollowWake.Core.Items;

namespace HollowWake.Core.Components;

/// <summary>An item's footprint in cells. Content decides it: an item base's width and height.</summary>
public interface IFootprints
{
    (int Width, int Height) Of(ItemInstance item);
}

/// <summary>
/// A grid of cells holding items with positions and footprints: the bag, a stash page, the reliquary. A mask
/// switches cells off. Each placed item records its top-left cell on itself (<see cref="ItemInstance.X"/>,
/// <see cref="ItemInstance.Y"/>) so the place persists with the item; the board keeps which item covers each
/// cell. Placing, moving and first fit live here; what may go on a board is its container's business.
/// </summary>
public sealed class Board
{
    private bool[] _usable;
    private ItemInstance?[] _cells;
    private readonly List<Placement> _placed = new();

    /// <summary>An item on the board: its top-left cell and its footprint.</summary>
    public readonly record struct Placement(ItemInstance Item, int X, int Y, int Width, int Height);

    /// <param name="mask">Rows of cells, X (or any character but . and space) for a usable cell; null makes every cell usable.</param>
    public Board(int width, int height, IReadOnlyList<string>? mask = null)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        _usable = BuildMask(Width, Height, mask);
        _cells = new ItemInstance?[Width * Height];
        UsableCells = CountUsable(_usable);
    }

    public int Width { get; private set; }
    public int Height { get; private set; }

    /// <summary>Cells that can hold an item.</summary>
    public int UsableCells { get; private set; }

    /// <summary>Usable cells no item covers.</summary>
    public int FreeCells
    {
        get
        {
            var free = 0;
            for (var i = 0; i < _cells.Length; i++)
            {
                if (_usable[i] && _cells[i] is null)
                {
                    free++;
                }
            }
            return free;
        }
    }

    /// <summary>Every item on the board in the order it was placed.</summary>
    public IReadOnlyList<Placement> Placed => _placed;

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public bool IsUsable(int x, int y) => InBounds(x, y) && _usable[y * Width + x];

    /// <summary>The item covering a cell, if any.</summary>
    public ItemInstance? At(int x, int y) => InBounds(x, y) ? _cells[y * Width + x] : null;

    public bool Contains(ItemInstance item) => IndexOf(item) >= 0;

    public bool TryGetPlacement(ItemInstance item, out Placement placement)
    {
        var i = IndexOf(item);
        placement = i >= 0 ? _placed[i] : default;
        return i >= 0;
    }

    /// <summary>Whether a footprint at a cell lies on usable cells that are free or covered only by <paramref name="ignoring"/>.</summary>
    public bool Fits(int x, int y, int width, int height, ItemInstance? ignoring = null)
    {
        if (width < 1 || height < 1 || x < 0 || y < 0 || x + width > Width || y + height > Height)
        {
            return false;
        }
        for (var dy = 0; dy < height; dy++)
        {
            for (var dx = 0; dx < width; dx++)
            {
                var i = (y + dy) * Width + x + dx;
                if (!_usable[i])
                {
                    return false;
                }
                var occupant = _cells[i];
                if (occupant is not null && !ReferenceEquals(occupant, ignoring))
                {
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>Places an item at a cell when it fits there; the item remembers the cell.</summary>
    public bool Place(ItemInstance item, int x, int y, int width, int height)
    {
        if (Contains(item) || !Fits(x, y, width, height))
        {
            return false;
        }
        Fill(item, x, y, width, height);
        _placed.Add(new Placement(item, x, y, width, height));
        item.X = x;
        item.Y = y;
        return true;
    }

    /// <summary>Takes an item off the board; it forgets its cell.</summary>
    public bool Remove(ItemInstance item)
    {
        var i = IndexOf(item);
        if (i < 0)
        {
            return false;
        }
        var p = _placed[i];
        Fill(null, p.X, p.Y, p.Width, p.Height);
        _placed.RemoveAt(i);
        item.X = null;
        item.Y = null;
        return true;
    }

    /// <summary>The first cell, row by row from the top left, where a footprint fits.</summary>
    public bool FindSpace(int width, int height, out int x, out int y)
    {
        for (y = 0; y + height <= Height; y++)
        {
            for (x = 0; x + width <= Width; x++)
            {
                if (Fits(x, y, width, height))
                {
                    return true;
                }
            }
        }
        x = -1;
        y = -1;
        return false;
    }

    /// <summary>The distinct items under a footprint, other than <paramref name="ignoring"/>, appended to <paramref name="into"/>.</summary>
    public void Overlapping(int x, int y, int width, int height, ItemInstance? ignoring, List<ItemInstance> into)
    {
        for (var dy = 0; dy < height; dy++)
        {
            for (var dx = 0; dx < width; dx++)
            {
                if (At(x + dx, y + dy) is { } occupant && !ReferenceEquals(occupant, ignoring) && !into.Contains(occupant))
                {
                    into.Add(occupant);
                }
            }
        }
    }

    /// <summary>The mask as rows of X and ., or null when every cell is usable.</summary>
    public List<string>? MaskRows()
    {
        if (UsableCells == _usable.Length)
        {
            return null;
        }
        var rows = new List<string>(Height);
        var row = new char[Width];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                row[x] = _usable[y * Width + x] ? 'X' : '.';
            }
            rows.Add(new string(row));
        }
        return rows;
    }

    /// <summary>
    /// Gives the board a new size and mask (an expansion bought or a rung unlocked). Items keep their cells
    /// where those still fit; the rest are taken off and appended to <paramref name="displaced"/>.
    /// </summary>
    public void Reshape(int width, int height, IReadOnlyList<string>? mask, List<ItemInstance> displaced)
    {
        var kept = _placed.ToList();
        foreach (var p in kept)
        {
            p.Item.X = null;
            p.Item.Y = null;
        }
        _placed.Clear();
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        _usable = BuildMask(Width, Height, mask);
        _cells = new ItemInstance?[Width * Height];
        UsableCells = CountUsable(_usable);
        foreach (var p in kept)
        {
            if (!Place(p.Item, p.X, p.Y, p.Width, p.Height))
            {
                displaced.Add(p.Item);
            }
        }
    }

    private int IndexOf(ItemInstance item)
    {
        for (var i = 0; i < _placed.Count; i++)
        {
            if (ReferenceEquals(_placed[i].Item, item))
            {
                return i;
            }
        }
        return -1;
    }

    private void Fill(ItemInstance? item, int x, int y, int width, int height)
    {
        for (var dy = 0; dy < height; dy++)
        {
            for (var dx = 0; dx < width; dx++)
            {
                _cells[(y + dy) * Width + x + dx] = item;
            }
        }
    }

    private static bool[] BuildMask(int width, int height, IReadOnlyList<string>? mask)
    {
        var usable = new bool[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                usable[y * width + x] = mask is null || (y < mask.Count && x < mask[y].Length && mask[y][x] is not ('.' or ' '));
            }
        }
        return usable;
    }

    private static int CountUsable(bool[] usable)
    {
        var count = 0;
        foreach (var u in usable)
        {
            if (u)
            {
                count++;
            }
        }
        return count;
    }
}

/// <summary>
/// The bag: a board of cells, items that found no room on it (kept, never lost, until there is room), and the
/// wallet. Footprints come from <see cref="Footprints"/> when one is attached (the hero's bag) and are one
/// cell otherwise.
/// </summary>
public sealed class Inventory : IComponent
{
    public Board Bag { get; private set; }

    /// <summary>Items the bag holds that have no cell, because the board shrank or footprints changed. They count as carried.</summary>
    public List<ItemInstance> Overflow { get; } = new();

    public Dictionary<string, double> Currency { get; } = new(StringComparer.Ordinal);

    /// <summary>Where footprints come from; null means every item takes one cell.</summary>
    public IFootprints? Footprints { get; set; }

    /// <summary>A bag of about <paramref name="capacity"/> one-cell items, ten to a row.</summary>
    public Inventory(int capacity = 40)
        : this(Math.Clamp(capacity, 1, 10), (Math.Max(1, capacity) + 9) / 10)
    {
    }

    public Inventory(int width, int height, IReadOnlyList<string>? mask = null)
    {
        Bag = new Board(width, height, mask);
    }

    /// <summary>Usable cells of the bag.</summary>
    public int Capacity => Bag.UsableCells;

    /// <summary>Free cells of the bag.</summary>
    public int FreeSlots => Bag.FreeCells;

    /// <summary>Everything carried: the items on the board, then any without a cell.</summary>
    public IEnumerable<ItemInstance> Items
    {
        get
        {
            foreach (var p in Bag.Placed)
            {
                yield return p.Item;
            }
            foreach (var item in Overflow)
            {
                yield return item;
            }
        }
    }

    public (int Width, int Height) FootprintOf(ItemInstance item) => Footprints?.Of(item) ?? (1, 1);

    /// <summary>Stacks onto matching stacks first, then places what is left in the first space that fits it.</summary>
    public bool TryAdd(ItemInstance item)
    {
        if (item.Stack > 1 || item.Count > 1)
        {
            foreach (var existing in Items)
            {
                if (existing.CanStackWith(item))
                {
                    var room = existing.Stack - existing.Count;
                    var moved = Math.Min(room, item.Count);
                    existing.Count += moved;
                    item.Count -= moved;
                    if (item.Count <= 0)
                    {
                        return true;
                    }
                }
            }
        }
        var (w, h) = FootprintOf(item);
        return Bag.FindSpace(w, h, out var x, out var y) && Bag.Place(item, x, y, w, h);
    }

    /// <summary>Places an item at a cell of the bag when it fits there.</summary>
    public bool TryAddAt(ItemInstance item, int x, int y)
    {
        var (w, h) = FootprintOf(item);
        return Bag.Place(item, x, y, w, h);
    }

    /// <summary>Whether an item could be added now, stacking or in free space, without adding it.</summary>
    public bool HasRoomFor(ItemInstance item)
    {
        if (item.Stack > 1)
        {
            var room = 0;
            foreach (var existing in Items)
            {
                if (existing.CanStackWith(item))
                {
                    room += existing.Stack - existing.Count;
                }
            }
            if (room >= item.Count)
            {
                return true;
            }
        }
        var (w, h) = FootprintOf(item);
        return Bag.FindSpace(w, h, out _, out _);
    }

    public bool Remove(ItemInstance item) => Bag.Remove(item) || Overflow.Remove(item);

    public ItemInstance? Find(long uid)
    {
        foreach (var item in Items)
        {
            if (item.Uid == uid)
            {
                return item;
            }
        }
        return null;
    }

    public int CountOf(Func<ItemInstance, bool> predicate)
    {
        var count = 0;
        foreach (var item in Items)
        {
            if (predicate(item))
            {
                count += item.Count;
            }
        }
        return count;
    }

    public double CurrencyOf(string id) => Currency.TryGetValue(id, out var v) ? v : 0;

    public void AddCurrency(string id, double amount)
    {
        Currency[id] = CurrencyOf(id) + amount;
        if (Currency[id] <= 0)
        {
            Currency.Remove(id);
        }
    }
}

public sealed class Equipment : IComponent
{
    public Dictionary<string, ItemInstance> BySlot { get; } = new(StringComparer.Ordinal);
    /// <summary>Slots occupied by an item equipped elsewhere (a two-handed weapon's off hand).</summary>
    public Dictionary<string, string> OccupiedBy { get; } = new(StringComparer.Ordinal);

    public ItemInstance? In(string slot) => BySlot.TryGetValue(slot, out var i) ? i : null;
    public IEnumerable<ItemInstance> All => BySlot.Values;
}
