namespace HollowWake.Core.Combat.Stages;

/// <summary>
/// The target rolls <c>block.chance</c> in the hit's tag context. By default a block keeps the fraction
/// <c>1 - block.amount</c> of every Thread (a full stop). With rule <c>combat.block: guard</c> a block stops up to
/// a value instead: rule <c>combat.block_value</c> read on the target (its <c>level</c> and <c>block.value</c>),
/// never more than <c>block.power</c> of the hit, so heavier wounds seep through. Rule
/// <c>combat.block_stops_ailments</c> decides whether what seeps through can still leave statuses.
/// </summary>
internal sealed class BlockStage : IHitStage
{
    public string Name => "block";

    public HitFlow Run(HitScope hit)
    {
        var ts = hit.Target!;
        var ctx = hit.Context;
        var blockChance = Math.Clamp(ts.Get("block.chance", hit.Tags), 0, 1);
        if (blockChance > 0 && ctx.Rng.Chance(blockChance))
        {
            hit.Result.Blocked = true;
            hit.BlockMultiplier = hit.Plan.GuardBlock ? GuardMultiplier(hit) : 1 - Math.Clamp(ts.Get("block.amount"), 0, 1);
            ts.Stamp("blocked", ctx.Sim.Clock.Time);
            for (var i = 0; i < hit.ThreadCount; i++)
            {
                if (hit.Live[i])
                {
                    hit.Amounts[i] *= hit.BlockMultiplier;
                }
            }
        }
        return HitFlow.Continue;
    }

    /// <summary>The fraction of the hit a guard-value block lets through.</summary>
    private static double GuardMultiplier(HitScope hit)
    {
        var ts = hit.Target!;
        var total = 0.0;
        for (var i = 0; i < hit.ThreadCount; i++)
        {
            if (hit.Live[i])
            {
                total += hit.Amounts[i];
            }
        }
        if (total <= 0)
        {
            return 1;
        }
        var scope = hit.Values.Clear().Set("level", ts.Level).Set("block.value", ts.Get("block.value", hit.Tags));
        var value = Math.Max(0, hit.Plan.BlockValue.Evaluate(scope));
        var power = Math.Clamp(ts.Get("block.power", hit.Tags), 0, 1);
        var stopped = Math.Min(total * power, value);
        return 1 - stopped / total;
    }
}
