using HollowWake.Core.Definitions;

namespace HollowWake.Core.Content.Kinds;

internal static class AbilityIconKinds
{
    public static void Register()
    {
        ContentKinds.Register<UiAbilityIconDef>("ui_ability_icons", Icon);
        ContentKinds.Register<UiAbilityStyleDef>("ui_ability_styles", Style);
    }

    private static void Icon(UiAbilityIconDef d, ValidationContext c)
    {
        if (d.Color is { } color && !InterfaceKinds.IsColor(color)) c.Error("color must be a hex colour.");
        if ((d.Art is null) == (d.Layers.Count == 0)) c.Error("provide either art or layers.");
        if (d.Badge is { } badge) c.Ref<UiAbilityIconDef>(badge, "badge");
        var seen = new HashSet<string> { d.Id };
        var target = d.Art;
        while (target is not null)
        {
            if (!seen.Add(target)) { c.Error("art references form a cycle."); break; }
            c.Ref<UiAbilityIconDef>(target, "art");
            target = c.Registry.Get<UiAbilityIconDef>(target)?.Art;
        }
        foreach (var layer in d.Layers)
        {
            if (!ValidPath(layer.Path)) c.Error("layer.path must be valid SVG path data.");
            foreach (var paint in new[] { layer.Fill, layer.Stroke })
                if (paint is not null and not ("tint" or "ink" or "shade")) c.Error("layer paint must be tint, ink or shade.");
            if (layer.Width is { } width && (!double.IsFinite(width) || width <= 0)) c.Error("layer.width must be positive and finite.");
            if (layer.Transform is { } t && (t.Count != 6 || t.Any(n => !double.IsFinite(n)))) c.Error("layer.transform must be six finite matrix coefficients.");
        }
    }

    private static void Style(UiAbilityStyleDef d, ValidationContext c)
    {
        foreach (var color in new[] { d.Background, d.Ink, d.Rim, d.DefaultColor, d.BadgeColor, d.BadgeBackground })
            if (!InterfaceKinds.IsColor(color)) c.Error("style colours must be hex colours.");
        if (!double.IsFinite(d.TintAlpha) || d.TintAlpha < 0 || d.TintAlpha > 1) c.Error("tint_alpha must be within [0,1].");
        if (!double.IsFinite(d.LineWidth) || d.LineWidth <= 0 || !double.IsFinite(d.RimWidth) || d.RimWidth < 0) c.Error("invalid stroke widths.");
        if (d.CacheLimit < 1 || d.CacheLimit > 4096 || d.MaxBakeSize < 12 || d.MaxBakeSize > 1024) c.Error("invalid raster cache bounds.");
        foreach (var art in d.Fallbacks.Values.Concat(d.Badges.Values).Concat(d.Rules.Select(r => r.Art)).Append(d.DefaultArt))
            c.Ref<UiAbilityIconDef>(art, "art");
        var kinds = d.Roles.Values.Concat(d.Parents.Keys).Concat(d.Parents.Values.Select(p => p.Kind))
            .Concat(d.Fallbacks.Keys).Concat(d.Badges.Keys).Concat(d.Rules.SelectMany(r => r.Kinds)).Distinct();
        foreach (var kind in kinds)
            if (!ContentKinds.TryGet(kind, out _)) c.Error($"unknown presentation kind '{kind}'.");
        foreach (var role in new[] { "weave", "form", "glyph", "thread" })
            if (!d.Roles.ContainsKey(role)) c.Error($"roles requires '{role}'.");
        foreach (var (kind, parent) in d.Parents)
        {
            if (string.IsNullOrWhiteSpace(parent.Field)) c.Error("parent.field must not be empty.");
            var seen = new HashSet<string> { kind };
            var next = parent.Kind;
            while (d.Parents.TryGetValue(next, out var link))
            {
                if (!seen.Add(next)) { c.Error("parent kinds form a cycle."); break; }
                next = link.Kind;
            }
        }
        // Explicit presentation links are open to every registered content kind.
        foreach (var kind in ContentKinds.All)
            foreach (var def in c.Registry.RawOfKind(kind.Name))
            {
                if (def.Object["meta"]?["ability_icon"]?.ToString() is { } icon) c.Ref<UiAbilityIconDef>(icon, $"{def.Id}.meta.ability_icon");
                if (def.Object["meta"]?["icon_color"]?.ToString() is { } color && !InterfaceKinds.IsColor(color))
                    c.Error($"{def.Id}.meta.icon_color must be a hex colour.");
            }
    }

    // SVG path data only: commands, finite numeric operands and their exact arities.
    // Paint, URLs and markup never enter the path grammar.
    private static bool ValidPath(string path)
    {
        var tokens = System.Text.RegularExpressions.Regex.Matches(path, @"[a-zA-Z]|[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?");
        if (tokens.Count == 0 || tokens[0].Value is not ("M" or "m")) return false;
        var end = 0;
        char command = '\0';
        var values = new List<double>();
        bool Complete()
        {
            var arity = char.ToUpperInvariant(command) switch
            {
                'M' or 'L' or 'T' => 2, 'H' or 'V' => 1, 'C' => 6, 'S' or 'Q' => 4, 'A' => 7, 'Z' => 0, _ => -1,
            };
            if (arity < 0 || (arity == 0 ? values.Count != 0 : values.Count == 0 || values.Count % arity != 0)) return false;
            if (char.ToUpperInvariant(command) == 'A')
                for (var i = 0; i < values.Count; i += 7)
                    if (values[i] < 0 || values[i + 1] < 0 || values[i + 3] is not (0 or 1) || values[i + 4] is not (0 or 1)) return false;
            return true;
        }
        foreach (System.Text.RegularExpressions.Match token in tokens)
        {
            var gap = path[end..token.Index];
            if (!System.Text.RegularExpressions.Regex.IsMatch(gap, @"^\s*,?\s*$")) return false;
            var isCommand = token.Value.Length == 1 && char.IsLetter(token.Value[0]);
            if (gap.Contains(',') && (isCommand || values.Count == 0)) return false;
            if (isCommand)
            {
                if (command != '\0' && !Complete()) return false;
                command = token.Value[0];
                values.Clear();
            }
            else
            {
                if (!double.TryParse(token.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number)) return false;
                values.Add(number);
            }
            end = token.Index + token.Length;
        }
        return string.IsNullOrWhiteSpace(path[end..]) && Complete();
    }
}
