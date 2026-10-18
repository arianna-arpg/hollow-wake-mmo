using HollowWake.Core.Attributes;
using HollowWake.Core.Definitions;
using HollowWake.Core.Expressions;
using static HollowWake.Core.Weaves.AxisUnits;
using static HollowWake.Core.Weaves.Dimension;

namespace HollowWake.Core.Weaves.Axes;

/// <summary>
/// The casting family: the cast layer the compiled weave states. Its timing (the cast mode, the scope, the
/// bar, mobility, recovery, reflexes) and the held-mode blocks through the compiled weave's accessors (a
/// channel, a charge-up, a guard, strike timing; an overcharge is the form's alone), its economy (costs and
/// what folds them, the cooldown and its clock, a bank of uses, a spend, a reservation and upkeep, a self
/// stack), and the weave-level attributes the runtime reads off the weave as a whole.
/// </summary>
internal static class CastingAxes
{
    public static void Read(AxisReading r)
    {
        var weave = r.Weave;
        var form = weave.Form;

        // ---- timing: the mode and the bar
        r.Add(Timing, "cast_mode", null, form.CastMode.ToString().ToLowerInvariant(), Word, AxisSource.Form, "form.cast_mode");
        r.Add(Timing, "scope", null, weave.CastScope, Word, AxisSource.Form, "form.cast_mode");
        r.Quantity(Timing, "cast_time", form.CastTime, Seconds, AxisSource.Form, "form.cast_time");
        r.Quantity(Timing, "cast_move", form.CastMove, Fraction, AxisSource.Form, "form.cast_move");
        r.Quantity(Timing, "recovery", form.Recovery, Seconds, AxisSource.Form, "form.recovery");
        if (form.Reflex)
        {
            r.Add(Timing, "reflex", null, "true", Flag, AxisSource.Form, "form.reflex");
        }

        Channel(r);
        Charge(r);
        Overcharge(r);
        Guard(r);
        StrikeTiming(r);
        Toggle(r);

        // ---- economy
        Costs(r);
        Cooldown(r);
        Uses(r);
        Spend(r);
        SelfStack(r);
        foreach (var (resource, amount) in form.Reserve)
        {
            r.Quantity(Economy, "reserve." + resource, amount, Resource, AxisSource.Form, "form.reserve." + resource);
        }
        if (form.Upkeep is { } upkeep)
        {
            foreach (var (resource, amount) in upkeep.PerSecond)
            {
                r.Quantity(Economy, "upkeep." + resource, amount, ResourcePerSecond, AxisSource.Form, "form.upkeep.per_second." + resource);
            }
            if (upkeep.Ramp != 0)
            {
                r.Add(Economy, "upkeep.ramp", upkeep.Ramp, null, FractionPerSecond, AxisSource.Form, "form.upkeep.ramp");
            }
        }

        // ---- the weave as a whole: what the runtime reads off it, where the weave states it
        foreach (var binding in AxisBindings.WeaveAttributes)
        {
            if (binding.Reported && r.Mentions(binding.Attribute))
            {
                r.Add(binding.Dimension, binding.Axis, r.Fold(binding.Attribute), null, binding.Unit, AxisSource.Attribute, "weave." + binding.Attribute, binding.Attribute);
            }
        }
    }

    private static (AxisSource Source, string Path) Held(object block, object? presetBlock, string name) =>
        presetBlock is not null && ReferenceEquals(block, presetBlock) ? (AxisSource.Preset, "preset." + name) : (AxisSource.Form, "form." + name);

    private static void Channel(AxisReading r)
    {
        if (r.Weave.Channel is not { } channel)
        {
            return;
        }
        var (source, at) = Held(channel, r.Preset?.Channel, "channel");
        if (channel.Interval is not null)
        {
            r.Quantity(Timing, "channel.interval", channel.Interval, Seconds, source, at + ".interval");
        }
        else if (r.Weave.Form.ChannelInterval is { } legacy)
        {
            r.Quantity(Timing, "channel.interval", legacy, Seconds, AxisSource.Form, "form.channel_interval");
        }
        else
        {
            Rule(r, Timing, "channel.interval", "casting.channel_interval", 0.5, Seconds, at + ".interval");
        }
        r.Quantity(Timing, "channel.windup", channel.Windup, Seconds, source, at + ".windup");
        r.Quantity(Timing, "channel.jitter", channel.Jitter, Seconds, source, at + ".jitter");
        r.Quantity(Timing, "channel.move", channel.Move, Fraction, source, at + ".move");
        r.Quantity(Timing, "channel.turn_rate", channel.TurnRate, DegreesPerSecond, source, at + ".turn_rate");
        if (channel.MaxHold is not null)
        {
            r.Quantity(Timing, "channel.max_hold", channel.MaxHold, Seconds, source, at + ".max_hold");
        }
        else
        {
            Rule(r, Timing, "channel.max_hold", "casting.max_hold", 8, Seconds, at + ".max_hold");
        }
        FlagIf(r, Timing, "channel.track_aim", channel.TrackAim, source, at + ".track_aim");
        FlagIf(r, Economy, "channel.cooldown_on_end", channel.CooldownOnEnd, source, at + ".cooldown_on_end");
        for (var i = 0; i < channel.Ramps.Count; i++)
        {
            var ramp = channel.Ramps[i];
            var detail = $"{ramp.Form.ToString().ToLowerInvariant()} per held second on the {ramp.On}, curve {ramp.Curve}"
                + (ramp.Max is { } max ? $", max {AxisReport.Format(max)}" : "") + (ramp.Min is { } min ? $", min {AxisReport.Format(min)}" : "");
            r.Add(Timing, "channel.ramp." + ramp.Attribute, ramp.Per, null, PerHeldSecond(ramp.Form), source, $"{at}.ramps[{i}]", ramp.Attribute, detail);
        }
        if (channel.Release is { } release)
        {
            r.Quantity(Timing, "channel.release.min_hold", release.MinHold, Seconds, source, at + ".release.min_hold");
            FlagIf(r, Timing, "channel.release.require_full", release.RequireFull, source, at + ".release.require_full");
            FlagIf(r, Timing, "channel.release.silent", !release.Pulses, source, at + ".release.pulses");
        }
        if (channel.Persist is { } persist)
        {
            r.Quantity(Timing, "channel.persist.per_held_second", persist.PerHeldSecond, Fraction, source, at + ".persist.per_held_second");
            r.Quantity(Timing, "channel.persist.max_duration", persist.MaxDuration, Seconds, source, at + ".persist.max_duration");
            r.Quantity(Payload, "channel.persist.fade", persist.Fade, Fraction, source, at + ".persist.fade");
        }
        if (channel.Brim is { } brim)
        {
            r.Quantity(Economy, "brim.fill_time", brim.FillTime, Seconds, source, at + ".brim.fill_time");
            r.Add(Economy, "brim.decay", brim.Decay, null, FractionPerSecond, source, at + ".brim.decay");
            r.Add(Economy, "brim.bank_at", brim.BankAt, null, Fraction, source, at + ".brim.bank_at");
            r.Add(Economy, "brim.min_release", brim.MinRelease, null, Fraction, source, at + ".brim.min_release");
            r.Add(Economy, "brim.spend", null, brim.Spend ? "true" : "false", Flag, source, at + ".brim.spend");
            FlagIf(r, Economy, "brim.auto_release", brim.AutoRelease, source, at + ".brim.auto_release");
            r.Add(Payload, "brim.scale.at_empty", brim.Scale.AtEmpty, null, Fraction, source, at + ".brim.scale.at_empty", detail: "curve " + brim.Scale.Curve);
            r.Add(Payload, "brim.scale.at_full", brim.Scale.AtFull, null, Fraction, source, at + ".brim.scale.at_full", detail: "curve " + brim.Scale.Curve);
        }
    }

    private static void Charge(AxisReading r)
    {
        if (r.Weave.Charge is not { } charge)
        {
            return;
        }
        var (source, at) = Held(charge, r.Preset?.Charge, "charge");
        if (charge.MaxTime is not null)
        {
            r.Quantity(Timing, "charge.max_time", charge.MaxTime, Seconds, source, at + ".max_time");
        }
        else
        {
            Rule(r, Timing, "charge.max_time", "casting.charge_time", 1.5, Seconds, at + ".max_time");
        }
        if (charge.FullHold is not null)
        {
            r.Quantity(Timing, "charge.full_hold", charge.FullHold, Seconds, source, at + ".full_hold");
        }
        else
        {
            Rule(r, Timing, "charge.full_hold", "casting.full_hold", 1, Seconds, at + ".full_hold");
        }
        for (var i = 0; i < charge.Scales.Count; i++)
        {
            var scale = charge.Scales[i];
            r.Add(Timing, "charge.scale." + scale.Attribute, scale.AtFull, null, ScaleUnit(scale.Form), source, $"{at}.scales[{i}]", scale.Attribute,
                $"{scale.Form.ToString().ToLowerInvariant()} from {AxisReport.Format(scale.AtZero)} at an empty charge");
        }
    }

    /// <summary>An overcharge stays the form's alone: the compiled weave holds no preset block for it.</summary>
    private static void Overcharge(AxisReading r)
    {
        if (r.Weave.Form.Overcharge is not { } over)
        {
            return;
        }
        const string at = "form.overcharge";
        r.Add(Timing, "overcharge.stages", r.Fold("overcharge.stages", over.Stages), null, Count, AxisSource.Form, at + ".stages", "overcharge.stages");
        if (over.Time is not null)
        {
            r.Quantity(Timing, "overcharge.time", over.Time, Seconds, AxisSource.Form, at + ".time");
        }
        else
        {
            Rule(r, Timing, "overcharge.time", "casting.stage_time", 0.6, Seconds, at + ".time");
        }
        r.Quantity(Timing, "overcharge.full_hold", over.FullHold, Seconds, AxisSource.Form, at + ".full_hold");
        foreach (var (resource, amount) in over.CostPerStage)
        {
            r.Quantity(Economy, "cost_per_stage." + resource, amount, Resource, AxisSource.Form, at + ".cost_per_stage." + resource);
        }
        for (var i = 0; i < over.PerStage.Count; i++)
        {
            var m = over.PerStage[i];
            var (number, word) = ModifierValue(r, m);
            r.Add(Timing, "overcharge.per_stage." + m.Attribute, number, word, ScaleUnit(m.Form), AxisSource.Form, $"{at}.per_stage[{i}]", m.Attribute, m.Form.ToString().ToLowerInvariant());
        }
        if (over.Spark is { } spark)
        {
            r.Add(Timing, "overcharge.spark.window", spark.Window, null, Seconds, AxisSource.Form, at + ".spark.window");
            r.Add(Payload, "overcharge.spark.bonus", spark.Bonus, null, Fraction, AxisSource.Form, at + ".spark.bonus");
        }
    }

    private static void Guard(AxisReading r)
    {
        if (r.Weave.Guard is not { } guard)
        {
            return;
        }
        var (source, at) = Held(guard, r.Preset?.Guard, "guard");
        r.Add(Geometry, "guard.arc", guard.Arc, null, Degrees, source, at + ".arc");
        if (guard.Pool is { } pool)
        {
            var amount = r.Evaluate(pool);
            var strength = r.Fold("guard.strength");
            r.Add(Economy, "guard.pool", amount is { } a && strength is { } s ? a * s : null, amount is null ? pool.Source : null, Resource, source, at + ".pool", "guard.strength",
                $"{pool.Source} x guard.strength", multiplied: true);
        }
        r.Quantity(Timing, "guard.move", guard.Move, Fraction, source, at + ".move");
        r.Quantity(Timing, "guard.turn_rate", guard.TurnRate, DegreesPerSecond, source, at + ".turn_rate");
        r.Quantity(Timing, "guard.parry", guard.Parry, Seconds, source, at + ".parry");
        if (guard.MaxDuration is not null)
        {
            r.Quantity(Timing, "guard.max_duration", guard.MaxDuration, Seconds, source, at + ".max_duration");
        }
        else
        {
            Rule(r, Timing, "guard.max_duration", "casting.max_hold", 8, Seconds, at + ".max_duration");
        }
        FlagIf(r, Timing, "guard.end_on_parry", guard.EndOnParry, source, at + ".end_on_parry");
        if (guard.Bash is { } bash)
        {
            r.Add(Economy, "guard.bash.threshold", bash.Threshold, null, Fraction, source, at + ".bash.threshold");
            r.Quantity(Timing, "guard.bash.arm_time", bash.ArmTime, Seconds, source, at + ".bash.arm_time");
            FlagIf(r, Timing, "guard.bash.on_break", bash.OnBreak, source, at + ".bash.on_break");
        }
        if (guard.Pulse is { } pulse)
        {
            if (pulse.Interval is not null)
            {
                r.Quantity(Timing, "guard.pulse.interval", pulse.Interval, Seconds, source, at + ".pulse.interval");
            }
            else
            {
                Rule(r, Timing, "guard.pulse.interval", "casting.channel_interval", 0.5, Seconds, at + ".pulse.interval");
            }
        }
    }

    private static void StrikeTiming(AxisReading r)
    {
        if (r.Weave.Timing is not { } timing)
        {
            return;
        }
        var (source, at) = Held(timing, r.Preset?.Timing, "timing");
        r.Add(Timing, "strike.kind", null, timing.Kind.ToString().ToLowerInvariant(), Word, source, at + ".kind");
        var multitude = timing.Kind == TimingKind.Multitude;
        r.Add(Timing, "strike.window", timing.Window, null, multitude ? Count : Seconds, source, at + ".window");
        if (!multitude)
        {
            r.Add(Payload, "strike.bonus", timing.Bonus, null, Fraction, source, at + ".bonus");
        }
    }

    private static void Toggle(AxisReading r)
    {
        if (r.Weave.Form.Toggle is not { } toggle)
        {
            return;
        }
        r.Add(Timing, "toggle", null, "true", Flag, AxisSource.Form, "form.toggle");
        r.Quantity(Timing, "toggle.max_duration", toggle.MaxDuration, Seconds, AxisSource.Form, "form.toggle.max_duration");
    }

    /// <summary>Each cost: its formula, plus what the weave adds to it, through the cost fold under the resource's scope.</summary>
    private static void Costs(AxisReading r)
    {
        var weave = r.Weave;
        var resources = new List<string>(weave.Costs.Keys);
        foreach (var m in weave.Modifiers)
        {
            const string added = "cost.added.";
            if (m.Attribute.StartsWith(added, StringComparison.Ordinal) && !resources.Contains(m.Attribute[added.Length..]))
            {
                resources.Add(m.Attribute[added.Length..]);
            }
        }
        foreach (var resource in resources)
        {
            var written = weave.Costs.TryGetValue(resource, out var expr);
            var amount = written ? r.Evaluate(expr) : 0;
            var adds = "cost.added." + resource;
            var tags = weave.Tags.With("cost." + resource);
            double? total = null;
            if (amount is { } a)
            {
                try
                {
                    var plus = r.Mentions(adds) ? r.Holder.Attributes.Get(adds, r.Holder, tags, weave.Modifiers) : 0;
                    total = Math.Max(0, r.Holder.Attributes.Apply("cost", Math.Max(0, a + plus), r.Holder, tags, weave.Modifiers));
                }
                catch (Exception e) when (e is ExprEvalException or AttributeCycleException or ArithmeticException)
                {
                    total = null;
                }
            }
            var detail = written && !expr!.IsConstant ? expr.Source : null;
            if (total is { } t && amount is { } b && Math.Abs(t - b) > 1e-9)
            {
                detail = (detail is null ? "" : detail + "; ") + $"{AxisReport.Format(b)} before the cost fold";
            }
            r.Add(Economy, "cost." + resource, total, total is null ? expr?.Source : null, Resource,
                written ? AxisSource.Form : AxisSource.Attribute, written ? "form.costs." + resource : "weave." + adds, written ? null : adds, detail);
        }
    }

    /// <summary>The cooldown: the form's plus what the weave adds through <c>cooldown.added</c>, and when its clock starts.</summary>
    private static void Cooldown(AxisReading r)
    {
        var weave = r.Weave;
        var form = weave.Form;
        var levied = r.Mentions("cooldown.added");
        if (form.Cooldown is { } cooldown)
        {
            var seconds = r.Evaluate(cooldown);
            if (levied && seconds is { } s && r.Fold("cooldown.added") is { } more)
            {
                seconds = Math.Max(0, s + more);
            }
            r.Add(Economy, "cooldown", seconds, seconds is null ? cooldown.Source : null, Seconds, AxisSource.Form, "form.cooldown",
                levied ? "cooldown.added" : null, levied ? $"{cooldown.Source} + cooldown.added" : cooldown.IsConstant ? null : cooldown.Source);
        }
        else if (levied)
        {
            r.Add(Economy, "cooldown", r.Fold("cooldown.added"), null, Seconds, AxisSource.Attribute, "weave.cooldown.added", "cooldown.added");
        }
        if (weave.CooldownAt is { } clock)
        {
            var fromPreset = r.Preset?.CooldownAt is not null;
            r.Add(Economy, "cooldown_at", null, clock, Word, fromPreset ? AxisSource.Preset : AxisSource.Form, fromPreset ? "preset.cooldown_at" : "form.cooldown_at");
        }
    }

    private static void Uses(AxisReading r)
    {
        if (r.Weave.Casting.Uses is not { } uses)
        {
            return;
        }
        var (source, at) = r.OwnerOf(uses, g => g.Uses, p => p.Uses, "uses");
        r.Add(Economy, "uses.max", r.Fold("uses.max", uses.Max), null, Count, source, at + ".max", "uses.max");
        r.Quantity(Economy, "uses.recharge", uses.Recharge, Seconds, source, at + ".recharge");
        if (uses.Magazine is { } magazine)
        {
            r.Add(Economy, "uses.magazine", null, magazine.Drip ? "drip" : "whole", Word, source, at + ".magazine");
            r.Quantity(Economy, "uses.magazine.refill", magazine.Refill, Seconds, source, at + ".magazine.refill");
        }
        if (uses.Still is { } still)
        {
            r.Add(Economy, "uses.still.bleed", still.Bleed, null, CountPerSecond, source, at + ".still.bleed");
        }
        FlagIf(r, Economy, "uses.manual", uses.Manual, source, at + ".manual");
        FlagIf(r, Economy, "uses.vent_all", uses.VentAll, source, at + ".vent_all");
        FlagIf(r, Causality, "uses.steps_from_bank", uses.StepsFromBank, source, at + ".steps_from_bank");
        for (var i = 0; i < uses.Empower.Count; i++)
        {
            var m = uses.Empower[i];
            var (number, word) = ModifierValue(r, m);
            r.Add(Economy, "uses.empower." + m.Attribute, number, word, ScaleUnit(m.Form), source, $"{at}.empower[{i}]", m.Attribute, m.Form.ToString().ToLowerInvariant());
        }
    }

    private static void Spend(AxisReading r)
    {
        if (r.Weave.Casting.Spend is not { } spend)
        {
            return;
        }
        var (source, at) = r.OwnerOf(spend, g => g.Spend, p => p.Spend, "spend");
        var detail = (spend.Minimum > 0 ? $"minimum {AxisReport.Format(spend.Minimum)}" : "") + (spend.Optional ? (spend.Minimum > 0 ? ", " : "") + "optional" : "");
        r.Add(Economy, "spend." + spend.Charge, spend.All ? null : spend.Amount, spend.All ? "all" : null, Count, source, at + ".amount", detail: detail.Length == 0 ? null : detail);
        for (var i = 0; i < spend.Scales.Count; i++)
        {
            var scale = spend.Scales[i];
            r.Add(Payload, "spend.scale." + scale.Attribute, scale.PerCharge, null, ScaleUnit(scale.Form), source, $"{at}.scales[{i}]", scale.Attribute,
                scale.Form.ToString().ToLowerInvariant() + " per charge spent");
        }
    }

    private static void SelfStack(AxisReading r)
    {
        if (r.Weave.Casting.SelfStack is not { } stack)
        {
            return;
        }
        var (source, at) = r.OwnerOf(stack, g => g.SelfStack, p => p.SelfStack, "self_stack");
        r.Add(Economy, "self_stack.max", stack.Max, null, Count, source, at + ".max");
        r.Add(Economy, "self_stack.duration", stack.Duration, null, Seconds, source, at + ".duration");
        if (stack.Decay > 0)
        {
            r.Add(Economy, "self_stack.decay", stack.Decay, null, Seconds, source, at + ".decay");
        }
    }

    private static void Rule(AxisReading r, Dimension dimension, string axis, string rule, double fallback, string unit, string path) =>
        r.Add(dimension, axis, r.Content.Rules.Number(rule, fallback), null, unit, AxisSource.Rule, path, detail: rule);

    private static void FlagIf(AxisReading r, Dimension dimension, string axis, bool on, AxisSource source, string path)
    {
        if (on)
        {
            r.Add(dimension, axis, null, "true", Flag, source, path);
        }
    }

    /// <summary>A modifier's value: its number, or its formula where the weave alone does not state one.</summary>
    private static (double? Number, string? Word) ModifierValue(AxisReading r, Modifier m)
    {
        if (m.ValueExpr is not { } formula)
        {
            return (m.Value, null);
        }
        var number = r.Evaluate(formula);
        return (number, number is null ? formula.Source : null);
    }

    private static string ScaleUnit(ModifierForm form) => form is ModifierForm.Increased or ModifierForm.More ? Fraction : Value;

    private static string PerHeldSecond(ModifierForm form) => form is ModifierForm.Increased or ModifierForm.More ? FractionPerSecond : Value;
}
