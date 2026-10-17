using Godot;
using HollowWake.Client.UI.Logic;
using HollowWake.Client.UI.Widgets;

namespace HollowWake.Client.UI.Book;

/// <summary>
/// One book on screen: a framed pane drawing its front leaf, with a tab strip when it holds two or more
/// leaves. Tabs keep binding order; a fresh tab glows until it is looked at. The close glyph closes the
/// front leaf through its own close path.
/// </summary>
public partial class BookView : Control
{
    private readonly Skin _skin;
    private Pane _pane = null!;
    private LeafView? _shown;

    public event Action<string>? TabSelected;
    public event Action<string>? CloseRequested;

    public BookView(Skin skin, string name)
    {
        _skin = skin;
        Name = name;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public string? FrontId { get; private set; }

    public override void _Ready()
    {
        _pane = new Pane(_skin, "", closable: true, draggable: false) { Name = "frame" };
        AddChild(_pane);
        _pane.TabSelected += id => TabSelected?.Invoke(id);
        _pane.CloseRequested += () =>
        {
            if (FrontId is { } id)
            {
                CloseRequested?.Invoke(id);
            }
        };
    }

    /// <summary>Draws a book: its leaves in tab order, the front one's view in the body.</summary>
    public void Show(Box box, IReadOnlyList<Leaf> leaves, Leaf front, LeafView view, bool emphasised)
    {
        Visible = true;
        Position = new Vector2(box.X, box.Y);
        Size = new Vector2(box.W, box.H);
        _pane.Position = Vector2.Zero;
        _pane.Size = Size;
        _pane.Title = front.Title;
        _pane.Emphasised = emphasised;
        _pane.SetTabs(leaves.Count >= 2
            ? leaves.Select(l => new TabInfo(l.Id, l.Title, l.Id == front.Id, l.Fresh)).ToList()
            : Array.Empty<TabInfo>());
        if (_shown != view)
        {
            if (_shown is not null && IsInstanceValid(_shown) && _shown.GetParent() == _pane.Body)
            {
                _pane.Body.RemoveChild(_shown);
            }
            if (view.GetParent() is { } parent && parent != _pane.Body)
            {
                parent.RemoveChild(view);
            }
            if (view.GetParent() is null)
            {
                _pane.Body.AddChild(view);
            }
            _shown = view;
        }
        FrontId = front.Id;
        _pane.Refresh();
    }

    public void Hide2()
    {
        Visible = false;
        if (_shown is not null && IsInstanceValid(_shown) && _shown.GetParent() == _pane.Body)
        {
            _pane.Body.RemoveChild(_shown);
        }
        _shown = null;
        FrontId = null;
    }

    /// <summary>Lets go of a leaf view that is about to be freed.</summary>
    public void Forget(LeafView view)
    {
        if (_shown == view)
        {
            if (IsInstanceValid(view) && view.GetParent() == _pane.Body)
            {
                _pane.Body.RemoveChild(view);
            }
            _shown = null;
            FrontId = null;
        }
    }

    public LeafView? Shown => _shown;
}
