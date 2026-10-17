using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Effects;
using HollowWake.Core.Expressions;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Weaves;

public sealed class WeaveCompileException : Exception
{
    public WeaveCompileException(string message) : base(message)
    {
    }
}

/// <summary>
/// A weave resolved from its spec: form, threads, glyphs, the union of their tags and mechanisms,
/// the weave-local attribute bases and modifiers, and the compiled effect graph. Every number a weave
/// has is computed on the caster's modifier stack with the weave's modifiers joining the same fold.
/// </summary>
public sealed class CompiledWeave
{
    public required WeaveSpec Spec { get; init; }
    public required FormDef Form { get; init; }
    public required IReadOnlyList<ThreadDef> Threads { get; init; }
    public required IReadOnlyList<GlyphDef> Glyphs { get; init; }
    public required TagSet Tags { get; init; }
    public required TagSet Mechanisms { get; init; }
    public required IReadOnlyDictionary<string, double> Bases { get; init; }
    public required IReadOnlyList<Modifier> Modifiers { get; init; }
    public required IReadOnlyList<CompiledEffect> Effects { get; init; }
    public required IReadOnlyDictionary<string, Expr> Costs { get; init; }
    public int Level { get; init; } = 1;
    public string Name { get; init; } = "";
    public string? Look { get; init; }
    public AiHint? Ai { get; init; }

    public string Key => Spec.ToString();
    public Targeting Targeting => Form.Targeting;
    public CastMode CastMode => Form.CastMode;

    /// <summary>A weave attribute as seen by this caster: form base, caster stack, glyph modifiers, tag scope.</summary>
    public double Attr(Stats caster, string attribute, TagSet? context = null)
    {
        var ctx = context ?? Tags;
        double? baseValue = Bases.TryGetValue(attribute, out var b) ? b : null;
        return caster.Attributes.Get(attribute, caster, ctx, Modifiers, baseValue);
    }

    public double Range(Stats caster) => Form.Range is null ? Attr(caster, "range") : Form.Range.Evaluate(ScopeFor(caster));
    public double CastTime(Stats caster) => Form.CastTime is null ? 0 : Math.Max(0, Form.CastTime.Evaluate(ScopeFor(caster)));
    public double Cooldown(Stats caster) => Form.Cooldown is null ? 0 : Math.Max(0, Form.Cooldown.Evaluate(ScopeFor(caster)));

    public IExprScope ScopeFor(Stats caster)
    {
        var overlay = new ScopeOverlay(caster);
        overlay.Mount("weave", new WeaveScope(this, caster, Tags));
        overlay.Set("level", caster.Level);
        return overlay;
    }

    public override string ToString() => Name.Length > 0 ? Name : Key;
}

/// <summary>Turns specs into compiled weaves, enforcing form limits and glyph gates.</summary>
public sealed class WeaveCompiler
{
    private readonly ContentRegistry _content;
    private readonly EffectRegistry _effects;
    private readonly Dictionary<string, CompiledWeave> _cache = new(StringComparer.Ordinal);

    public WeaveCompiler(ContentRegistry content, EffectRegistry effects)
    {
        _content = content;
        _effects = effects;
    }

    public ContentRegistry Content => _content;

    public CompiledWeave Compile(WeaveSpec spec)
    {
        var key = spec.ToString() + "#" + spec.Level;
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }
        var compiled = Build(spec);
        _cache[key] = compiled;
        return compiled;
    }

    public bool TryCompile(WeaveSpec spec, out CompiledWeave? weave, out string? error)
    {
        try
        {
            weave = Compile(spec);
            error = null;
            return true;
        }
        catch (WeaveCompileException ex)
        {
            weave = null;
            error = ex.Message;
            return false;
        }
    }

    private CompiledWeave Build(WeaveSpec spec)
    {
        WeaveDef? preset = null;
        var formId = spec.Form;
        var threadIds = spec.Threads.ToList();
        var glyphIds = spec.Glyphs.ToList();
        var bases = new Dictionary<string, double>(StringComparer.Ordinal);
        var modifiers = new List<Modifier>();
        var extraEffects = new List<TypedSpec>();
        var name = spec.Name ?? "";
        string? look = null;
        AiHint? ai = null;

        if (spec.Preset is not null)
        {
            preset = _content.Get<WeaveDef>(spec.Preset) ?? throw new WeaveCompileException($"Unknown preset weave '{spec.Preset}'.");
            formId ??= preset.Form;
            if (threadIds.Count == 0)
            {
                threadIds = preset.Threads.ToList();
            }
            glyphIds = preset.Glyphs.Concat(glyphIds).Distinct(StringComparer.Ordinal).ToList();
            foreach (var (k, v) in preset.Attributes)
            {
                bases[k] = v;
            }
            modifiers.AddRange(preset.Modifiers.Select(m => m.WithSource("preset:" + preset.Id)));
            extraEffects.AddRange(preset.Effects);
            if (name.Length == 0)
            {
                name = preset.DisplayName;
            }
            look = preset.Icon ?? preset.Color;
            ai = preset.Ai;
        }
        if (formId is null)
        {
            throw new WeaveCompileException("A weave needs a form or a preset.");
        }
        var form = _content.Get<FormDef>(formId) ?? throw new WeaveCompileException($"Unknown form '{formId}'.");
        var threads = new List<ThreadDef>();
        foreach (var t in threadIds)
        {
            threads.Add(_content.Get<ThreadDef>(t) ?? throw new WeaveCompileException($"Unknown thread '{t}'."));
        }
        if (threads.Count < form.MinThreads || threads.Count > form.MaxThreads)
        {
            throw new WeaveCompileException($"Form '{form.Id}' takes {form.MinThreads}..{form.MaxThreads} threads; got {threads.Count}.");
        }
        foreach (var (k, v) in form.Attributes)
        {
            bases.TryAdd(k, v);
        }
        var tags = form.Tags.With("weave", "form." + form.Id);
        foreach (var t in threads)
        {
            tags = tags.Union(t.Tags).With(t.Id, "thread." + t.Id);
        }
        var mechanisms = form.Mechanisms;

        var glyphs = new List<GlyphDef>();
        foreach (var gid in glyphIds)
        {
            var g = _content.Get<GlyphDef>(gid) ?? throw new WeaveCompileException($"Unknown glyph '{gid}'.");
            glyphs.Add(g);
        }
        if (glyphs.Count > form.MaxGlyphs)
        {
            throw new WeaveCompileException($"Form '{form.Id}' takes at most {form.MaxGlyphs} glyphs; got {glyphs.Count}.");
        }
        // Tags and mechanisms from every glyph are visible to every gate: a glyph may enable another.
        foreach (var g in glyphs)
        {
            tags = tags.Union(g.AddsTags).With("glyph." + g.Id);
            mechanisms = mechanisms.Union(g.AddsMechanisms);
        }
        foreach (var g in glyphs)
        {
            if (!g.Requires.IsEmpty && !tags.HasAny(g.Requires))
            {
                throw new WeaveCompileException($"Glyph '{g.Id}' needs one of {g.Requires} on the weave.");
            }
            if (!g.Excludes.IsEmpty && tags.HasAny(g.Excludes))
            {
                throw new WeaveCompileException($"Glyph '{g.Id}' cannot be used with {g.Excludes}.");
            }
            if (!g.RequiresMechanisms.IsEmpty && !mechanisms.HasAll(g.RequiresMechanisms))
            {
                throw new WeaveCompileException($"Glyph '{g.Id}' needs the weave to have {g.RequiresMechanisms}.");
            }
            var levelsAbove = Math.Max(0, spec.Level - 1);
            modifiers.AddRange(g.Modifiers.Select(m => m.WithSource("glyph:" + g.Id)));
            if (levelsAbove > 0)
            {
                modifiers.AddRange(g.PerLevel.Select(m => m.Scaled(levelsAbove).WithSource("glyph:" + g.Id)));
            }
            extraEffects.AddRange(g.AddsEffects);
        }
        var compileCtx = new EffectCompileContext
        {
            Content = _content,
            Registry = _effects,
            Threads = threads.Select(t => t.Id).ToList(),
            Owner = $"weave {spec}",
        };
        var effects = new List<CompiledEffect>();
        try
        {
            effects.AddRange(_effects.CompileAll(form.Effects, compileCtx));
            effects.AddRange(_effects.CompileAll(extraEffects, compileCtx));
        }
        catch (InvalidOperationException ex)
        {
            throw new WeaveCompileException(ex.Message);
        }
        if (name.Length == 0)
        {
            name = $"{string.Join(" ", threads.Select(t => t.DisplayName))} {form.DisplayName}".Trim();
        }
        return new CompiledWeave
        {
            Spec = spec,
            Form = form,
            Threads = threads,
            Glyphs = glyphs,
            Tags = tags,
            Mechanisms = mechanisms,
            Bases = bases,
            Modifiers = modifiers,
            Effects = effects,
            Costs = form.Costs,
            Level = Math.Max(1, spec.Level),
            Name = name,
            Look = look ?? threads.FirstOrDefault()?.Color,
            Ai = ai ?? form.Ai,
        };
    }
}
