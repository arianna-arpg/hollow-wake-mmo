using System.Numerics;
using HollowWake.Core.Ecs;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Sim;

// The vocabulary of things that happen. Quests, loot, progression, territory, networking and tests
// all listen to these rather than to each other, which keeps systems independent and mods able to react.

public sealed record EntitySpawned(Entity Entity, string? Pattern, Vector3 Position) : GameEvent;
public sealed record EntityDespawned(Entity Entity, string? Pattern) : GameEvent;
public sealed record EntityMoved(Entity Entity, Vector3 From, Vector3 To) : GameEvent;
public sealed record EntityJumped(Entity Entity) : GameEvent;

public sealed record DamageDealt(Entity Source, Entity Target, double Total, bool Crit, TagSet Tags, IReadOnlyDictionary<string, double> ByThread, bool Evaded, bool Blocked) : GameEvent;
public sealed record Healed(Entity Source, Entity Target, double Amount, string Resource) : GameEvent;
public sealed record EntityDied(Entity Entity, Entity Killer, string? Pattern, TagSet Tags, Vector3 Position) : GameEvent;
public sealed record EntityRevived(Entity Entity, Vector3 Position) : GameEvent;

public sealed record StatusApplied(Entity Target, Entity Source, string Status, int Stacks, double Duration) : GameEvent;
public sealed record StatusExpired(Entity Target, string Status) : GameEvent;

public sealed record WeaveStarted(Entity Caster, string Weave, Entity Target, Vector3 Point, double CastTime) : GameEvent;
public sealed record WeaveCast(Entity Caster, string Weave, Entity Target, Vector3 Point, TagSet Tags) : GameEvent;
public sealed record WeaveFailed(Entity Caster, string Weave, string Reason) : GameEvent;
public sealed record WeaveInterrupted(Entity Caster, string Weave) : GameEvent;

public sealed record ItemAcquired(Entity Entity, long ItemUid, string Base, int Count, TagSet Tags, string Rarity) : GameEvent;
public sealed record ItemDropped(Entity Drop, Entity From, string Base, string Rarity, Vector3 Position) : GameEvent;
public sealed record ItemEquipped(Entity Entity, long ItemUid, string Slot) : GameEvent;
public sealed record ItemCrafted(Entity Crafter, string Recipe, long ItemUid, double Quality) : GameEvent;
public sealed record CurrencyChanged(Entity Entity, string Currency, double Delta, double Total) : GameEvent;

public sealed record ExperienceGained(Entity Entity, double Amount, double Total) : GameEvent;
public sealed record LevelUp(Entity Entity, int Level) : GameEvent;
public sealed record PassiveAllocated(Entity Entity, string Node) : GameEvent;

public sealed record QuestAccepted(Entity Entity, string Quest) : GameEvent;
public sealed record QuestProgressed(Entity Entity, string Quest, string Objective, int Count, int Required) : GameEvent;
public sealed record QuestCompleted(Entity Entity, string Quest) : GameEvent;
public sealed record QuestTurnedIn(Entity Entity, string Quest) : GameEvent;

public sealed record Interacted(Entity Entity, Entity Target, string Kind, string? Ref) : GameEvent;
public sealed record PoiDiscovered(Entity Entity, string Poi, string Name) : GameEvent;
public sealed record ZoneEntered(Entity Entity, string Zone) : GameEvent;

public sealed record FountBindingStarted(Entity Entity, string Fount, double Duration) : GameEvent;
public sealed record FountBound(Entity Entity, string Fount, string Holding) : GameEvent;
public sealed record FountContested(string Fount, string Holding, Entity By) : GameEvent;
public sealed record DecreeEnacted(string Holding, string Decree) : GameEvent;
public sealed record StructureRaised(string Holding, string Structure, Vector3 Position) : GameEvent;
public sealed record HoldingYield(string Holding, IReadOnlyDictionary<string, double> Yield) : GameEvent;

public sealed record ChatMessage(Entity From, string Channel, string Text) : GameEvent;
public sealed record Notice(Entity To, string Text) : GameEvent;

/// <summary>Raised by the <c>event</c> effect and by mods; quests can count it by name.</summary>
public sealed record CustomEvent(string Name, Entity Source, Entity Target, IReadOnlyDictionary<string, double> Data) : GameEvent;
