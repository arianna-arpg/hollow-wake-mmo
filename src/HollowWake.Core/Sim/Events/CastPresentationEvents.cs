using System.Numerics;
using HollowWake.Core.Ecs;
using HollowWake.Core.Tags;
using HollowWake.Core.Definitions;

namespace HollowWake.Core.Sim;

/// <summary>Confirmed semantic cast lifecycle. Contains no renderer instructions. Positions use core coordinates.</summary>
public sealed record CastPresentationEvent(long CastId, string Phase, Entity Caster, Entity Target,
    string Weave, string Name, string Mode, TagSet Tags, Vector3 Position, Vector3 Aim,
    double StartedAt, double Duration, double Time, bool Fired, string Reason) : GameEvent
{
    public TagSet Mechanisms { get; init; } = TagSet.Empty;
    /// <summary>An independently confirmed outcome; it need not have an originating cast identity.</summary>
    public bool Outcome { get; init; }
    private static readonly Dictionary<CastMode, string> ModeNames = Enum.GetValues<CastMode>()
        .ToDictionary(m => m, m => m.ToString().ToLowerInvariant());
    public static string ModeName(CastMode mode) => ModeNames[mode];
}
