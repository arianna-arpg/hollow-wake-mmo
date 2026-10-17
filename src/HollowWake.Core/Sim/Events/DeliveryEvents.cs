using System.Numerics;
using HollowWake.Core.Ecs;

namespace HollowWake.Core.Sim;

// Events of deliveries: projectiles forking, chaining, bouncing and returning, placements telegraphed and
// landing, displacements starting and ending, bodies thrown against walls. Declared here so no package
// edits Sim/Events.cs. Spawns and despawns of the delivery entities themselves stay EntitySpawned and
// EntityDespawned with the pattern "projectile" or "field".

/// <summary>A projectile split where it hit: it ends there and its children fly on.</summary>
public sealed record ProjectileForked(Entity Projectile, Entity Owner, int Children, Vector3 Position) : GameEvent;

/// <summary>A projectile that hit <paramref name="From"/> turned toward <paramref name="To"/> instead of ending.</summary>
public sealed record ProjectileChained(Entity Projectile, Entity Owner, Entity From, Entity To) : GameEvent;

/// <summary>A projectile glanced off a wall or the ground instead of ending.</summary>
public sealed record ProjectileBounced(Entity Projectile, Entity Owner, Vector3 Position) : GameEvent;

/// <summary>A returning projectile turned back toward its origin or its caster.</summary>
public sealed record ProjectileTurned(Entity Projectile, Entity Owner, Vector3 Position) : GameEvent;

/// <summary>A returning projectile arrived back where it was going: caught by its caster or home at its origin.</summary>
public sealed record ProjectileReturned(Entity Projectile, Entity Owner, Vector3 Position) : GameEvent;

/// <summary>A strike or placement was marked on the ground and lands after <paramref name="Seconds"/>.</summary>
public sealed record PlacementTelegraphed(Entity Owner, Vector3 Position, float Radius, double Seconds) : GameEvent;

/// <summary>A delayed placement (a field with a delay) arrived and began to act.</summary>
public sealed record PlacementLanded(Entity Placement, Entity Owner, Vector3 Position) : GameEvent;

/// <summary>A body began to be carried over time: a dash, a leap, a blink with a delay.</summary>
public sealed record DisplacementStarted(Entity Entity, string Kind, Vector3 From, Vector3 To) : GameEvent;

/// <summary>A carried body arrived, or was stopped short.</summary>
public sealed record DisplacementEnded(Entity Entity, string Kind, Vector3 Position, bool Arrived) : GameEvent;

/// <summary>A shoved body was stopped by something it could not enter, at the speed it was moving.</summary>
public sealed record BodyCollided(Entity Entity, Entity Source, Vector3 Position, float Speed) : GameEvent;
