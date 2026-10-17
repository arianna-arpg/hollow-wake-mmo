namespace HollowWake.Core.Combat.Stages;

/// <summary>
/// The target's <c>damage.taken</c> fold scoped by the hit's tags plus each Thread, folded through the hit
/// scope; what remains above zero is the damage received.
/// </summary>
internal sealed class TakenStage : IHitStage
{
    public string Name => "taken";

    public HitFlow Run(HitScope hit)
    {
        var ts = hit.Target!;
        var tags = hit.Tags;
        var received = hit.Result.Received;
        for (var i = 0; i < hit.ThreadCount; i++)
        {
            if (!hit.Live[i])
            {
                continue;
            }
            var thread = hit.ThreadIds[i];
            var amount = ts.Attributes.Apply("damage.taken", hit.Amounts[i], hit.VictimScope, tags.With(thread));
            hit.Amounts[i] = amount;
            if (amount > 0)
            {
                received[thread] = amount;
                hit.Total += amount;
            }
        }
        hit.Result.Total = hit.Total;
        return HitFlow.Continue;
    }
}
