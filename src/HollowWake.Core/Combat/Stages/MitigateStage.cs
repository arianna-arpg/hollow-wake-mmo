using HollowWake.Core.Definitions;
using HollowWake.Core.Expressions;

namespace HollowWake.Core.Combat.Stages;

/// <summary>
/// Per Thread, by the Thread's mitigation rule: armor reduction by rule <c>combat.armor_reduction</c>, or
/// resistance capped by the Thread's own maximum and rule <c>combat.resist_cap</c>, less the attacker's
/// penetration down to rule <c>combat.resist_floor</c>.
/// </summary>
internal sealed class MitigateStage : IHitStage
{
    public string Name => "mitigate";

    public HitFlow Run(HitScope hit)
    {
        var ts = hit.Target!;
        var source = hit.Source;
        var ctx = hit.Context;
        var sim = ctx.Sim;
        var rules = sim.Content.Rules;
        var tags = hit.Tags;
        var extra = hit.Extra;
        var resistCap = hit.Plan.ResistCap;
        var resistFloor = hit.Plan.ResistFloor;
        // The armor step can be skipped by hit tag (damage over time ignores armor) while resistances still apply.
        var skipArmor = hit.Plan.Skips(tags, "armor");
        for (var i = 0; i < hit.ThreadCount; i++)
        {
            if (!hit.Live[i])
            {
                continue;
            }
            var thread = hit.ThreadIds[i];
            var amount = hit.Amounts[i];
            var def = sim.Content.Get<ThreadDef>(thread);
            var mitigation = def?.Mitigation ?? "resist";
            if (mitigation == "armor")
            {
                if (skipArmor)
                {
                    continue;
                }
                var armor = ts.Get("armor");
                var scope = new DictScope(ctx.Rng).Set("armor", armor).Set("raw", amount).Set("level", ts.Level);
                var reduction = Math.Clamp(rules.Eval("combat.armor_reduction", "armor / (armor + 10 * raw)", scope), 0, 0.9);
                amount *= 1 - reduction;
            }
            else if (mitigation == "resist")
            {
                var resistAttr = def?.Resist ?? $"resist.{thread}";
                var capAttr = resistAttr + ".max";
                var cap = Math.Min(resistCap, ts.Attributes.Catalog.Contains(capAttr) || ts.Attributes.HasModifiers(capAttr) ? ts.Get(capAttr) : resistCap);
                var res = Math.Min(ts.Get(resistAttr), cap);
                if (source is not null)
                {
                    var penAttr = def?.Penetration ?? $"pen.{thread}";
                    var pen = source.Attributes.Get(penAttr, source, tags, extra);
                    if (pen > 0)
                    {
                        res = Math.Max(resistFloor, res - pen);
                    }
                }
                amount *= 1 - res;
            }
            hit.Amounts[i] = amount;
        }
        return HitFlow.Continue;
    }
}
