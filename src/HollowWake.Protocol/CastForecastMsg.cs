namespace HollowWake.Protocol;

/// <summary>A complete, ordered view of active preparations in this viewer's interest. Absence removes a
/// preparation; it never means a released projectile/field has ended. Epoch is announced by EnterWorld.</summary>
public sealed record CastForecastMsg(string Epoch, long Sequence, double ServerTime, double ValidUntil,
    List<CastForecastInfo> Casts, int Omitted) : Message
{
    /// <summary>Unchanged members reference the exact full-record sequence previously sent. A missing
    /// revision is unknown, never permission to reuse stale geometry; periodic full records repair it.</summary>
    public List<CastForecastRef>? Retained { get; init; }
}

public sealed record CastForecastRef(long CastId, long Revision);

public sealed record CastForecastInfo
{
    public long CastId { get; init; }
    public long Source { get; init; }
    public string Name { get; init; } = "";
    public string Mode { get; init; } = "";
    public double StartedAt { get; init; }
    public double? NextEmissionStartedAt { get; init; }
    public double? NextEmissionAt { get; init; }
    public double? ReadyAt { get; init; }
    public double? MaxHoldEndsAt { get; init; }
    public int EmissionOrdinal { get; init; }
    public string DirectionPolicy { get; init; } = "unknown";
    public bool DirectionCommitted { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
    public string Coverage { get; init; } = "unavailable";
    public string? UnavailableReason { get; init; }
    public CastForecastRegion? Region { get; init; }
}

/// <summary>Sampled primary candidate region. All dimensions are resolved by combat. A planar drawing
/// is a conditional envelope: live size/origin, height, LOS, requirements and target selection still apply.</summary>
public sealed record CastForecastRegion
{
    public string Node { get; init; } = "root";
    public string Shape { get; init; } = "";
    public ShapeSizeInfo Size { get; init; } = new();
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
    public float Heading { get; init; }
    public string Anchor { get; init; } = "unknown";
    public float HeightTolerance { get; init; }
    public string TargetTest { get; init; } = "center";
    public bool RequiresLineOfSight { get; init; }
    public bool HasRequirement { get; init; }
    public bool HasTargetLimit { get; init; }
    public string ViewerEligibility { get; init; } = "unknown";
}
