namespace HollowWake.Core.Combat.Stages;

/// <summary>Immunity flags refuse the hit outright: nothing lands and nothing is published.</summary>
internal sealed class ImmunityStage : IHitStage
{
    public string Name => "immunity";

    public HitFlow Run(HitScope hit)
    {
        var ts = hit.Target!;
        if (ts.HasFlag("immune") || ts.HasFlag("invulnerable"))
        {
            hit.Result.Immune = true;
            return HitFlow.Stop;
        }
        return HitFlow.Continue;
    }
}
