using System.Numerics;
using HollowWake.Core.Ecs;

namespace HollowWake.Core.Components;

/// <summary>Data-driven AI state.</summary>
public sealed class Behavior : IComponent
{
    public required string BehaviorId;
    public string State = "";
    public double NextThinkAt;
    public Entity Target = Entity.None;
    public Vector3 Home;
    public Dictionary<string, double> Memory = new(StringComparer.Ordinal);
    public double StateEnteredAt;
    public bool Entered;

    /// <summary>The standing order this AI obeys, written by a command and read by <c>obey_command</c>; null when none stands.</summary>
    public MinionOrder? Order;

    /// <summary>
    /// When a device may begin each of its weaves again, by weave key: the pace <c>construct_cast</c> and
    /// <c>spring</c> keep, rule <c>minions.construct_interval</c> after each cast they began. Null until one began.
    /// </summary>
    internal Dictionary<string, double>? PacedUntil;

    // Caches the behavior system keeps between thinks. None of it is state worth saving. The movement plan
    // lives on Motion, where the move-to handler keeps it for every mover.

    /// <summary>The perception scope, refilled every think.</summary>
    internal Ai.PerceptionScope? Perception;
}
