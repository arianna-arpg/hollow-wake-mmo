namespace HollowWake.Client.UI.Logic;

/// <summary>Where the workspace's surfaces stand this frame.</summary>
public sealed record WorkspaceRects(
    Box Core,
    Box PageBook,
    Box WorldBook,
    bool Merged,
    bool PagesLeft,
    bool RailBadges,
    bool RailChips);

/// <summary>A seat remembered as fractions of the viewport, so it survives a resize.</summary>
public readonly record struct Seat(float FX, float FY)
{
    public static Seat Of(Box box, Box viewport) =>
        new(viewport.W <= 0 ? 0 : (box.X - viewport.X) / viewport.W, viewport.H <= 0 ? 0 : (box.Y - viewport.Y) / viewport.H);

    public (float X, float Y) At(Box viewport) => (viewport.X + FX * viewport.W, viewport.Y + FY * viewport.H);
}

/// <summary>
/// The placement law of the workspace. The core pane stands where the player last seated it (by default at
/// the right, just above the cluster), always above the cluster's drawn rectangle and inside the viewport.
/// Pages dock beside it on the side with more room, share its height and move with it. The world's panes
/// stand on the other side when that side has room; otherwise they share the page side and the two bind into
/// one book. On a narrow screen books clamp inside the viewport, and the rail drops its count badges before
/// its wallet chips when the core pane is too short for both.
/// </summary>
public static class PaneLayout
{
    public sealed record Input(
        Box Viewport,
        Box Cluster,
        Seat? CoreSeat,
        float CoreWidth,
        float CoreHeight,
        float PageWidth,
        float WorldWidth,
        float Edge,
        bool HasPages,
        bool HasWorld,
        int Ribbons,
        float RibbonHeight,
        float BadgeHeight,
        int Chips,
        float ChipHeight,
        float RailTop);

    public static WorkspaceRects Compute(Input i)
    {
        var view = i.Viewport;
        var edge = i.Edge;
        // Everything fits above the cluster: from the top edge down to the cluster's top, less a margin.
        var bottom = MathF.Max(view.Y + edge * 4, i.Cluster.IsEmpty ? view.Bottom - edge : i.Cluster.Y - edge);
        var area = new Box(view.X + edge, view.Y + edge, MathF.Max(0, view.W - edge * 2), MathF.Max(0, bottom - view.Y - edge));
        var coreH = MathF.Min(i.CoreHeight, area.H);
        var coreW = MathF.Min(i.CoreWidth, area.W);
        Box core;
        if (i.CoreSeat is { } seat)
        {
            var (x, y) = seat.At(view);
            core = new Box(x, y, coreW, coreH).ClampedInto(area);
        }
        else
        {
            core = new Box(area.Right - coreW, area.Bottom - coreH, coreW, coreH);
        }
        var gap = edge / 2;
        var roomLeft = core.X - area.X;
        var roomRight = area.Right - core.Right;
        var pagesLeft = roomLeft >= roomRight;
        Box Beside(bool left, float width)
        {
            var w = MathF.Min(width, area.W);
            var x = left ? core.X - gap - w : core.Right + gap;
            return new Box(x, core.Y, w, core.H).ClampedInto(area);
        }
        bool Fits(bool left, float width) => (left ? roomLeft : roomRight) >= width + gap;

        var pageBook = i.HasPages ? Beside(pagesLeft, i.PageWidth) : Box.Empty;
        Box worldBook;
        var merged = false;
        if (!i.HasWorld)
        {
            worldBook = Box.Empty;
        }
        else if (!i.HasPages)
        {
            // Alone, the world's pane takes the side with room, as pages would.
            worldBook = Beside(pagesLeft, i.WorldWidth);
        }
        else if (Fits(!pagesLeft, i.WorldWidth))
        {
            worldBook = Beside(!pagesLeft, i.WorldWidth);
        }
        else
        {
            // No room across the core pane: the world's pane shares the page side and binds with the pages.
            merged = true;
            var width = MathF.Max(i.PageWidth, i.WorldWidth);
            pageBook = Beside(pagesLeft, width);
            worldBook = pageBook;
        }

        // The rail: ribbons always; count badges, then wallet chips, go when the pane is too short.
        var railRoom = core.H - i.RailTop;
        var ribbons = i.Ribbons * i.RibbonHeight;
        var badges = i.Ribbons * i.BadgeHeight;
        var chips = i.Chips * i.ChipHeight;
        var showBadges = ribbons + badges + chips <= railRoom;
        var showChips = ribbons + (showBadges ? badges : 0) + chips <= railRoom;
        return new WorkspaceRects(core, pageBook, worldBook, merged, pagesLeft, showBadges, showChips);
    }
}
