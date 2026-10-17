using HollowWake.Core.Expressions;

namespace HollowWake.Core.Combat.Stages;

/// <summary>
/// Hits carrying an evadable tag (rule <c>combat.evadable_tags</c>, <c>attack</c> by default) roll rule
/// <c>combat.hit_chance</c> over the attacker's accuracy and the target's evasion. An evaded hit is
/// published as evaded and ends.
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
        var scope = new DictScope(ctx.Rng).Set("accuracy", accuracy).Set("evasion", evasion).Set("level", source.Level);
        var chance = Math.Clamp(ctx.Sim.Content.Rules.Eval("combat.hit_chance", "clamp(accuracy / max(1, accuracy + evasion * 0.25), 0.05, 1)", scope), 0, 1);
        if (ctx.Rng.Chance(chance))
        {
            return HitFlow.Continue;
        }
        hit.Result.Evaded = true;
        ts.Stamp("evaded", ctx.Sim.Clock.Time);
        hit.Publish();
        return HitFlow.Stop;
    }
}
