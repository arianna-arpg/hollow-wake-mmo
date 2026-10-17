using HollowWake.Core.Ecs;

namespace HollowWake.Core.Sim;

// Events of progression and items: items moved between containers, stones socketed, forms learned, choices
// picked, passives refunded, conversations and trade. Declared here so no package edits Sim/Events.cs.

/// <summary>An item changed place: <paramref name="From"/> and <paramref name="To"/> name the containers (bag, equipment, world, a container id).</summary>
public sealed record ItemMoved(Entity Entity, long ItemUid, string From, string To) : GameEvent;
