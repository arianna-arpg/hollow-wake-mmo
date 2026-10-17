using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Expressions;
using HollowWake.Core.Maths;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;
using HollowWake.Core.Weaves;

namespace HollowWake.Core.Effects;

/// <summary>
/// Everything an effect needs while executing: who did it, to whom or where, through which weave,
/// with which tags, how deep in the chain, and a scope for its expressions where <c>source.*</c>,
/// <c>target.*</c>, <c>weave.*</c> and named variables resolve.
/// </summary>
public sealed class EffectContext
{
    public required Simulation Sim { get; init; }
    public Entity Source { get; init; } = Entity.None;
    public Entity Target { get; init; } = Entity.None;
    public Vector3 Point { get; init; }
    public Vector2 Direction { get; init; }
    public CompiledWeave? Weave { get; init; }
    public TagSet Tags { get; init; } = TagSet.Empty;

    /// <summary>
    /// How deep in a chain this effect runs. Nested deliveries, repeats, delays and casts of other weaves
    /// run one deeper; <c>effects.max_depth</c> caps it, and damage and status events carry it so triggers
    /// can refuse to chain without limit.
    /// </summary>
    public int Depth { get; init; }

    public required Rng Rng { get; init; }
    public double Potency { get; init; } = 1;

    /// <summary>Multiplies the amounts of damage effects only: a weave cast by a <c>cast</c> effect with <c>damage_scale</c>.</summary>
    public double DamageScale { get; init; } = 1;

    /// <summary>
    /// Where deliveries start when not at the source's body: a weave cast by a <c>cast</c> effect at a point
    /// fires its projectiles, bursts and cones from that point. Null means the source's position.
    /// </summary>
    public Vector3? From { get; init; }

    public Dictionary<string, double> Vars { get; init; } = new(StringComparer.Ordinal);

    /// <summary>The original caster for summoned and chained effects, used for ownership and credit.</summary>
    public Entity Origin
    {
        get => _origin.IsNone ? Source : _origin;
        init => _origin = value;
    }

    private readonly Entity _origin = Entity.None;

    public Stats? SourceStats => Sim.World.Get<Stats>(Source);
    public Stats? TargetStats => Sim.World.Get<Stats>(Target);
    public Transform? SourceTransform => Sim.World.Get<Transform>(Source);
    public Transform? TargetTransform => Sim.World.Get<Transform>(Target);

    /// <summary>Where the effect is centered: the target if any, else the point.</summary>
    public Vector3 Focus => Sim.World.TryGet<Transform>(Target, out var t) ? t.Position : Point;

    private IExprScope? _scope;

    public IExprScope Scope
    {
        get
        {
            if (_scope is not null)
            {
                return _scope;
            }
            var sourceStats = SourceStats;
            var overlay = new ScopeOverlay(sourceStats, Rng);
            if (sourceStats is not null)
            {
                overlay.Mount("source", sourceStats);
                overlay.Mount("caster", sourceStats);
            }
            if (TargetStats is { } ts)
            {
                overlay.Mount("target", ts);
            }
            if (Weave is not null && sourceStats is not null)
            {
                overlay.Mount("weave", new WeaveScope(Weave, sourceStats, Tags));
            }
            foreach (var (k, v) in Vars)
            {
                overlay.Set(k, v);
            }
            if (Sim.World.TryGet<Transform>(Source, out var st) && Sim.World.TryGet<Transform>(Target, out var tt))
            {
                overlay.Set("distance", st.DistanceTo(tt));
            }
            overlay.Set("depth", Depth);
            overlay.Set("time", Sim.Clock.Time);
            _scope = overlay;
            return overlay;
        }
    }

    public double Eval(Expr? expr, double fallback = 0) => expr is null ? fallback : expr.Evaluate(Scope);

    public EffectContext WithTarget(Entity target) => Copy(target: target, point: Sim.World.TryGet<Transform>(target, out var t) ? t.Position : Point);

    public EffectContext WithPoint(Vector3 point) => Copy(point: point);

    public EffectContext Targetless() => Copy(target: Entity.None);

    public EffectContext Deeper() => Copy(depth: Depth + 1);

    public EffectContext WithVars(Action<Dictionary<string, double>> edit)
    {
        var vars = new Dictionary<string, double>(Vars, StringComparer.Ordinal);
        edit(vars);
        return Copy(vars: vars);
    }

    public EffectContext WithTags(TagSet extra) => Copy(tags: Tags.Union(extra));

    public EffectContext WithSource(Entity source) => Copy(source: source);

    /// <summary>
    /// A detached copy for work that runs later: its own copy of the variables, so effects still running
    /// now cannot change what it reads, and a scope built fresh when it first evaluates, so it reads the
    /// stats of the moment it runs.
    /// </summary>
    public EffectContext Fork(int deeper = 0) => Copy(depth: Depth + deeper, vars: new Dictionary<string, double>(Vars, StringComparer.Ordinal));

    private EffectContext Copy(Entity? source = null, Entity? target = null, Vector3? point = null, int? depth = null, Dictionary<string, double>? vars = null, TagSet? tags = null)
    {
        return new EffectContext
        {
            Origin = _origin.IsNone ? Source : _origin,
            Sim = Sim,
            Source = source ?? Source,
            Target = target ?? Target,
            Point = point ?? Point,
            Direction = Direction,
            Weave = Weave,
            Tags = tags ?? Tags,
            Depth = depth ?? Depth,
            Rng = Rng,
            Potency = Potency,
            DamageScale = DamageScale,
            From = From,
            Vars = vars ?? Vars,
        };
    }
}

/// <summary>
/// Names of the variables a weave runtime sets on the contexts of a cast so expressions can read how the
/// cast was made. They are ordinary variables: only casts whose mode sets one can read it, and a formula
/// reading one where it was never set fails like any unknown name.
/// </summary>
public static class CastVars
{
    /// <summary>Seconds a held or channeled cast has been held.</summary>
    public const string Held = "cast.held";

    /// <summary>How full a charge-up or brim was when released, from zero to one.</summary>
    public const string Fill = "cast.fill";

    /// <summary>The stage an overcharge reached.</summary>
    public const string Stage = "cast.stage";

    /// <summary>How many charges a cast spent.</summary>
    public const string ChargesSpent = "charges_spent";

    /// <summary>Every name above, for validators.</summary>
    public static readonly IReadOnlyList<string> All = new[] { Held, Fill, Stage, ChargesSpent };
}

/// <summary>Resolves <c>weave.*</c> names: weave attributes folded on the caster with the weave's local modifiers.</summary>
public sealed class WeaveScope : IExprScope
{
    private readonly CompiledWeave _weave;
    private readonly Stats _caster;
    private readonly TagSet _tags;

    public WeaveScope(CompiledWeave weave, Stats caster, TagSet tags)
    {
        _weave = weave;
        _caster = caster;
        _tags = tags;
    }

    public bool TryResolve(string name, out double value)
    {
        if (name == "level")
        {
            value = _weave.Level;
            return true;
        }
        value = _weave.Attr(_caster, name, _tags);
        return true;
    }

    public bool HasFlag(string flag) => _weave.Tags.Has(flag) || _weave.Mechanisms.Has(flag);
    public double NextRandom() => _caster.NextRandom();
}

/// <summary>What a handler gets at compile time: content, the registry (for nested lists), the weave's threads.</summary>
public sealed class EffectCompileContext
{
    public required ContentRegistry Content { get; init; }
    public required EffectRegistry Registry { get; init; }
    public IReadOnlyList<string> Threads { get; init; } = Array.Empty<string>();
    public string Owner { get; init; } = "";
    public List<string> Errors { get; } = new();

    /// <summary>
    /// The weave compiler compiling this graph, when a weave is being compiled. Handlers that refer to other
    /// weaves (<c>cast</c>) resolve them through it; null when statuses or tools compile effects alone.
    /// </summary>
    public WeaveCompiler? Compiler { get; init; }

    public CompiledEffect[] CompileList(JsonNode? node, string field)
    {
        if (node is null)
        {
            return Array.Empty<CompiledEffect>();
        }
        var specs = node.Deserialize<List<TypedSpec>>(ContentJson.Options) ?? new List<TypedSpec>();
        return Registry.CompileAll(specs, this).ToArray();
    }

    /// <summary>Resolves "$threads" to the weave's threads, "$thread" to the first, or a literal list.</summary>
    public List<string> ResolveThreads(JsonNode? node)
    {
        switch (node)
        {
            case null:
                return Threads.ToList();
            case JsonValue v when v.TryGetValue<string>(out var s):
                if (s == "$threads")
                {
                    return Threads.ToList();
                }
                if (s == "$thread")
                {
                    return Threads.Take(1).ToList();
                }
                return new List<string> { s };
            case JsonArray arr:
                return arr.Select(n => n?.GetValue<string>() ?? "").Where(s => s.Length > 0).SelectMany(s => s == "$threads" ? Threads : new[] { s }).ToList();
            default:
                return Threads.ToList();
        }
    }

    public Expr? ExprOf(JsonNode? node, Expr? fallback = null)
    {
        if (node is null)
        {
            return fallback;
        }
        return node.Deserialize<Expr>(ContentJson.Options) ?? fallback;
    }

    public TagSet TagsOf(JsonNode? node) => node is null ? TagSet.Empty : node.Deserialize<TagSet>(ContentJson.Options) ?? TagSet.Empty;

    public string? StringOf(JsonNode? node) => node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    public double NumberOf(JsonNode? node, double fallback) => JsonNum.Of(node, fallback);

    public bool BoolOf(JsonNode? node, bool fallback) => node is JsonValue v && v.TryGetValue<bool>(out var b) ? b : fallback;

    public int IntOf(JsonNode? node, int fallback) => JsonNum.IntOf(node, fallback);

    /// <summary>A parameter that must be one of a fixed set of words; anything else fails compilation with the choices.</summary>
    public string ChoiceOf(JsonNode? node, string field, string fallback, params string[] choices)
    {
        var value = StringOf(node) ?? fallback;
        if (Array.IndexOf(choices, value) < 0)
        {
            throw new InvalidOperationException($"'{field}' must be one of {string.Join(", ", choices)}, not '{value}' in {Owner}.");
        }
        return value;
    }
}

/// <summary>A compiled effect ready to execute many times.</summary>
public abstract class CompiledEffect
{
    public required string Type { get; init; }
    public abstract void Execute(EffectContext ctx);

    public static void ExecuteAll(IReadOnlyList<CompiledEffect> effects, EffectContext ctx)
    {
        var limit = (int)ctx.Sim.Content.Rules.Number("effects.max_depth", 12);
        if (ctx.Depth > limit)
        {
            ctx.Sim.Log.Warn($"effect chain exceeded depth {limit}; stopping");
            return;
        }
        foreach (var e in effects)
        {
            try
            {
                e.Execute(ctx);
            }
            catch (ExprEvalException ex)
            {
                ctx.Sim.Log.Error($"effect {e.Type} expression failed: {ex.Message}");
            }
        }
    }
}

/// <summary>
/// What an effect type reads: every parameter it accepts, which of them hold effect lists that glyph hooks
/// may extend (keyed by hook name: <c>on_hit</c>, <c>on_kill</c> ...), and which list a wrapper puts the
/// effects it wraps in. The weave compiler reads it to append hooks and apply patches; validators read it
/// to reject patches that name parameters the type does not have.
/// </summary>
public sealed class EffectSignature
{
    private readonly HashSet<string> _params;
    private readonly Dictionary<string, string> _hooks;

    /// <summary>A handler that declares nothing: any parameter is accepted and nothing hooks into it.</summary>
    public static readonly EffectSignature Open = new(Array.Empty<string>(), open: true);

    private EffectSignature(IEnumerable<string> parameters, bool open)
    {
        _params = new HashSet<string>(parameters, StringComparer.Ordinal);
        _hooks = new Dictionary<string, string>(StringComparer.Ordinal);
        IsOpen = open;
    }

    /// <summary>A signature reading exactly these parameters.</summary>
    public static EffectSignature Of(params string[] parameters) => new(parameters, open: false);

    /// <summary>True when the handler declared nothing, so its parameters cannot be checked.</summary>
    public bool IsOpen { get; }

    public IReadOnlyCollection<string> Params => _params;

    /// <summary>Hook name to the parameter holding that effect list.</summary>
    public IReadOnlyDictionary<string, string> Hooks => _hooks;

    /// <summary>For wrappers (<c>repeat</c>, <c>sequence</c>, <c>delay</c>): the list a wrap patch puts the wrapped node in.</summary>
    public string? Wraps { get; private set; }

    public bool Accepts(string parameter) => IsOpen || _params.Contains(parameter);

    /// <summary>Declares that the list in <paramref name="parameter"/> runs at the moment named <paramref name="hook"/>.</summary>
    public EffectSignature Hook(string hook, string parameter)
    {
        _params.Add(parameter);
        _hooks[hook] = parameter;
        return this;
    }

    /// <summary>Declares this type a wrapper whose inner list is <paramref name="parameter"/>.</summary>
    public EffectSignature Wrapping(string parameter)
    {
        _params.Add(parameter);
        Wraps = parameter;
        return this;
    }
}

public interface IEffectHandler
{
    string Type { get; }
    CompiledEffect Compile(JsonObject parameters, EffectCompileContext ctx);

    /// <summary>What the handler reads. Handlers that declare nothing accept any parameter and expose no hooks.</summary>
    EffectSignature Signature => EffectSignature.Open;
}

/// <summary>Registry of effect types. Content composes; code (including mods) extends.</summary>
public sealed class EffectRegistry
{
    private readonly Dictionary<string, IEffectHandler> _handlers = new(StringComparer.Ordinal);

    private static readonly Lazy<EffectRegistry> SharedDefault = new(CreateDefault, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// A default registry shared for reading only: content validators and tools check effect types,
    /// parameters and hooks against it. Simulations create their own with <see cref="CreateDefault"/>.
    /// </summary>
    public static EffectRegistry Shared => SharedDefault.Value;

    public void Register(IEffectHandler handler) => _handlers[handler.Type] = handler;

    public bool Has(string type) => _handlers.ContainsKey(type);

    public IEnumerable<string> Types => _handlers.Keys.OrderBy(k => k, StringComparer.Ordinal);

    /// <summary>The signature a registered type declared; false for unknown types.</summary>
    public bool TryGetSignature(string type, out EffectSignature signature)
    {
        if (_handlers.TryGetValue(type, out var handler))
        {
            signature = handler.Signature;
            return true;
        }
        signature = EffectSignature.Open;
        return false;
    }

    /// <summary>Every hook name some registered type exposes, plus <c>on_cast</c>, which the weave compiler adds to the root.</summary>
    public IReadOnlyCollection<string> HookNames
    {
        get
        {
            var names = new SortedSet<string>(StringComparer.Ordinal) { EffectHooks.OnCast };
            foreach (var handler in _handlers.Values)
            {
                foreach (var hook in handler.Signature.Hooks.Keys)
                {
                    names.Add(hook);
                }
            }
            return names;
        }
    }

    public CompiledEffect Compile(TypedSpec spec, EffectCompileContext ctx)
    {
        if (!_handlers.TryGetValue(spec.Type, out var handler))
        {
            throw new InvalidOperationException($"Unknown effect type '{spec.Type}' in {ctx.Owner}.");
        }
        return handler.Compile(spec.Params, ctx);
    }

    public IEnumerable<CompiledEffect> CompileAll(IEnumerable<TypedSpec> specs, EffectCompileContext ctx)
    {
        foreach (var s in specs)
        {
            yield return Compile(s, ctx);
        }
    }

    public static EffectRegistry CreateDefault()
    {
        var r = new EffectRegistry();
        BuiltinEffects.RegisterAll(r);
        return r;
    }
}

/// <summary>
/// The moments glyph hooks name. Effect types declare which of their lists run at which moment through
/// <see cref="EffectSignature.Hook"/>; the weave compiler appends each glyph's hook list into every node
/// exposing that moment, and <see cref="OnCast"/> into the weave's root list.
/// </summary>
public static class EffectHooks
{
    /// <summary>Each body a delivery reaches: a projectile's impact, an area's or chain's targets, a field's pulse.</summary>
    public const string OnHit = "on_hit";

    /// <summary>A hit that kills.</summary>
    public const string OnKill = "on_kill";

    /// <summary>A delivery ending for any reason.</summary>
    public const string OnEnd = "on_end";

    /// <summary>A delivery's periodic beat while it lasts.</summary>
    public const string OnTick = "on_tick";

    /// <summary>A lobbed or delayed delivery arriving.</summary>
    public const string OnLand = "on_land";

    /// <summary>Effects appended to the weave's root list: they run once each time the weave resolves.</summary>
    public const string OnCast = "on_cast";
}
