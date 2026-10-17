namespace HollowWake.Protocol;

/// <summary>
/// Reliable, opt-in, zone-scoped cast lifecycle. Start/sync describes a currently held cast; release
/// confirms execution, never a hit. Impact confirms damage at the supplied core-space point. End is
/// authoritative cast completion, not a promise about cooldown or a visual recovery duration.
/// Sequence deduplicates messages; CastId correlates delayed contacts with their original cast.
/// </summary>
public sealed record CastCueMsg(long Sequence, long CastId, string Phase, long Source, long Target,
    string Weave, string Name, string Mode, List<string> Tags, double ServerTime, double StartedAt,
    double Duration, float X, float Y, float Z, float AimX, float AimY, float AimZ,
    bool Fired, string Reason) : Message
{
    /// <summary>Mechanisms of the compiled form and active glyphs, never a renderer instruction.</summary>
    public List<string>? Mechanisms { get; init; }
    public bool Outcome { get; init; }
}
