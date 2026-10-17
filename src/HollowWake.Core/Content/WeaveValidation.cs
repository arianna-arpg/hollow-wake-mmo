using System.Text.Json.Nodes;
using HollowWake.Core.Definitions;
using HollowWake.Core.Effects;
using HollowWake.Core.Weaves;

namespace HollowWake.Core.Content;

/// <summary>
/// Validation shared by the kinds that hold weaves and glyphs: glyph hooks, patches, mechanisms and levels,
/// glyph references with levels and rolled rows, effect types, and whether a weave in content fits (every
/// glyph of a preset or a starting weave must be active; a dormant one is a warning). Effect types,
/// parameters and hooks are checked against <see cref="EffectRegistry.Shared"/>.
/// </summary>
public static class WeaveValidation
{
    /// <summary>The highest glyph level rule <c>glyphs.max_level</c> allows.</summary>
    public static int MaxGlyphLevel(ContentRegistry registry) => Math.Max(1, (int)registry.Rules.Number("glyphs.max_level", 20));

    /// <summary>A glyph's own fields: level, mechanisms, added effects, hooks and patches.</summary>
    public static void Glyph(GlyphDef d, ValidationContext c)
    {
        var effects = EffectRegistry.Shared;
        if (d.Level < 1 || d.Level > MaxGlyphLevel(c.Registry))
        {
            c.Error($"level {d.Level} is outside 1..{MaxGlyphLevel(c.Registry)} (rule glyphs.max_level).");
        }
        foreach (var m in d.RequiresMechanisms)
        {
            if (MechanismRegistry.Validate(m, c.Registry) is { } problem)
            {
                c.Error($"requires_mechanisms {problem}.");
            }
        }
        foreach (var m in d.AddsMechanisms)
        {
            if (m.Contains(':'))
            {
                c.Error($"adds_mechanisms cannot declare '{m}': parameterized mechanisms are probed from the weave, not declared.");
            }
        }
        Effects(d.AddsEffects, c, "adds_effects");
        var hookNames = effects.HookNames;
        foreach (var (hook, list) in d.Hooks)
        {
            if (!hookNames.Contains(hook))
            {
                c.Error($"hooks names unknown hook '{hook}'; known hooks are {string.Join(", ", hookNames)}.");
            }
            Effects(list, c, $"hooks.{hook}");
        }
        for (var i = 0; i < d.Patches.Count; i++)
        {
            Patch(d.Patches[i], c, $"patches[{i}]");
        }
    }

    /// <summary>A patch must name a known effect type and only parameters that type has; a wrap must be a wrapper.</summary>
    public static void Patch(EffectPatch patch, ValidationContext c, string field)
    {
        var effects = EffectRegistry.Shared;
        if (!effects.TryGetSignature(patch.Type, out var signature))
        {
            c.Error($"{field} patches unknown effect type '{patch.Type}'.");
            return;
        }
        if (patch.Set is null && patch.Wrap is null)
        {
            c.Error($"{field} has neither 'set' nor 'wrap'.");
        }
        if (patch.Set is { } set)
        {
            foreach (var (param, value) in set)
            {
                if (param == "type" || !signature.Accepts(param))
                {
                    c.Error($"{field} sets '{param}', which '{patch.Type}' does not have.");
                }
                if (patch.EffectiveMode == PatchMode.Append && value is not JsonArray)
                {
                    c.Error($"{field} appends to '{param}' with a value that is not a list.");
                }
            }
        }
        if (patch.Wrap is { } wrap)
        {
            if (!effects.TryGetSignature(wrap.Type, out var wrapper))
            {
                c.Error($"{field} wraps in unknown effect type '{wrap.Type}'.");
            }
            else if (wrapper.Wraps is null)
            {
                c.Error($"{field} wraps in '{wrap.Type}', which is not a wrapper.");
            }
            else
            {
                foreach (var (param, _) in wrap.Params)
                {
                    if (!wrapper.Accepts(param))
                    {
                        c.Error($"{field} wrapper '{wrap.Type}' has no parameter '{param}'.");
                    }
                }
            }
        }
    }

    /// <summary>Every top-level effect in a list must be a registered type.</summary>
    public static void Effects(IEnumerable<TypedSpec>? specs, ValidationContext c, string field)
    {
        if (specs is null)
        {
            return;
        }
        foreach (var spec in specs)
        {
            if (!EffectRegistry.Shared.Has(spec.Type))
            {
                c.Error($"'{field}' uses unknown effect type '{spec.Type}'.");
            }
        }
    }

    /// <summary>Glyph references must name glyphs, at a level within bounds, with rows their axes have. True when all do.</summary>
    public static bool GlyphRefs(IEnumerable<GlyphRef> refs, ValidationContext c, string field)
    {
        var ok = true;
        var max = MaxGlyphLevel(c.Registry);
        foreach (var r in refs)
        {
            if (!c.Ref<GlyphDef>(r.Id, field))
            {
                ok = false;
                continue;
            }
            var g = c.Registry.Get<GlyphDef>(r.Id)!;
            if (r.Level is { } level && (level < 1 || level > max))
            {
                c.Error($"'{field}' sets glyph '{r.Id}' to level {level}, outside 1..{max}.");
                ok = false;
            }
            foreach (var (axis, row) in r.Rows)
            {
                var a = g.Axes.FirstOrDefault(x => x.Id == axis);
                if (a is null)
                {
                    c.Error($"'{field}' gives glyph '{r.Id}' a row on unknown axis '{axis}'.");
                    ok = false;
                }
                else if (!a.Rows.Any(x => x.Id == row))
                {
                    c.Error($"'{field}' gives glyph '{r.Id}' unknown row '{row}' on axis '{axis}'.");
                    ok = false;
                }
            }
        }
        return ok;
    }

    /// <summary>A weave in content must compile with every glyph active: a failure is an error, a dormant glyph a warning.</summary>
    public static void Fits(WeaveSpec spec, ValidationContext c, string field)
    {
        var compiler = new WeaveCompiler(c.Registry, EffectRegistry.Shared);
        if (!compiler.TryCompile(spec, out var weave, out var error, GlyphFit.Dormant))
        {
            c.Error($"'{field}' does not compile: {error}");
            return;
        }
        foreach (var dormant in weave!.Dormant)
        {
            c.Warn($"'{field}' glyph '{dormant.Ref}' is dormant: {dormant.Reason}");
        }
    }
}
