using System.Numerics;
using HollowWake.Core.Ecs;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Sim;

/// <summary>Confirmed semantic cast lifecycle. Contains no renderer instructions. Positions use core coordinates.</summary>
public sealed record CastPresentationEvent(long CastId, string Phase, Entity Caster, Entity Target,
    string Weave, string Name, string Mode, TagSet Tags, Vector3 Position, Vector3 Aim,
    double StartedAt, double Duration, double Time, bool Fired, string Reason) : GameEvent;
