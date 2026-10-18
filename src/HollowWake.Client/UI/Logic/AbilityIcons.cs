using System.Globalization;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Protocol;

namespace HollowWake.Client.UI.Logic;

public sealed record AbilityFace(string Art, string Color, string? Badge, string? Tone = null);

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
        foreach (var kind in result.Style.Roles.Values.Concat(result.Style.Parents.Keys).Concat(result.Style.Parents.Values.Select(p => p.Kind))
                     .Concat(result.Style.Fallbacks.Keys).Concat(result.Style.Rules.SelectMany(r => r.Kinds)).Distinct())
            foreach (var o in raw(kind)) result._definitions[kind + "." + Json.Str(o["id"])] = o;
        return result;
    }

    public AbilityFace Resolve(string kind, string? id, IReadOnlyList<string>? threads = null)
    {
        var definition = _definitions.GetValueOrDefault(kind + "." + id);
        var chain = new List<(string Kind, string? Id, JsonObject? Definition)>();
        var parents = new HashSet<string>();
        var currentKind = kind;
        var currentId = id;
        while (parents.Add(currentKind + "." + currentId))
        {
            var current = _definitions.GetValueOrDefault(currentKind + "." + currentId);
            chain.Add((currentKind, currentId, current));
            if (!Style.Parents.TryGetValue(currentKind, out var parent) || Json.StrOrNull(current?[parent.Field]) is not { } parentId) break;
            currentKind = parent.Kind;
            currentId = parentId;
        }
        var tags = chain.SelectMany(c => Json.Strings(c.Definition?["tags"])).ToHashSet(StringComparer.Ordinal);
        var activeThreads = threads ?? Json.Strings(definition?["threads"]);
        string? threadColor = null;
        foreach (var thread in activeThreads)
        {
            tags.Add(thread);
            var t = _definitions.GetValueOrDefault(Role("thread") + "." + thread);
            tags.UnionWith(Json.Strings(t?["tags"]));
            threadColor ??= Json.StrOrNull(t?["color"]);
        }
        string? Bound(string k, string? i, JsonObject? d)
        {
            var explicitArt = Json.StrOrNull(d?["meta"]?["ability_icon"]);
            var ownIcon = Json.StrOrNull(d?["icon"]);
            return explicitArt is not null && Art.ContainsKey(explicitArt) ? explicitArt
                : Art.ContainsKey(k + "." + i) ? k + "." + i
                : ownIcon is not null && Art.ContainsKey(ownIcon) ? ownIcon : null;
        }
        var key = Bound(kind, id, definition)
            ?? Style.Rules.FirstOrDefault(r => r.Kinds.Contains(kind) && r.Tags.Any(t => tags.Any(tag => tag == t || tag.StartsWith(t + ".", StringComparison.Ordinal))))?.Art
            ?? chain.Skip(1).Select(c => Bound(c.Kind, c.Id, c.Definition)).FirstOrDefault(k => k is not null)
            ?? Style.Fallbacks.GetValueOrDefault(kind, Style.DefaultArt);
        var color = Json.StrOrNull(definition?["meta"]?["icon_color"]) ?? Json.StrOrNull(definition?["color"]) ?? threadColor;
        string? badge = Style.Badges.GetValueOrDefault(kind);
        var seen = new HashSet<string>();
        while (seen.Add(key) && Art.TryGetValue(key, out var entry))
        {
            color ??= entry.Color;
            badge = entry.Badge ?? badge;
            if (entry.Art is null) break;
            key = entry.Art;
        }
        return new AbilityFace(key, Rgba.TryParse(color, out _) ? color! : Style.DefaultColor, badge);
    }

    private string Role(string role) => Style.Roles.GetValueOrDefault(role, "");

    public AbilityFace Glyph(string id) => Resolve(Role("glyph"), id);

    public AbilityFace? Item(ItemInfo item) => item.Glyph is { } glyph ? Glyph(glyph.Id)
        : item.Form is { } form ? Resolve(Role(form.Weave is not null ? "weave" : "form"), form.Weave ?? form.Id) : null;

    public AbilityFace Seat(string? key, string? form, string? preset, FaceInfo? converted, IReadOnlyList<string>? threads = null)
    {
        if (converted is not null)
        {
            if (converted.Weave is not null) return Resolve(Role("weave"), converted.Weave);
            if (converted.Form is not null) return Resolve(Role("form"), converted.Form, threads);
            // Older servers provide only a visual hint. Never reuse the unconverted skill's art.
            return Direct(converted.Icon);
        }
        return preset is not null || (key is not null && _definitions.ContainsKey(Role("weave") + "." + key))
            ? Resolve(Role("weave"), preset ?? key, threads) : Resolve(Role("form"), form, threads);
    }

    private AbilityFace Direct(string? icon) => new(icon is not null && Art.ContainsKey(icon) ? icon : Style.DefaultArt, Style.DefaultColor, null);

    /// <summary>Restricted SVG vocabulary shared with the browser: paths, matrices and paint tokens only.</summary>
    public string Svg(AbilityFace face)
    {
        static string E(string s) => SecurityElement.Escape(s) ?? "";
        static string N(double n) => n.ToString("0.###############", CultureInfo.InvariantCulture);
        // Godot's SVG loader cannot read #rrggbbaa. Keep alpha as paint opacity,
        // including theme tones, authored palettes, rims and badge backgrounds.
        static string Paint(string attribute, string value)
        {
            if (value.Length == 9 && value[0] == '#' &&
                uint.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgba))
                return $"{attribute}=\"{E(value[..7])}\" {attribute}-opacity=\"{N((rgba & 255) / 255d)}\"";
            return $"{attribute}=\"{E(value)}\"";
        }
        var s = Style;
        var b = new StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" stroke-linecap=\"round\" stroke-linejoin=\"round\">");
        b.Append($"<rect width=\"24\" height=\"24\" {Paint("fill", s.Background)}/><rect width=\"24\" height=\"24\" {Paint("fill", face.Tone ?? face.Color)} opacity=\"{N(s.TintAlpha)}\"/>");
        void Layers(string id, string tint, string ink, string rim)
        {
            var seen = new HashSet<string>();
            while (seen.Add(id) && Art.GetValueOrDefault(id)?.Art is { } target) id = target;
            string Token(string? token) => token switch { "tint" => tint, "ink" => ink, "shade" => rim, _ => "none" };
            foreach (var layer in Art.GetValueOrDefault(id)?.Layers ?? new())
            {
                var transform = layer.Transform is { Count: 6 } matrix ? " transform=\"matrix(" + string.Join(" ", matrix.Select(N)) + ")\"" : "";
                var path = E(layer.Path);
                var width = layer.Width ?? s.LineWidth;
                b.Append($"<g{transform}><path d=\"{path}\" {Paint("fill", Token(layer.Fill))}/>");
                if (layer.Stroke is not null)
                    b.Append($"<path d=\"{path}\" fill=\"none\" {Paint("stroke", rim)} stroke-width=\"{N(width + s.RimWidth)}\"/><path d=\"{path}\" fill=\"none\" {Paint("stroke", Token(layer.Stroke))} stroke-width=\"{N(width)}\"/>");
                b.Append("</g>");
            }
        }
        Layers(face.Art, face.Tone ?? face.Color, face.Tone ?? s.Ink, s.Rim);
        if (face.Badge is { } badge && Art.ContainsKey(badge))
        {
            b.Append($"<g transform=\"scale(.34)\"><rect width=\"24\" height=\"24\" rx=\"4\" {Paint("fill", s.BadgeBackground)}/>");
            Layers(badge, face.Tone ?? s.BadgeColor, face.Tone ?? s.BadgeColor, s.BadgeBackground);
            b.Append("</g>");
        }
        return b.Append("</svg>").ToString();
    }
}
