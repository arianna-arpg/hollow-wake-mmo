using System.Numerics;
using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Expressions;
using HollowWake.Core.Loot;
using HollowWake.Core.Patterns;
using HollowWake.Core.Progression;
using HollowWake.Core.Sim;
using HollowWake.Core.StatusEffects;

namespace HollowWake.Core.Combat;

/// <summary>Deaths: mark (and leave a corpse to raise), stop, credit, drop, propagate, and later revive players.</summary>
public sealed class DeathService
{
    /// <summary>Kills a body. <paramref name="depth"/> is the effect depth of the killing hit, carried by <see cref="EntityDied"/>.</summary>
    public void Kill(Simulation sim, Entity victim, Entity killer, int depth = 0)
    {
        if (!sim.World.IsAlive(victim) || sim.World.Has<Dead>(victim))
        {
            return;
        }
        var stats = sim.World.Get<Stats>(victim);
        var identity = sim.World.Get<Identity>(victim);
        var t = sim.World.Get<Transform>(victim);
        stats?.SetResource("life", 0);
        sim.World.Add(victim, new Dead { DiedAt = sim.Clock.Time, Killer = killer });
        Minions.Corpses.Mark(sim, victim);
        sim.World.Get<Motion>(victim)?.Stop();
        if (sim.World.TryGet<WeaveBook>(victim, out var book))
        {
            book.Casting = null;
        }
        if (sim.TryService<StatusService>(out var statuses))
        {
            statuses.PropagateOnDeath(sim, victim);
        }
        var credit = CreditTarget(sim, killer);
        if (!credit.IsNone && sim.World.TryGet<Stats>(credit, out var ks))
        {
            ks.Stamp("killed", sim.Clock.Time);
        }
        sim.Events.Publish(new EntityDied(victim, credit, identity?.Pattern, stats?.Tags ?? Tags.TagSet.Empty, t?.Position ?? default) { Tick = sim.Clock.Tick, Depth = depth });

        if (sim.World.TryGet<Drops>(victim, out var drops) && t is not null)
        {
            var rng = sim.RngFor(victim, "death");
            var level = stats?.Level ?? 1;
            if (drops.Xp is not null && !credit.IsNone && sim.TryService<ProgressionService>(out var progression))
            {
                var scope = new ScopeOverlay(stats).Set("level", level).Set("rarity_rank", stats?.Gauge("rarity_rank") ?? 0);
                var xp = drops.Xp.Evaluate(scope);
                var rarityMult = identity?.Rarity is { } r && sim.Content.Get<Definitions.MonsterRarityDef>(r) is { } rd ? rd.XpMultiplier : 1;
                progression.GrantXp(sim, credit, xp * rarityMult);
            }
            if (drops.Table is not null && sim.TryService<LootService>(out var loot))
            {
                var chance = drops.DropChance;
                var bonusRolls = identity?.Rarity is { } r2 && sim.Content.Get<Definitions.MonsterRarityDef>(r2) is { } rd2 ? rd2.BonusLootRolls : 0;
                var rolls = (rng.Chance(chance) ? 1 : 0) + bonusRolls;
                for (var i = 0; i < rolls; i++)
                {
                    loot.DropTable(sim, drops.Table, level, t.Position, credit, victim, rng.Fork((ulong)i));
                }
            }
        }
        if (sim.World.Has<Player>(victim) && sim.World.TryGet<Player>(victim, out var player))
        {
            player.RespawnAt = sim.Clock.Time + sim.Content.Rules.Number("combat.respawn_seconds", 5);
        }
    }

    /// <summary>Kills credit the owner of a minion or projectile, so quests and experience reach the player.</summary>
    public static Entity CreditTarget(Simulation sim, Entity killer)
    {
        var guard = 0;
        while (!killer.IsNone && sim.World.IsAlive(killer) && guard++ < 8)
        {
            if (sim.World.TryGet<Minion>(killer, out var minion) && !minion.Owner.IsNone && sim.World.IsAlive(minion.Owner))
            {
                killer = minion.Owner;
                continue;
            }
            if (sim.World.TryGet<Identity>(killer, out var id) && !id.Owner.IsNone && sim.World.IsAlive(id.Owner) && !sim.World.Has<Player>(killer) && (sim.World.Has<Effects.Projectile>(killer) || sim.World.Has<Effects.Field>(killer)))
            {
                killer = id.Owner;
                continue;
            }
            break;
        }
        return killer;
    }

    public bool Revive(Simulation sim, Entity e, Vector3 at, double lifeFraction = 1)
    {
        if (!sim.World.TryGet<Dead>(e, out _) || !sim.World.TryGet<Stats>(e, out var stats))
        {
            return false;
        }
        sim.World.Remove<Dead>(e);
        sim.World.Remove<Corpse>(e);
        if (sim.World.TryGet<Statuses>(e, out var statuses))
        {
            foreach (var s in statuses.Active.ToList())
            {
                stats.Attributes.RemoveSource(s.ModifierSource);
            }
            statuses.Active.Clear();
            sim.Service<StatusService>().Refresh(sim, e, stats, statuses);
        }
        stats.FillResources();
        stats.SetResource("life", stats.ResourceMax("life") * lifeFraction);
        if (sim.World.TryGet<Transform>(e, out var t))
        {
            t.Position = sim.Terrain.SnapToGround(at, 3);
            sim.Spatial.Update(e, t.Position);
        }
        if (sim.World.TryGet<Motion>(e, out var m))
        {
            m.Stop();
            m.Velocity = Vector3.Zero;
            m.Grounded = true;
        }
        sim.Events.Publish(new EntityRevived(e, at) { Tick = sim.Clock.Tick });
        return true;
    }
}
