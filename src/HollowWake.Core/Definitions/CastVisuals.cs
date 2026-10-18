namespace HollowWake.Core.Definitions;

/// <summary>Client-only cosmetic recipes. The simulation never uses these values to resolve an action.</summary>
public sealed record CastVisualsDef : Definition
{
    public int MaxEffects { get; init; } = 128;
    public int MaxLayersPerEvent { get; init; } = 8;
    public double MaxEventAge { get; init; } = 1.5;
    public double MaxPreparation { get; init; } = 60;
    public List<CastVisualProfile> Profiles { get; init; } = new();
    /// <summary>Authorable semantic outcome vocabulary. Unrecognized outcomes use a neutral outcome cue.</summary>
    public List<string> Outcomes { get; init; } = new();
    /// <summary>Default material vocabulary instantiated for every Thread, including content-added Threads.</summary>
    public Dictionary<string, CastVisualPhase> ThreadPhases { get; init; } = new();
}

public static class CastVisualThemes
{
    public static CastVisualsDef Resolve(CastVisualsDef config, IEnumerable<ThreadDef> threads) => config with
    {
        Profiles = config.Profiles.Concat(threads.OrderBy(t => t.Id, StringComparer.Ordinal).Select(t => new CastVisualProfile
        {
            Id = "thread." + t.Id, Layer = "thread." + t.Id, Match = new() { t.Id },
            Color = t.Color, Phases = config.ThreadPhases,
        })).ToList()
    };
}

public sealed record CastVisualProfile
{
    public string Id { get; init; } = "";
    /// <summary>One winning profile per layer; distinct layers compose without combination-specific recipes.</summary>
    public string Layer { get; init; } = "base";
    public bool Fallback { get; init; }
    public List<string> Match { get; init; } = new();
    public List<string> MatchMechanisms { get; init; } = new();
    public int Priority { get; init; }
    public string Color { get; init; } = "#f3e5c0";
    public Dictionary<string, CastVisualPhase> Phases { get; init; } = new();
}

public sealed record CastVisualPhase
{
    public string Primitive { get; init; } = "segments";
    public int Pieces { get; init; } = 8;
    public double Arc { get; init; } = 360;
    public double Seconds { get; init; } = 0.25;
    public double Size { get; init; } = 1;
    public double Height { get; init; } = 0.6;
    public double Expand { get; init; }
    public double Spin { get; init; }
    public double Opacity { get; init; } = 1;
    public double Stroke { get; init; } = 0.055;
    public bool ShowOnDead { get; init; }
    /// <summary>Pairs of vertices in local core coordinates; each segment has x,y,z,x,y,z.</summary>
    public List<float[]> Segments { get; init; } = new();
}
