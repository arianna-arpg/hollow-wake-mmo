using System.Numerics;
using HollowWake.Core.Attributes;
using HollowWake.Core.Ecs;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Components;

/// <summary>
/// A body that belongs to someone: a summon, a construct, a raised corpse. It fights on its owner's side
/// (relations follow the owner), its kills credit the owner, and it counts against the capacity of its
/// group, or of its pool group when it has one. Summons expire at <see cref="ExpiresAt"/>.
/// </summary>
public sealed class Minion : IComponent
{
    public Entity Owner = Entity.None;
    public double ExpiresAt = double.PositiveInfinity;

    /// <summary>The summon group: what a summon's <c>max_active</c> counts, and what commands, casts and recalls can address.</summary>
    public string? Group;

    /// <summary>A capacity several summons share: the owner's <c>minion.max</c> scoped by <c>pool.{group}</c>.</summary>
    public string? PoolGroup;

    /// <summary>Summoning into an exclusive group dismisses the owner's minions of the other groups in it.</summary>
    public string? ExclusiveGroup;

    /// <summary>The key of the weave that summoned the body, so that weave's own effects can address its minions.</summary>
    public string? Weave;

    public double BornAt;

    /// <summary>Birth order in the simulation: the oldest body is replaced first.</summary>
    public long Sequence;

    /// <summary>Constructs stand where they were placed: they are not recalled, commanded or led.</summary>
    public bool Construct;

    /// <summary>The construct casts with its owner's numbers and credits its owner.</summary>
    public bool InheritCaster;

    /// <summary>What the body inherits from its owner; null when it inherits nothing.</summary>
    public MinionInheritance? Inheritance;

    /// <summary>The contract that brings the body back when it falls; null for an ordinary summon.</summary>
    public Minions.MinionContract? Contract;

    /// <summary>Life draining away on a growing curve; null for a body that lasts.</summary>
    public MinionDecay? Decay;

    /// <summary>Modifiers the owner's other minions wear while this body lives (an apex's presence); null for none.</summary>
    public IReadOnlyList<Modifier>? Presence;

    /// <summary>The presence sources the body wears now, kept in step by the minion system.</summary>
    internal List<string>? WornPresences;

    /// <summary>Where and when a leash recall last saw the body make progress toward its owner.</summary>
    internal Vector3 ProgressFrom;

    internal double ProgressAt = double.NegativeInfinity;

    /// <summary>A leash recall that teleported the body cannot do so again before this time.</summary>
    internal double RecallReadyAt;

    /// <summary>The owner's cast count an echo has replayed up to; negative until the echo first looks.</summary>
    internal long EchoedCast = -1;
}

/// <summary>
/// What a minion inherits: its owner's <c>minion.*</c> attributes folded in <see cref="Context"/> (the
/// summoning weave's tags and the body's own) with <see cref="Extras"/> (the summoning weave's modifiers)
/// joining the fold, landing on the minion as modifiers under <see cref="Source"/>. The minion system
/// folds again when the owner's attributes change, and touches the minion only when a value moved.
/// </summary>
public sealed class MinionInheritance
{
    public required string Source;
    public required TagSet Context;
    public IReadOnlyList<Modifier>? Extras;

    /// <summary>The owner's attribute version the values were folded at.</summary>
    public int OwnerVersion = int.MinValue;

    /// <summary>The next time the owner's version is compared.</summary>
    public double NextCheckAt;

    /// <summary>The value each inheritance rule produced at the last fold, by rule index.</summary>
    public double[] Values = Array.Empty<double>();
}

/// <summary>Unmitigable unlife: from <see cref="StartsAt"/> the body loses <see cref="Rate"/> life a second, growing by <see cref="Growth"/> each second.</summary>
public sealed class MinionDecay
{
    public double StartsAt;
    public double Rate;
    public double Growth = 1;
}

/// <summary>
/// A standing order, written into an AI's behavior by a command and read by <c>obey_command</c>: which
/// command, the handler kind that drives it, the mark and the pinned quarry, who gave it and until when.
/// </summary>
public sealed class MinionOrder
{
    public required string Command;
    public required string Kind;
    public Vector3 Point;
    public Entity Target = Entity.None;
    public Entity Issuer = Entity.None;
    public double Until;

    /// <summary>How far around the mark "whatever holds it" reaches.</summary>
    public float MarkRadius;

    /// <summary>Close enough to the mark (or the issuer, for a recall) to count as arrived.</summary>
    public float Arrive;
}

/// <summary>A dead body that can be raised or consumed until it despawns: what it was and when it fell.</summary>
public sealed class Corpse : IComponent
{
    public required string Pattern;
    public int Level = 1;
    public double DiedAt;
}

/// <summary>
/// A dome: hostile projectiles reaching its radius are dissolved, turned back on their own side, or slowed
/// while inside. The minion system reads it before projectiles move.
/// </summary>
public sealed class Intercept : IComponent
{
    public float Radius = 3;

    /// <summary><c>dissolve</c>, <c>deflect</c> or <c>slow</c>.</summary>
    public string Mode = "dissolve";

    /// <summary>For <c>slow</c>: the fraction of their speed projectiles keep inside.</summary>
    public float Slow = 0.35f;

    /// <summary>Projectiles slowed now, so they regain their speed when they leave.</summary>
    internal Dictionary<Entity, float>? Slowed;
}
