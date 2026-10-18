using System.Globalization;
using HollowWake.Core.Definitions;

namespace HollowWake.Core.Content.Kinds;

/// <summary>
/// Content for the interface: themes, icon sets, pages, the layout, the character sheet, the Options pane and
/// the input map. The clients read them; the server reads the pages' gates and pip sources to serve the page
/// state. Registers the kinds and their validators.
/// </summary>
internal static class InterfaceKinds
{
    /// <summary>The frame styles both clients draw. A theme names one of them and colours it.</summary>
    public static readonly IReadOnlyList<string> FrameStyles = new[] { "stone", "brass" };

    /// <summary>
    /// Where a page lives: the core pane itself, a ribbon page docked beside it, a popout ribbon, or the menu
    /// alone (a client page such as Options, which the page state never hides).
    /// </summary>
    public static readonly IReadOnlyList<string> Places = new[] { "core", "ribbon", "popout", "menu" };

    /// <summary>What a sheet section lists.</summary>
    public static readonly IReadOnlyList<string> SectionKinds = new[] { "attributes", "threads", "quests" };

    /// <summary>The kinds of option entries the Options pane draws.</summary>
    public static readonly IReadOnlyList<string> OptionKinds = new[] { "toggle", "choice", "slider", "key", "color", "text" };

    /// <summary>Where an option's value lives.</summary>
    public static readonly IReadOnlyList<string> OptionScopes = new[] { "client", "account", "character" };

    /// <summary>When a changed option takes effect.</summary>
    public static readonly IReadOnlyList<string> OptionApplies = new[] { "live", "on_close", "restart" };

    /// <summary>Key names the clients understand besides single letters and digits.</summary>
    public static readonly IReadOnlyList<string> NamedKeys = new[]
    {
        "Tab", "Escape", "Esc", "Enter", "Return", "Space", "Backspace", "Delete", "Insert", "Home", "End", "PageUp", "PageDown",
        "Up", "Down", "Left", "Right", "Minus", "Equal", "BracketLeft", "BracketRight", "Semicolon", "Apostrophe", "Comma",
        "Period", "Slash", "Backslash", "QuoteLeft", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
    };

    /// <summary>Controller inputs by name: buttons, then stick directions.</summary>
    public static readonly IReadOnlyList<string> PadInputs = new[]
    {
        "A", "B", "X", "Y", "LB", "RB", "LT", "RT", "Back", "Start", "LeftStick", "RightStick", "DpadUp", "DpadDown", "DpadLeft", "DpadRight",
        "LeftStickUp", "LeftStickDown", "LeftStickLeft", "LeftStickRight", "RightStickUp", "RightStickDown", "RightStickLeft", "RightStickRight",
    };

    public static void Register()
    {
        LoadingKinds.Register();
        // Page gates and pip sources read the hero; the page scope adds the unspent points of each currency.
        CastVisualKinds.Register();
        CastForecastKinds.Register();
        ScopeNames.DeclarePrefix("points.");
        // Option gates and requirements read the client's facts and the other options.
        ScopeNames.DeclarePrefix("client.");
        ScopeNames.DeclarePrefix("option.");
        ContentKinds.Register<UiThemeDef>("ui_themes", Theme);
        ContentKinds.Register<UiIconSetDef>("ui_icon_sets", IconSet);
        ContentKinds.Register<UiPageDef>("ui_pages", Page);
        ContentKinds.Register<UiLayoutDef>("ui_layouts", Layout);
        ContentKinds.Register<UiSheetDef>("ui_sheets", Sheet);
        ContentKinds.Register<UiOptionPageDef>("ui_options", OptionPage);
        ContentKinds.Register<InputActionDef>("input_actions", InputAction);
        // What the clients draw the world with: appearances, surfaces, the day and the motion timings.
        AppearanceKinds.Register();
    }

    /// <summary>Whether a key name is one the clients bind: a letter, a digit or a named key, with modifiers.</summary>
    public static bool IsKeyName(string text)
    {
        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return false;
        }
        var key = parts[^1];
        foreach (var modifier in parts[..^1])
        {
            if (modifier is not ("Shift" or "Ctrl" or "Control" or "Alt"))
            {
                return false;
            }
        }
        return (key.Length == 1 && char.IsLetterOrDigit(key[0])) || NamedKeys.Contains(key);
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
        foreach (var (id, path) in d.Accents)
        {
            if (!d.Icons.ContainsKey(id) || !path.TrimStart().StartsWith("M", StringComparison.OrdinalIgnoreCase))
            {
                c.Error($"accents.{id} must name an existing glyph and contain SVG path data.");
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
        // Status presentation is read from each status's meta block; the first icon set checks the keys the
        // clients read once for all (statuses belong to another package, so these only warn).
        if (d.Id == c.Registry.All<UiIconSetDef>().Keys.Min(StringComparer.Ordinal))
        {
            foreach (var status in c.Registry.Each<StatusDef>())
            {
                StatusMeta(status, c);
            }
        }
        // A status the rows show wears a glyph: its meta names one, or this set maps it.
        foreach (var status in c.Registry.Each<StatusDef>())
        {
            var meta = status.Meta;
            var hidden = meta?["hidden"] is System.Text.Json.Nodes.JsonValue h && h.TryGetValue<bool>(out var isHidden) && isHidden;
            var named = meta?["icon"] is System.Text.Json.Nodes.JsonValue v && v.TryGetValue<string>(out var icon) && d.Icons.ContainsKey(icon);
            if (!hidden && !named && !d.Statuses.ContainsKey(status.Id))
            {
                c.Warn($"status '{status.Id}' has no glyph in this set: map it under statuses, or name one in the status's meta.icon.");
            }
        }
    }

    /// <summary>What a status's meta block may say to the interface.</summary>
    public static readonly IReadOnlyList<string> StatusKinds = new[] { "buff", "debuff", "neutral" };

    /// <summary>How a status may draw the eye.</summary>
    public static readonly IReadOnlyList<string> StatusCues = new[] { "pulse", "none" };

    private static void StatusMeta(StatusDef status, ValidationContext c)
    {
        if (status.Meta is not { } meta)
        {
            return;
        }
        string? Text(string key) => meta[key] is System.Text.Json.Nodes.JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        if (Text("icon") is { } icon && !c.Registry.Each<UiIconSetDef>().Any(set => set.Icons.ContainsKey(icon)))
        {
            c.Warn($"status '{status.Id}' meta.icon '{icon}' is in no icon set.");
        }
        if (Text("color") is { } color && !IsColor(color))
        {
            c.Warn($"status '{status.Id}' meta.color '{color}' is not a colour (#rgb, #rrggbb or #rrggbbaa).");
        }
        if (Text("kind") is { } kind && !StatusKinds.Contains(kind))
        {
            c.Warn($"status '{status.Id}' meta.kind '{kind}' is not one of {string.Join(", ", StatusKinds)}.");
        }
        if (Text("cue") is { } cue && !StatusCues.Contains(cue))
        {
            c.Warn($"status '{status.Id}' meta.cue '{cue}' is not one of {string.Join(", ", StatusCues)}.");
        }
    }

    private static void Page(UiPageDef d, ValidationContext c)
    {
        if (!Places.Contains(d.Place))
        {
            c.Error($"place '{d.Place}' is not one of {string.Join(", ", Places)}.");
        }
        // The page's keys are the bindings of its input action; a page without one is reached by menu and ribbon.
        var action = d.Action ?? "page_" + d.Id;
        if (!c.Registry.Has<InputActionDef>(action))
        {
            if (d.Action is not null)
            {
                c.Error($"action '{action}' is not a declared input action.");
            }
            else
            {
                c.Warn($"no input action '{action}' requests the page; it can still be opened from the menu and the ribbon.");
            }
        }
        if (d.Icon.Length > 0 && !c.Registry.Each<UiIconSetDef>().Any(s => s.Icons.ContainsKey(d.Icon)))
        {
            c.Warn($"icon '{d.Icon}' is in no icon set.");
        }
        c.Expression(d.Gate, "gate");
        c.Expression(d.Pips, "pips");
        var pages = c.Registry.All<UiPageDef>();
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

    /// <summary>A key name in one spelling: modifiers in a fixed order, the key's letter upper case.</summary>
    public static string CanonicalKey(string text)
    {
        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return "";
        }
        var mods = parts[..^1].Select(m => m == "Control" ? "Ctrl" : m).OrderBy(m => m, StringComparer.Ordinal);
        var key = parts[^1];
        key = key.Length == 1 ? key.ToUpperInvariant() : key == "Esc" ? "Escape" : key == "Return" ? "Enter" : key;
        return string.Join("+", mods.Append(key));
    }

    private static void InputAction(InputActionDef d, ValidationContext c)
    {
        if (d.Group.Trim().Length == 0)
        {
            c.Error("group is required.");
        }
        foreach (var key in d.Keys.Where(k => !IsKeyName(k)))
        {
            c.Error($"keys names '{key}', which is not a key the clients bind.");
        }
        foreach (var pad in d.Pad.Where(p => !PadInputs.Contains(p)))
        {
            c.Error($"pad names '{pad}', which is not a controller input the clients bind.");
        }
        // Defaults never collide within a context; each pair is reported once, on the later id.
        foreach (var other in c.Registry.Each<InputActionDef>())
        {
            if (other.Id == d.Id || other.Context != d.Context || string.CompareOrdinal(other.Id, d.Id) > 0)
            {
                continue;
            }
            foreach (var key in d.Keys.Where(k => other.Keys.Any(o => CanonicalKey(o) == CanonicalKey(k))))
            {
                c.Error($"key '{key}' is also bound to '{other.Id}' in the {d.Context} context.");
            }
            foreach (var pad in d.Pad.Where(other.Pad.Contains))
            {
                c.Error($"pad '{pad}' is also bound to '{other.Id}' in the {d.Context} context.");
            }
        }
    }

    /// <summary>The option entries of a page, with the key entries its binding groups expand into.</summary>
    public static IEnumerable<(UiOptionGroupDef Group, UiOptionDef Entry)> EntriesOf(UiOptionPageDef page, ContentRegistry registry)
    {
        foreach (var group in page.Groups)
        {
            foreach (var e in group.Entries)
            {
                yield return (group, e);
            }
            if (group.Bindings is { } bindings)
            {
                foreach (var action in registry.Each<InputActionDef>().Where(a => a.Group == bindings).OrderBy(a => a.Order).ThenBy(a => a.Id, StringComparer.Ordinal))
                {
                    yield return (group, new UiOptionDef { Id = "key." + action.Id, Name = action.DisplayName, Kind = "key", Action = action.Id, Applies = "live" });
                }
            }
        }
    }

    private static void OptionPage(UiOptionPageDef d, ValidationContext c)
    {
        c.Expression(d.Gate, "gate");
        if (d.Icon.Length > 0 && !c.Registry.Each<UiIconSetDef>().Any(s => s.Icons.ContainsKey(d.Icon)))
        {
            c.Warn($"icon '{d.Icon}' is in no icon set.");
        }
        // Nested records bind their known keys and drop the rest; a typo there would vanish silently.
        if (c.Registry.Raw("ui_options", d.Id) is { } raw && raw["groups"] is System.Text.Json.Nodes.JsonArray groups)
        {
            foreach (var g in groups.OfType<System.Text.Json.Nodes.JsonObject>())
            {
                foreach (var unknown in ContentJson.UnknownKeys(typeof(UiOptionGroupDef), g))
                {
                    c.Warn($"group '{JsonNum.StringOf(g["id"])}' has an unknown key '{unknown}' (ignored).");
                }
                if (g["entries"] is System.Text.Json.Nodes.JsonArray entries)
                {
                    foreach (var e in entries.OfType<System.Text.Json.Nodes.JsonObject>())
                    {
                        foreach (var unknown in ContentJson.UnknownKeys(typeof(UiOptionDef), e))
                        {
                            c.Warn($"entry '{JsonNum.StringOf(e["id"])}' has an unknown key '{unknown}' (ignored).");
                        }
                    }
                }
            }
        }
        var groupIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var g in d.Groups)
        {
            if (g.Id.Length == 0 || !groupIds.Add(g.Id))
            {
                c.Error($"group '{g.Id}' needs an id unique on the page.");
            }
            if (g.Bindings is { } bindings && !c.Registry.Each<InputActionDef>().Any(a => a.Group == bindings))
            {
                c.Error($"group '{g.Id}' binds input action group '{bindings}', which no input action is in.");
            }
        }
        // Entry ids are unique across every page: each id is reported once, on the later page in id order.
        var mine = EntriesOf(d, c.Registry).Select(x => x.Entry.Id).ToList();
        foreach (var dup in mine.GroupBy(i => i).Where(x => x.Count() > 1))
        {
            c.Error($"entry '{dup.Key}' is declared twice on the page.");
        }
        foreach (var other in c.Registry.Each<UiOptionPageDef>().Where(p => string.CompareOrdinal(p.Id, d.Id) < 0))
        {
            foreach (var id in EntriesOf(other, c.Registry).Select(x => x.Entry.Id).Intersect(mine))
            {
                c.Error($"entry '{id}' is also an entry of page '{other.Id}'.");
            }
        }
        foreach (var (group, e) in EntriesOf(d, c.Registry))
        {
            Option(e, c, $"{group.Id}.{e.Id}");
        }
    }

    private static void Option(UiOptionDef e, ValidationContext c, string at)
    {
        if (e.Id.Length == 0)
        {
            c.Error($"an entry in {at} has no id.");
            return;
        }
        if (!OptionKinds.Contains(e.Kind))
        {
            c.Error($"{at}: kind '{e.Kind}' is not one of {string.Join(", ", OptionKinds)}.");
            return;
        }
        if (!OptionScopes.Contains(e.Scope))
        {
            c.Error($"{at}: scope '{e.Scope}' is not one of {string.Join(", ", OptionScopes)}.");
        }
        if (!OptionApplies.Contains(e.Applies))
        {
            c.Error($"{at}: applies '{e.Applies}' is not one of {string.Join(", ", OptionApplies)}.");
        }
        switch (e.Kind)
        {
            case "toggle":
                if (e.Default is not System.Text.Json.Nodes.JsonValue v || !v.TryGetValue<bool>(out _))
                {
                    c.Error($"{at}: a toggle's default must be true or false.");
                }
                break;
            case "slider":
                if (e.Min >= e.Max)
                {
                    c.Error($"{at}: min must be below max.");
                }
                if (e.Step <= 0)
                {
                    c.Error($"{at}: step must be positive.");
                }
                if (!JsonNum.TryDouble(e.Default, out var number) || number < e.Min || number > e.Max)
                {
                    c.Error($"{at}: the default must be a number from {e.Min} to {e.Max}.");
                }
                break;
            case "choice":
                var choices = e.Choices.Select(x => x.Id).ToList();
                if (e.ChoicesFrom is { } kind)
                {
                    if (!ContentKinds.TryGet(kind, out _))
                    {
                        c.Error($"{at}: choices_from names '{kind}', which is not a content kind.");
                        break;
                    }
                    choices.AddRange(c.Registry.IdsOfKind(kind));
                }
                if (choices.Count == 0)
                {
                    c.Error($"{at}: a choice needs choices or choices_from.");
                    break;
                }
                if (JsonNum.StringOf(e.Default) is not { } chosen || !choices.Contains(chosen))
                {
                    c.Error($"{at}: the default must be one of the choices.");
                }
                break;
            case "key":
                if (e.Action is null || !c.Registry.Has<InputActionDef>(e.Action))
                {
                    c.Error($"{at}: action '{e.Action}' is not a declared input action.");
                }
                break;
            case "color":
                if (!IsColor(JsonNum.StringOf(e.Default)))
                {
                    c.Error($"{at}: a colour's default must be a colour (#rgb, #rrggbb or #rrggbbaa).");
                }
                break;
            case "text":
                if (e.Default is not null && (JsonNum.StringOf(e.Default) is not { } text || text.Length > e.MaxLength))
                {
                    c.Error($"{at}: a text's default must be a string of at most {e.MaxLength} characters.");
                }
                break;
        }
        c.Expression(e.Gate, at + ".gate");
        c.Expression(e.Requires, at + ".requires");
        if (e.Requires is not null && e.RequiresReason.Length == 0)
        {
            c.Warn($"{at}: requires has no requires_reason to show while it is greyed.");
        }
    }
}
