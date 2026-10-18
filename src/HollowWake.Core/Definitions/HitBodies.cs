namespace HollowWake.Core.Definitions;

/// <summary>Simulation anatomy. Horizontal numbers multiply live clearance radius, vertical numbers live height.</summary>
public sealed record HitBodyDef : Definition
{
    public List<HitVolumeSpec> Volumes { get; init; } = new();
    /// <summary>Index of the volume whose center is the target aim point.</summary>
    public int Aim { get; init; }
}

public sealed record HitVolumeSpec
{
    public string Shape { get; init; } = "ellipsoid";
    public float Forward { get; init; }
    public float Left { get; init; }
    public float Up { get; init; }
    public float Radius { get; init; }
    public float HalfHeight { get; init; }
}
