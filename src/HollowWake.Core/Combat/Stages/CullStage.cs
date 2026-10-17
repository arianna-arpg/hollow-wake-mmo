namespace HollowWake.Core.Combat.Stages;

/// <summary>A target left at or below the attacker's <c>cull.threshold</c> of its life is finished.</summary>
internal sealed class CullStage : IHitStage
{
    public string Name => "cull";

    public HitFlow Run(HitScope hit)
    {
        var ts = hit.Target!;
        if (hit.Source is not { } source || ts.Resource("life") <= 0)
        {
            return HitFlow.Continue;
        }
        var cull = Math.Clamp(source.Attributes.Get("cull.threshold", source, hit.Tags, hit.Extra), 0, 1);
        if (cull > 0 && ts.ResourceFraction("life") <= cull)
        {
            ts.SetResource("life", 0);
        }
        return HitFlow.Continue;
    }
}
