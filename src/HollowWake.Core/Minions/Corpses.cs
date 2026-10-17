using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Sim;

namespace HollowWake.Core.Minions;

/// <summary>
/// Corpses: dead bodies that remember what they were through their identity, so they can be raised as
/// that kind or consumed until the lifetime system clears them after <c>combat.corpse_seconds</c>. Any dead
/// body with a pattern is one, except players, minions and bodies tagged <c>no_corpse</c>; a death adds
/// nothing, so dying costs no more than it did.
/// </summary>
public static class Corpses
{
    /// <summary>Whether a body is a corpse that can still be raised or consumed.</summary>
    public static bool Is(Ecs.World world, Entity e) =>
        !e.IsNone
        && world.IsAlive(e)
        && !world.IsDeferredDestroy(e)
        && world.Has<Dead>(e)
        && !world.Has<Player>(e)
        && !world.Has<Minion>(e)
        && world.TryGet<Identity>(e, out var identity)
        && identity.Pattern is not null
        && !identity.IsPlayer
        && !identity.Tags.Has("no_corpse");

    /// <summary>The pattern a corpse was, or null for a body that is not one.</summary>
    public static string? PatternOf(Ecs.World world, Entity e) => Is(world, e) ? world.Get<Identity>(e)!.Pattern : null;

    /// <summary>Lays a freshly made body down as a corpse without a death: no event, no credit, no loot.</summary>
    public static void LayDown(Simulation sim, Entity e)
    {
        sim.World.Get<Stats>(e)?.SetResource("life", 0);
        sim.World.Add(e, new Dead { DiedAt = sim.Clock.Time });
        sim.World.Remove<Behavior>(e);
        sim.World.Get<Motion>(e)?.Stop();
    }
}
