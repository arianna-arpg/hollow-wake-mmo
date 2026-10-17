using HollowWake.Core.Components;
using HollowWake.Core.Sim;
using HollowWake.Core.StatusEffects;

namespace HollowWake.Core.Combat.Stages;

/// <summary>
/// The damage received reaches the pools: first the absorption pools statuses carry (newest first), then the
/// pools of rule <c>combat.pools</c> in order (<c>poise</c>, <c>ward</c>, <c>shield</c>, then <c>life</c>);
/// the statuses a pool's rule names are applied by the bearer to itself;
/// life takes the rest when the rule leaves it out. What each pool does comes from
/// <c>combat.pool_rules</c> (see <see cref="PoolRule"/>): an absorbing pool takes what it can, up to its
/// fraction; a break bar takes break damage without absorbing anything and breaks when emptied; a hit that
/// reaches a pool the body holds applies the pool's <c>on_hurt</c> status, one that empties it applies its
/// <c>on_break</c> status and is published as <see cref="PoolBroken"/>. A hit that cannot kill leaves a point
/// of life. Statuses banking what their bearer takes hear of the hit, which is then recorded as recent: hurt
/// on the target, hit (and crit) on the attacker.
/// </summary>
internal sealed class PoolsStage : IHitStage
{
    public string Name => "pools";

    public HitFlow Run(HitScope hit)
    {
        var ts = hit.Target!;
        var plan = hit.Plan;
        var sim = hit.Sim;
        var ctx = hit.Context;
        var remaining = hit.Total;
        sim.TryService<StatusService>(out var statuses);
        if (remaining > 0 && statuses is not null)
        {
            var before = remaining;
            remaining = statuses.Absorb(sim, ctx.Target, ts, remaining);
            hit.Result.Absorbed += before - remaining;
        }
        var landing = hit.Total > 0;
        var pools = plan.Pools;
        var rules = plan.PoolRules;
        for (var k = 0; k < pools.Length; k++)
        {
            var pool = pools[k];
            var rule = rules[k];
            var isLife = pool == "life";
            if (rule.Break)
            {
                if (remaining > 0 && ts.Resources.ContainsKey(pool) && !hit.Skips(pool))
                {
                    BreakBar(hit, rule, statuses, remaining);
                }
                continue;
            }
            if (remaining <= 0)
            {
                break;
            }
            if (!isLife && !ts.Resources.ContainsKey(pool))
            {
                continue;
            }
            if (rule.OnHurt is { } hurt && landing && statuses is not null)
            {
                // The pool's own states are the bearer's: the attacker applies nothing, so its status triggers stay quiet.
                statuses.Apply(sim, ctx.Target, hurt, ctx.Target, new StatusApplication { FixedDuration = true, SourceKey = pool, Depth = ctx.Depth });
            }
            var cur = ts.Resource(pool);
            var take = Math.Min(cur, remaining * rule.Fraction);
            if (isLife && hit.Packet.CannotKill)
            {
                take = Math.Min(take, Math.Max(0, cur - 1));
            }
            if (take > 0)
            {
                ts.AddResource(pool, -take);
                remaining -= take;
                if (!isLife)
                {
                    hit.Result.Absorbed += take;
                    if (cur > 0 && ts.Resource(pool) <= 0)
                    {
                        Broke(hit, rule, statuses);
                    }
                }
            }
            if (isLife)
            {
                remaining = 0;
                break;
            }
        }
        if (remaining > 0 && !plan.PoolsIncludeLife)
        {
            var life = ts.Resource("life");
            var take = hit.Packet.CannotKill ? Math.Min(remaining, Math.Max(0, life - 1)) : remaining;
            ts.AddResource("life", -take);
        }
        if (landing && statuses is not null)
        {
            statuses.DamageTaken(sim, ctx.Target, ts, hit.Total, ctx.Depth);
        }
        var now = sim.Clock.Time;
        ts.Stamp("hurt", now);
        if (hit.Source is { } source)
        {
            source.Stamp("hit", now);
            if (hit.Packet.Crit)
            {
                source.Stamp("crit", now);
            }
        }
        return HitFlow.Continue;
    }

    /// <summary>
    /// A break bar (poise) loses what of the hit reaches it times the attacker's <c>dealt</c> times its own
    /// <c>taken</c> attribute, scoped by the hit's tags; emptied, it breaks. A bar already empty takes nothing.
    /// </summary>
    private static void BreakBar(HitScope hit, PoolRule rule, StatusService? statuses, double reaching)
    {
        var ts = hit.Target!;
        var cur = ts.Resource(rule.Resource);
        if (cur <= 0)
        {
            return;
        }
        var dealt = hit.Source is { } source && rule.Dealt is { } d ? source.Attributes.Get(d, source, hit.Tags, hit.Extra) : 1;
        var taken = rule.Taken is { } t ? ts.Get(t, hit.Tags) : 1;
        var damage = reaching * dealt * taken;
        if (damage <= 0)
        {
            return;
        }
        ts.AddResource(rule.Resource, -damage);
        hit.Result.Broken += Math.Min(cur, damage);
        if (ts.Resource(rule.Resource) <= 0)
        {
            Broke(hit, rule, statuses);
        }
    }

    /// <summary>A pool or bar emptied by the hit: its break status, the note, and the break triggers.</summary>
    private static void Broke(HitScope hit, PoolRule rule, StatusService? statuses)
    {
        var sim = hit.Sim;
        var ctx = hit.Context;
        if (rule.OnBreak is { } status && statuses is not null)
        {
            statuses.Apply(sim, ctx.Target, status, ctx.Target, new StatusApplication { FixedDuration = true, SourceKey = rule.Resource, Depth = ctx.Depth });
        }
        sim.Events.Publish(new PoolBroken(ctx.Target, ctx.Source, rule.Resource) { Tick = sim.Clock.Tick, Depth = ctx.Depth });
        CombatTriggers.Raise(sim, ctx.Source, CombatTriggers.Break, ctx.Source, ctx.Target, rule.Resource, ctx.Depth);
        CombatTriggers.Raise(sim, ctx.Target, CombatTriggers.Broken, ctx.Source, ctx.Target, rule.Resource, ctx.Depth);
    }
}
