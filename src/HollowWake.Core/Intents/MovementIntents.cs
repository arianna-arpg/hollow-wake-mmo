using System.Numerics;

namespace HollowWake.Core.Intents;

/// <summary>
/// Walks to a point under the shared planning policy (<see cref="Movement.MovePlanner"/>). <see cref="Stop"/>
/// is how near the point counts as arrived, the planner's default when unset. <see cref="KeepPlan"/> marks a
/// destination sent again every think, as a chase or a follow sends it: a plan made toward a goal near it
/// stands until it runs out or goes stale, instead of being searched again. A click sets neither and always
/// plans afresh. The wire carries the point only.
/// </summary>
public sealed record MoveToIntent(Vector3 Point) : IIntent
{
    public float? Stop { get; init; }

    public bool KeepPlan { get; init; }
}

public sealed record MoveDirIntent(Vector2 Direction) : IIntent;

public sealed record StopIntent : IIntent;

public sealed record JumpIntent : IIntent;

/// <summary>Turns the sender toward a point on the ground; a point under its feet leaves the facing as it is.</summary>
public sealed record FaceIntent(Vector3 Point) : IIntent;
