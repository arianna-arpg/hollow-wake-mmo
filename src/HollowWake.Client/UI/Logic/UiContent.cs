using System.Text.Json.Nodes;

namespace HollowWake.Client.UI.Logic;

/// <summary>A font by role: families in order of preference, a size in pixels at scale one, and a weight.</summary>
public sealed record FontSpec(IReadOnlyList<string> Families, double Size, int Weight)
{
    public static readonly FontSpec Default = new(new[] { "serif" }, 14, 400);
}

/// <summary>A pool's colours: the fill and the deep end of its gradient.</summary>
public readonly record struct Swatch(Rgba Fill, Rgba Deep);

/// <summary>The look of the interface as the clients read it from ui_themes content.</summary>
public sealed class UiTheme
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public bool Default { get; init; }
    public string Frame { get; init; } = "stone";
    public string IconSet { get; init; } = "runic";
    public Dictionary<string, FontSpec> Fonts { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, Rgba> Surface { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, double> Material { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, Swatch> Resources { get; init; } = new(StringComparer.Ordinal);
    public Swatch ResourceFallback { get; init; } = new(new Rgba(0.6f, 0.6f, 0.6f), new Rgba(0.2f, 0.2f, 0.2f));
    public Dictionary<string, Rgba> Threads { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, Rgba> Rarities { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, Rgba> Notices { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, Rgba> States { get; init; } = new(StringComparer.Ordinal);

    public FontSpec Font(string role) => Fonts.TryGetValue(role, out var f) ? f : Fonts.TryGetValue("body", out var body) ? body : FontSpec.Default;

    public Rgba S(string name) => Surface.TryGetValue(name, out var c) ? c : Rgba.White;

    public Rgba S(string name, Rgba fallback) => Surface.TryGetValue(name, out var c) ? c : fallback;

    public double M(string name, double fallback) => Material.TryGetValue(name, out var v) ? v : fallback;

    public Swatch Pool(string resource) => Resources.TryGetValue(resource, out var s) ? s : ResourceFallback;

    public Rgba Thread(string thread) => Threads.TryGetValue(thread, out var c) ? c : S("ink");

    public Rgba Rarity(string? rarity) => rarity is not null && Rarities.TryGetValue(rarity, out var c) ? c : Rarities.TryGetValue("normal", out var n) ? n : S("ink");

    public Rgba Notice(string level) => Notices.TryGetValue(level, out var c) ? c : Notices.TryGetValue("info", out var i) ? i : S("ink");

    public Rgba State(string name, Rgba fallback) => States.TryGetValue(name, out var c) ? c : fallback;

    public static UiTheme From(JsonObject o)
    {
        static Dictionary<string, Rgba> Colors(JsonNode? node)
        {
            var map = new Dictionary<string, Rgba>(StringComparer.Ordinal);
            foreach (var (k, v) in Json.StringMap(node))
            {
                if (Rgba.TryParse(v, out var c))
                {
                    map[k] = c;
                }
            }
            return map;
        }
        static Swatch SwatchOf(JsonObject s, Swatch fallback) =>
            new(Rgba.Parse(Json.Str(s["fill"]), fallback.Fill), Rgba.Parse(Json.Str(s["deep"]), fallback.Deep));

        var fallback = new Swatch(new Rgba(0.6f, 0.6f, 0.6f), new Rgba(0.2f, 0.2f, 0.2f));
        if (o["resource_fallback"] is JsonObject rf)
        {
            fallback = SwatchOf(rf, fallback);
        }
        var resources = new Dictionary<string, Swatch>(StringComparer.Ordinal);
        foreach (var (k, s) in Json.Objects(o["resources"]))
        {
            resources[k] = SwatchOf(s, fallback);
        }
        var fonts = new Dictionary<string, FontSpec>(StringComparer.Ordinal);
        foreach (var (role, f) in Json.Objects(o["fonts"]))
        {
            var families = Json.Strings(f["families"]);
            fonts[role] = new FontSpec(families.Count > 0 ? families : new List<string> { "serif" }, Json.Num(f["size"], 14), (int)Json.Num(f["weight"], 400));
        }
        return new UiTheme
        {
            Id = Json.Str(o["id"]),
            Name = Json.Str(o["name"]),
            Default = Json.Bool(o["default"]),
            Frame = Json.Str(o["frame"], "stone"),
            IconSet = Json.Str(o["icon_set"], "runic"),
            Surface = Colors(o["surface"]),
            Material = Json.NumberMap(o["material"]),
            Threads = Colors(o["threads"]),
            Rarities = Colors(o["rarities"]),
            Notices = Colors(o["notices"]),
            States = Colors(o["states"]),
            Resources = resources,
            ResourceFallback = fallback,
            Fonts = fonts,
        };
    }
}

/// <summary>Glyphs as SVG path data and the maps from pools, statuses, looks and slots to them.</summary>
public sealed class UiIconSet
{
    public string Id { get; init; } = "";
    public double Box { get; init; } = 24;
    public double Stroke { get; init; } = 1.75;
    public Dictionary<string, string> Icons { get; init; } = new(StringComparer.Ordinal);
    public HashSet<string> Filled { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Accents { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Pools { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Statuses { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Looks { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Slots { get; init; } = new(StringComparer.Ordinal);
    public string Fallback { get; init; } = "rune";

    /// <summary>The glyph id to draw for an icon id, the fallback when the set lacks it.</summary>
    public string Resolve(string? icon) => icon is not null && Icons.ContainsKey(icon) ? icon : Fallback;

    public string ForPool(string pool) => Resolve(Pools.GetValueOrDefault(pool));
    public string ForStatus(string status) => Resolve(Statuses.GetValueOrDefault(status));
    public string ForSlot(string slot) => Resolve(Slots.GetValueOrDefault(slot));

    /// <summary>A weave's glyph: its look, else any key word of its key that names a look or form, else the fallback.</summary>
    public string ForWeave(string? look, string? key)
    {
        if (look is not null && Looks.TryGetValue(look, out var byLook))
        {
            return Resolve(byLook);
        }
        if (key is not null)
        {
            foreach (var part in key.Split(new[] { '_', '[', ']', '+', ',', '.', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (Looks.TryGetValue(part, out var byPart))
                {
                    return Resolve(byPart);
                }
            }
        }
        return Fallback;
    }

    /// <summary>An SVG document for a glyph, white strokes on nothing, ready to rasterize and tint.</summary>
    public string Svg(string icon, double pixels, string color = "#ffffff")
    {
        var id = Resolve(icon);
        var path = Icons.GetValueOrDefault(id) ?? "";
        var box = Box.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var size = pixels.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var stroke = Stroke.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var style = Filled.Contains(id)
            ? $"fill=\"{color}\" stroke=\"none\""
            : $"fill=\"none\" stroke=\"{color}\" stroke-width=\"{stroke}\" stroke-linecap=\"round\" stroke-linejoin=\"round\"";
        var accent = Accents.TryGetValue(id, out var detail) ? $"<path d=\"{detail}\" fill=\"{color}\"/>" : "";
        return $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{size}\" height=\"{size}\" viewBox=\"0 0 {box} {box}\"><path d=\"{path}\" {style}/>{accent}</svg>";
    }

    public static UiIconSet From(JsonObject o) => new()
    {
        Id = Json.Str(o["id"]),
        Box = Json.Num(o["box"], 24),
        Stroke = Json.Num(o["stroke"], 1.75),
        Icons = Json.StringMap(o["icons"]),
        Filled = new HashSet<string>(Json.Strings(o["filled"]), StringComparer.Ordinal),
        Accents = Json.StringMap(o["accents"]),
        Pools = Json.StringMap(o["pools"]),
        Statuses = Json.StringMap(o["statuses"]),
        Looks = Json.StringMap(o["looks"]),
        Slots = Json.StringMap(o["slots"]),
        Fallback = Json.Str(o["fallback"], "rune"),
    };
}

/// <summary>A page of the core pane as content declares it.</summary>
public sealed record UiPage(string Id, string Name, string Action, string Icon, int Order, string Place, string Description)
{
    public bool IsCore => Place == "core";
    public bool OnRibbon => Place is "ribbon" or "popout";

    /// <summary>A client page reached from the menu alone (Options); the page state never hides it.</summary>
    public bool InMenuOnly => Place == "menu";

    /// <summary>The kind of container the page shows (reliquary, stash), or null for a page of its own.</summary>
    public string? Container { get; init; }

    public static UiPage From(JsonObject o) => new(
        Json.Str(o["id"]),
        Json.Str(o["name"], Json.Str(o["id"])),
        Json.Str(o["action"], "page_" + Json.Str(o["id"])),
        Json.Str(o["icon"]),
        (int)Json.Num(o["order"]),
        Json.Str(o["place"], "ribbon"),
        Json.Str(o["description"]))
    {
        Container = Json.StrOrNull(o["container"]),
    };
}

/// <summary>One orb of the cluster: the resource that fills it and the pools drawn on and around it, nearest first.</summary>
public sealed record UiOrb(string Side, string Fill, IReadOnlyList<string> Overlays, IReadOnlyList<string> Rim, IReadOnlyList<string> Rings)
{
    public static UiOrb From(JsonObject o) => new(
        Json.Str(o["side"], "left"),
        Json.Str(o["fill"]),
        Json.Strings(o["overlays"]),
        Json.Strings(o["rim"]),
        Json.Strings(o["rings"]));
}

/// <summary>The arrangement of the interface: orbs, the book's ladder, the doll, keys and metrics.</summary>
public sealed class UiLayout
{
    public string Id { get; init; } = "default";
    public List<UiOrb> Orbs { get; init; } = new();
    public List<string> Ranks { get; init; } = new() { "page", "station", "decision" };
    public Dictionary<string, string> Panes { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, (int Column, int Row)> Doll { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, double> Metrics { get; init; } = new(StringComparer.Ordinal);

    public double M(string name, double fallback) => Metrics.TryGetValue(name, out var v) ? v : fallback;

    /// <summary>The rank of a world pane kind on the ladder (higher fronts), or of a page (the lowest).</summary>
    public int RankOf(string kindOrRank)
    {
        var rank = Panes.TryGetValue(kindOrRank, out var r) ? r : kindOrRank;
        var i = Ranks.IndexOf(rank);
        return i < 0 ? 0 : i;
    }

    public UiOrb? OrbOn(string side) => Orbs.FirstOrDefault(o => o.Side == side);

    public static UiLayout From(JsonObject o)
    {
        var doll = new Dictionary<string, (int, int)>(StringComparer.Ordinal);
        if (o["doll"] is JsonObject d)
        {
            foreach (var (slot, cell) in d)
            {
                if (cell is JsonArray a && a.Count == 2)
                {
                    doll[slot] = ((int)Json.Num(a[0]), (int)Json.Num(a[1]));
                }
            }
        }
        var ranks = Json.Strings(o["ranks"]);
        return new UiLayout
        {
            Id = Json.Str(o["id"], "default"),
            Orbs = Json.Items(o["orbs"]).Select(UiOrb.From).ToList(),
            Ranks = ranks.Count > 0 ? ranks : new List<string> { "page", "station", "decision" },
            Panes = Json.StringMap(o["panes"]),
            Doll = doll,
            Metrics = Json.NumberMap(o["metrics"]),
        };
    }
}

/// <summary>A section of the character sheet.</summary>
public sealed record UiSheetSection(string Id, string Name, string Kind, IReadOnlyList<string> Attributes, IReadOnlyList<string> Categories, IReadOnlyList<string> Exclude, string? Pattern, string? Cap)
{
    public static UiSheetSection From(JsonObject o) => new(
        Json.Str(o["id"]),
        Json.Str(o["name"], Json.Str(o["id"])),
        Json.Str(o["kind"], "attributes"),
        Json.Strings(o["attributes"]),
        Json.Strings(o["categories"]),
        Json.Strings(o["exclude"]),
        Json.StrOrNull(o["pattern"]),
        Json.StrOrNull(o["cap"]));
}

/// <summary>
/// Everything the interface reads from its own content: every theme and the one in use, the icon set, the
/// pages in order, the layout and the sheet. Built from raw content entries, so the clients never bind
/// simulation types to draw.
/// </summary>
public sealed class UiContent
{
    public required UiTheme Theme { get; init; }
    public required IReadOnlyList<UiTheme> Themes { get; init; }
    public required UiIconSet Icons { get; init; }
    public required IReadOnlyList<UiPage> Pages { get; init; }
    public required UiLayout Layout { get; init; }
    public required IReadOnlyList<UiSheetSection> Sheet { get; init; }
    public IReadOnlyDictionary<string, UiIconSet> IconSets { get; init; } = new Dictionary<string, UiIconSet>();

    public UiPage? Page(string id) => Pages.FirstOrDefault(p => p.Id == id);

    public UiPage? CorePage => Pages.FirstOrDefault(p => p.IsCore);

    /// <summary>
    /// The same content drawn with a theme and an icon set the player picked: the theme by id (else this one),
    /// the icon set by id, else the one the theme names, else this one.
    /// </summary>
    public UiContent WithLook(string? themeId, string? iconSetId)
    {
        var theme = Themes.FirstOrDefault(t => t.Id == themeId) ?? Theme;
        var icons = iconSetId is not null && IconSets.TryGetValue(iconSetId, out var chosen) ? chosen
            : IconSets.TryGetValue(theme.IconSet, out var named) ? named : Icons;
        return new UiContent { Theme = theme, Themes = Themes, Icons = icons, Pages = Pages, Layout = Layout, Sheet = Sheet, IconSets = IconSets };
    }

    /// <summary>
    /// Reads the interface content. <paramref name="rawOfKind"/> yields the entries of a content kind as JSON
    /// objects; <paramref name="themeId"/> picks a theme, else the default, else the first.
    /// </summary>
    public static UiContent Load(Func<string, IEnumerable<JsonObject>> rawOfKind, string? themeId = null)
    {
        var themes = rawOfKind("ui_themes").Select(UiTheme.From).OrderBy(t => t.Id, StringComparer.Ordinal).ToList();
        if (themes.Count == 0)
        {
            themes.Add(new UiTheme { Id = "plain", Name = "Plain", Default = true });
        }
        var theme = themes.FirstOrDefault(t => t.Id == themeId) ?? themes.FirstOrDefault(t => t.Default) ?? themes[0];
        var sets = LoadIconSets(rawOfKind);
        var icons = sets.TryGetValue(theme.IconSet, out var set) ? set : sets.Values.FirstOrDefault() ?? new UiIconSet();
        var pages = rawOfKind("ui_pages").Select(UiPage.From).OrderBy(p => p.Order).ThenBy(p => p.Id, StringComparer.Ordinal).ToList();
        var layoutRaw = rawOfKind("ui_layouts").ToList();
        var layout = layoutRaw.Select(UiLayout.From).FirstOrDefault(l => l.Id == "default") ?? layoutRaw.Select(UiLayout.From).FirstOrDefault() ?? new UiLayout();
        var sheetRaw = rawOfKind("ui_sheets").FirstOrDefault(s => Json.Str(s["id"]) == "character") ?? rawOfKind("ui_sheets").FirstOrDefault();
        var sheet = sheetRaw is null ? new List<UiSheetSection>() : Json.Items(sheetRaw["sections"]).Select(UiSheetSection.From).ToList();
        return new UiContent { Theme = theme, Themes = themes, Icons = icons, Pages = pages, Layout = layout, Sheet = sheet, IconSets = sets };
    }

    public static Dictionary<string, UiIconSet> LoadIconSets(Func<string, IEnumerable<JsonObject>> rawOfKind) =>
        rawOfKind("ui_icon_sets").Select(UiIconSet.From).ToDictionary(s => s.Id, StringComparer.Ordinal);
}
