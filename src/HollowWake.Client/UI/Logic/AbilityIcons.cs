using System.Globalization;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Protocol;

namespace HollowWake.Client.UI.Logic;

public sealed record AbilityFace(string Art, string Color, string? Badge);

/// <summary>One content-driven identity for item memories, sockets, rack, carry and hotbar.</summary>
public sealed class AbilityIcons
{
    public Dictionary<string, UiAbilityIconDef> Art { get; } = new(StringComparer.Ordinal);
    public UiAbilityStyleDef Style { get; private set; } = new() { Id = "default" };
    private readonly Dictionary<string, JsonObject> _definitions = new(StringComparer.Ordinal);

    public static AbilityIcons Load(Func<string, IEnumerable<JsonObject>> raw)
    {
        var result = new AbilityIcons();
        foreach (var o in raw("ui_ability_icons"))
        {
            var icon = o.Deserialize<UiAbilityIconDef>(ContentJson.Options)!;
            result.Art[icon.Id] = icon;
        }
        var style = raw("ui_ability_styles").FirstOrDefault(o => Json.Str(o["id"]) == "default");
        if (style is not null) result.Style = style.Deserialize<UiAbilityStyleDef>(ContentJson.Options)!;
        foreach (var kind in new[] { "weaves", "forms", "glyphs" })
            foreach (var o in raw(kind)) result._definitions[kind + "." + Json.Str(o["id"])] = o;
        return result;
    }

    public AbilityFace Resolve(string kind, string? id)
    {
        var definition = _definitions.GetValueOrDefault(kind + "." + id);
        var explicitArt = Json.StrOrNull(definition?["meta"]?["ability_icon"]);
        var key = explicitArt is not null && Art.ContainsKey(explicitArt) ? explicitArt
            : Art.ContainsKey(kind + "." + id) ? kind + "." + id
            : Style.Aliases.TryGetValue(kind, out var alias) && Art.ContainsKey(alias + "." + id) ? alias + "." + id
            : null;
        if (key is null)
        {
            if (kind == "weaves" && definition?["form"] is { } formId && !Art.ContainsKey(Json.Str(definition?["icon"])))
            {
                var formFace = Resolve("forms", Json.Str(formId));
                var ownColor = Json.StrOrNull(definition?["meta"]?["icon_color"]) ?? Json.StrOrNull(definition?["color"]);
                return formFace with { Color = Rgba.TryParse(ownColor, out _) ? ownColor! : formFace.Color };
            }
            var ownIcon = Json.StrOrNull(definition?["icon"]);
            var tags = Json.Strings(definition?["tags"]).ToHashSet(StringComparer.Ordinal);
            if (kind == "weaves" && _definitions.TryGetValue("forms." + Json.Str(definition?["form"]), out var form))
                tags.UnionWith(Json.Strings(form["tags"]));
            key = ownIcon is not null && Art.ContainsKey(ownIcon) ? ownIcon
                : Style.Rules.FirstOrDefault(r => r.Kinds.Contains(kind) && r.Tags.Any(tags.Contains))?.Art
                  ?? Style.Fallbacks.GetValueOrDefault(kind, "arcane");
        }
        var color = Json.StrOrNull(definition?["meta"]?["icon_color"]) ?? Json.StrOrNull(definition?["color"]);
        string? badge = Style.Badges.GetValueOrDefault(kind);
        var seen = new HashSet<string>();
        var first = true;
        while (seen.Add(key) && Art.TryGetValue(key, out var entry))
        {
            // An authored face owns its palette; content meta can override it for a new identity.
            if (first) color = Json.StrOrNull(definition?["meta"]?["icon_color"]) ?? entry.Color ?? color;
            badge = entry.Badge ?? badge;
            first = false;
            if (entry.Art is null) break;
            key = entry.Art;
        }
        return new AbilityFace(key, Rgba.TryParse(color, out _) ? color! : Style.DefaultColor, badge);
    }

    public AbilityFace? Item(ItemInfo item) => item.Glyph is { } glyph ? Resolve("glyphs", glyph.Id)
        : item.Form is { } form ? Resolve(form.Weave is not null ? "weaves" : "forms", form.Weave ?? form.Id) : null;

    public AbilityFace Seat(string? key, string? form, string? preset, FaceInfo? converted)
    {
        if (converted is not null)
        {
            if (converted.Weave is not null) return Resolve("weaves", converted.Weave);
            if (converted.Form is not null) return Resolve("forms", converted.Form);
            // Older servers provide only a visual hint. Never reuse the unconverted skill's art.
            return Direct(converted.Icon);
        }
        return preset is not null || (key is not null && _definitions.ContainsKey("weaves." + key))
            ? Resolve("weaves", preset ?? key) : Resolve("forms", form);
    }

    private AbilityFace Direct(string? icon) => new(icon is not null && Art.ContainsKey(icon) ? icon : Style.Fallbacks.GetValueOrDefault("weaves", "arcane"), Style.DefaultColor, null);

    /// <summary>Restricted SVG vocabulary shared with the browser: paths, matrices and paint tokens only.</summary>
    public string Svg(AbilityFace face)
    {
        static string E(string s) => SecurityElement.Escape(s) ?? "";
        static string N(double n) => n.ToString("0.###############", CultureInfo.InvariantCulture);
        var s = Style;
        var b = new StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" stroke-linecap=\"round\" stroke-linejoin=\"round\">");
        b.Append($"<rect width=\"24\" height=\"24\" fill=\"{E(s.Background)}\"/><rect width=\"24\" height=\"24\" fill=\"{E(face.Color)}\" opacity=\"{N(s.TintAlpha)}\"/>");
        void Layers(string id, string tint, string ink, string rim)
        {
            var seen = new HashSet<string>();
            while (seen.Add(id) && Art.GetValueOrDefault(id)?.Art is { } target) id = target;
            string Paint(string? token) => E(token switch { "tint" => tint, "ink" => ink, "shade" => rim, _ => "none" });
            foreach (var layer in Art.GetValueOrDefault(id)?.Layers ?? new())
            {
                var transform = layer.Transform is { Count: 6 } matrix ? " transform=\"matrix(" + string.Join(" ", matrix.Select(N)) + ")\"" : "";
                var path = E(layer.Path);
                var width = layer.Width ?? s.LineWidth;
                b.Append($"<g{transform}><path d=\"{path}\" fill=\"{Paint(layer.Fill)}\"/>");
                if (layer.Stroke is not null)
                    b.Append($"<path d=\"{path}\" fill=\"none\" stroke=\"{E(rim)}\" stroke-width=\"{N(width + s.RimWidth)}\"/><path d=\"{path}\" fill=\"none\" stroke=\"{Paint(layer.Stroke)}\" stroke-width=\"{N(width)}\"/>");
                b.Append("</g>");
            }
        }
        Layers(face.Art, face.Color, s.Ink, s.Rim);
        if (face.Badge is { } badge && Art.ContainsKey(badge))
        {
            b.Append($"<g transform=\"scale(.34)\"><rect width=\"24\" height=\"24\" rx=\"4\" fill=\"{E(s.BadgeBackground)}\"/>");
            Layers(badge, s.BadgeColor, s.BadgeColor, s.BadgeBackground);
            b.Append("</g>");
        }
        return b.Append("</svg>").ToString();
    }
}
