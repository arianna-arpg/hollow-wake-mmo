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

/// <summary>A conversation showed a line to the hero.</summary>
public sealed record DialogueShown(Entity Entity, Entity Speaker, string Dialogue, string Node) : GameEvent;

/// <summary>The hero took a choice in a conversation (quests and tests can count it).</summary>
public sealed record DialogueChosen(Entity Entity, Entity Speaker, string Dialogue, string Node, string Choice) : GameEvent;

/// <summary>A conversation ended: <paramref name="Why"/> is ended, closed, departed, replaced or broken.</summary>
public sealed record DialogueEnded(Entity Entity, Entity Speaker, string Dialogue, string Why) : GameEvent;

/// <summary>A vendor pane opened for the hero, its stock minted.</summary>
public sealed record VendorOpened(Entity Entity, Entity Vendor, string Id) : GameEvent;

/// <summary>Something the open vendor pane shows changed (a trade, the wallet).</summary>
public sealed record VendorChanged(Entity Entity, Entity Vendor) : GameEvent;

/// <summary>The hero's vendor pane closed.</summary>
public sealed record VendorClosed(Entity Entity, Entity Vendor, string Why) : GameEvent;

/// <summary>The hero's crafting station pane closed.</summary>
public sealed record StationClosed(Entity Entity, Entity Station, string Why) : GameEvent;

/// <summary>The hero bought from a vendor.</summary>
public sealed record ItemBought(Entity Entity, Entity Vendor, string Stock, long ItemUid, IReadOnlyDictionary<string, double> Paid) : GameEvent;

/// <summary>The hero sold to a vendor.</summary>
public sealed record ItemSold(Entity Entity, Entity Vendor, long ItemUid, IReadOnlyDictionary<string, double> Received) : GameEvent;

/// <summary>A container board changed: an item went in or out, a rung unlocked, or a stash page was bought.</summary>
public sealed record ContainerChanged(Entity Entity, string Board) : GameEvent;

/// <summary>A stone went into a gear item's socket (or came out, when <paramref name="Socketed"/> is false).</summary>
public sealed record GearSocketChanged(Entity Entity, long Host, int Socket, long Stone, bool Socketed) : GameEvent;

/// <summary>A station performed a service for the hero (levelled a stone, merged codices, reset passives).</summary>
public sealed record StationServed(Entity Entity, Entity Station, string Service, IReadOnlyDictionary<string, double> Paid) : GameEvent;
