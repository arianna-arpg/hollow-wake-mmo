using System.Text.Json.Nodes;
using HollowWake.Core.Combat;
using HollowWake.Core.Combat.Stages;
using HollowWake.Core.Definitions;
using HollowWake.Core.Sim;

namespace HollowWake.Core.Content.Kinds;

/// <summary>
/// Content for combat: status fields beyond the core, the hit pipeline's rules (stage order, stage skips,
/// victim tags), and later charges and procs. Adds validators to the existing kinds.
/// </summary>
internal static class CombatKinds
{
    public static void Register()
    {
        ContentKinds.AddValidator<StatusDef>(Status);
        ContentKinds.AddValidator<RulesDef>(Rules);
    }

    private static void Status(StatusDef d, ValidationContext c)
    {
        if (d.ConsumeOn is { } consume)
        {
            if (!TriggerKinds.TryGet(consume.Event, out _))
            {
                c.Error($"consume_on.event names unknown trigger kind '{consume.Event}'.");
            }
            if (consume.Count < 1)
            {
                c.Error("consume_on.count must be at least 1.");
            }
        }
        WeaveValidation.Effects(d.OnBearerHit, c, "on_bearer_hit");
    }

    /// <summary>The combat rule group: every staged name is a stage, every skipped name a stage or a declared step.</summary>
    private static void Rules(RulesDef d, ValidationContext c)
    {
        if (d.Id != "combat")
        {
            return;
        }
        foreach (var problem in HitPlan.For(c.Registry.Rules).Problems)
        {
            c.Error(problem);
        }
        if (d.Values?["stage_skips"] is JsonObject skips)
        {
            foreach (var (tag, list) in skips)
            {
                if (list is not JsonArray names)
                {
                    continue;
                }
                foreach (var item in names)
                {
                    var name = JsonNum.StringOf(item) ?? "";
                    if (!HitStages.IsSkippable(name))
                    {
                        c.Error($"stage_skips.{tag} names '{name}', which is neither a hit stage nor a declared step.");
                    }
                }
            }
        }
        if (d.Values?["victim_tags"] is JsonObject victimTags)
        {
            foreach (var (tag, _) in victimTags)
            {
                c.Expression(c.Registry.Rules.Expr("combat.victim_tags." + tag, "0"), "victim_tags." + tag);
            }
        }
    }
}
