using HollowWake.Core.Attributes;
using HollowWake.Core.Definitions;
using HollowWake.Core.Effects;
using HollowWake.Core.Expressions;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;

namespace HollowWake.Core.Content.Kinds;

/// <summary>
/// Content for casting: the casting fields of forms (cast modes and their blocks, strike timing, triggers,
/// use banks, gates, charge spends, toggles with reservation and upkeep, slot behaviors, target kinds, the
/// cooldown clock), the casting fields of glyphs and preset overrides. Registers the cast variables and the
/// <c>slot.</c> names as scope names, the trigger kinds the cast modes raise, and validators on the
/// existing kinds.
/// </summary>
internal static class CastingKinds
{
    public static void Register()
    {
        foreach (var name in CastVars.All)
        {
            ScopeNames.Declare(name);
        }
        ScopeNames.DeclarePrefix("slot.");
        CastingTriggers.Register();
        ContentKinds.AddValidator<FormDef>(Form);
        ContentKinds.AddValidator<GlyphDef>(Glyph);
        ContentKinds.AddValidator<WeaveDef>((d, c) =>
        {
            CooldownAt(d.CooldownAt, c);
            Hint(d.Ai, c);
            Trigger(d.Trigger, c, "trigger");
            Gates(d.Gates, c);
            Spend(d.Spend, c, "spend");
            Uses(d.Uses, c, "uses");
            Convert(d.Convert, c, "convert");
            Meta(d.MetaAction, c, "meta_action");
            FollowUp(d.FollowUp, c, "follow_up");
            Cycle(d.Cycle, c, "cycle");
            SelfStack(d.SelfStack, c, "self_stack");
            Chain(d.Chain, c, "chain");
            c.Modifiers(d.EquipModifiers, "equip_modifiers");
        });
    }

    /// <summary>An AI hint's soft minimum and its range attribute (row A23).</summary>
    private static void Hint(AiHint? hint, ValidationContext c)
    {
        if (hint is null)
        {
            return;
        }
        if (hint.MinRange is < 0)
        {
            c.Error("ai.min_range must be at least 0.");
        }
        if (hint.NearWeight is < 0)
        {
            c.Error("ai.near_weight must be at least 0.");
        }
        if (hint.NearWeight is not null && hint.MinRange is null)
        {
            c.Warn("ai.near_weight has no min_range to apply below.");
        }
        if (hint.RangeAttribute is { } attribute)
        {
            Declared(attribute, c, "ai.range_attribute");
        }
    }

    private static void Form(FormDef d, ValidationContext c)
    {
        if (d.ReleaseCheck is not (null or "full" or "range" or "none"))
            c.Error("release_check must be full, range or none.");
        c.Expression(d.CastMove, "cast_move");
        Hint(d.Ai, c);
        CooldownAt(d.CooldownAt, c);
        if (d.Channel is { } channel)
        {
            if (d.CastMode != CastMode.Channel)
            {
                c.Warn("has a channel block but its cast_mode is not channel.");
            }
            c.Expression(channel.Interval, "channel.interval");
            c.Expression(channel.Windup, "channel.windup");
            c.Expression(channel.Jitter, "channel.jitter");
            c.Expression(channel.Move, "channel.move");
            c.Expression(channel.TurnRate, "channel.turn_rate");
            c.Expression(channel.MaxHold, "channel.max_hold");
            foreach (var ramp in channel.Ramps)
            {
                if (!Curves.Has(ramp.Curve))
                {
                    c.Error($"channel.ramps '{ramp.Attribute}' names unknown curve '{ramp.Curve}'; known curves are {string.Join(", ", Curves.Names)}.");
                }
                if (ramp.On is not ("weave" or "caster"))
                {
                    c.Error($"channel.ramps '{ramp.Attribute}' must be on weave or caster, not '{ramp.On}'.");
                }
                Declared(ramp.Attribute, c, "channel.ramps");
            }
            if (channel.Release is { } release)
            {
                WeaveValidation.Effects(release.Effects, c, "channel.release.effects");
                c.Expression(release.MinHold, "channel.release.min_hold");
            }
            if (channel.Persist is { } persist)
            {
                c.Expression(persist.PerHeldSecond, "channel.persist.per_held_second");
                c.Expression(persist.MaxDuration, "channel.persist.max_duration");
                c.Expression(persist.Fade, "channel.persist.fade");
            }
            if (channel.Brim is { } brim)
            {
                c.Expression(brim.FillTime, "channel.brim.fill_time");
                if (!Curves.Has(brim.Scale.Curve))
                {
                    c.Error($"channel.brim.scale names unknown curve '{brim.Scale.Curve}'.");
                }
                if (brim.BankAt is < 0 or > 1 || brim.MinRelease is < 0 or > 1)
                {
                    c.Error("channel.brim bank_at and min_release are fractions from 0 to 1.");
                }
            }
        }
        if (d.Charge is { } charge)
        {
            if (d.CastMode != CastMode.Charge)
            {
                c.Warn("has a charge block but its cast_mode is not charge.");
            }
            c.Expression(charge.MaxTime, "charge.max_time");
            c.Expression(charge.FullHold, "charge.full_hold");
            foreach (var scale in charge.Scales)
            {
                Declared(scale.Attribute, c, "charge.scales");
            }
        }
        else if (d.CastMode == CastMode.Charge)
        {
            c.Warn("has cast_mode charge but no charge block.");
        }
        if (d.Overcharge is { } over)
        {
            if (d.CastMode != CastMode.Overcharge)
            {
                c.Warn("has an overcharge block but its cast_mode is not overcharge.");
            }
            if (over.Stages < 1)
            {
                c.Error("overcharge.stages must be at least 1.");
            }
            c.Expression(over.Time, "overcharge.time");
            c.Expression(over.FullHold, "overcharge.full_hold");
            c.Modifiers(over.PerStage, "overcharge.per_stage");
            Costs(over.CostPerStage, c, "overcharge.cost_per_stage");
        }
        else if (d.CastMode == CastMode.Overcharge)
        {
            c.Warn("has cast_mode overcharge but no overcharge block.");
        }
        if (d.Guard is { } guard)
        {
            if (d.CastMode != CastMode.Guard)
            {
                c.Warn("has a guard block but its cast_mode is not guard.");
            }
            if (guard.Arc is <= 0 or > 360)
            {
                c.Error("guard.arc is in degrees, above 0 and at most 360.");
            }
            if (guard.Pool is null)
            {
                c.Warn("guard has no pool, so it absorbs nothing.");
            }
            c.Expression(guard.Pool, "guard.pool");
            c.Expression(guard.Move, "guard.move");
            c.Expression(guard.TurnRate, "guard.turn_rate");
            c.Expression(guard.Parry, "guard.parry");
            c.Expression(guard.MaxDuration, "guard.max_duration");
            if (guard.Bash is { } bash)
            {
                WeaveValidation.Effects(bash.Effects, c, "guard.bash.effects");
                c.Expression(bash.ArmTime, "guard.bash.arm_time");
                if (bash.Threshold is < 0 or > 1)
                {
                    c.Error("guard.bash.threshold is a fraction of the pool from 0 to 1.");
                }
            }
            if (guard.Pulse is { } pulse)
            {
                c.Ref<WeaveDef>(pulse.Weave, "guard.pulse.weave");
                c.Expression(pulse.Interval, "guard.pulse.interval");
            }
        }
        else if (d.CastMode == CastMode.Guard)
        {
            c.Warn("has cast_mode guard but no guard block.");
        }
        if (d.Timing is { } timing)
        {
            if (d.CastMode != CastMode.Cast)
            {
                c.Warn("has strike timing but only a cast bar reads a second press.");
            }
            if (timing.Window <= 0)
            {
                c.Error("timing.window must be above 0.");
            }
        }
        if (d.Trigger is not null && (d.CastMode is CastMode.Channel or CastMode.Charge or CastMode.Overcharge or CastMode.Guard || d.Toggle is not null))
        {
            c.Warn("has a trigger but a held mode or a toggle cannot be triggered.");
        }
        Trigger(d.Trigger, c, "trigger");
        Uses(d.Uses, c, "uses");
        Gates(d.Gates, c);
        Spend(d.Spend, c, "spend");
        c.Expression(d.Recovery, "recovery");
        if (d.Reflex && d.CastMode != CastMode.Instant)
        {
            c.Warn("is a reflex but only instant weaves resolve past a running cast.");
        }
        if (d.Toggle is { } toggle)
        {
            WeaveValidation.Effects(toggle.OnDeactivate, c, "toggle.on_deactivate");
            c.Expression(toggle.MaxDuration, "toggle.max_duration");
        }
        if ((d.Reserve.Count > 0 || d.Upkeep is not null) && d.Toggle is null)
        {
            c.Warn("reserves or pays upkeep but is not a toggle, so nothing holds the reservation.");
        }
        Costs(d.Reserve, c, "reserve");
        if (d.Upkeep is { } upkeep)
        {
            Costs(upkeep.PerSecond, c, "upkeep.per_second");
        }
        Convert(d.Convert, c, "convert");
        Meta(d.MetaAction, c, "meta_action");
        FollowUp(d.FollowUp, c, "follow_up");
        Cycle(d.Cycle, c, "cycle");
        SelfStack(d.SelfStack, c, "self_stack");
        Chain(d.Chain, c, "chain");
        c.Modifiers(d.EquipModifiers, "equip_modifiers");
        if (d.Target is { } target)
        {
            if (!TargetKinds.TryGet(target.Kind, out _))
            {
                c.Error($"target.kind names unknown target kind '{target.Kind}'; known kinds are {string.Join(", ", TargetKinds.All.Select(k => k.Name))}.");
            }
            if (target.Fallback is not ("fail" or "self" or "aim"))
            {
                c.Error($"target.fallback must be fail, self or aim, not '{target.Fallback}'.");
            }
            c.Expression(target.Require, "target.require");
            c.Expression(target.SearchRadius, "target.search_radius");
        }
        if (d.Tree is { } tree)
        {
            Tree(tree, c);
        }
    }

    /// <summary>
    /// A form's tree (row D23): unique node ids, links and excludes naming nodes of the same tree, ranks of at
    /// least one, a root to start from, and every node reachable from a root through its links (an unlinked
    /// node could never be bought). Node modifiers, patches and glyphs are checked as a glyph's are.
    /// </summary>
    private static void Tree(TreeSpec tree, ValidationContext c)
    {
        if (tree.OpensAt < 1)
        {
            c.Error("tree.opens_at must be at least 1.");
        }
        if (tree.Nodes.Count == 0)
        {
            c.Warn("tree has no nodes.");
            return;
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in tree.Nodes)
        {
            if (!ids.Add(node.Id))
            {
                c.Error($"tree node '{node.Id}' is declared twice.");
            }
        }
        foreach (var node in tree.Nodes)
        {
            var field = $"tree.{node.Id}";
            if (node.Ranks < 1)
            {
                c.Error($"{field}.ranks must be at least 1.");
            }
            foreach (var link in node.Links)
            {
                if (link == node.Id)
                {
                    c.Error($"{field} links to itself.");
                }
                else if (!ids.Contains(link))
                {
                    c.Error($"{field} links to unknown node '{link}'.");
                }
            }
            foreach (var other in node.Excludes)
            {
                if (other == node.Id)
                {
                    c.Error($"{field} excludes itself.");
                }
                else if (!ids.Contains(other))
                {
                    c.Error($"{field} excludes unknown node '{other}'.");
                }
            }
            c.Modifiers(node.Modifiers, field + ".modifiers");
            for (var i = 0; i < node.Patches.Count; i++)
            {
                WeaveValidation.Patch(node.Patches[i], c, $"{field}.patches[{i}]");
            }
            if (node.Glyph is { } glyph)
            {
                WeaveValidation.GlyphRefs(new[] { glyph }, c, field + ".glyph");
            }
            if (node.Modifiers.Count == 0 && node.Patches.Count == 0 && node.Glyph is null)
            {
                c.Warn($"{field} grants nothing.");
            }
        }
        var reached = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in tree.Nodes)
        {
            if (node.Links.Count == 0)
            {
                reached.Add(node.Id);
            }
        }
        if (reached.Count == 0)
        {
            c.Error("tree has no root: every node links to another, so none can be bought first.");
            return;
        }
        bool grew;
        do
        {
            grew = false;
            foreach (var node in tree.Nodes)
            {
                if (!reached.Contains(node.Id) && node.Links.Exists(reached.Contains))
                {
                    reached.Add(node.Id);
                    grew = true;
                }
            }
        }
        while (grew);
        foreach (var node in tree.Nodes)
        {
            if (!reached.Contains(node.Id))
            {
                c.Error($"tree.{node.Id} is unlinked: none of its links leads back to a root.");
            }
        }
    }

    private static void Glyph(GlyphDef d, ValidationContext c)
    {
        Trigger(d.Trigger, c, "trigger");
        Gates(d.Gates, c);
        Spend(d.Spend, c, "spend");
        Uses(d.Uses, c, "uses");
        Convert(d.Convert, c, "convert");
        Meta(d.MetaAction, c, "meta_action");
        FollowUp(d.FollowUp, c, "follow_up");
        Cycle(d.Cycle, c, "cycle");
        SelfStack(d.SelfStack, c, "self_stack");
        Chain(d.Chain, c, "chain");
        c.Modifiers(d.EquipModifiers, "equip_modifiers");
    }

    /// <summary>A trigger names a registered trigger kind and sane numbers.</summary>
    internal static void Trigger(TriggerSpec? t, ValidationContext c, string field)
    {
        if (t is null)
        {
            return;
        }
        if (!TriggerKinds.TryGet(t.On, out _))
        {
            c.Error($"'{field}.on' names unknown trigger kind '{t.On}'; known kinds are {string.Join(", ", TriggerKinds.All.Select(k => k.Name))}.");
        }
        c.Expression(t.Chance, field + ".chance");
        c.Expression(t.Power, field + ".power");
        if (t.Icd < 0)
        {
            c.Error($"'{field}.icd' cannot be negative.");
        }
        if (t.LifeFraction is { } fraction && (fraction <= 0 || fraction > 1))
        {
            c.Error($"'{field}.life_fraction' must be above 0 and at most 1.");
        }
        if (t.Status is not null)
        {
            c.Ref<StatusDef>(t.Status, field + ".status");
        }
        if (t.MaxCastTime is < 0)
        {
            c.Error($"'{field}.max_cast_time' cannot be negative.");
        }
    }

    private static void Uses(UsesSpec? u, ValidationContext c, string field)
    {
        if (u is null)
        {
            return;
        }
        if (u.Max < 1)
        {
            c.Error($"'{field}.max' must be at least 1.");
        }
        c.Expression(u.Recharge, field + ".recharge");
        c.Expression(u.Magazine?.Refill, field + ".magazine.refill");
        c.Modifiers(u.Empower, field + ".empower");
        if (u.Still is { Bleed: < 0 })
        {
            c.Error($"'{field}.still.bleed' cannot be negative.");
        }
        if (u.Magazine is not null && u.Still is not null)
        {
            c.Warn($"'{field}' has both a magazine and still recovery; the magazine decides.");
        }
    }

    private static void Gates(IEnumerable<Expr> gates, ValidationContext c)
    {
        foreach (var gate in gates)
        {
            c.Expression(gate, "gates");
        }
    }

    private static void Spend(SpendSpec? s, ValidationContext c, string field)
    {
        if (s is null)
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(s.Charge))
        {
            c.Error($"'{field}.charge' names no charge.");
        }
        if (s.Amount <= 0 && !s.All)
        {
            c.Error($"'{field}.amount' must be above 0, or set all.");
        }
        foreach (var scale in s.Scales)
        {
            Declared(scale.Attribute, c, field + ".scales");
        }
    }

    private static void Costs(Dictionary<string, Expr> costs, ValidationContext c, string field)
    {
        foreach (var (resource, expr) in costs)
        {
            c.Ref<ResourceDef>(resource, field);
            c.Expression(expr, $"{field}.{resource}");
        }
    }

    private static void Convert(ConvertSpec? convert, ValidationContext c, string field)
    {
        if (convert is null)
        {
            return;
        }
        c.Ref<WeaveDef>(convert.Weave, field + ".weave");
        c.Expression(convert.When, field + ".when");
    }

    private static void Meta(MetaSpec? meta, ValidationContext c, string field)
    {
        if (meta is not null)
        {
            c.Ref<WeaveDef>(meta.Weave, field + ".weave");
        }
    }

    private static void FollowUp(FollowUpSpec? follow, ValidationContext c, string field)
    {
        if (follow is null)
        {
            return;
        }
        c.Ref<WeaveDef>(follow.Weave, field + ".weave");
        c.Expression(follow.Delay, field + ".delay");
        c.Expression(follow.Chance, field + ".chance");
    }

    private static void Cycle(CycleSpec? cycle, ValidationContext c, string field)
    {
        if (cycle is null)
        {
            return;
        }
        if (cycle.Count < 1)
        {
            c.Error($"'{field}.count' must be at least 1.");
        }
        WeaveValidation.Effects(cycle.Effects, c, field + ".effects");
    }

    private static void SelfStack(SelfStackSpec? stack, ValidationContext c, string field)
    {
        if (stack is null)
        {
            return;
        }
        if (stack.Max < 1)
        {
            c.Error($"'{field}.max' must be at least 1.");
        }
        c.Modifiers(stack.Modifiers, field + ".modifiers");
    }

    private static void Chain(ComboSpec? chain, ValidationContext c, string field)
    {
        if (chain is null)
        {
            return;
        }
        if (chain.Steps.Count == 0)
        {
            c.Warn($"'{field}' has no steps.");
        }
        c.Refs<WeaveDef>(chain.Steps, field + ".steps");
        if (chain.Window <= 0)
        {
            c.Error($"'{field}.window' must be above 0.");
        }
    }

    private static void Declared(string attribute, ValidationContext c, string field)
    {
        if (!c.Registry.Attributes.Contains(attribute))
        {
            c.Warn($"{field} names undeclared attribute '{attribute}'.");
        }
    }

    private static void CooldownAt(string? at, ValidationContext c)
    {
        if (at is not null and not ("press" or "complete"))
        {
            c.Error($"cooldown_at must be press or complete, not '{at}'.");
        }
    }
}
