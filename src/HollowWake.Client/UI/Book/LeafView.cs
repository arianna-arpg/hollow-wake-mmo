using Godot;
using HollowWake.Client.UI.Core;
using HollowWake.Protocol;

namespace HollowWake.Client.UI.Book;

/// <summary>What a page or a world pane reads and how it acts: the hero's states, the skin, the item host and the wire.</summary>
public sealed class LeafContext
{
    public required Skin Skin { get; init; }
    public required HeroState Hero { get; init; }
    public required IItemHost Items { get; init; }
    public required Action<Message> Send { get; init; }

    /// <summary>Opens a small menu of actions at a screen point (right-click quick actions).</summary>
    public required Action<Vector2, IReadOnlyList<(string Label, Action Act)>> Menu { get; init; }

    public double Now { get; set; }
}

/// <summary>The content of a leaf of a book: a development page or a pane the world opened.</summary>
public abstract partial class LeafView : Control
{
    protected LeafView(LeafContext context)
    {
        Ctx = context;
        MouseFilter = MouseFilterEnum.Pass;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
    }

    protected LeafContext Ctx { get; }
    protected Skin Skin => Ctx.Skin;
    protected HeroState Hero => Ctx.Hero;

    /// <summary>The width the leaf would like beside the core pane.</summary>
    public virtual float PreferredWidth => Skin.Metric("page_width", 460);

    /// <summary>Called every frame while the leaf is drawn; rebuild only when what it reads changed.</summary>
    public abstract void Refresh();

    /// <summary>Drop targets the leaf offers while an item is carried.</summary>
    public virtual IEnumerable<IDropTarget> DropTargets => Array.Empty<IDropTarget>();

    /// <summary>A small label in the theme's ink.</summary>
    protected Label MakeLabel(string text, Color? color = null, int? size = null, bool wrap = false, Font? font = null)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off,
            MouseFilter = MouseFilterEnum.Pass,
        };
        label.AddThemeColorOverride("font_color", color ?? Skin.S("ink"));
        label.AddThemeFontSizeOverride("font_size", size ?? Skin.BodySize);
        if (font is not null)
        {
            label.AddThemeFontOverride("font", font);
        }
        return label;
    }

    protected Button MakeButton(string text, Action pressed, bool enabled = true, string? tip = null)
    {
        var button = new Button { Text = text, FocusMode = FocusModeEnum.None, Disabled = !enabled, TooltipText = tip ?? "", MouseFilter = MouseFilterEnum.Stop };
        button.Pressed += pressed;
        return button;
    }

    protected static void Clear(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }
}
