namespace HollowWake.Core.Definitions;

/// <summary>Client-only cosmetic recipes. The simulation never uses these values to resolve an action.</summary>
public sealed record CastVisualsDef : Definition
{
    public int MaxEffects { get; init; } = 128;
    public double MaxEventAge { get; init; } = 1.5;
    public double MaxPreparation { get; init; } = 60;
    public List<CastVisualProfile> Profiles { get; init; } = new();
}

public sealed record CastVisualProfile
{
    public string Id { get; init; } = "";
    public List<string> Match { get; init; } = new();
    public int Priority { get; init; }
    public string Color { get; init; } = "#f3e5c0";
    public Dictionary<string, CastVisualPhase> Phases { get; init; } = new();
}

public sealed record CastVisualPhase
{
    public double Seconds { get; init; } = 0.25;
    public double Size { get; init; } = 1;
    public double Height { get; init; } = 0.6;
    public double Expand { get; init; }
    public double Spin { get; init; }
    public double Opacity { get; init; } = 1;
    public double Stroke { get; init; } = 0.055;
    /// <summary>Pairs of vertices in local core coordinates; each segment has x,y,z,x,y,z.</summary>
    public List<float[]> Segments { get; init; } = new();
}
