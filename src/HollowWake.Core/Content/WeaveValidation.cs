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

    /// <summary>
    /// Every top-level effect in a list must be a registered type (an error); then its parameters and its
    /// nested effect lists are checked as <see cref="EffectParams"/> does.
    /// </summary>
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
        EffectParams(specs, c, field);
    }

    /// <summary>
    /// Warns about what a list of effects would silently ignore: a parameter its type does not read (its
    /// <see cref="EffectSignature"/> says what it reads; types that declare nothing are not checked), and in
    /// nested lists an effect type nobody registered. Nested lists are found wherever a parameter holds a list
    /// of objects naming a <c>type</c>, at any depth. Specs <paramref name="skip"/> accepts (dialogue
    /// actions, say) are left to their own checks; top-level unknown types are left to the caller.
    /// </summary>
    public static void EffectParams(IEnumerable<TypedSpec>? specs, ValidationContext c, string field, Func<string, bool>? skip = null)
    {
        if (specs is null)
        {
            return;
        }
        var i = 0;
        foreach (var spec in specs)
        {
            var at = $"{field}[{i++}]";
            if (skip?.Invoke(spec.Type) == true)
            {
                continue;
            }
            if (EffectRegistry.Shared.Has(spec.Type))
            {
                Node(spec.Type, spec.Params, c, at);
            }
        }
    }

    /// <summary>The parameters of one effect node, then the lists nested in them.</summary>
    private static void Node(string type, JsonObject parameters, ValidationContext c, string at)
    {
        if (EffectRegistry.Shared.TryGetSignature(type, out var signature) && !signature.IsOpen)
        {
            foreach (var (key, _) in parameters)
            {
                if (key != "type" && !signature.Accepts(key))
                {
                    c.Warn($"'{at}' effect '{type}' has parameter '{key}', which it does not read.");
                }
            }
        }
        Nested(parameters, c, at);
    }

    private static void Nested(JsonObject node, ValidationContext c, string at)
    {
        foreach (var (key, value) in node)
        {
            switch (value)
            {
                case JsonArray list:
                    for (var i = 0; i < list.Count; i++)
                    {
                        if (list[i] is JsonObject element)
                        {
                            Element(element, c, $"{at}.{key}[{i}]");
                        }
                    }
                    break;
                case JsonObject obj:
                    Nested(obj, c, $"{at}.{key}");
                    break;
            }
        }
    }

    /// <summary>An object inside a list: an effect when it names a type, else a holder of further lists (choices, waves).</summary>
    private static void Element(JsonObject element, ValidationContext c, string at)
    {
        if (element["type"] is JsonValue value && value.TryGetValue<string>(out var type))
        {
            if (EffectRegistry.Shared.Has(type))
            {
                Node(type, element, c, at);
                return;
            }
            c.Warn($"'{at}' names unknown effect type '{type}'.");
        }
        Nested(element, c, at);
    }

    /// <summary>
    /// Adds the effect checks to the kinds whose effect lists no other validator walks: form and preset
    /// graphs, a status's own lists, and dialogue effects (dialogue actions keep their own check).
    /// </summary>
    internal static void RegisterEffectChecks()
    {
        ContentKinds.AddValidator<FormDef>((d, c) => EffectParams(d.Effects, c, "effects"));
        ContentKinds.AddValidator<WeaveDef>((d, c) => EffectParams(d.Effects, c, "effects"));
        ContentKinds.AddValidator<StatusDef>((d, c) =>
        {
            EffectParams(d.OnApply, c, "on_apply");
            EffectParams(d.OnExpire, c, "on_expire");
            if (d.Tick is { } tick)
            {
                EffectParams(tick.Effects, c, "tick.effects");
            }
        });
        ContentKinds.AddValidator<DialogueDef>((d, c) =>
        {
            static bool IsAction(string type) => Dialogue.DialogueActions.Shared.TryGet(type, out _, out _);
            foreach (var (id, node) in d.Nodes)
            {
                EffectParams(node.Effects, c, $"nodes.{id}.effects", IsAction);
                foreach (var choice in node.Choices)
                {
                    EffectParams(choice.Effects, c, $"nodes.{id}.choices.{choice.Id}.effects", IsAction);
                }
            }
        });
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
