namespace HollowWake.Core.Combat.Stages;

/// <summary>
/// The target's <c>damage.taken</c> fold scoped by the hit's tags plus each Thread, folded through the hit
/// scope; what remains above zero is the damage received. The fold is a step hits can skip by tag
/// (<c>damage_taken</c>, for damage already folded once, such as a stagger paying out); skipping the stage
/// itself would land nothing.
/// </summary>
internal sealed class TakenStage : IHitStage
{
    public string Name => "taken";

    public HitFlow Run(HitScope hit)
    {
        var ts = hit.Target!;
        var tags = hit.Tags;
        var received = hit.Result.Received;
        var fold = !hit.Plan.Skips(tags, "damage_taken");
        for (var i = 0; i < hit.ThreadCount; i++)
        {
            if (!hit.Live[i])
            {
                continue;
            }
            var thread = hit.ThreadIds[i];
            var amount = fold ? ts.Attributes.Apply("damage.taken", hit.Amounts[i], hit.VictimScope, tags.With(thread)) : hit.Amounts[i];
            hit.Amounts[i] = amount;
            if (amount > 0)
            {
                received[thread] = received.TryGetValue(thread, out var already) ? already + amount : amount;
                hit.Total += amount;
            }
        }
        hit.Result.Total = hit.Total;
        return HitFlow.Continue;
    }
}
