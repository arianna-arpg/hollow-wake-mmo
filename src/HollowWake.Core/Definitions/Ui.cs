using HollowWake.Core.Expressions;

namespace HollowWake.Core.Definitions;

// The interface as content: the look (themes and icon sets), the pages of the core pane, the layout of the
// cluster and the panes, and the character sheet's sections. The clients read these; the server reads the
// pages' gates and pip sources to serve the page state. Nothing in the simulation reads a colour or a font.

/// <summary>
/// A look for the interface: the frame style, the surface colours, the palette per resource, per Thread,
/// per rarity and per notice, the fonts and the icon set. Several themes may exist; the clients draw the
/// one marked default unless the player picks another.
/// </summary>
public sealed record UiThemeDef : Definition
{
    /// <summary>The theme the clients start with. Exactly one theme is the default.</summary>
    public bool Default { get; init; }

    /// <summary>The frame style the clients draw panes and the cluster with (a style both clients know).</summary>
    public string Frame { get; init; } = "stone";

    /// <summary>The icon set every glyph comes from.</summary>
    public string IconSet { get; init; } = "runic";

    /// <summary>Fonts by role: body, title, numerals.</summary>
    public Dictionary<string, UiFontDef> Fonts { get; init; } = new();

    /// <summary>Named surface colours: the panes, their edges, wells, ink, accents.</summary>
    public Dictionary<string, string> Surface { get; init; } = new();

    /// <summary>Surface tunables: grain, fibre, vignette, bevel, corner radius, glass and etching strength.</summary>
    public Dictionary<string, double> Material { get; init; } = new();

    /// <summary>The palette per resource: the fill of its orb, ring or band and the deep end of its gradient.</summary>
    public Dictionary<string, UiSwatchDef> Resources { get; init; } = new();

    /// <summary>The swatch of a resource the palette does not name.</summary>
    public UiSwatchDef ResourceFallback { get; init; } = new();

    /// <summary>The palette per Thread.</summary>
    public Dictionary<string, string> Threads { get; init; } = new();

    /// <summary>Item and monster rarities.</summary>
    public Dictionary<string, string> Rarities { get; init; } = new();

    /// <summary>Notice levels: info, levelup, quest, event, discovery, error.</summary>
    public Dictionary<string, string> Notices { get; init; } = new();

    /// <summary>Interface states: reserved caps, broken rings, dormant and sealed faces, pips, badges, cooldown shade.</summary>
    public Dictionary<string, string> States { get; init; } = new();
}

/// <summary>A font: families in order of preference (generic names such as serif last), a size in pixels and a weight.</summary>
public sealed record UiFontDef
{
    public List<string> Families { get; init; } = new();
    public double Size { get; init; } = 14;
    public int Weight { get; init; } = 400;
}

/// <summary>A pool's colours: its fill and the deep end of its gradient.</summary>
public sealed record UiSwatchDef
{
    public string Fill { get; init; } = "#9a9a9a";
    public string Deep { get; init; } = "#333333";
}

/// <summary>
/// An icon set: glyphs as SVG path data in a square view box, drawn as strokes unless listed as filled, and
/// which glyph stands for each pool, status, weave look and equipment slot.
/// </summary>
public sealed record UiIconSetDef : Definition
{
    /// <summary>The side of the square view box the paths are drawn in.</summary>
    public double Box { get; init; } = 24;

    /// <summary>The stroke width of stroked glyphs, in view box units.</summary>
    public double Stroke { get; init; } = 1.75;

    /// <summary>Glyph id to SVG path data.</summary>
    public Dictionary<string, string> Icons { get; init; } = new();

    /// <summary>Glyphs drawn filled rather than stroked.</summary>
    public List<string> Filled { get; init; } = new();

    public Dictionary<string, string> Pools { get; init; } = new();
    public Dictionary<string, string> Statuses { get; init; } = new();

    /// <summary>Weave looks and form ids to glyphs.</summary>
    public Dictionary<string, string> Looks { get; init; } = new();

    public Dictionary<string, string> Slots { get; init; } = new();

    /// <summary>The glyph of anything the maps do not name.</summary>
    public string Fallback { get; init; } = "rune";
}

/// <summary>
/// A page of the core pane: the core pane itself, a ribbon page or a popout ribbon. The page state the server
/// sends is keyed by these ids: it evaluates the gate (the existence law: a page whose gate fails is absent)
/// and the pip source (how many things on the page are unspent) over the hero.
/// </summary>
public sealed record UiPageDef : Definition
{
    /// <summary>The key that requests the page, as a key name (I, K, Tab, F1).</summary>
    public string Key { get; init; } = "";

    public string Icon { get; init; } = "";
    public int Order { get; init; }

    /// <summary>core (the core pane itself), ribbon (a page docked beside it) or popout (a ribbon that opens a popout).</summary>
    public string Place { get; init; } = "ribbon";

    /// <summary>The existence law, evaluated by the server over the hero; absent means the page always exists.</summary>
    public Expr? Gate { get; init; }

    /// <summary>How many things on the page are unspent, evaluated by the server over the hero; absent means none.</summary>
    public Expr? Pips { get; init; }

    /// <summary>For a container page: the kind of container it shows (a stash, a station's board).</summary>
    public string? Container { get; init; }
}

/// <summary>
/// The arrangement the interface follows: the cluster's orbs with the pools drawn on and around them, the
/// equipment doll's cells, the book's primacy ladder and the rank of each world pane, the keys of the
/// interface itself and the sizes and tunables of every surface.
/// </summary>
public sealed record UiLayoutDef : Definition
{
    /// <summary>The orbs of the cluster with their fill and the pools on and around them, nearest first.</summary>
    public List<UiOrbDef> Orbs { get; init; } = new();

    /// <summary>The book's primacy ladder, lowest first: a newcomer of a higher rank takes the front.</summary>
    public List<string> Ranks { get; init; } = new();

    /// <summary>World pane kinds to their rank on the ladder.</summary>
    public Dictionary<string, string> Panes { get; init; } = new();

    /// <summary>Equipment slots to [column, row] cells of the doll; slots it does not place follow in slot order.</summary>
    public Dictionary<string, double[]> Doll { get; init; } = new();

    /// <summary>Interface keys by role (menu, book_next, book_previous), as key names.</summary>
    public Dictionary<string, string> Keys { get; init; } = new();

    /// <summary>Sizes in pixels at scale one and other client tunables, by name.</summary>
    public Dictionary<string, double> Metrics { get; init; } = new();
}

/// <summary>One orb of the cluster.</summary>
public sealed record UiOrbDef
{
    /// <summary>left or right of the weave bar.</summary>
    public string Side { get; init; } = "left";

    /// <summary>The resource that fills the orb.</summary>
    public string Fill { get; init; } = "";

    /// <summary>Pools laid over the fill from the top (a shield veils the orb).</summary>
    public List<string> Overlays { get; init; } = new();

    /// <summary>Pools drawn as an etched band hugging the orb (ward).</summary>
    public List<string> Rim { get; init; } = new();

    /// <summary>Pools drawn as concentric rings around the orb, nearest first.</summary>
    public List<string> Rings { get; init; } = new();
}

/// <summary>The character sheet: sections in order, each listing attributes, one row per Thread, or the quest log.</summary>
public sealed record UiSheetDef : Definition
{
    public List<UiSheetSectionDef> Sections { get; init; } = new();
}

/// <summary>
/// One section of the sheet. <c>attributes</c> lists the named attributes, then every attribute of the named
/// categories in content order, leaving out ids that start with an excluded prefix; <c>threads</c> lists one
/// row per Thread from a pattern such as <c>resist.{thread}</c>, with an optional cap pattern; <c>quests</c>
/// lists the quest log.
/// </summary>
public sealed record UiSheetSectionDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Kind { get; init; } = "attributes";
    public List<string> Attributes { get; init; } = new();
    public List<string> Categories { get; init; } = new();
    public List<string> Exclude { get; init; } = new();
    public string? Pattern { get; init; }
    public string? Cap { get; init; }
}
