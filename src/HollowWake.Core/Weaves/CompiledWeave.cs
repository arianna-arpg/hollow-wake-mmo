using System.Text.Json;
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

/// <summary>How the compiler treats a socketed glyph whose gate fails.</summary>
public enum GlyphFit
{
    /// <summary>The weave does not compile; the error names the first misfit in socket order.</summary>
    Strict,
    /// <summary>The misfit stays socketed but dormant with its reason, and the weave compiles without it.</summary>
    Dormant,
}

/// <summary>A socketed glyph that contributes nothing, not even costs, because its gate fails; and why.</summary>
public sealed record DormantGlyph(GlyphRef Ref, GlyphDef Glyph, string Reason);

/// <summary>The cast-scope tags the compiler adds from a form's cast mode and base cast time.</summary>
public static class CastScopes
{
    public const string Instant = "cast.instant";
    public const string Timed = "cast.timed";
    public const string Channel = "cast.channel";
    public const string Held = "cast.held";

    /// <summary>The scope of a form: channels and held modes by mode; a cast with no base time, or an instant form, is instant.</summary>
    public static string For(FormDef form) => form.CastMode switch
    {
        CastMode.Instant => Instant,
        CastMode.Channel => Channel,
        CastMode.Charge => Held,
        _ => form.CastTime is null || (form.CastTime.IsConstant && form.CastTime.ConstantValue <= 0) ? Instant : Timed,
    };
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
    /// <summary>The active glyphs, in socket order. Dormant glyphs are in <see cref="Dormant"/>.</summary>
    public required IReadOnlyList<GlyphDef> Glyphs { get; init; }
    public required TagSet Tags { get; init; }
    /// <summary>The declared mechanisms plus every probe that holds and every parameterized requirement met.</summary>
    public required TagSet Mechanisms { get; init; }
    public required IReadOnlyDictionary<string, double> Bases { get; init; }
    public required IReadOnlyList<Modifier> Modifiers { get; init; }
    public required IReadOnlyList<CompiledEffect> Effects { get; init; }
    public required IReadOnlyDictionary<string, Expr> Costs { get; init; }
    /// <summary>The effective level: the spec's level plus the level bonuses of the active glyphs.</summary>
    public int Level { get; init; } = 1;
    public string Name { get; init; } = "";
    public string? Look { get; init; }
    public AiHint? Ai { get; init; }

    /// <summary>The active glyphs as socketed, with their levels and rolled rows, in socket order.</summary>
    public IReadOnlyList<GlyphRef> GlyphRefs { get; init; } = Array.Empty<GlyphRef>();

    /// <summary>Socketed glyphs whose gates fail, in socket order, each with its reason.</summary>
    public IReadOnlyList<DormantGlyph> Dormant { get; init; } = Array.Empty<DormantGlyph>();

    /// <summary>The mechanisms the form and the active glyphs declare, before probes.</summary>
    public TagSet DeclaredMechanisms { get; init; } = TagSet.Empty;

    /// <summary>The cast-scope tag among <see cref="Tags"/>: <c>cast.instant</c>, <c>cast.timed</c>, <c>cast.channel</c> or <c>cast.held</c>.</summary>
    public string CastScope { get; init; } = CastScopes.Instant;

    /// <summary>Levels the active glyphs add to the spec's level.</summary>
    public int LevelBonus { get; init; }

    /// <summary>The root effect list after patches and hooks, as the effects were compiled from it.</summary>
    public IReadOnlyList<TypedSpec> Specs { get; init; } = Array.Empty<TypedSpec>();

    internal WeaveProbe? Probe { get; init; }

    public string Key => Spec.ToString();
    public Targeting Targeting => Form.Targeting;
    public CastMode CastMode => Form.CastMode;

    /// <summary>True when every socketed glyph is active.</summary>
    public bool Fits => Dormant.Count == 0;

    /// <summary>Whether the weave has a mechanism, including parameterized probes such as <c>affliction:bleeding</c>.</summary>
    public bool HasMechanism(string mechanism) =>
        Mechanisms.Has(mechanism) || (Probe is not null && mechanism.Contains(':') && MechanismRegistry.Holds(Probe, mechanism));

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

/// <summary>
/// Turns specs into compiled weaves. In order: the preset, form and Threads resolve; glyphs resolve at
/// their levels with their rolled rows; then, to a fixpoint, the active glyphs' tags, mechanisms,
/// modifiers, patches and hooks build the weave, the mechanism probes run on it, and the first active
/// glyph in socket order whose gate fails goes dormant (or, in strict mode, fails the compile). Glyphs
/// that enable each other through tags, mechanisms or what their patches add therefore self-lift.
/// </summary>
public sealed class WeaveCompiler
{
    private readonly ContentRegistry _content;
    private readonly EffectRegistry _effects;
    private readonly Dictionary<string, CompiledWeave> _cache = new(StringComparer.Ordinal);
    // Presets being compiled up the current chain; a cast effect naming one resolves it later instead.
    private readonly HashSet<string> _building = new(StringComparer.Ordinal);

    public WeaveCompiler(ContentRegistry content, EffectRegistry effects)
    {
        _content = content;
        _effects = effects;
    }

    public ContentRegistry Content => _content;

    public EffectRegistry Effects => _effects;

    /// <summary>True while the preset is being compiled further up the current compile.</summary>
    public bool IsBuilding(string preset) => _building.Contains(preset);

    public CompiledWeave Compile(WeaveSpec spec) => Compile(spec, GlyphFit.Strict);

    public CompiledWeave Compile(WeaveSpec spec, GlyphFit fit)
    {
        var key = CacheKey(spec, fit);
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }
        var compiled = Build(spec, fit);
        _cache[key] = compiled;
        return compiled;
    }

    public bool TryCompile(WeaveSpec spec, out CompiledWeave? weave, out string? error, GlyphFit fit = GlyphFit.Strict)
    {
        try
        {
            weave = Compile(spec, fit);
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

    private static string CacheKey(WeaveSpec spec, GlyphFit fit) =>
        $"{spec.Preset}|{spec.Form}|{string.Join(",", spec.Threads)}|{string.Join(",", spec.Glyphs)}|{spec.Level}|{spec.Name}|{(int)fit}";

    /// <summary>A glyph as socketed: its reference, definition, level and the rows it rolled.</summary>
    private sealed record Socketed(GlyphRef Ref, GlyphDef Def, int Level, IReadOnlyList<GlyphRow> Rows);

    /// <summary>The weave as it stands with one set of active glyphs.</summary>
    private sealed class Evaluation
    {
        public required TagSet Tags;
        public required TagSet Declared;
        public required TagSet Mechanisms;
        public required List<Modifier> Modifiers;
        public required WeaveGraph Graph;
        public required WeaveProbe Probe;
        public required int LevelBonus;
        public readonly List<string> Satisfied = new();
    }

    private CompiledWeave Build(WeaveSpec spec, GlyphFit fit)
    {
        var tracked = spec.Preset is not null && _building.Add(spec.Preset);
        try
        {
            return BuildCore(spec, fit);
        }
        finally
        {
            if (tracked)
            {
                _building.Remove(spec.Preset!);
            }
        }
    }

    private CompiledWeave BuildCore(WeaveSpec spec, GlyphFit fit)
    {
        WeaveDef? preset = null;
        var formId = spec.Form;
        var threadIds = spec.Threads.ToList();
        var glyphRefs = spec.Glyphs.ToList();
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
            glyphRefs = DistinctById(preset.Glyphs.Concat(glyphRefs));
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
        var bases = new Dictionary<string, double>(StringComparer.Ordinal);
        if (preset is not null)
        {
            foreach (var (k, v) in preset.Attributes)
            {
                bases[k] = v;
            }
        }
        foreach (var (k, v) in form.Attributes)
        {
            bases.TryAdd(k, v);
        }
        var castScope = CastScopes.For(form);
        var baseTags = form.Tags.With("weave", "form." + form.Id);
        foreach (var t in threads)
        {
            baseTags = baseTags.Union(t.Tags).With(t.Id, "thread." + t.Id);
        }
        baseTags = baseTags.With(castScope);

        var maxLevel = Math.Max(1, (int)_content.Rules.Number("glyphs.max_level", 20));
        var glyphs = new List<Socketed>();
        foreach (var gref in glyphRefs)
        {
            var g = _content.Get<GlyphDef>(gref.Id) ?? throw new WeaveCompileException($"Unknown glyph '{gref.Id}'.");
            glyphs.Add(new Socketed(gref, g, Math.Clamp(gref.Level ?? g.Level, 1, maxLevel), RowsOf(gref, g)));
        }
        if (glyphs.Count > form.MaxGlyphs)
        {
            throw new WeaveCompileException($"Form '{form.Id}' takes at most {form.MaxGlyphs} glyphs; got {glyphs.Count}.");
        }

        // Every glyph starts active; the first misfit in socket order goes dormant and the weave is rebuilt
        // without it, until every active glyph's gate holds. Only removals happen, so this ends.
        var active = new bool[glyphs.Count];
        Array.Fill(active, true);
        var reasons = new string?[glyphs.Count];
        Evaluation state;
        while (true)
        {
            state = Evaluate(preset, form, threads, baseTags, bases, glyphs, active);
            var failing = -1;
            string? reason = null;
            for (var i = 0; i < glyphs.Count; i++)
            {
                if (!active[i])
                {
                    continue;
                }
                reason = Gate(glyphs[i], state);
                if (reason is not null)
                {
                    failing = i;
                    break;
                }
            }
            if (failing < 0)
            {
                break;
            }
            if (fit == GlyphFit.Strict)
            {
                throw new WeaveCompileException(reason!);
            }
            active[failing] = false;
            reasons[failing] = reason;
        }

        var compileCtx = new EffectCompileContext
        {
            Content = _content,
            Registry = _effects,
            Threads = threads.Select(t => t.Id).ToList(),
            Owner = $"weave {spec}",
            Compiler = this,
        };
        List<TypedSpec> specs;
        var effects = new List<CompiledEffect>();
        try
        {
            specs = state.Graph.ToSpecs();
            effects.AddRange(_effects.CompileAll(specs, compileCtx));
        }
        catch (InvalidOperationException ex)
        {
            throw new WeaveCompileException(ex.Message);
        }
        catch (JsonException ex)
        {
            throw new WeaveCompileException(ex.Message);
        }
        if (name.Length == 0)
        {
            name = $"{string.Join(" ", threads.Select(t => t.DisplayName))} {form.DisplayName}".Trim();
        }
        var activeGlyphs = new List<GlyphDef>();
        var activeRefs = new List<GlyphRef>();
        var dormant = new List<DormantGlyph>();
        for (var i = 0; i < glyphs.Count; i++)
        {
            if (active[i])
            {
                activeGlyphs.Add(glyphs[i].Def);
                activeRefs.Add(glyphs[i].Ref);
            }
            else
            {
                dormant.Add(new DormantGlyph(glyphs[i].Ref, glyphs[i].Def, reasons[i]!));
            }
        }
        var mechanisms = state.Satisfied.Count == 0 ? state.Mechanisms : state.Mechanisms.Union(TagSet.From(state.Satisfied));
        return new CompiledWeave
        {
            Spec = spec,
            Form = form,
            Threads = threads,
            Glyphs = activeGlyphs,
            Tags = state.Tags,
            Mechanisms = mechanisms,
            Bases = bases,
            Modifiers = state.Modifiers,
            Effects = effects,
            Costs = form.Costs,
            Level = Math.Max(1, Math.Max(1, spec.Level) + state.LevelBonus),
            Name = name,
            Look = look ?? threads.FirstOrDefault()?.Color,
            Ai = ai ?? form.Ai,
            GlyphRefs = activeRefs,
            Dormant = dormant,
            DeclaredMechanisms = state.Declared,
            CastScope = castScope,
            LevelBonus = state.LevelBonus,
            Specs = specs,
            Probe = state.Probe,
        };
    }

    /// <summary>The weave with the active glyphs: tags, declared and probed mechanisms, modifiers, and the patched, hooked graph.</summary>
    private Evaluation Evaluate(WeaveDef? preset, FormDef form, List<ThreadDef> threads, TagSet baseTags, Dictionary<string, double> bases, List<Socketed> glyphs, bool[] active)
    {
        var tags = baseTags;
        var declared = form.Mechanisms;
        // Tags and mechanisms from every active glyph are visible to every gate: a glyph may enable another.
        for (var i = 0; i < glyphs.Count; i++)
        {
            if (active[i])
            {
                var g = glyphs[i].Def;
                tags = tags.Union(g.AddsTags).With("glyph." + g.Id);
                declared = declared.Union(g.AddsMechanisms);
            }
        }
        var modifiers = new List<Modifier>();
        if (preset is not null)
        {
            modifiers.AddRange(preset.Modifiers.Select(m => m.WithSource("preset:" + preset.Id)));
        }
        var levelBonus = 0;
        for (var i = 0; i < glyphs.Count; i++)
        {
            if (!active[i])
            {
                continue;
            }
            var (_, g, level, rows) = glyphs[i];
            var source = "glyph:" + g.Id;
            modifiers.AddRange(g.Modifiers.Select(m => m.WithSource(source)));
            foreach (var row in rows)
            {
                modifiers.AddRange(row.Modifiers.Select(m => m.WithSource(source)));
            }
            var levelsAbove = level - 1;
            if (levelsAbove > 0)
            {
                modifiers.AddRange(g.PerLevel.Select(m => m.Scaled(levelsAbove).WithSource(source)));
            }
            levelBonus += g.LevelBonus + (int)Math.Floor(g.LevelBonusPerLevel * levelsAbove);
        }

        // The graph: the form's and preset's effects take patches in glyph order; glyphs' added effects
        // join the root; hooks append into every node exposing them; on_cast lists close the root.
        var graph = new WeaveGraph(_effects);
        foreach (var s in form.Effects)
        {
            graph.Add(s, patchable: true);
        }
        if (preset is not null)
        {
            foreach (var s in preset.Effects)
            {
                graph.Add(s, patchable: true);
            }
        }
        var patcher = new EffectPatcher(graph);
        for (var i = 0; i < glyphs.Count; i++)
        {
            if (!active[i])
            {
                continue;
            }
            foreach (var patch in glyphs[i].Def.Patches)
            {
                try
                {
                    patcher.Apply(patch, glyphs[i].Def.Id);
                }
                catch (InvalidOperationException ex)
                {
                    throw new WeaveCompileException(ex.Message);
                }
            }
        }
        for (var i = 0; i < glyphs.Count; i++)
        {
            if (active[i])
            {
                foreach (var s in glyphs[i].Def.AddsEffects)
                {
                    graph.Add(s, patchable: false);
                }
            }
        }
        var hooksUsed = false;
        for (var i = 0; i < glyphs.Count && !hooksUsed; i++)
        {
            hooksUsed = active[i] && glyphs[i].Def.Hooks.Count > 0;
        }
        if (hooksUsed)
        {
            var targets = patcher.HookTargets();
            for (var i = 0; i < glyphs.Count; i++)
            {
                if (!active[i])
                {
                    continue;
                }
                foreach (var (hook, list) in glyphs[i].Def.Hooks)
                {
                    if (hook != EffectHooks.OnCast)
                    {
                        patcher.AppendHooks(targets, hook, list);
                    }
                }
            }
            for (var i = 0; i < glyphs.Count; i++)
            {
                if (active[i] && glyphs[i].Def.Hooks.TryGetValue(EffectHooks.OnCast, out var onCast))
                {
                    foreach (var s in onCast)
                    {
                        graph.Add(s, patchable: false);
                    }
                }
            }
        }

        var probe = new WeaveProbe
        {
            Content = _content,
            Form = form,
            Threads = threads,
            Tags = tags,
            Declared = declared,
            Modifiers = modifiers,
            Bases = bases,
            Graph = graph,
        };
        var mechanisms = declared.Union(TagSet.From(MechanismRegistry.Held(probe)));
        return new Evaluation
        {
            Tags = tags,
            Declared = declared,
            Mechanisms = mechanisms,
            Modifiers = modifiers,
            Graph = graph,
            Probe = probe,
            LevelBonus = levelBonus,
        };
    }

    /// <summary>Null when the glyph's gate holds on the weave as it stands; else the reason it fails.</summary>
    private static string? Gate(Socketed socketed, Evaluation state)
    {
        var g = socketed.Def;
        if (!g.Requires.IsEmpty && !state.Tags.HasAny(g.Requires))
        {
            return $"Glyph '{g.Id}' needs one of {g.Requires} on the weave.";
        }
        if (!g.Excludes.IsEmpty && state.Tags.HasAny(g.Excludes))
        {
            return $"Glyph '{g.Id}' cannot be used with {g.Excludes}.";
        }
        var required = g.RequiresMechanisms;
        foreach (var row in socketed.Rows)
        {
            required = required.Union(row.RequiresMechanisms);
        }
        if (required.IsEmpty)
        {
            return null;
        }
        foreach (var m in required)
        {
            if (state.Mechanisms.Has(m))
            {
                continue;
            }
            if (m.Contains(':') && MechanismRegistry.Holds(state.Probe, m))
            {
                state.Satisfied.Add(m);
                continue;
            }
            return $"Glyph '{g.Id}' needs the weave to have {required}.";
        }
        return null;
    }

    /// <summary>The row a reference rolled on each of the glyph's axes; the first row where it names none.</summary>
    private static IReadOnlyList<GlyphRow> RowsOf(GlyphRef gref, GlyphDef g)
    {
        foreach (var axis in gref.Rows.Keys)
        {
            if (!g.Axes.Any(a => a.Id == axis))
            {
                throw new WeaveCompileException($"Glyph '{g.Id}' has no axis '{axis}'.");
            }
        }
        if (g.Axes.Count == 0)
        {
            return Array.Empty<GlyphRow>();
        }
        var rows = new List<GlyphRow>();
        foreach (var axis in g.Axes)
        {
            if (gref.Rows.TryGetValue(axis.Id, out var rowId))
            {
                rows.Add(axis.Rows.FirstOrDefault(r => r.Id == rowId) ?? throw new WeaveCompileException($"Glyph '{g.Id}' has no row '{rowId}' on axis '{axis.Id}'."));
            }
            else if (axis.Rows.Count > 0)
            {
                rows.Add(axis.Rows[0]);
            }
        }
        return rows;
    }

    private static List<GlyphRef> DistinctById(IEnumerable<GlyphRef> refs)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var list = new List<GlyphRef>();
        foreach (var r in refs)
        {
            if (seen.Add(r.Id))
            {
                list.Add(r);
            }
        }
        return list;
    }
}
