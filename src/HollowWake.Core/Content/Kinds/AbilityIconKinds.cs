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
            if (string.IsNullOrWhiteSpace(layer.Path)) c.Error("layer.path must not be empty.");
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
        foreach (var art in d.Fallbacks.Values.Concat(d.Badges.Values).Concat(d.Rules.Select(r => r.Art))) c.Ref<UiAbilityIconDef>(art, "art");
        // Check explicit presentation links once; imported art may precede its gameplay port.
        if (d.Id == "default")
            foreach (var def in c.Registry.Each<FormDef>().Cast<Definition>().Concat(c.Registry.Each<WeaveDef>()).Concat(c.Registry.Each<GlyphDef>()))
                if (def.Meta?["ability_icon"]?.ToString() is { } icon) c.Ref<UiAbilityIconDef>(icon, $"{def.Id}.meta.ability_icon");
    }
}
