namespace HollowWake.Core.Combat.Stages;

/// <summary>
/// Defender-side conversion before mitigation: the target's <c>taken_as.{from}.{to}</c> takes a fraction of each
/// Thread it receives as another Thread (capped at the whole of it), which is then mitigated as that Thread.
/// Bodies with no such modifiers pass untouched.
/// </summary>
internal sealed class TakenAsStage : IHitStage
{
    public string Name => "taken_as";

    public HitFlow Run(HitScope hit)
    {
        var ts = hit.Target!;
        var names = CombatNames.For(hit.Sim.Content);
        var threads = names.Threads;
        var tags = hit.Tags;
        var count = hit.ThreadCount;
        for (var i = 0; i < count; i++)
        {
            if (!hit.Live[i])
            {
                continue;
            }
            var from = hit.ThreadIds[i];
            var amount = hit.Amounts[i];
            var converted = 0.0;
            for (var j = 0; j < threads.Length; j++)
            {
                if (threads[j] == from)
                {
                    continue;
                }
                var attr = names.TakenAs(from, j);
                if (!ts.Attributes.Carries(attr))
                {
                    continue;
                }
                var fraction = Math.Clamp(ts.Attributes.Get(attr, ts, tags), 0, 1 - converted);
                if (fraction <= 0)
                {
                    continue;
                }
                converted += fraction;
                hit.AddToThread(threads[j], amount * fraction);
            }
            if (converted > 0)
            {
                hit.Amounts[i] = amount * (1 - converted);
                hit.Live[i] = hit.Amounts[i] > 0;
            }
        }
        return HitFlow.Continue;
    }
}
