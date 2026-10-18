using HollowWake.Core.Expressions;
using static HollowWake.Core.Weaves.AxisUnits;
using static HollowWake.Core.Weaves.Dimension;

namespace HollowWake.Core.Weaves.Axes;

/// <summary>
/// The targeting family: what the weave is aimed at and how far it reaches (the form's targeting mode, its
/// range as written or as the weave's <c>range</c> folds), the target block a form names (a registered target
/// kind, its requirement, its search radius and what happens when nothing fits), and the gates a caster must
/// meet, the form's, the preset's and the glyphs'.
/// </summary>
internal static class TargetingAxes
{
    public static void Read(AxisReading r)
    {
        var weave = r.Weave;
        var form = weave.Form;
        r.Add(Targeting, "mode", null, form.Targeting.ToString().ToLowerInvariant(), Word, AxisSource.Form, "form.targeting");
        if (form.Range is { } range)
        {
            r.Quantity(Targeting, "range", range, Units, AxisSource.Form, "form.range");
        }
        else if (r.Mentions("range"))
        {
            r.Add(Targeting, "range", r.Fold("range"), null, Units, AxisSource.Attribute, "weave.range", "range");
        }

        if (form.Target is { } target)
        {
            r.Add(Targeting, "target.kind", null, target.Kind, Word, AxisSource.Form, "form.target.kind");
            if (target.Require is { } require)
            {
                r.Add(Targeting, "target.require", null, require.Source, Condition, AxisSource.Form, "form.target.require");
            }
            r.Quantity(Targeting, "target.search_radius", target.SearchRadius, Units, AxisSource.Form, "form.target.search_radius");
            r.Add(Targeting, "target.fallback", null, target.Fallback, Word, AxisSource.Form, "form.target.fallback");
            if (target.Consume)
            {
                r.Add(Targeting, "target.consume", null, "true", Flag, AxisSource.Form, "form.target.consume");
            }
        }

        // Gates: the form's, then the preset's, then each glyph's, as the compiled weave holds them.
        foreach (var (gate, source, path) in Gates(r))
        {
            r.Add(Targeting, "gate", null, gate.Source, Condition, source, path);
        }
    }

    /// <summary>Each gate the compiled weave holds, with who set it: the form, the preset or a glyph.</summary>
    internal static IEnumerable<(Expr Gate, AxisSource Source, string Path)> Gates(AxisReading r)
    {
        var form = r.Weave.Form;
        for (var i = 0; i < form.Gates.Count; i++)
        {
            yield return (form.Gates[i], AxisSource.Form, $"form.gates[{i}]");
        }
        if (r.Preset is { } preset)
        {
            for (var i = 0; i < preset.Gates.Count; i++)
            {
                yield return (preset.Gates[i], AxisSource.Preset, $"preset.gates[{i}]");
            }
        }
        foreach (var glyph in r.Weave.Glyphs)
        {
            for (var i = 0; i < glyph.Gates.Count; i++)
            {
                yield return (glyph.Gates[i], AxisSource.Glyph, $"glyph.{glyph.Id}.gates[{i}]");
            }
        }
    }
}
