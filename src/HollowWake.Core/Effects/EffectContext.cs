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
    public int Depth { get; init; }
    public required Rng Rng { get; init; }
    public double Potency { get; init; } = 1;
    public Dictionary<string, double> Vars { get; init; } = new(StringComparer.Ordinal);

    /// <summary>The original caster for summoned and chained effects, used for ownership and credit.</summary>
    public Entity Origin => _origin.IsNone ? Source : _origin;
    private readonly Entity _origin = Entity.None;

    public EffectContext()
    {
    }

    private EffectContext(Entity origin)
    {
        _origin = origin;
    }

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

    private EffectContext Copy(Entity? source = null, Entity? target = null, Vector3? point = null, int? depth = null, Dictionary<string, double>? vars = null, TagSet? tags = null)
    {
        return new EffectContext(Origin)
        {
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
            Vars = vars ?? Vars,
        };
    }
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

public interface IEffectHandler
{
    string Type { get; }
    CompiledEffect Compile(JsonObject parameters, EffectCompileContext ctx);
}

/// <summary>Registry of effect types. Content composes; code (including mods) extends.</summary>
public sealed class EffectRegistry
{
    private readonly Dictionary<string, IEffectHandler> _handlers = new(StringComparer.Ordinal);

    public void Register(IEffectHandler handler) => _handlers[handler.Type] = handler;

    public bool Has(string type) => _handlers.ContainsKey(type);

    public IEnumerable<string> Types => _handlers.Keys.OrderBy(k => k, StringComparer.Ordinal);

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
