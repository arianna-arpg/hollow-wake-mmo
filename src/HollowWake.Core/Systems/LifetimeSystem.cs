using System.Numerics;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Sim;
using HollowWake.Core.StatusEffects;

namespace HollowWake.Core.Systems;

/// <summary>Expires lifetimes and minions, clears corpses, removes despawned bodies from the index.</summary>
public sealed class LifetimeSystem : SimSystem
{
    public override string Name => "lifetimes";
    public override SimPhase Phase => SimPhase.Cleanup;

    public override void Update(Simulation sim)
    {
        var now = sim.Clock.Time;
        var corpseSeconds = sim.Content.Rules.Number("combat.corpse_seconds", 12);
        foreach (var (e, life) in sim.World.Query<Lifetime>())
        {
            if (now >= life.ExpiresAt)
            {
                Despawn(sim, e);
            }
        }
        foreach (var (e, minion) in sim.World.Query<Minion>())
        {
            if (now >= minion.ExpiresAt || (!minion.Owner.IsNone && !sim.World.IsAlive(minion.Owner)))
            {
                Despawn(sim, e);
            }
        }
        foreach (var (e, dead) in sim.World.Query<Dead>())
        {
            if (sim.World.Has<Player>(e))
            {
                continue;
            }
            if (now - dead.DiedAt >= corpseSeconds)
            {
                Despawn(sim, e);
            }
        }
        foreach (var (e, drop) in sim.World.Query<LootDrop>())
        {
            if (now >= drop.ExpiresAt)
            {
                Despawn(sim, e);
            }
        }
    }

    public static void Despawn(Simulation sim, Entity e)
    {
        if (!sim.World.IsAlive(e))
        {
            return;
        }
        sim.Spatial.Remove(e);
        if (sim.TryService<Patterns.BlockedCells>(out var blocked) && sim.World.Has<Prop>(e))
        {
            blocked.UnblockAll(e);
        }
        sim.Events.Publish(new EntityDespawned(e, sim.World.Get<Identity>(e)?.Pattern) { Tick = sim.Clock.Tick });
        sim.World.DestroyDeferred(e);
    }
}
