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
