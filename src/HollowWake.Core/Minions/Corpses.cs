using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Sim;

namespace HollowWake.Core.Minions;

/// <summary>
/// Corpses: dead bodies that remember what they were, so they can be raised as that kind or consumed, until
/// the lifetime system clears them after <c>combat.corpse_seconds</c>. Players, minions and bodies tagged
/// <c>no_corpse</c> leave none.
/// </summary>
public static class Corpses
{
    /// <summary>Marks a body that just died as a corpse when it can leave one.</summary>
    public static void Mark(Simulation sim, Entity e)
    {
        if (!sim.World.TryGet<Identity>(e, out var identity) || identity.Pattern is null || identity.IsPlayer || identity.Tags.Has("no_corpse"))
        {
            return;
        }
        if (sim.World.Has<Player>(e) || sim.World.Has<Minion>(e))
        {
            return;
        }
        sim.World.Add(e, new Corpse { Pattern = identity.Pattern, Level = sim.World.Get<Stats>(e)?.Level ?? 1, DiedAt = sim.Clock.Time });
    }

    /// <summary>Lays a freshly made body down as a corpse without a death: no event, no credit, no loot.</summary>
    public static void LayDown(Simulation sim, Entity e, string pattern)
    {
        var stats = sim.World.Get<Stats>(e);
        stats?.SetResource("life", 0);
        sim.World.Add(e, new Dead { DiedAt = sim.Clock.Time });
        sim.World.Remove<Behavior>(e);
        sim.World.Get<Motion>(e)?.Stop();
        sim.World.Add(e, new Corpse { Pattern = pattern, Level = stats?.Level ?? 1, DiedAt = sim.Clock.Time });
    }
}
