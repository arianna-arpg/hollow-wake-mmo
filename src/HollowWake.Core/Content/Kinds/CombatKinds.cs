using System.Text.Json.Nodes;
using HollowWake.Core.Combat;
using HollowWake.Core.Combat.Stages;
using HollowWake.Core.Definitions;
using HollowWake.Core.Sim;

namespace HollowWake.Core.Content.Kinds;

/// <summary>
/// Content for combat: status fields beyond the core, charge kinds, procs, and the hit pipeline's rules (stage
/// order and skips, victim tags, pools and their rules) and the proc rules. Registers the trigger kinds the
/// combat package raises and the names its effects set, then adds validators to the existing kinds.
/// </summary>
internal static class CombatKinds
{
    public static void Register()
    {
        CombatTriggers.EnsureRegistered();
        foreach (var name in new[] { "remaining_damage", "pop", "banked", "elapsed", "total", "consumed", "consumed_damage", "spread", "moved", "ruptured", "last_damage", "crit", "angle", "pen", "roll" })
        {
            ScopeNames.Declare(name);
        }
        ContentKinds.Register<ChargeDef>("charges", Charge);
        ContentKinds.Register<ProcDef>("procs", Proc);
        ContentKinds.AddValidator<StatusDef>(Status);
        ContentKinds.AddValidator<RulesDef>(Rules);
        ContentKinds.AddValidator<AttributeDef>(PerPoint);
    }

    /// <summary>
    /// Per-point modifiers scale by the attribute that grants them: plain flat, increased or more lines on other
    /// declared attributes, never on one that grants per point itself (that would read in a circle).
    /// </summary>
    private static void PerPoint(AttributeDef d, ValidationContext c)
    {
        c.Modifiers(d.PerPoint, "per_point");
        foreach (var m in d.PerPoint)
        {
            if (m.Attribute == d.Id)
            {
                c.Error("per_point modifies the attribute itself.");
            }
            if (m.Gauge is not null || m.GaugeAt is not null)
            {
                c.Error($"per_point line on '{m.Attribute}' has a gauge; the points are its gauge.");
            }
            if (m.Form is Attributes.ModifierForm.Link or Attributes.ModifierForm.Override)
            {
                c.Error($"per_point line on '{m.Attribute}' must be flat, increased or more.");
            }
            if (c.Registry.Attributes.Get(m.Attribute) is { PerPoint.Count: > 0 })
            {
                c.Error($"per_point targets '{m.Attribute}', which grants per point itself.");
            }
        }
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
        WeaveValidation.Effects(d.OnBearerStruck, c, "on_bearer_struck");
        WeaveValidation.Effects(d.OnReapply, c, "on_reapply");
        WeaveValidation.Effects(d.OnBankLethal, c, "on_bank_lethal");
        if (d.Bank is { } bank)
        {
            c.Expression(bank.Cap, "bank.cap");
            c.Expression(bank.Fraction, "bank.fraction");
            var spends = d.Tick is not null || d.OnExpire.Count > 0 || d.OnBankLethal.Count > 0 || d.OnBearerStruck.Count > 0 || d.OnBearerHit.Count > 0;
            if (bank.From == BankSource.Applications && !spends)
            {
                c.Warn("bank fed by applications has no tick or hook to spend it; only ruptures from elsewhere will.");
            }
        }
        else if (d.OnBankLethal.Count > 0)
        {
            c.Error("on_bank_lethal needs a bank.");
        }
        c.Expression(d.Baseline, "baseline");
        c.Expression(d.Pool, "pool");
        if (d.BoundTo is not null && c.Ref<StatusDef>(d.BoundTo, "bound_to") && c.Registry.Get<StatusDef>(d.BoundTo) is { Pool: null })
        {
            c.Warn($"bound_to names '{d.BoundTo}', which carries no pool to end with.");
        }
        if (d.StackClock == StackClock.Independent && d.StackPolicy != StackPolicy.Stack)
        {
            c.Warn("stack_clock: independent keeps a timer per stack; it is meant for stack_policy: stack.");
        }
    }

    private static void Charge(ChargeDef d, ValidationContext c)
    {
        c.Expression(d.Duration, "duration");
        if (d.Decay < 0)
        {
            c.Error("decay must not be negative.");
        }
        if (d.Decay > 0 && d.Duration is null)
        {
            c.Warn("decay has no duration to run out; the charges never decay.");
        }
        if (!c.Registry.Attributes.Contains(ChargeNames.MaxOf(d.Id)))
        {
            c.Warn($"'{ChargeNames.MaxOf(d.Id)}' is not declared, so the charge has no maximum.");
        }
    }

    private static void Proc(ProcDef d, ValidationContext c)
    {
        if (!TriggerKinds.TryGet(d.On, out _))
        {
            c.Error($"on names unknown trigger kind '{d.On}'.");
        }
        var chance = d.Chance ?? "proc." + d.Id;
        if (!c.Registry.Attributes.Contains(chance))
        {
            c.Warn($"chance attribute '{chance}' is not declared.");
        }
        if (d.Icd < 0 || d.Ppm < 0)
        {
            c.Error("icd and ppm must not be negative.");
        }
        if (d.Falloff is { } falloff && (falloff < 0 || falloff > 1))
        {
            c.Error("falloff must be between 0 and 1.");
        }
        if (d.Target is not ("other" or "owner"))
        {
            c.Error($"target must be other or owner, not '{d.Target}'.");
        }
        if (d.Effects.Count == 0)
        {
            c.Warn("proc has no effects.");
        }
        WeaveValidation.Effects(d.Effects, c, "effects");
        c.Expression(d.When, "when");
        c.Expression(d.Victim, "victim");
    }

    private static void Rules(RulesDef d, ValidationContext c)
    {
        if (d.Id == "procs")
        {
            ProcRules(d, c);
            return;
        }
        if (d.Id == "attributes")
        {
            TradeRules(d, c);
            return;
        }
        if (d.Id != "combat")
        {
            return;
        }
        var rules = c.Registry.Rules;
        var plan = HitPlan.For(rules);
        foreach (var problem in plan.Problems)
        {
            c.Error(problem);
        }
        var pools = rules.Strings("combat.pools");
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
                    if (!HitStages.IsSkippable(name) && !pools.Contains(name))
                    {
                        c.Error($"stage_skips.{tag} names '{name}', which is neither a hit stage, a declared step nor a pool.");
                    }
                }
            }
        }
        if (d.Values?["victim_tags"] is JsonObject victimTags)
        {
            foreach (var (tag, _) in victimTags)
            {
                c.Expression(rules.Expr("combat.victim_tags." + tag, "0"), "victim_tags." + tag);
            }
        }
        foreach (var pool in pools)
        {
            if (pool != "life" && !c.Registry.Has<ResourceDef>(pool))
            {
                c.Error($"pools names '{pool}', which is not a resource.");
            }
        }
        if (d.Values?["pool_rules"] is JsonObject poolRules)
        {
            foreach (var (pool, _) in poolRules)
            {
                if (!pools.Contains(pool))
                {
                    c.Error($"pool_rules.{pool} rules a pool combat.pools does not list.");
                }
            }
        }
        foreach (var rule in plan.PoolRules)
        {
            foreach (var (field, status) in new[] { ("on_hurt", rule.OnHurt), ("on_break", rule.OnBreak), ("rearm", rule.Rearm) })
            {
                if (status is not null)
                {
                    c.Ref<StatusDef>(status, $"pool_rules.{rule.Resource}.{field}");
                }
            }
            if (rule.Break)
            {
                foreach (var attribute in new[] { rule.Dealt, rule.Taken })
                {
                    if (attribute is not null && !c.Registry.Attributes.Contains(attribute))
                    {
                        c.Error($"pool_rules.{rule.Resource} reads undeclared attribute '{attribute}', which would read zero and never break.");
                    }
                }
            }
        }
        if (plan.StageNames.Contains("stagger"))
        {
            c.Ref<StatusDef>(plan.StaggerStatus, "stagger_status");
        }
        if (plan.StageNames.Contains("cheat_death"))
        {
            c.Ref<StatusDef>(plan.CheatDeathStatus, "cheat_death_status");
        }
        if (rules.Text("combat.evasion", "roll") is not ("roll" or "entropy") and var evasion)
        {
            c.Error($"evasion must be roll or entropy, not '{evasion}'.");
        }
        if (rules.Text("combat.block", "fraction") is not ("fraction" or "guard") and var block)
        {
            c.Error($"block must be fraction or guard, not '{block}'.");
        }
    }

    /// <summary>Trades name declared attributes; their dials are never traded themselves, and nothing trades with itself.</summary>
    private static void TradeRules(RulesDef d, ValidationContext c)
    {
        if (d.Values?["trades"] is not JsonArray trades)
        {
            return;
        }
        var traded = new HashSet<string>(StringComparer.Ordinal);
        var dials = new List<string>();
        foreach (var item in trades)
        {
            if (item is not JsonObject trade)
            {
                c.Error("trades lists something that is not { from, to, rate, forgo }.");
                continue;
            }
            var from = JsonNum.StringOf(trade["from"]);
            var to = JsonNum.StringOf(trade["to"]);
            if (from is null || to is null || from == to)
            {
                c.Error("a trade needs a from and a different to.");
                continue;
            }
            traded.Add(from);
            traded.Add(to);
            foreach (var name in new[] { from, to })
            {
                if (!c.Registry.Attributes.Contains(name))
                {
                    c.Error($"trade names undeclared attribute '{name}'.");
                }
            }
            foreach (var key in new[] { "rate", "forgo" })
            {
                if (JsonNum.StringOf(trade[key]) is { } dial)
                {
                    dials.Add(dial);
                    if (!c.Registry.Attributes.Contains(dial))
                    {
                        c.Error($"trade dial '{dial}' is not a declared attribute.");
                    }
                }
            }
        }
        foreach (var dial in dials)
        {
            if (traded.Contains(dial))
            {
                c.Error($"trade dial '{dial}' is itself traded.");
            }
        }
    }

    private static void ProcRules(RulesDef d, ValidationContext c)
    {
        var rules = c.Registry.Rules;
        if (rules.Number("procs.max_depth", 3) < 0)
        {
            c.Error("max_depth must not be negative.");
        }
        foreach (var key in new[] { "depth_falloff", "chance_cap" })
        {
            var v = rules.Number("procs." + key, 0.5);
            if (v < 0 || v > 1)
            {
                c.Error($"{key} must be between 0 and 1.");
            }
        }
        _ = d;
    }
}
