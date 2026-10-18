using HollowWake.Core.Definitions;
using static HollowWake.Core.Weaves.AxisUnits;
using static HollowWake.Core.Weaves.Dimension;

namespace HollowWake.Core.Weaves.Axes;

/// <summary>
/// The triggers family, and with it causality: what starts a resolution besides the press, and how far one
/// press cascades. The weave's triggers (each by kind, with its chance folded through <c>trigger.chance</c>,
/// its internal cooldown and its conditions), its follow-ups, metas, converts, cycles and combo chains, the
/// sequels its graph casts (the parameters <see cref="AxisBindings"/> hands to this reader), and the riders
/// glyphs hook onto its moments.
/// </summary>
internal static class TriggerAxes
{
    public const string Family = "triggers";

    public static void Read(AxisReading r)
    {
        var weave = r.Weave;
        var casting = weave.Casting;
        foreach (var trigger in casting.Triggers)
        {
            Trigger(r, trigger);
        }

        foreach (var follow in casting.FollowUps)
        {
            var (source, at) = r.OwnerOf(follow, g => g.FollowUp, p => p.FollowUp, "follow_up");
            var chance = follow.Chance is { } c ? r.Evaluate(c) : 1;
            r.Add(Causality, "follow_up." + follow.Weave, chance, chance is null ? follow.Chance!.Source : null, Fraction, source, at + ".chance");
            if (follow.Delay is not null)
            {
                r.Quantity(Timing, "follow_up.delay", follow.Delay, Seconds, source, at + ".delay");
            }
            else
            {
                r.Add(Timing, "follow_up.delay", r.Content.Rules.Number("casting.follow_up_delay", 0.3), null, Seconds, AxisSource.Rule, at + ".delay", detail: "casting.follow_up_delay");
            }
        }

        foreach (var meta in casting.Metas)
        {
            var (source, at) = r.OwnerOf(meta, g => g.MetaAction, p => p.MetaAction, "meta_action");
            r.Add(Causality, "meta", null, meta.Weave, Word, source, at, detail: meta.Label.Length == 0 ? null : meta.Label);
        }

        if (casting.Convert is { } convert)
        {
            var (source, at) = r.OwnerOf(convert, g => g.Convert, p => p.Convert, "convert");
            r.Add(Causality, "convert." + convert.Weave, null, convert.When.Source, Condition, source, at + ".when");
        }
        if (casting.Cycle is { } cycle)
        {
            var (source, at) = r.OwnerOf(cycle, g => g.Cycle, p => p.Cycle, "cycle");
            r.Add(Causality, "cycle", cycle.Count, null, Count, source, at + ".count", detail: "every so many uses");
        }
        if (casting.Chain is { } combo)
        {
            var (source, at) = r.OwnerOf(combo, g => g.Chain, p => p.Chain, "chain");
            r.Add(Causality, "combo", null, string.Join(",", combo.Steps), Word, source, at + ".steps");
            r.Add(Timing, "combo.window", combo.Window, null, Seconds, source, at + ".window");
        }

        // The weaves the graph casts, and the riders glyphs hook onto its moments.
        var riders = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in r.Nodes)
        {
            foreach (var binding in AxisBindings.For(node.Type))
            {
                if (binding.ReadBy == Family && binding.Dimension is { } dimension && r.Words(node.Node[binding.Parameter]) is { Length: > 0 } word)
                {
                    var who = node.Provenance(binding.Parameter);
                    r.Add(new AxisValue
                    {
                        Dimension = dimension,
                        Name = AxisValue.DimensionName(dimension) + "." + binding.Axis,
                        Word = word,
                        Unit = Word,
                        Source = who?.Source ?? AxisSource.Literal,
                        Path = $"{node.Path}.{binding.Parameter}",
                        Effect = node.Type,
                        Parameter = binding.Parameter,
                        Detail = who?.By,
                    });
                }
            }
            if (node.AddedAs == AxisSource.Hook && node.AddedBy is { } by && riders.Add(by))
            {
                // "glyph {id} {moment}": one rider per glyph and moment, at the first place it lands.
                var parts = by.Split(' ');
                var moment = parts.Length >= 3 ? parts[2] : "hook";
                r.Add(Causality, "hook." + moment, null, parts.Length >= 2 ? parts[1] : by, Word, AxisSource.Hook, node.Path, detail: by);
            }
        }
    }

    private static void Trigger(AxisReading r, WeaveTrigger trigger)
    {
        var spec = trigger.Spec;
        var (source, at) = trigger.Source switch
        {
            "form" => (AxisSource.Form, "form.trigger"),
            "preset" => (AxisSource.Preset, "preset.trigger"),
            var glyph => (AxisSource.Glyph, $"glyph.{glyph}.trigger"),
        };
        var name = "trigger." + spec.On;
        var baseChance = spec.Chance is { } chance ? r.Evaluate(chance) : 1;
        var folded = baseChance is { } b ? r.Fold("trigger.chance", b) : null;
        r.Add(Causality, name, folded, folded is null ? spec.Chance?.Source : null, Fraction, source, at + ".chance", "trigger.chance",
            spec.Tags.IsEmpty ? null : "on events tagged " + spec.Tags);
        if (spec.Icd > 0)
        {
            r.Add(Causality, name + ".icd", spec.Icd, null, Seconds, source, at + ".icd");
        }
        if (spec.LifeFraction is { } life)
        {
            r.Add(Causality, name + ".life_fraction", life, null, Fraction, source, at + ".life_fraction");
        }
        if (spec.Status is { } status)
        {
            r.Add(Causality, name + ".status", null, status, Word, source, at + ".status");
        }
        if (spec.Power is { } power)
        {
            r.Quantity(Payload, name + ".power", power, Resource, source, at + ".power");
        }
        if (spec.MaxCastTime is { } longest)
        {
            r.Add(Causality, name + ".max_cast_time", longest, null, Seconds, source, at + ".max_cast_time");
        }
    }
}
