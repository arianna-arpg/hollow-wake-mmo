using HollowWake.Core.Definitions;

namespace HollowWake.Core.Combat.Stages;

/// <summary>
/// Per Thread, by the Thread's mitigation rule: armor reduction by rule <c>combat.armor_reduction</c> (over the
/// target's <c>armor</c>, the hit's <c>raw</c> amount, the target's <c>level</c> and the attacker's armor
/// penetration <c>pen</c>; the clamp lives in the rule), or resistance capped by the Thread's own maximum and
/// rule <c>combat.resist_cap</c>, less the attacker's penetration down to rule <c>combat.resist_floor</c>. The
/// armor step can be skipped by hit tag (rule <c>combat.stage_skips</c>) while resistances still apply.
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
        var tags = hit.Tags;
        var extra = hit.Extra;
        var names = hit.Names;
        var resistCap = hit.Plan.ResistCap;
        var resistFloor = hit.Plan.ResistFloor;
        var skipArmor = hit.Skips("armor");
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
            var index = names.ThreadIndex(thread);
            if (mitigation == "armor")
            {
                if (skipArmor)
                {
                    continue;
                }
                var armor = ts.Get("armor");
                var pen = source is not null && (source.Attributes.Carries("pen.armor") || DamagePipeline.Mentions(extra, "pen.armor"))
                    ? Math.Clamp(source.Attributes.Get("pen.armor", source, tags, extra), 0, 1)
                    : 0;
                var scope = hit.Values.Clear().Set("armor", armor).Set("raw", amount).Set("level", ts.Level).Set("pen", pen);
                var reduction = Math.Clamp(hit.Plan.ArmorReduction.Evaluate(scope), 0, 1);
                amount *= 1 - reduction;
            }
            else if (mitigation == "resist")
            {
                var resistAttr = index >= 0 ? names.Resist[index] : def?.Resist ?? $"resist.{thread}";
                var capAttr = index >= 0 ? names.ResistMax[index] : resistAttr + ".max";
                var cap = Math.Min(resistCap, ts.Attributes.Catalog.Contains(capAttr) || ts.Attributes.HasModifiers(capAttr) ? ts.Get(capAttr) : resistCap);
                var res = Math.Min(ts.Get(resistAttr), cap);
                if (source is not null)
                {
                    var penAttr = index >= 0 ? names.Pen[index] : def?.Penetration ?? $"pen.{thread}";
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
