using HollowWake.Core.Ecs;

namespace HollowWake.Core.Sim;

// Events of progression and items: items moved between containers, stones socketed, forms learned, choices
// picked, passives refunded, conversations and trade. Declared here so no package edits Sim/Events.cs.

/// <summary>An item changed place: <paramref name="From"/> and <paramref name="To"/> name the containers (bag, equipment, world, a container id).</summary>
public sealed record ItemMoved(Entity Entity, long ItemUid, string From, string To) : GameEvent;

/// <summary>A codex was learned into a rack seat (a copy already there went back to the bag).</summary>
public sealed record FormLearned(Entity Entity, int Slot, long Codex) : GameEvent;

/// <summary>A rack seat's codex and stones went back to the bag.</summary>
public sealed record FormUnlearned(Entity Entity, int Slot, long Codex) : GameEvent;

/// <summary>A glyph stone was socketed into a rack seat.</summary>
public sealed record StoneSocketed(Entity Entity, int Slot, int Socket, long Stone) : GameEvent;

/// <summary>A glyph stone came out of a rack seat into the bag.</summary>
public sealed record StoneUnsocketed(Entity Entity, int Slot, int Socket, long Stone) : GameEvent;

/// <summary>An option was picked on a choice node.</summary>
public sealed record PassiveChosen(Entity Entity, string Node, string Option) : GameEvent;

/// <summary>A node was refunded with its picks; its points went back to their currency.</summary>
public sealed record PassiveRefunded(Entity Entity, string Node) : GameEvent;

/// <summary>A realm opened to the hero and its roots were granted.</summary>
public sealed record RealmOpened(Entity Entity, string Realm) : GameEvent;

/// <summary>Points of a currency were granted (a quest reward for a realm).</summary>
public sealed record PassivePointsGranted(Entity Entity, string Currency, int Amount) : GameEvent;

/// <summary>A story flag was set or cleared on a hero (by a conversation or a reward).</summary>
public sealed record StoryFlagSet(Entity Entity, string Flag, bool On) : GameEvent;
