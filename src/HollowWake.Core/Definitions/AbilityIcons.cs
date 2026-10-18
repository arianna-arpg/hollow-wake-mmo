namespace HollowWake.Core.Definitions;

/// <summary>Presentation-only vector artwork or a colored reference to another artwork.</summary>
public sealed record UiAbilityIconDef : Definition
{
    public string? Art { get; init; }
    public string? Color { get; init; }
    public string? Badge { get; init; }
    public List<UiAbilityLayerDef> Layers { get; init; } = new();
}

public sealed record UiAbilityLayerDef
{
    public string Path { get; init; } = "";
    public string? Fill { get; init; }
    public string? Stroke { get; init; }
    public double? Width { get; init; }
    public List<double>? Transform { get; init; }
}

/// <summary>Shared paint and fallback policy, consumed only by the clients.</summary>
public sealed record UiAbilityStyleDef : Definition
{
    public string Background { get; init; } = "#111820";
    public string Ink { get; init; } = "#f4ebd7";
    public string Rim { get; init; } = "#070c12";
    public double TintAlpha { get; init; } = .16;
    public double LineWidth { get; init; } = 1.35;
    public double RimWidth { get; init; } = .8;
    public int CacheLimit { get; init; } = 128;
    public int MaxBakeSize { get; init; } = 192;
    public string DefaultColor { get; init; } = "#b8b8b8";
    public string BadgeColor { get; init; } = "#ffe3a0";
    public string BadgeBackground { get; init; } = "#21182e";
    public Dictionary<string, string> Badges { get; init; } = new();
    public Dictionary<string, string> Fallbacks { get; init; } = new();
    public Dictionary<string, string> Aliases { get; init; } = new();
    public List<UiAbilityRuleDef> Rules { get; init; } = new();
}

public sealed record UiAbilityRuleDef
{
    public List<string> Kinds { get; init; } = new() { "weaves", "forms" };
    public List<string> Tags { get; init; } = new();
    public string Art { get; init; } = "";
}
