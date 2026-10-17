namespace HollowWake.Client.UI.Logic;

/// <summary>Which side of the core pane a leaf belongs to: the hero's development pages or the panes the world opens.</summary>
public enum LeafGroup
{
    Page,
    World,
}

/// <summary>How an arrival asks to land: by the laws, at the front (a key, a click, a menu pick) or quietly behind.</summary>
public enum Ask
{
    Auto,
    Front,
    Behind,
}

/// <summary>One pane bound into a book: a development page or a pane the world opened.</summary>
public sealed class Leaf
{
    public required string Id { get; init; }
    public required LeafGroup Group { get; init; }

    /// <summary>The leaf's kind: a page id for pages, the pane kind (dialogue, vendor, ...) for world panes.</summary>
    public required string Kind { get; init; }

    /// <summary>The rung on the primacy ladder: higher ranks take the front from lower ones.</summary>
    public int Rank { get; set; }

    public string Title { get; set; } = "";

    /// <summary>The entity that opened a world pane, or zero.</summary>
    public long Source { get; set; }

    /// <summary>How far the hero may stand from the source before the pane departs; zero takes the default.</summary>
    public float Reach { get; set; }

    /// <summary>When the leaf was bound: tab order is binding order.</summary>
    public long BoundAt { get; set; }

    /// <summary>When the leaf last came to the front; zero if it never has.</summary>
    public long FrontedAt { get; set; }

    /// <summary>Arrived behind and not yet looked at.</summary>
    public bool Fresh { get; set; }

    public override string ToString() => $"{Id}({Group}, rank {Rank}{(Fresh ? ", fresh" : "")})";
}

/// <summary>
/// The book rule, after the prototype's folio in simplified form. Panes that would overlap bind into one book
/// with a tab strip; the core pane never binds. The first leaf opened holds the front; a newcomer of the same
/// rank arrives behind as a fresh tab; a newcomer of a higher rank (a station over a development page, a
/// decision over anything) takes the front; an explicit ask is absolute. Closing the front promotes the leaf
/// the player last looked at, else the first in tab order. Tab order is binding order and never reshuffles.
/// When the page side and the world side of the core pane both have room, each side is its own book;
/// when they would overlap they are one.
/// </summary>
public sealed class BookRule
{
    private readonly List<Leaf> _leaves = new();
    private long _clock;

    /// <summary>Whether pages and world panes share one book (they would overlap) or stand on two sides.</summary>
    public bool Merged { get; set; } = true;

    /// <summary>Every bound leaf in binding (tab) order.</summary>
    public IReadOnlyList<Leaf> Leaves => _leaves;

    public Leaf? Get(string id) => _leaves.Find(l => l.Id == id);

    public bool Has(string id) => _leaves.Exists(l => l.Id == id);

    /// <summary>The leaves of the book a leaf of this group binds into, in tab order.</summary>
    public List<Leaf> BookOf(LeafGroup group) => Merged ? new List<Leaf>(_leaves) : _leaves.FindAll(l => l.Group == group);

    /// <summary>The leaves of one group only, whether or not the sides are merged.</summary>
    public List<Leaf> Within(LeafGroup group) => _leaves.FindAll(l => l.Group == group);

    /// <summary>The front of a set of leaves: the one most recently fronted, else the first in tab order.</summary>
    public static Leaf? FrontOf(IReadOnlyList<Leaf> book)
    {
        Leaf? best = null;
        foreach (var leaf in book)
        {
            if (best is null || leaf.FrontedAt > best.FrontedAt)
            {
                best = leaf;
            }
        }
        return best;
    }

    /// <summary>The drawn front of the book a group binds into.</summary>
    public Leaf? Front(LeafGroup group) => FrontOf(BookOf(group));

    /// <summary>The front among one group's leaves, whatever the other side shows.</summary>
    public Leaf? FrontWithin(LeafGroup group) => FrontOf(Within(group));

    /// <summary>The fronts that are drawn: one when merged, one per non-empty side otherwise.</summary>
    public List<Leaf> Drawn()
    {
        var drawn = new List<Leaf>(2);
        if (Merged)
        {
            if (FrontOf(_leaves) is { } f)
            {
                drawn.Add(f);
            }
            return drawn;
        }
        if (FrontWithin(LeafGroup.Page) is { } p)
        {
            drawn.Add(p);
        }
        if (FrontWithin(LeafGroup.World) is { } w)
        {
            drawn.Add(w);
        }
        return drawn;
    }

    public bool IsDrawn(string id) => Drawn().Exists(l => l.Id == id);

    /// <summary>The drawn front the player touched last: what Escape closes and what the Tab walk turns.</summary>
    public Leaf? Active()
    {
        Leaf? best = null;
        foreach (var leaf in Drawn())
        {
            if (best is null || leaf.FrontedAt > best.FrontedAt || (leaf.FrontedAt == best.FrontedAt && leaf.Rank > best.Rank))
            {
                best = leaf;
            }
        }
        return best;
    }

    /// <summary>
    /// Binds a leaf, or updates one already bound. A new leaf takes the front when its book is empty, when it
    /// is asked to, or when it outranks the front; otherwise it arrives behind, fresh. An existing leaf asked to
    /// the front is selected; otherwise it keeps its place.
    /// </summary>
    public Leaf Bind(string id, LeafGroup group, string kind, int rank, string title, Ask ask = Ask.Auto, long source = 0, float reach = 0)
    {
        if (Get(id) is { } existing)
        {
            existing.Title = title;
            existing.Source = source;
            existing.Reach = reach;
            existing.Rank = rank;
            if (ask == Ask.Front)
            {
                Select(existing.Id);
            }
            return existing;
        }
        var front = Front(group);
        var leaf = new Leaf { Id = id, Group = group, Kind = kind, Rank = rank, Title = title, Source = source, Reach = reach, BoundAt = ++_clock };
        _leaves.Add(leaf);
        var fronts = ask switch
        {
            Ask.Front => true,
            Ask.Behind => false,
            _ => front is null || rank > front.Rank,
        };
        if (fronts)
        {
            leaf.FrontedAt = ++_clock;
        }
        else
        {
            leaf.Fresh = true;
        }
        return leaf;
    }

    /// <summary>Brings a leaf to the front of its book (a tab click, a key, the walk); it is no longer fresh.</summary>
    public bool Select(string id)
    {
        var leaf = Get(id);
        if (leaf is null)
        {
            return false;
        }
        leaf.FrontedAt = ++_clock;
        leaf.Fresh = false;
        return true;
    }

    /// <summary>Unbinds a leaf. Its book's front becomes the leaf last looked at, else the first in tab order.</summary>
    public bool Close(string id) => _leaves.RemoveAll(l => l.Id == id) > 0;

    /// <summary>Walks the active book's tabs in tab order (Tab forward, Shift+Tab back); only books of two or more walk.</summary>
    public Leaf? Walk(int direction)
    {
        var active = Active();
        if (active is null)
        {
            return null;
        }
        var book = BookOf(active.Group);
        if (book.Count < 2)
        {
            return null;
        }
        var i = book.IndexOf(active);
        var next = book[((i + Math.Sign(direction == 0 ? 1 : direction)) % book.Count + book.Count) % book.Count];
        Select(next.Id);
        return next;
    }

    /// <summary>Whether a book stands that the Tab walk can turn.</summary>
    public bool CanWalk => Active() is { } a && BookOf(a.Group).Count >= 2;
}
