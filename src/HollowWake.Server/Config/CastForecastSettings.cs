namespace HollowWake.Server.Config;

public sealed record CastForecastSettings
{
    /// <summary>Complete baselines run at snapshot cadence. Overflow is explicitly reported, never silently safe.</summary>
    public int MaxCasts { get; init; } = 256;
    public double ValidSeconds { get; init; } = 0.5;
    public double RefreshSeconds { get; init; } = 2;
    public CastForecastSettings Normalized() => this with
    {
        MaxCasts = Math.Clamp(MaxCasts, 1, 2048),
        ValidSeconds = double.IsFinite(ValidSeconds) ? Math.Clamp(ValidSeconds, 0.1, 3) : 0.5,
        RefreshSeconds = double.IsFinite(RefreshSeconds) ? Math.Clamp(RefreshSeconds, 0.1, 10) : 2,
    };
}
