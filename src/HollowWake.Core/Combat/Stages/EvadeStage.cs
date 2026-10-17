namespace HollowWake.Core.Combat.Stages;

/// <summary>
/// Hits carrying an evadable tag (rule <c>combat.evadable_tags</c>, <c>attack</c> by default) are evaded by
/// rule <c>combat.hit_chance</c> over the attacker's accuracy and the target's evasion: rolled per hit, or with
/// rule <c>combat.evasion: entropy</c> accumulated on the target (seeded from its own stream) so a high evasion
/// slips the opening blows and then lets hits land on schedule. An evaded hit is published as evaded and ends.
/// </summary>
internal sealed class EvadeStage : IHitStage
{
    public string Name => "evade";

    public HitFlow Run(HitScope hit)
    {
        var source = hit.Source;
        var tags = hit.Tags;
        if (source is null || !tags.HasAny(hit.Plan.EvadableTags))
        {
            return HitFlow.Continue;
        }
        var ts = hit.Target!;
        var ctx = hit.Context;
        var accuracy = source.Attributes.Get("accuracy", source, tags, hit.Extra);
        var evasion = ts.Get("evasion");
        var scope = hit.Values.Clear().Set("accuracy", accuracy).Set("evasion", evasion).Set("level", source.Level);
        var chance = Math.Clamp(hit.Plan.HitChance.Evaluate(scope), 0, 1);
        if (hit.Plan.EntropyEvasion)
        {
            if (double.IsNaN(ts.Entropy))
            {
                ts.Entropy = ts.NextRandom();
            }
            ts.Entropy += 1 - chance;
            if (ts.Entropy < 1)
            {
                return HitFlow.Continue;
            }
            ts.Entropy -= 1;
        }
        else if (ctx.Rng.Chance(chance))
        {
            return HitFlow.Continue;
        }
        hit.Result.Evaded = true;
        ts.Stamp("evaded", ctx.Sim.Clock.Time);
        hit.Publish();
        return HitFlow.Stop;
    }
}
