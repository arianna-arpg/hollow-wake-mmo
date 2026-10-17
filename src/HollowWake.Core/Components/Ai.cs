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

    // Caches the behavior system keeps between thinks. None of it is state worth saving.

    /// <summary>Where the current movement plan leads. Meaningful while <see cref="Planned"/> is set.</summary>
    public Vector3 PlanGoal;
    /// <summary>When the current plan was made; plans are renewed after <c>move.ai_repath_seconds</c>.</summary>
    public double PlannedAt = double.NegativeInfinity;
    /// <summary>The motion's path or move target was planned by the AI toward <see cref="PlanGoal"/>.</summary>
    public bool Planned;
    /// <summary>The last search for <see cref="PlanGoal"/> could not bring the AI closer; once what it found runs out, the AI walks straight at the goal until a renewal.</summary>
    public bool PlanFailed;
    /// <summary>The perception scope, refilled every think.</summary>
    internal Ai.PerceptionScope? Perception;
}
