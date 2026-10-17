using HollowWake.Core.Definitions;
using HollowWake.Core.Effects;
using HollowWake.Core.Expressions;
using HollowWake.Core.Sim;

namespace HollowWake.Core.Content.Kinds;

/// <summary>
/// Content for casting: the casting fields of forms (cast modes, channels, charge-ups, triggers, the
/// cooldown clock), glyph triggers and preset overrides. Registers the cast variables as scope names and
/// adds validators to the existing kinds; trigger kinds that casting raises register here too.
/// </summary>
internal static class CastingKinds
{
    public static void Register()
    {
        foreach (var name in CastVars.All)
        {
            ScopeNames.Declare(name);
        }
        ContentKinds.AddValidator<FormDef>(Form);
        ContentKinds.AddValidator<GlyphDef>((d, c) => Trigger(d.Trigger, c, "trigger"));
        ContentKinds.AddValidator<WeaveDef>((d, c) => CooldownAt(d.CooldownAt, c));
    }

    private static void Form(FormDef d, ValidationContext c)
    {
        c.Expression(d.CastMove, "cast_move");
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
                if (!c.Registry.Attributes.Contains(ramp.Attribute))
                {
                    c.Warn($"channel.ramps ramps undeclared attribute '{ramp.Attribute}'.");
                }
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
        }
        if (d.Charge is { } charge)
        {
            if (d.CastMode != CastMode.Charge)
            {
                c.Warn("has a charge block but its cast_mode is not charge.");
            }
            c.Expression(charge.MaxTime, "charge.max_time");
            foreach (var scale in charge.Scales)
            {
                if (!c.Registry.Attributes.Contains(scale.Attribute))
                {
                    c.Warn($"charge.scales scales undeclared attribute '{scale.Attribute}'.");
                }
            }
        }
        else if (d.CastMode == CastMode.Charge)
        {
            c.Warn("has cast_mode charge but no charge block.");
        }
        Trigger(d.Trigger, c, "trigger");
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

    private static void CooldownAt(string? at, ValidationContext c)
    {
        if (at is not null and not ("press" or "complete"))
        {
            c.Error($"cooldown_at must be press or complete, not '{at}'.");
        }
    }
}
