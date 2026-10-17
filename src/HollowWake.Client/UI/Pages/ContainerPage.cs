using Godot;
using HollowWake.Client.UI.Book;
using HollowWake.Client.UI.Core;
using HollowWake.Protocol;

namespace HollowWake.Client.UI.Pages;

/// <summary>
/// A container's board as a page or a pane: the reliquary (whose seated relics are active), the stashes with
/// their pages, a station's board. It shows each board the inventory state sends for the page, a tab per board
/// when there are several (a stash's pages, named by their stash when more than one stash is shown), its unlock
/// rungs with what each requires, why it cannot be changed from here when it cannot, a page not yet bought with
/// its price, and the board's overflow (items it holds without a cell) to take them out of. Items move between
/// it and the bag by drag and drop or click-lift, one move intent each.
/// </summary>
public partial class ContainerPage : LeafView
{
    private readonly Func<ContainerInfo, bool> _matches;
    private readonly string _emptyText;
    private VBoxContainer _column = null!;
    private HBoxContainer _pages = null!;
    private BoardView _board = null!;
    private Label _note = null!;
    private Button _buy = null!;
    private Label _overflowNote = null!;
    private BoardView _overflow = null!;
    private VBoxContainer _rungs = null!;
    private string _selected = "";
    private int _version = -1;

    /// <param name="matches">Which containers this page shows (a kind, or the pages of one stash).</param>
    public ContainerPage(LeafContext context, Func<ContainerInfo, bool> matches, string emptyText) : base(context)
    {
        _matches = matches;
        _emptyText = emptyText;
        Name = "ContainerPage";
    }

    public override IEnumerable<IDropTarget> DropTargets
    {
        get
        {
            if (_board is { } b && b.Info is not null)
            {
                yield return b;
            }
            if (_overflow is { } o && o.Info is not null && o.Visible)
            {
                yield return o;
            }
        }
    }

    public ContainerInfo? Shown { get; private set; }

    /// <summary>The boards the page tabs between: every matching container but the overflow boards, in page order.</summary>
    public IReadOnlyList<ContainerInfo> Boards =>
        Hero.Inventory?.Containers.Where(c => c.Kind != "overflow" && _matches(c)).OrderBy(c => c.Of ?? c.Id, StringComparer.Ordinal).ThenBy(c => c.Page ?? 0).ToList()
        ?? new List<ContainerInfo>();

    public override void _Ready()
    {
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Auto };
        AddChild(scroll);
        scroll.SetAnchorsPreset(LayoutPreset.FullRect);
        _column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _column.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_column);
        _pages = new HBoxContainer();
        _column.AddChild(_pages);
        _note = MakeLabel("", Skin.S("ink_muted"), wrap: true);
        _note.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _column.AddChild(_note);
        // Buys the stash's next page at the server's price; the server answers with the page unlocked.
        _buy = MakeButton("Buy this page", () =>
        {
            if (Shown is { Locked: true } page)
            {
                Ctx.Send(new BuyPageMsg(page.Of ?? page.Id));
            }
        });
        _buy.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        _buy.Visible = false;
        _column.AddChild(_buy);
        _board = new BoardView(Ctx.Items, "");
        _column.AddChild(_board);
        _rungs = new VBoxContainer();
        _column.AddChild(_rungs);
        _overflowNote = MakeLabel("", Skin.S("ink_muted"), wrap: true);
        _overflowNote.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _column.AddChild(_overflowNote);
        _overflow = new BoardView(Ctx.Items, "");
        _column.AddChild(_overflow);
    }

    public override void Refresh()
    {
        if (Hero.InventoryVersion == _version)
        {
            return;
        }
        _version = Hero.InventoryVersion;
        var containers = Boards;
        Clear(_pages);
        Clear(_rungs);
        if (containers.Count == 0)
        {
            Shown = null;
            _note.Text = _emptyText;
            _buy.Visible = false;
            _board.Show(null, 0);
            ShowOverflow(null);
            return;
        }
        if (containers.All(c => c.Id != _selected))
        {
            _selected = containers[0].Id;
        }
        if (containers.Count > 1)
        {
            // Pages of one stash read "Page 2"; with more than one stash shown, each tab names its stash.
            var stashes = containers.Select(c => c.Of ?? c.Id).Distinct(StringComparer.Ordinal).Count();
            foreach (var c in containers)
            {
                var id = c.Id;
                var label = c.Page is { } page ? (stashes > 1 ? $"{c.Name} {page}" : $"Page {page}") : c.Name;
                var tab = MakeButton(label, () =>
                {
                    _selected = id;
                    _version = -1;
                    Refresh();
                }, tip: c.Locked == true && c.Price is { } price ? "Not yet bought: " + PriceText(price) : null);
                tab.ToggleMode = true;
                tab.ButtonPressed = id == _selected;
                if (c.Locked == true)
                {
                    tab.Modulate = new Color(1, 1, 1, 0.6f);
                }
                _pages.AddChild(tab);
            }
        }
        var shown = containers.First(c => c.Id == _selected);
        Shown = shown;
        var notes = new List<string>();
        if (shown.Active == true)
        {
            notes.Add("Items seated here are active.");
        }
        if (!shown.Reachable)
        {
            notes.Add(shown.Reason ?? "Out of reach from here.");
        }
        // Pages are bought in order: the first locked page of its stash can be bought, later ones wait for it.
        var next = shown.Locked == true
            ? containers.Where(c => c.Locked == true && (c.Of ?? c.Id) == (shown.Of ?? shown.Id)).OrderBy(c => c.Page ?? 0).FirstOrDefault()
            : null;
        if (shown.Locked == true)
        {
            notes.Add("This page is not yet bought" + (shown.Price is { } cost ? ": " + PriceText(cost) + "." : "."));
            if (next is not null && next.Id != shown.Id)
            {
                notes.Add("The pages before it are bought first.");
            }
        }
        _note.Text = string.Join(" ", notes);
        _buy.Visible = next is not null && next.Id == shown.Id;
        _buy.Disabled = !shown.Reachable;
        _buy.TooltipText = shown.Reachable ? (shown.Price is { } p ? "Costs " + PriceText(p) : "") : shown.Reason ?? "Out of reach from here.";
        _board.Container = shown.Id;
        _board.Show(shown.Board, Skin.Metric("cell", 36), locked: !shown.Reachable || shown.Locked == true);
        foreach (var rung in shown.Board.Rungs ?? new List<RungInfo>())
        {
            var text = $"Rung {rung.Index + 1}: {rung.Cells.Count} cell{(rung.Cells.Count == 1 ? "" : "s")}{(rung.Unlocked ? "" : rung.Requirement is { } req ? ", requires " + req : ", locked")}";
            _rungs.AddChild(MakeLabel(text, rung.Unlocked ? Skin.S("ink") : Skin.S("ink_muted"), Skin.BodySize - 1));
        }
        ShowOverflow(Hero.Inventory?.Containers.FirstOrDefault(c => c.Kind == "overflow" && c.Of == shown.Id));
    }

    /// <summary>The items a board holds without a cell, to drag out of (nothing drops onto them).</summary>
    private void ShowOverflow(ContainerInfo? overflow)
    {
        _overflowNote.Visible = overflow is not null;
        _overflow.Visible = overflow is not null;
        if (overflow is null)
        {
            _overflow.Show(null, 0);
            return;
        }
        _overflowNote.Text = $"{overflow.Name}: {overflow.Reason ?? "items without a place on the board."}";
        _overflow.Container = overflow.Id;
        _overflow.Show(overflow.Board, Skin.Metric("cell", 36), locked: true);
    }

    private string PriceText(IReadOnlyDictionary<string, double> price) =>
        string.Join(", ", price.Select(p => $"{p.Value:0} {Skin.Names.CurrencyName(p.Key)}"));
}
