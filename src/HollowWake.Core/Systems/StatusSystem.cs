using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Sim;
using HollowWake.Core.StatusEffects;

namespace HollowWake.Core.Systems;

/// <summary>
/// Ticks statuses, decays and drains charges, regenerates resources, keeps the pools' state flags, maintains
/// condition flags.
/// </summary>
/// <remarks>
/// Pools whose rule (<c>combat.pool_rules.{id}.states</c>) asks for it carry the flag the interface reads for
/// their state, stamped with the time it began: <c>pool.{id}.broken</c> while empty or while the pool's
/// <c>on_break</c> status lasts, <c>pool.{id}.rearming</c> while its <c>on_hurt</c> or <c>rearm</c> status lasts,
/// <c>pool.{id}.recharging</c> while it refills, none once it is full. Entering recharging and coming back full
/// are published as <see cref="PoolRecharging"/> and <see cref="PoolRestored"/>.
/// </remarks>
public sealed class StatusSystem : SimSystem
{
    public override string Name => "statuses";
    public override SimPhase Phase => SimPhase.Status;

    // The resources that regenerate and the pools that keep state flags, read once per content set rather than every tick.
    private ContentRegistry? _content;
    private ResourceDef[] _regenerating = Array.Empty<ResourceDef>();
    private (PoolRule Rule, string? Regen)[] _stated = Array.Empty<(PoolRule, string?)>();

    public override void Update(Simulation sim)
    {
        var dt = sim.Clock.Dt;
        var now = sim.Clock.Time;
        var service = sim.Service<StatusService>();
        sim.TryService<ChargeService>(out var charges);
        sim.TryService<ProcService>(out var procs);
        if (!ReferenceEquals(_content, sim.Content))
        {
            _content = sim.Content;
            _regenerating = sim.Content.Each<ResourceDef>().Where(r => r.Regen is not null).ToArray();
            _stated = HitPlan.For(sim.Content.Rules).PoolRules
                .Where(r => r.States)
                .Select(r => (r, sim.Content.Get<ResourceDef>(r.Resource)?.Regen))
                .ToArray();
        }
        // The loop stays small and the per-body work lives in its own method, so the runtime can optimize a
        // long-running update without recompiling the whole body in the middle of a tick.
        foreach (var (e, stats) in sim.World.Query<Stats>())
        {
            stats.Now = now;
            if (sim.World.Has<Dead>(e))
            {
                continue;
            }
            Advance(sim, service, charges, e, stats, now, dt);
            procs?.Watch(e, stats);
        }
    }

    private void Advance(Simulation sim, StatusService service, ChargeService? chargeService, Entity e, Stats stats, double now, double dt)
    {
        sim.World.TryGet<Statuses>(e, out var statuses);
        if (statuses is not null)
        {
            service.Tick(sim, e, stats, statuses, dt);
            if (!sim.World.IsAlive(e) || sim.World.Has<Dead>(e))
            {
                return;
            }
        }
        if (chargeService is not null && sim.World.TryGet<Charges>(e, out var charges) && charges.Counts.Count > 0)
        {
            chargeService.Tick(sim, e, charges, dt);
        }
        stats.SyncReserved();
        var regenerating = _regenerating;
        for (var i = 0; i < regenerating.Length; i++)
        {
            var r = regenerating[i];
            // A pool the body neither holds nor has anything granting a maximum for costs a lookup, not a fold.
            if (!stats.Resources.ContainsKey(r.Id) && !stats.Attributes.Carries(r.Max))
            {
                continue;
            }
            var regen = stats.Get(r.Regen!);
            if (regen != 0)
            {
                stats.AddResource(r.Id, regen * dt);
            }
        }
        var stated = _stated;
        for (var i = 0; i < stated.Length; i++)
        {
            PoolState(sim, e, stats, statuses, stated[i].Rule, stated[i].Regen, now);
        }
        if (sim.World.TryGet<Motion>(e, out var m))
        {
            var moving = now - m.LastMoveTime <= 0.15;
            var stationary = now - m.LastMoveTime >= 0.6;
            Set(stats, "moving", moving);
            Set(stats, "stationary", stationary);
            Set(stats, "airborne", !m.Grounded && !m.Flying);
        }
    }

    /// <summary>Keeps one pool's state flag on a body that holds the pool.</summary>
    private static void PoolState(Simulation sim, Entity e, Stats stats, Statuses? statuses, PoolRule rule, string? regenAttribute, double now)
    {
        var pool = rule.Resource;
        if (!stats.Resources.TryGetValue(pool, out var current))
        {
            // A body that never held the pool has no state to keep.
            return;
        }
        var max = stats.ResourceMax(pool);
        if (max <= 0)
        {
            // The pool is gone (its maximum fell to nothing): it shows no state.
            stats.Flags.Remove(rule.BrokenFlag);
            stats.Flags.Remove(rule.RearmingFlag);
            stats.Flags.Remove(rule.RechargingFlag);
            return;
        }
        var wasStated = stats.Flags.Contains(rule.BrokenFlag) || stats.Flags.Contains(rule.RearmingFlag) || stats.Flags.Contains(rule.RechargingFlag);
        var broken = current <= 0 || (rule.OnBreak is { } onBreak && statuses is not null && statuses.Has(onBreak));
        var rearming = !broken && statuses is not null && ((rule.OnHurt is { } hurt && statuses.Has(hurt)) || (rule.Rearm is { } rearm && statuses.Has(rearm)));
        var recharging = !broken && !rearming && current < max - 1e-9 && regenAttribute is not null && stats.Get(regenAttribute) > 0;
        Mark(stats, rule.BrokenFlag, broken, now);
        Mark(stats, rule.RearmingFlag, rearming, now);
        if (Mark(stats, rule.RechargingFlag, recharging, now))
        {
            sim.Events.Publish(new PoolRecharging(e, pool, current) { Tick = sim.Clock.Tick });
        }
        if (wasStated && !broken && !rearming && !recharging && current >= max - 1e-9)
        {
            sim.Events.Publish(new PoolRestored(e, pool, current) { Tick = sim.Clock.Tick });
        }
    }

    /// <summary>Sets or clears a state flag, stamping it when it turns on; true when it just turned on.</summary>
    private static bool Mark(Stats stats, string flag, bool on, double now)
    {
        if (on)
        {
            if (stats.Flags.Add(flag))
            {
                stats.Stamp(flag, now);
                return true;
            }
            return false;
        }
        stats.Flags.Remove(flag);
        return false;
    }

    private static void Set(Stats stats, string flag, bool on)
    {
        if (on)
        {
            stats.Flags.Add(flag);
        }
        else
        {
            stats.Flags.Remove(flag);
        }
    }
}
