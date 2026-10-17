using System.Text.Json;
using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Effects;
using HollowWake.Core.Expressions;
using HollowWake.Core.Sim;
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
        CastMode.Charge or CastMode.Overcharge or CastMode.Guard => Held,
        _ => form.CastTime is null || (form.CastTime.IsConstant && form.CastTime.ConstantValue <= 0) ? Instant : Timed,
    };
}

/// <summary>The named sections of a weave's graph besides its main list.</summary>
public static class WeaveSections
{
    /// <summary>A channel's release payload.</summary>
    public const string Release = "release";
    /// <summary>A guard's bash.</summary>
    public const string Bash = "bash";
    /// <summary>What a toggle runs when it turns off.</summary>
    public const string Deactivate = "deactivate";
    /// <summary>A cycle's payload.</summary>
    public const string Cycle = "cycle";
}

/// <summary>One trigger a compiled weave carries: its spec, its kind, and the form or glyph it came from.</summary>
public sealed record WeaveTrigger(TriggerSpec Spec, TriggerKind Kind, string Source, int Index)
{
    /// <summary>The slot state names of this trigger's internal cooldown and damage accumulator.</summary>
    public string IcdKey { get; } = "trigger." + Index + ".icd";
    public string AccumKey { get; } = "trigger." + Index + ".life";
}

/// <summary>
/// The casting side of a compiled weave, resolved once from the form and the active glyphs: triggers,
/// gates, the use bank, the charge spend, slot behaviors, worn modifiers and the compiled sections. Where a
/// weave holds only one of a kind the first active glyph in socket order wins over later glyphs and over
/// the form; triggers, gates, metas, follow-ups and worn modifiers add up.
/// </summary>
public sealed class WeaveCasting
{
    public static readonly WeaveCasting None = new();

    public IReadOnlyList<WeaveTrigger> Triggers { get; init; } = Array.Empty<WeaveTrigger>();
    public IReadOnlyList<Expr> Gates { get; init; } = Array.Empty<Expr>();
    public UsesSpec? Uses { get; init; }
    public SpendSpec? Spend { get; init; }
    public ConvertSpec? Convert { get; init; }
    public IReadOnlyList<MetaSpec> Metas { get; init; } = Array.Empty<MetaSpec>();
    public IReadOnlyList<FollowUpSpec> FollowUps { get; init; } = Array.Empty<FollowUpSpec>();
    public CycleSpec? Cycle { get; init; }
    public SelfStackSpec? SelfStack { get; init; }
    public ComboSpec? Chain { get; init; }
    public IReadOnlyList<Modifier> EquipModifiers { get; init; } = Array.Empty<Modifier>();
    public IReadOnlyList<CompiledEffect> Release { get; init; } = Array.Empty<CompiledEffect>();
    public IReadOnlyList<CompiledEffect> Bash { get; init; } = Array.Empty<CompiledEffect>();
    public IReadOnlyList<CompiledEffect> Deactivate { get; init; } = Array.Empty<CompiledEffect>();
    public IReadOnlyList<CompiledEffect> CycleEffects { get; init; } = Array.Empty<CompiledEffect>();
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

    /// <summary>Glyphs a slot graft added beyond the sockets (attribute family <c>glyph.slot{n}.{glyph}</c>).</summary>
    public IReadOnlyList<GlyphRef> Grafts { get; init; } = Array.Empty<GlyphRef>();

    /// <summary>The mechanisms the form and the active glyphs declare, before probes.</summary>
    public TagSet DeclaredMechanisms { get; init; } = TagSet.Empty;

    /// <summary>The cast-scope tag among <see cref="Tags"/>: <c>cast.instant</c>, <c>cast.timed</c>, <c>cast.channel</c> or <c>cast.held</c>.</summary>
    public string CastScope { get; init; } = CastScopes.Instant;

    /// <summary>Levels the active glyphs add to the spec's level.</summary>
    public int LevelBonus { get; init; }

    /// <summary>The root effect list after patches and hooks, as the effects were compiled from it.</summary>
    public IReadOnlyList<TypedSpec> Specs { get; init; } = Array.Empty<TypedSpec>();

    /// <summary>When the cooldown clock starts, <c>press</c> or <c>complete</c>: the preset's, else the form's; null takes the rule.</summary>
    public string? CooldownAt { get; init; }

    /// <summary>Triggers, gates, banks, slot behaviors and compiled sections.</summary>
    public WeaveCasting Casting { get; init; } = WeaveCasting.None;

    internal WeaveProbe? Probe { get; init; }

    /// <summary>What the weave runtime keeps for this weave: its cost entries and their tag contexts, built on first use.</summary>
    internal WeavePlan? Plan;

    /// <summary>For a view with cast-local modifiers, the weave it was made from; null for a compiled weave.</summary>
    internal CompiledWeave? Origin { get; init; }

    /// <summary>The compiled weave itself: a view's origin, or this.</summary>
    public CompiledWeave Root => Origin ?? this;

    private string[]? _forbidFlags;
    private string? _key;

    /// <summary>The flag that forbids each of the weave's tags (<c>forbid:{tag}</c>), in tag order, built once.</summary>
    public IReadOnlyList<string> ForbidFlags => _forbidFlags ??= Origin is { } origin ? (string[])origin.ForbidFlags : BuildForbidFlags();

    private string[] BuildForbidFlags()
    {
        var tags = Tags.Items;
        var flags = new string[tags.Count];
        for (var i = 0; i < flags.Length; i++)
        {
            flags[i] = "forbid:" + tags[i];
        }
        return flags;
    }

    /// <summary>The spec as text, built once: cooldowns, random streams and events key by it.</summary>
    public string Key => _key ??= Origin?.Key ?? Spec.ToString();

    public Targeting Targeting => Form.Targeting;
    public CastMode CastMode => Form.CastMode;

    /// <summary>True when every socketed glyph is active.</summary>
    public bool Fits => Dormant.Count == 0;

    /// <summary>True when the weave casts itself on events rather than when pressed.</summary>
    public bool IsTriggered => Casting.Triggers.Count > 0;

    /// <summary>True for a weave that is pressed on and off.</summary>
    public bool IsToggle => Form.Toggle is not null;

    /// <summary>True for the modes a caster holds: channels, charge-ups, overcharges and guards.</summary>
    public bool IsHeld => Form.CastMode is CastMode.Channel or CastMode.Charge or CastMode.Overcharge or CastMode.Guard;

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

    /// <summary>Whether the weave's own bases or modifiers name an attribute.</summary>
    internal bool Mentions(string attribute)
    {
        if (Bases.ContainsKey(attribute))
        {
            return true;
        }
        var mods = Modifiers;
        for (var i = 0; i < mods.Count; i++)
        {
            if (mods[i].Attribute == attribute)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// <see cref="Attr(Stats, string, TagSet?)"/> for an attribute that is usually untouched: folded only when
    /// the weave, the caster's stack or the attribute's declaration could move it off zero, else zero without
    /// a fold, so the many casts that never meet it pay nothing for it.
    /// </summary>
    internal double AttrOrZero(Stats caster, string attribute)
    {
        if (Mentions(attribute) || caster.Attributes.HasModifiers(attribute) || caster.Attributes.TryGetBaseOverride(attribute, out _))
        {
            return Attr(caster, attribute);
        }
        var def = caster.Attributes.Catalog.Get(attribute);
        return def is null || (def.Formula is null && def.Base == 0) ? 0 : Attr(caster, attribute);
    }

    /// <summary>A weave attribute with the base given here when neither the form nor the preset sets one: the spec's own number.</summary>
    public double Attr(Stats caster, string attribute, double fallbackBase)
    {
        var baseValue = Bases.TryGetValue(attribute, out var b) ? b : fallbackBase;
        return caster.Attributes.Get(attribute, caster, Tags, Modifiers, baseValue);
    }

    public double Range(Stats caster) => Form.Range is null ? Attr(caster, "range") : Form.Range.Evaluate(ScopeFor(caster));
    public double CastTime(Stats caster) => Form.CastTime is null ? 0 : Math.Max(0, Form.CastTime.Evaluate(ScopeFor(caster)));

    /// <summary>The cooldown before recovery: the form's value plus attribute <c>cooldown.added</c> in the weave's scope.</summary>
    public double Cooldown(Stats caster) => Math.Max(0, (Form.Cooldown is null ? 0 : Form.Cooldown.Evaluate(ScopeFor(caster))) + AttrOrZero(caster, "cooldown.added"));

    public IExprScope ScopeFor(Stats caster)
    {
        var overlay = new ScopeOverlay(caster);
        overlay.Mount("weave", new WeaveScope(this, caster, Tags));
        overlay.Set("level", caster.Level);
        return overlay;
    }

    /// <summary>
    /// A view of this weave whose modifiers are <paramref name="modifiers"/> (the weave's own followed by a
    /// cast's local ones): everything else, its key and its forbid flags are shared. The weave runtime builds
    /// one per beat of a cast that carries local modifiers, so ramps and charge scales join every fold of
    /// that beat's effects and nothing else.
    /// </summary>
    internal CompiledWeave WithModifiers(IReadOnlyList<Modifier> modifiers) => new()
    {
        Spec = Spec,
        Form = Form,
        Threads = Threads,
        Glyphs = Glyphs,
        Tags = Tags,
        Mechanisms = Mechanisms,
        Bases = Bases,
        Modifiers = modifiers,
        Effects = Effects,
        Costs = Costs,
        Level = Level,
        Name = Name,
        Look = Look,
        Ai = Ai,
        GlyphRefs = GlyphRefs,
        Dormant = Dormant,
        Grafts = Grafts,
        DeclaredMechanisms = DeclaredMechanisms,
        CastScope = CastScope,
        LevelBonus = LevelBonus,
        Specs = Specs,
        CooldownAt = CooldownAt,
        Casting = Casting,
        Probe = Probe,
        Origin = Root,
    };

    public override string ToString() => Name.Length > 0 ? Name : Key;
}

/// <summary>
/// Turns specs into compiled weaves. In order: the preset, form and Threads resolve; glyphs resolve at
/// their levels with their rolled rows, slot grafts joining after the sockets; then, to a fixpoint, the
/// active glyphs' tags, mechanisms, modifiers, patches and hooks build the weave, the mechanism probes run
/// on it, and the last active glyph in socket order whose gate fails goes dormant (in strict mode the first
/// misfit fails the compile). Glyphs that enable each other through tags, mechanisms or what their patches
/// add therefore self-lift, and where glyphs exclude each other the earlier socket stays active.
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

    public CompiledWeave Compile(WeaveSpec spec, GlyphFit fit) => Compile(spec, fit, null);

    /// <summary>
    /// Compiles a spec with glyphs grafted onto it beyond its sockets: they do not count against the form's
    /// glyph limit, a socketed copy of the same glyph wins over a graft, and a misfit graft goes dormant.
    /// </summary>
    public CompiledWeave Compile(WeaveSpec spec, GlyphFit fit, IReadOnlyList<GlyphRef>? grafts)
    {
        var key = CacheKey(spec, fit, grafts);
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }
        var compiled = Build(spec, fit, grafts);
        _cache[key] = compiled;
        return compiled;
    }

    public bool TryCompile(WeaveSpec spec, out CompiledWeave? weave, out string? error, GlyphFit fit = GlyphFit.Strict) => TryCompile(spec, out weave, out error, fit, null);

    public bool TryCompile(WeaveSpec spec, out CompiledWeave? weave, out string? error, GlyphFit fit, IReadOnlyList<GlyphRef>? grafts)
    {
        try
        {
            weave = Compile(spec, fit, grafts);
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

    private static string CacheKey(WeaveSpec spec, GlyphFit fit, IReadOnlyList<GlyphRef>? grafts)
    {
        var key = $"{spec.Preset}|{spec.Form}|{string.Join(",", spec.Threads)}|{string.Join(",", spec.Glyphs)}|{spec.Level}|{spec.Name}|{(int)fit}";
        return grafts is { Count: > 0 } ? key + "|g:" + string.Join(",", grafts) : key;
    }

    /// <summary>A glyph as socketed: its reference, definition, level and the rows it rolled.</summary>
    private sealed record Socketed(GlyphRef Ref, GlyphDef Def, int Level, IReadOnlyList<GlyphRow> Rows, bool Graft);

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

    private CompiledWeave Build(WeaveSpec spec, GlyphFit fit, IReadOnlyList<GlyphRef>? grafts)
    {
        var tracked = spec.Preset is not null && _building.Add(spec.Preset);
        try
        {
            return BuildCore(spec, fit, grafts);
        }
        finally
        {
            if (tracked)
            {
                _building.Remove(spec.Preset!);
            }
        }
    }

    private CompiledWeave BuildCore(WeaveSpec spec, GlyphFit fit, IReadOnlyList<GlyphRef>? grafts)
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
            glyphs.Add(new Socketed(gref, g, Math.Clamp(gref.Level ?? g.Level, 1, maxLevel), RowsOf(gref, g), Graft: false));
        }
        if (glyphs.Count > form.MaxGlyphs)
        {
            throw new WeaveCompileException($"Form '{form.Id}' takes at most {form.MaxGlyphs} glyphs; got {glyphs.Count}.");
        }
        var grafted = new List<GlyphRef>();
        if (grafts is not null)
        {
            foreach (var gref in grafts)
            {
                // A socketed copy wins over a graft of the same glyph.
                if (glyphs.Exists(s => s.Ref.Id == gref.Id))
                {
                    continue;
                }
                if (_content.Get<GlyphDef>(gref.Id) is not { } g)
                {
                    continue;
                }
                glyphs.Add(new Socketed(gref, g, Math.Clamp(gref.Level ?? g.Level, 1, maxLevel), RowsOf(gref, g), Graft: true));
                grafted.Add(gref);
            }
        }

        // Every glyph starts active. While some active glyph's gate fails, the last such glyph in socket order
        // goes dormant (so where glyphs exclude each other the earlier socket keeps its place) and the weave is
        // rebuilt without it. Only removals happen, so this ends. Strict mode fails on the first socketed
        // misfit instead; a graft that does not fit always goes dormant.
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
                if (!active[i] || Gate(glyphs[i], state) is not { } why)
                {
                    continue;
                }
                if (fit == GlyphFit.Strict && !glyphs[i].Graft)
                {
                    throw new WeaveCompileException(why);
                }
                failing = i;
                reason = why;
            }
            if (failing < 0)
            {
                break;
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
        IReadOnlyList<CompiledEffect> release, bash, deactivate, cycle;
        try
        {
            specs = state.Graph.ToSpecs();
            effects.AddRange(_effects.CompileAll(specs, compileCtx));
            release = Section(state.Graph, WeaveSections.Release, compileCtx);
            bash = Section(state.Graph, WeaveSections.Bash, compileCtx);
            deactivate = Section(state.Graph, WeaveSections.Deactivate, compileCtx);
            cycle = Section(state.Graph, WeaveSections.Cycle, compileCtx);
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
            Grafts = grafted,
            DeclaredMechanisms = state.Declared,
            CastScope = castScope,
            LevelBonus = state.LevelBonus,
            Specs = specs,
            CooldownAt = preset?.CooldownAt ?? form.CooldownAt,
            Casting = CastingOf(form, activeGlyphs, release, bash, deactivate, cycle),
            Probe = state.Probe,
        };
    }

    private IReadOnlyList<CompiledEffect> Section(WeaveGraph graph, string section, EffectCompileContext ctx) =>
        graph.HasSection(section) ? _effects.CompileAll(graph.ToSpecs(section), ctx).ToArray() : Array.Empty<CompiledEffect>();

    /// <summary>The casting fields of the form and the active glyphs, combined.</summary>
    private static WeaveCasting CastingOf(FormDef form, List<GlyphDef> glyphs, IReadOnlyList<CompiledEffect> release, IReadOnlyList<CompiledEffect> bash, IReadOnlyList<CompiledEffect> deactivate, IReadOnlyList<CompiledEffect> cycle)
    {
        var triggers = new List<WeaveTrigger>();
        var gates = new List<Expr>(form.Gates);
        var metas = new List<MetaSpec>();
        var followUps = new List<FollowUpSpec>();
        var equip = new List<Modifier>(form.EquipModifiers);
        if (form.Trigger is { } formTrigger)
        {
            triggers.Add(new WeaveTrigger(formTrigger, KindOf(formTrigger, form.Id), "form", triggers.Count));
        }
        if (form.MetaAction is { } formMeta)
        {
            metas.Add(formMeta);
        }
        if (form.FollowUp is { } formFollow)
        {
            followUps.Add(formFollow);
        }
        UsesSpec? uses = null;
        SpendSpec? spend = null;
        ConvertSpec? convert = null;
        CycleSpec? cycleSpec = null;
        SelfStackSpec? selfStack = null;
        ComboSpec? chain = null;
        foreach (var g in glyphs)
        {
            if (g.Trigger is { } trigger)
            {
                triggers.Add(new WeaveTrigger(trigger, KindOf(trigger, g.Id), g.Id, triggers.Count));
            }
            gates.AddRange(g.Gates);
            if (g.MetaAction is { } meta)
            {
                metas.Add(meta);
            }
            if (g.FollowUp is { } follow)
            {
                followUps.Add(follow);
            }
            equip.AddRange(g.EquipModifiers);
            uses ??= g.Uses;
            spend ??= g.Spend;
            convert ??= g.Convert;
            cycleSpec ??= g.Cycle;
            selfStack ??= g.SelfStack;
            chain ??= g.Chain;
        }
        var nothing = triggers.Count == 0 && gates.Count == 0 && metas.Count == 0 && followUps.Count == 0 && equip.Count == 0
            && uses is null && form.Uses is null && spend is null && form.Spend is null && convert is null && form.Convert is null
            && cycleSpec is null && form.Cycle is null && selfStack is null && form.SelfStack is null && chain is null && form.Chain is null
            && release.Count == 0 && bash.Count == 0 && deactivate.Count == 0 && cycle.Count == 0;
        if (nothing)
        {
            return WeaveCasting.None;
        }
        return new WeaveCasting
        {
            Triggers = triggers,
            Gates = gates,
            Uses = uses ?? form.Uses,
            Spend = spend ?? form.Spend,
            Convert = convert ?? form.Convert,
            Metas = metas,
            FollowUps = followUps,
            Cycle = cycleSpec ?? form.Cycle,
            SelfStack = selfStack ?? form.SelfStack,
            Chain = chain ?? form.Chain,
            EquipModifiers = equip,
            Release = release,
            Bash = bash,
            Deactivate = deactivate,
            CycleEffects = cycle,
        };
    }

    private static TriggerKind KindOf(TriggerSpec trigger, string owner) =>
        TriggerKinds.TryGet(trigger.On, out var kind) ? kind : throw new WeaveCompileException($"'{owner}' triggers on unknown kind '{trigger.On}'.");

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
        var activeDefs = new List<GlyphDef>();
        for (var i = 0; i < glyphs.Count; i++)
        {
            if (!active[i])
            {
                continue;
            }
            var (_, g, level, rows, _) = glyphs[i];
            activeDefs.Add(g);
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

        // The graph: the form's and preset's effects take patches in glyph order, and so do the form's
        // sections (a channel's release, a guard's bash, a toggle's deactivation, a cycle's payload); glyphs'
        // added effects join the root; hooks append into every node exposing them; on_cast lists close the root.
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
        if (form.Channel?.Release is { } release)
        {
            foreach (var s in release.Effects)
            {
                graph.Add(s, patchable: true, WeaveSections.Release);
            }
        }
        if (form.Guard?.Bash is { } bash)
        {
            foreach (var s in bash.Effects)
            {
                graph.Add(s, patchable: true, WeaveSections.Bash);
            }
        }
        if (form.Toggle is { } toggle)
        {
            foreach (var s in toggle.OnDeactivate)
            {
                graph.Add(s, patchable: true, WeaveSections.Deactivate);
            }
        }
        // The first active glyph's cycle wins over the form's.
        var cycleFromGlyph = activeDefs.Find(g => g.Cycle is not null)?.Cycle;
        if ((cycleFromGlyph ?? form.Cycle) is { } cycle)
        {
            foreach (var s in cycle.Effects)
            {
                graph.Add(s, patchable: cycleFromGlyph is null, WeaveSections.Cycle);
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
            Glyphs = activeDefs,
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
