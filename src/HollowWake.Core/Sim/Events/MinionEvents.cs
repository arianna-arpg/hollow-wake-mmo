using System.Numerics;
using HollowWake.Core.Ecs;

namespace HollowWake.Core.Sim;

// Events of minions and constructs: commands given, contracts respawning their bodies, corpses raised and
// consumed, constructs deployed. Declared here so no package edits Sim/Events.cs.

/// <summary>
/// A player asked their minions to do something. The intent and its wire message belong to the progression
/// package; the minion system subscribes and writes the command into minion memory, so the two never share a file.
/// </summary>
public sealed record MinionCommandRequested(Entity Owner, string Command, Entity Target, Vector3 Point, string? Group) : GameEvent;

/// <summary>A body was raised for an owner: a summon, a construct, a contract's return or a replenished body.</summary>
public sealed record MinionSummoned(Entity Minion, Entity Owner, string? Group, string? Weave, bool Construct) : GameEvent;

/// <summary>A summon found nothing to raise, such as a corpse summon with no corpse in reach.</summary>
public sealed record SummonFizzled(Entity Owner, string? Weave, string Reason) : GameEvent;

/// <summary>An order went out: how many recipients heeded it and how many defied it.</summary>
public sealed record MinionsCommanded(Entity Issuer, string Command, Vector3 Point, Entity Target, int Heeded, int Defied) : GameEvent;

/// <summary>A minion executed a weave on its owner's order (<c>minion_cast</c>), without a cast of its own.</summary>
public sealed record MinionOrdered(Entity Minion, Entity Owner, string Weave, Vector3 Point) : GameEvent;

/// <summary>A contract began: its reservation is held and its fallen bodies will return.</summary>
public sealed record ContractStarted(Entity Owner, string Weave) : GameEvent;

/// <summary>A contract ended: its bodies were dismissed and its reservation released.</summary>
public sealed record ContractEnded(Entity Owner, string Weave) : GameEvent;

/// <summary>A contract brought a fallen body back.</summary>
public sealed record ContractRespawned(Entity Owner, Entity Minion, string Weave) : GameEvent;

/// <summary>A corpse rose as a minion and is gone.</summary>
public sealed record CorpseRaised(Entity Corpse, Entity Minion, string Pattern) : GameEvent;

/// <summary>A minion was spent by an effect of its owner's, its life feeding the effect.</summary>
public sealed record MinionConsumed(Entity Minion, Entity Owner, double Life) : GameEvent;

/// <summary>A dome caught a projectile: dissolved it, turned it back, or slowed it.</summary>
public sealed record ProjectileIntercepted(Entity Projectile, Entity Dome, string Mode) : GameEvent;
