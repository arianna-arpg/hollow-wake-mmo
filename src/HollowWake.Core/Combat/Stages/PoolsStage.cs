namespace HollowWake.Core.Combat.Stages;

/// <summary>
/// The damage received drains the pools in rule <c>combat.pools</c> order (<c>ward</c> then <c>life</c>);
/// life takes the rest when the rule leaves it out. The hit is then recorded as recent: hurt on the
/// target, hit (and crit) on the attacker.
/// </summary>
internal sealed class PoolsStage : IHitStage
{
    public string Name => "pools";

    public HitFlow Run(HitScope hit)
    {
        var ts = hit.Target!;
        var plan = hit.Plan;
        var remaining = hit.Total;
        foreach (var pool in plan.Pools)
        {
            if (remaining <= 0)
            {
                break;
            }
            if (pool != "life" && !ts.Resources.ContainsKey(pool))
            {
                continue;
            }
            var cur = ts.Resource(pool);
            var take = Math.Min(cur, remaining);
            if (take > 0)
            {
                ts.AddResource(pool, -take);
                remaining -= take;
            }
            if (pool == "life")
            {
                break;
            }
        }
        if (remaining > 0 && !plan.PoolsIncludeLife)
        {
            ts.AddResource("life", -remaining);
        }
        var now = hit.Sim.Clock.Time;
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
}
