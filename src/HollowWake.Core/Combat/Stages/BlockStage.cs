namespace HollowWake.Core.Combat.Stages;

/// <summary>
/// The target rolls <c>block.chance</c> in the hit's tag context; a block keeps the fraction
/// <c>1 - block.amount</c> of every Thread (a full stop by default).
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
            hit.BlockMultiplier = 1 - Math.Clamp(ts.Get("block.amount"), 0, 1);
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
}
