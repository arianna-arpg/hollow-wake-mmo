namespace HollowWake.Client.UI.Logic;

/// <summary>An item on a board: its uid, the cell of its top-left corner and its footprint in cells.</summary>
public readonly record struct BoardItem(long Uid, int X, int Y, int W, int H)
{
    public bool Covers(int x, int y) => x >= X && x < X + W && y >= Y && y < Y + H;

    public bool Overlaps(int x, int y, int w, int h) => x < X + W && X < x + w && y < Y + H && Y < y + h;
}

/// <summary>Whether a carried item can land where it hovers.</summary>
public enum DropFit
{
    /// <summary>The footprint lies on free cells.</summary>
    Fits,

    /// <summary>The footprint covers exactly one other item, which the move would swap out.</summary>
    Swap,

    /// <summary>The footprint covers a masked cell or more than one item.</summary>
    Blocked,

    /// <summary>The pointer is not over the board.</summary>
    Outside,
}

/// <summary>Where a carried footprint would land and how: the anchor cell, the fit and the item a swap would lift.</summary>
public readonly record struct DropTarget(int X, int Y, DropFit Fit, long SwapUid);

/// <summary>
/// A grid of cells with items laid on it by position and footprint, as the server sends it. The interface
/// uses it to find the cell under the pointer and to tint a drop target while an item is carried; the server
/// decides every move, so nothing here places an item.
/// </summary>
public sealed class Board
{
    private readonly bool[] _blocked;

    public int Width { get; }
    public int Height { get; }
    public IReadOnlyList<BoardItem> Items { get; }

    /// <param name="mask">Blocked cells, row by row; null or short means every cell is open.</param>
    public Board(int width, int height, IReadOnlyList<BoardItem> items, IReadOnlyList<bool>? mask = null)
    {
        Width = Math.Max(0, width);
        Height = Math.Max(0, height);
        Items = items;
        _blocked = new bool[Width * Height];
        if (mask is not null)
        {
            for (var i = 0; i < Math.Min(mask.Count, _blocked.Length); i++)
            {
                _blocked[i] = mask[i];
            }
        }
    }

    public bool InBounds(int x, int y, int w = 1, int h = 1) => x >= 0 && y >= 0 && x + w <= Width && y + h <= Height;

    public bool Blocked(int x, int y) => !InBounds(x, y) || _blocked[y * Width + x];

    public BoardItem? ItemAt(int x, int y)
    {
        foreach (var item in Items)
        {
            if (item.Covers(x, y))
            {
                return item;
            }
        }
        return null;
    }

    public BoardItem? Find(long uid)
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

    /// <summary>
    /// Where a carried footprint lands with the pointer at a fractional cell position: centred under the
    /// pointer, pulled inside the board, and tested against the mask and the other items (the carried item
    /// itself never blocks its own move).
    /// </summary>
    public DropTarget Target(long carriedUid, int w, int h, float cellX, float cellY)
    {
        if (cellX < 0 || cellY < 0 || cellX >= Width || cellY >= Height || w > Width || h > Height)
        {
            return new DropTarget(0, 0, DropFit.Outside, 0);
        }
        var x = Math.Clamp((int)MathF.Floor(cellX - w / 2f + 0.5f), 0, Width - w);
        var y = Math.Clamp((int)MathF.Floor(cellY - h / 2f + 0.5f), 0, Height - h);
        return Test(carriedUid, x, y, w, h);
    }

    /// <summary>The fit of a footprint anchored at a cell.</summary>
    public DropTarget Test(long carriedUid, int x, int y, int w, int h)
    {
        if (!InBounds(x, y, w, h))
        {
            return new DropTarget(x, y, DropFit.Outside, 0);
        }
        for (var cy = y; cy < y + h; cy++)
        {
            for (var cx = x; cx < x + w; cx++)
            {
                if (_blocked[cy * Width + cx])
                {
                    return new DropTarget(x, y, DropFit.Blocked, 0);
                }
            }
        }
        long swap = 0;
        var count = 0;
        foreach (var item in Items)
        {
            if (item.Uid == carriedUid || !item.Overlaps(x, y, w, h))
            {
                continue;
            }
            count++;
            swap = item.Uid;
        }
        return count switch
        {
            0 => new DropTarget(x, y, DropFit.Fits, 0),
            1 => new DropTarget(x, y, DropFit.Swap, swap),
            _ => new DropTarget(x, y, DropFit.Blocked, 0),
        };
    }

    /// <summary>The first anchor, row by row, where a footprint fits on free cells; for previews only.</summary>
    public (int X, int Y)? FirstFit(int w, int h, long ignoreUid = 0)
    {
        for (var y = 0; y + h <= Height; y++)
        {
            for (var x = 0; x + w <= Width; x++)
            {
                if (Test(ignoreUid, x, y, w, h).Fit == DropFit.Fits)
                {
                    return (x, y);
                }
            }
        }
        return null;
    }

    /// <summary>
    /// A board for a bag the server sends as a list without positions (before the grid inventory): items in
    /// list order, one cell each, filling rows of the given width.
    /// </summary>
    public static Board FromList(IReadOnlyList<long> uids, int capacity, int columns)
    {
        columns = Math.Max(1, columns);
        var rows = Math.Max(1, (Math.Max(capacity, uids.Count) + columns - 1) / columns);
        var items = new List<BoardItem>(uids.Count);
        for (var i = 0; i < uids.Count; i++)
        {
            items.Add(new BoardItem(uids[i], i % columns, i / columns, 1, 1));
        }
        var mask = new bool[columns * rows];
        for (var i = Math.Max(capacity, uids.Count); i < mask.Length; i++)
        {
            mask[i] = true;
        }
        return new Board(columns, rows, items, mask);
    }
}
