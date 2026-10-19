namespace HollowWake.Core.Definitions;

/// <summary>Presentation only. Geometry and clocks always come from the authoritative delivery.</summary>
public sealed record CastForecastStyleDef : Definition
{
    public int Segments { get; init; } = 64;
    public int MaxSegments { get; init; } = 256;
    public double MaxChordError { get; init; } = 0.025;
    public double Stroke { get; init; } = 0.045;
    public double DashFraction { get; init; } = 0.7;
    public double UnderlayScale { get; init; } = 2.4;
    public double DirectionSize { get; init; } = 0.45;
    public double HeightMarkerSize { get; init; } = 0.22;
    public double Lift { get; init; } = 0.08;
    // Screen-space pixels; independent of the authoritative world-space boundary dimensions.
    public double BarWidth { get; init; } = 98;
    public double BarHeight { get; init; } = 4;
    public double LabelHeight { get; init; } = 0.5;
    public double FontSize { get; init; } = 12;
    public string Color { get; init; } = "#f5db97";
    public string CandidateColor { get; init; } = "#fff5d7";
    public string UnknownColor { get; init; } = "#bdc7dc";
    public string UnderlayColor { get; init; } = "#182231";
    public Dictionary<string, string> Labels { get; init; } = new()
    {
        ["primary"] = "", ["unavailable"] = "Area unknown",
        ["ready"] = "Ready · held", ["channel"] = "Channeling", ["waiting"] = "Awaiting resolution",
        ["stale"] = "Forecast delayed", ["overflow"] = "Additional casts nearby",
    };
}
