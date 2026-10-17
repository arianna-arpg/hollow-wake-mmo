using Godot;
using HollowWake.Client.UI.Book;
using HollowWake.Client.UI.Core;
using HollowWake.Protocol;

namespace HollowWake.Client.UI.Pages;

/// <summary>
/// A container's board as a page or a pane: the reliquary (whose seated relics are active), a stash with its
/// pages, a station's board. It shows each board the inventory state sends for the container, its unlock rungs
/// with what each requires, and why it cannot be changed from here when it cannot. Items move between it and
/// the bag by drag and drop or click-lift, one move intent each.
/// </summary>
public partial class ContainerPage : LeafView
{
    private readonly Func<ContainerInfo, bool> _matches;
    private readonly string _emptyText;
    private VBoxContainer _column = null!;
    private HBoxContainer _pages = null!;
    private BoardView _board = null!;
    private Label _note = null!;
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

    public override IEnumerable<IDropTarget> DropTargets => _board is { } b && b.Info is not null ? new IDropTarget[] { b } : Array.Empty<IDropTarget>();

    public ContainerInfo? Shown { get; private set; }

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
        _board = new BoardView(Ctx.Items, "");
        _column.AddChild(_board);
        _rungs = new VBoxContainer();
        _column.AddChild(_rungs);
    }

    public override void Refresh()
    {
        if (Hero.InventoryVersion == _version)
        {
            return;
        }
        _version = Hero.InventoryVersion;
        var containers = Hero.Inventory?.Containers.Where(_matches).OrderBy(c => c.Page ?? 0).ToList() ?? new List<ContainerInfo>();
        Clear(_pages);
        Clear(_rungs);
        if (containers.Count == 0)
        {
            Shown = null;
            _note.Text = _emptyText;
            _board.Show(null, 0);
            return;
        }
        if (containers.All(c => c.Id != _selected))
        {
            _selected = containers[0].Id;
        }
        if (containers.Count > 1)
        {
            foreach (var c in containers)
            {
                var id = c.Id;
                var tab = MakeButton(c.Page is { } page ? $"Page {page}" : c.Name, () =>
                {
                    _selected = id;
                    _version = -1;
                    Refresh();
                }, tip: c.Locked == true && c.Price is { } price ? "Locked: " + string.Join(", ", price.Select(p => $"{p.Value:0} {Skin.Names.CurrencyName(p.Key)}")) : null);
                tab.ToggleMode = true;
                tab.ButtonPressed = id == _selected;
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
        if (shown.Locked == true)
        {
            notes.Add("This page is not yet bought" + (shown.Price is { } cost ? ": " + string.Join(", ", cost.Select(p => $"{p.Value:0} {Skin.Names.CurrencyName(p.Key)}")) : "."));
        }
        _note.Text = string.Join(" ", notes);
        _board.Container = shown.Id;
        _board.Show(shown.Board, Skin.Metric("cell", 36), locked: !shown.Reachable || shown.Locked == true);
        foreach (var rung in shown.Board.Rungs ?? new List<RungInfo>())
        {
            var text = $"Rung {rung.Index + 1}: {rung.Cells.Count} cell{(rung.Cells.Count == 1 ? "" : "s")}{(rung.Unlocked ? "" : rung.Requirement is { } req ? ", requires " + req : ", locked")}";
            _rungs.AddChild(MakeLabel(text, rung.Unlocked ? Skin.S("ink") : Skin.S("ink_muted"), Skin.BodySize - 1));
        }
    }
}
