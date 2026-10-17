namespace HollowWake.Core.Combat.Stages;

/// <summary>
/// A per-hit cap: no single hit lands more than the target's <c>hit.cap</c> (a fraction of its maximum life;
/// zero, the default, means no cap). A capped hit is marked clamped.
/// </summary>
internal sealed class CapStage : IHitStage
{
    public string Name => "cap";

    public HitFlow Run(HitScope hit)
    {
        var ts = hit.Target!;
        if (hit.Total <= 0 || !ts.Attributes.Carries("hit.cap"))
        {
            return HitFlow.Continue;
        }
        var cap = ts.Get("hit.cap", hit.Tags);
        if (cap <= 0)
        {
            return HitFlow.Continue;
        }
        var limit = cap * ts.ResourceMax("life");
        if (hit.Total > limit)
        {
            hit.ScaleReceived(limit / hit.Total);
            hit.Result.Clamped = true;
        }
        return HitFlow.Continue;
    }
}
