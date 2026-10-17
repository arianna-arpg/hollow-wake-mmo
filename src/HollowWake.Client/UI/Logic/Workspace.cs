namespace HollowWake.Client.UI.Logic;

/// <summary>How a page was requested: its key, a menu row, its ribbon on the rail, or a show from within a page.</summary>
public enum Via
{
    Key,
    Menu,
    Ribbon,
    Show,
}

/// <summary>What one press of Escape did.</summary>
public enum EscapeStep
{
    Nothing,
    Tray,
    Pane,
    Core,
}

/// <summary>
/// The core pane and the panes beside it, as one state machine both clients share. The core pane is the
/// inventory and the one home: requesting any page or opening any world pane opens it. Requesting a page
/// selects it; repeating the page's key or menu row closes the core pane; activating the selected page's
/// ribbon dismisses only that page. Closing the core pane hides its pages and remembers them, with the one
/// selected, for the next time it opens; the world's panes close with it. Escape folds the menu tray first,
/// then closes the front pane, and closes the core pane last. A world pane departs when the hero walks out
/// of reach of what opened it.
/// </summary>
public sealed class Workspace
{
    private readonly Func<string, UiPage?> _pages;
    private readonly Func<string, int> _rankOf;
    private readonly List<string> _retained = new();
    private string? _retainedFront;

    public BookRule Book { get; } = new();
    public bool CoreOpen { get; private set; }
    public bool TrayOpen { get; set; }

    /// <summary>Raised when a world pane closes by any path, so the server can end what it opened.</summary>
    public event Action<Leaf>? WorldClosed;

    /// <param name="pages">The content page with an id, or null.</param>
    /// <param name="rankOf">A world pane kind's rank on the primacy ladder (pages rank lowest).</param>
    public Workspace(Func<string, UiPage?> pages, Func<string, int> rankOf)
    {
        _pages = pages;
        _rankOf = rankOf;
    }

    public static Workspace For(UiContent content) => new(content.Page, content.Layout.RankOf);

    /// <summary>The page the page side shows, whether or not a world pane fronts a merged book.</summary>
    public string? SelectedPage => Book.FrontWithin(LeafGroup.Page)?.Id;

    /// <summary>Whether the page is the one drawn on its side (not a tab behind another).</summary>
    public bool IsShown(string pageId) => CoreOpen && Book.IsDrawn(pageId);

    /// <summary>A page asked for by key, menu, ribbon or a show from within the workspace.</summary>
    public void RequestPage(string pageId, Via via)
    {
        var page = _pages(pageId);
        if (page is null)
        {
            return;
        }
        if (page.InMenuOnly)
        {
            // A menu page (Options) is a pane of its own: its key or row toggles it, the core pane untouched.
            if (Book.Get(page.Id) is { } open && (via is Via.Key or Via.Menu) && Book.IsDrawn(open.Id))
            {
                CloseWorld(open.Id);
                return;
            }
            OpenWorld(page.Id, page.Id, page.Name, ask: Ask.Front, standalone: true);
            return;
        }
        if (page.IsCore)
        {
            if (via == Via.Show)
            {
                OpenCore();
            }
            else if (CoreOpen)
            {
                CloseCore();
            }
            else
            {
                OpenCore();
            }
            return;
        }
        if (CoreOpen && IsShown(pageId))
        {
            switch (via)
            {
                case Via.Key:
                case Via.Menu:
                    CloseCore();
                    return;
                case Via.Ribbon:
                    DismissPage(pageId);
                    return;
            }
        }
        OpenCore();
        Book.Bind(page.Id, LeafGroup.Page, page.Id, _rankOf("page"), page.Name, Ask.Front);
    }

    /// <summary>Opens the core pane and restores the pages it held when it last closed, the selected one first.</summary>
    public void OpenCore()
    {
        if (CoreOpen)
        {
            return;
        }
        CoreOpen = true;
        foreach (var id in _retained)
        {
            if (_pages(id) is { } page)
            {
                Book.Bind(page.Id, LeafGroup.Page, page.Id, _rankOf("page"), page.Name, Ask.Behind);
            }
        }
        // The page that was selected comes back selected, without taking a front a world pane holds.
        if (_retainedFront is not null && Book.Get(_retainedFront) is { } selected)
        {
            var worldFront = Book.FrontWithin(LeafGroup.World);
            Book.Select(selected.Id);
            if (Book.Merged && worldFront is not null)
            {
                Book.Select(worldFront.Id);
            }
        }
        _retained.Clear();
        _retainedFront = null;
    }

    /// <summary>Closes the core pane: its pages hide and are remembered; the world's panes close.</summary>
    public void CloseCore()
    {
        if (!CoreOpen)
        {
            return;
        }
        _retained.Clear();
        _retained.AddRange(Book.Within(LeafGroup.Page).Select(l => l.Id));
        _retainedFront = Book.FrontWithin(LeafGroup.Page)?.Id;
        foreach (var leaf in Book.Within(LeafGroup.Page))
        {
            Book.Close(leaf.Id);
        }
        foreach (var leaf in Book.Within(LeafGroup.World).Where(l => !l.Standalone))
        {
            CloseWorld(leaf.Id);
        }
        CoreOpen = false;
        TrayOpen = false;
    }

    /// <summary>Dismisses one page and forgets it; its book promotes another.</summary>
    public void DismissPage(string pageId) => Book.Close(pageId);

    /// <summary>
    /// A pane the world opened (a dialogue, a vendor, a station, a stash): opens the core pane beside it. A
    /// standalone pane (Options) stands without it.
    /// </summary>
    public Leaf OpenWorld(string id, string kind, string title, long source = 0, float reach = 0, Ask ask = Ask.Auto, bool standalone = false)
    {
        if (!standalone)
        {
            OpenCore();
        }
        var leaf = Book.Bind(id, LeafGroup.World, kind, _rankOf(kind), title, ask, source, reach);
        leaf.Standalone = standalone;
        return leaf;
    }

    /// <summary>Whether anything at all is open: the core pane or a standalone pane.</summary>
    public bool AnythingOpen => CoreOpen || Book.Leaves.Count > 0;

    public bool HasWorld(string id) => Book.Get(id) is { Group: LeafGroup.World };

    public void CloseWorld(string id)
    {
        var leaf = Book.Get(id);
        if (leaf is null || leaf.Group != LeafGroup.World)
        {
            return;
        }
        Book.Close(id);
        WorldClosed?.Invoke(leaf);
    }

    /// <summary>Clicks a tab: that leaf comes to the front of its book.</summary>
    public void SelectTab(string id) => Book.Select(id);

    /// <summary>Closes a leaf by its own glyph or tab, whichever side it is on.</summary>
    public void CloseLeaf(string id)
    {
        if (Book.Get(id) is not { } leaf)
        {
            return;
        }
        if (leaf.Group == LeafGroup.World)
        {
            CloseWorld(id);
        }
        else
        {
            DismissPage(id);
        }
    }

    /// <summary>One press of Escape: the tray, then the front pane, then the core pane.</summary>
    public EscapeStep Escape()
    {
        if (TrayOpen)
        {
            TrayOpen = false;
            return EscapeStep.Tray;
        }
        if (Book.Active() is { } front)
        {
            CloseLeaf(front.Id);
            return EscapeStep.Pane;
        }
        if (CoreOpen)
        {
            CloseCore();
            return EscapeStep.Core;
        }
        return EscapeStep.Nothing;
    }

    /// <summary>
    /// The departure law: closes every world pane whose source is gone or farther than its reach (or the
    /// default reach when the pane names none). <paramref name="distanceTo"/> answers null for a source the
    /// client no longer knows.
    /// </summary>
    public List<Leaf> Depart(Func<long, float?> distanceTo, float defaultReach)
    {
        var departed = new List<Leaf>();
        foreach (var leaf in Book.Within(LeafGroup.World))
        {
            if (leaf.Source == 0)
            {
                continue;
            }
            var reach = leaf.Reach > 0 ? leaf.Reach : defaultReach;
            var d = distanceTo(leaf.Source);
            if (d is null || d > reach)
            {
                departed.Add(leaf);
            }
        }
        foreach (var leaf in departed)
        {
            CloseWorld(leaf.Id);
        }
        return departed;
    }
}
