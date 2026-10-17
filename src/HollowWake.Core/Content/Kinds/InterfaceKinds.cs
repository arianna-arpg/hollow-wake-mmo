using System.Globalization;
using HollowWake.Core.Definitions;

namespace HollowWake.Core.Content.Kinds;

/// <summary>
/// Content for the interface: themes, icon sets, pages, the layout and the character sheet. The clients
/// read them; the server reads the pages' gates and pip sources to serve the page state. Registers the
/// kinds and their validators.
/// </summary>
internal static class InterfaceKinds
{
    /// <summary>The frame styles both clients draw. A theme names one of them and colours it.</summary>
    public static readonly IReadOnlyList<string> FrameStyles = new[] { "stone", "brass" };

    /// <summary>Where a page lives: the core pane itself, a ribbon page docked beside it, or a popout ribbon.</summary>
    public static readonly IReadOnlyList<string> Places = new[] { "core", "ribbon", "popout" };

    /// <summary>What a sheet section lists.</summary>
    public static readonly IReadOnlyList<string> SectionKinds = new[] { "attributes", "threads", "quests" };

    public static void Register()
    {
        // Page gates and pip sources read the hero; the page scope adds the unspent points of each currency.
        ScopeNames.DeclarePrefix("points.");
        ContentKinds.Register<UiThemeDef>("ui_themes", Theme);
        ContentKinds.Register<UiIconSetDef>("ui_icon_sets", IconSet);
        ContentKinds.Register<UiPageDef>("ui_pages", Page);
        ContentKinds.Register<UiLayoutDef>("ui_layouts", Layout);
        ContentKinds.Register<UiSheetDef>("ui_sheets", Sheet);
    }

    /// <summary>A colour as the clients read it: #rgb, #rrggbb or #rrggbbaa.</summary>
    public static bool IsColor(string? text)
    {
        if (text is null || text.Length < 4 || text[0] != '#')
        {
            return false;
        }
        var hex = text.AsSpan(1);
        if (hex.Length is not (3 or 6 or 8))
        {
            return false;
        }
        return uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _);
    }

    private static void Colors(ValidationContext c, IEnumerable<KeyValuePair<string, string>> colors, string field)
    {
        foreach (var (key, value) in colors)
        {
            if (!IsColor(value))
            {
                c.Error($"{field}.{key} '{value}' is not a colour (#rgb, #rrggbb or #rrggbbaa).");
            }
        }
    }

    private static void Theme(UiThemeDef d, ValidationContext c)
    {
        if (!FrameStyles.Contains(d.Frame))
        {
            c.Error($"frame '{d.Frame}' is not a frame style the clients draw ({string.Join(", ", FrameStyles)}).");
        }
        c.Ref<UiIconSetDef>(d.IconSet, "icon_set");
        Colors(c, d.Surface, "surface");
        Colors(c, d.Threads, "threads");
        Colors(c, d.Rarities, "rarities");
        Colors(c, d.Notices, "notices");
        Colors(c, d.States, "states");
        foreach (var (resource, swatch) in d.Resources)
        {
            Colors(c, new Dictionary<string, string> { ["fill"] = swatch.Fill, ["deep"] = swatch.Deep }, "resources." + resource);
        }
        Colors(c, new Dictionary<string, string> { ["fill"] = d.ResourceFallback.Fill, ["deep"] = d.ResourceFallback.Deep }, "resource_fallback");
        foreach (var thread in d.Threads.Keys)
        {
            c.Ref<ThreadDef>(thread, "threads");
        }
        foreach (var thread in c.Registry.Each<ThreadDef>())
        {
            if (!d.Threads.ContainsKey(thread.Id))
            {
                c.Warn($"Thread '{thread.Id}' has no colour in the palette.");
            }
        }
        foreach (var (role, font) in d.Fonts)
        {
            if (font.Families.Count == 0)
            {
                c.Error($"fonts.{role} names no family.");
            }
            if (font.Size <= 0)
            {
                c.Error($"fonts.{role}.size must be positive.");
            }
        }
        foreach (var (name, value) in d.Material)
        {
            if (value < 0 || double.IsNaN(value))
            {
                c.Error($"material.{name} must not be negative.");
            }
        }
        // Exactly one default; reported once, on the first theme in id order.
        var themes = c.Registry.All<UiThemeDef>();
        var first = themes.Keys.Min(StringComparer.Ordinal);
        if (d.Id == first)
        {
            var defaults = themes.Values.Count(t => t.Default);
            if (defaults != 1)
            {
                c.Error($"exactly one theme must be the default; {defaults} are.");
            }
        }
    }

    private static void IconSet(UiIconSetDef d, ValidationContext c)
    {
        if (d.Box <= 0)
        {
            c.Error("box must be positive.");
        }
        if (d.Stroke <= 0)
        {
            c.Error("stroke must be positive.");
        }
        foreach (var (id, path) in d.Icons)
        {
            var trimmed = path.TrimStart();
            if (trimmed.Length == 0 || (trimmed[0] != 'M' && trimmed[0] != 'm'))
            {
                c.Error($"icons.{id} is not SVG path data (it must start with a move-to).");
            }
        }
        foreach (var id in d.Filled)
        {
            if (!d.Icons.ContainsKey(id))
            {
                c.Error($"filled names '{id}', which is not an icon of the set.");
            }
        }
        void Mapped(Dictionary<string, string> map, string field)
        {
            foreach (var (key, icon) in map)
            {
                if (!d.Icons.ContainsKey(icon))
                {
                    c.Error($"{field}.{key} names '{icon}', which is not an icon of the set.");
                }
            }
        }
        Mapped(d.Pools, "pools");
        Mapped(d.Statuses, "statuses");
        Mapped(d.Looks, "looks");
        Mapped(d.Slots, "slots");
        if (!d.Icons.ContainsKey(d.Fallback))
        {
            c.Error($"fallback '{d.Fallback}' is not an icon of the set.");
        }
        // A glyph for a status or slot another layer removed is harmless, so these only warn.
        foreach (var status in d.Statuses.Keys.Where(s => !c.Registry.Has<StatusDef>(s)))
        {
            c.Warn($"statuses.{status} names no declared status.");
        }
        foreach (var slot in d.Slots.Keys.Where(s => !c.Registry.Has<SlotDef>(s)))
        {
            c.Warn($"slots.{slot} names no declared equipment slot.");
        }
    }

    private static void Page(UiPageDef d, ValidationContext c)
    {
        if (!Places.Contains(d.Place))
        {
            c.Error($"place '{d.Place}' is not one of {string.Join(", ", Places)}.");
        }
        if (d.Key.Trim().Length == 0)
        {
            c.Warn("page has no key; it can still be requested from the menu and the ribbon.");
        }
        if (d.Icon.Length > 0 && !c.Registry.Each<UiIconSetDef>().Any(s => s.Icons.ContainsKey(d.Icon)))
        {
            c.Warn($"icon '{d.Icon}' is in no icon set.");
        }
        c.Expression(d.Gate, "gate");
        c.Expression(d.Pips, "pips");
        var pages = c.Registry.All<UiPageDef>();
        foreach (var other in pages.Values)
        {
            if (other.Id != d.Id && d.Key.Length > 0 && string.Equals(other.Key, d.Key, StringComparison.OrdinalIgnoreCase) && string.CompareOrdinal(other.Id, d.Id) < 0)
            {
                c.Error($"key '{d.Key}' is also the key of page '{other.Id}'.");
            }
        }
        if (d.Id == pages.Keys.Min(StringComparer.Ordinal))
        {
            var cores = pages.Values.Count(p => p.Place == "core");
            if (cores != 1)
            {
                c.Error($"exactly one page is the core pane (place: core); {cores} are.");
            }
        }
    }

    private static void Layout(UiLayoutDef d, ValidationContext c)
    {
        if (d.Orbs.Count == 0)
        {
            c.Error("the cluster has no orbs.");
        }
        foreach (var orb in d.Orbs)
        {
            if (orb.Side is not ("left" or "right"))
            {
                c.Error($"orb side '{orb.Side}' must be left or right.");
            }
            if (orb.Fill.Length == 0)
            {
                c.Error("an orb names no resource to fill it.");
            }
        }
        var pools = d.Orbs.SelectMany(o => new[] { o.Fill }.Concat(o.Overlays).Concat(o.Rim).Concat(o.Rings)).Where(p => p.Length > 0).ToList();
        foreach (var dup in pools.GroupBy(p => p).Where(g => g.Count() > 1))
        {
            c.Error($"pool '{dup.Key}' is drawn more than once in the cluster.");
        }
        if (d.Ranks.Count == 0)
        {
            c.Error("ranks is empty; the book needs a primacy ladder.");
        }
        foreach (var dup in d.Ranks.GroupBy(r => r).Where(g => g.Count() > 1))
        {
            c.Error($"rank '{dup.Key}' is listed twice.");
        }
        foreach (var (pane, rank) in d.Panes)
        {
            if (!d.Ranks.Contains(rank))
            {
                c.Error($"panes.{pane} has rank '{rank}', which is not on the ladder.");
            }
        }
        foreach (var (slot, cell) in d.Doll)
        {
            if (!c.Registry.Has<SlotDef>(slot))
            {
                c.Warn($"doll.{slot} names no declared equipment slot.");
            }
            if (cell.Length != 2 || cell[0] < 0 || cell[1] < 0)
            {
                c.Error($"doll.{slot} must be a [column, row] pair of non-negative numbers.");
            }
        }
        foreach (var (name, value) in d.Metrics)
        {
            if (value < 0 || double.IsNaN(value))
            {
                c.Error($"metrics.{name} must not be negative.");
            }
        }
    }

    private static void Sheet(UiSheetDef d, ValidationContext c)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in d.Sections)
        {
            if (s.Id.Length == 0 || !ids.Add(s.Id))
            {
                c.Error($"section '{s.Id}' needs a unique id.");
            }
            if (!SectionKinds.Contains(s.Kind))
            {
                c.Error($"section '{s.Id}' has kind '{s.Kind}', not one of {string.Join(", ", SectionKinds)}.");
                continue;
            }
            foreach (var a in s.Attributes)
            {
                if (!c.Registry.Attributes.Contains(a))
                {
                    c.Warn($"section '{s.Id}' lists '{a}', which is not a declared attribute.");
                }
            }
            if (s.Kind == "threads")
            {
                if (s.Pattern is null || !s.Pattern.Contains("{thread}", StringComparison.Ordinal))
                {
                    c.Error($"section '{s.Id}' needs a pattern with {{thread}}.");
                }
                if (s.Cap is not null && !s.Cap.Contains("{thread}", StringComparison.Ordinal))
                {
                    c.Error($"section '{s.Id}' has a cap pattern without {{thread}}.");
                }
            }
        }
    }
}
