using System.Collections.Concurrent;
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
    /// <summary>The originating cast, retained through delayed and nested deliveries.</summary>
    public long CastId { get; init; }
    public required Simulation Sim { get; init; }
    public Entity Source { get; init; } = Entity.None;
    public Entity Target { get; init; } = Entity.None;
    public Vector3 Point { get; init; }
    public Vector2 Direction { get; init; }
    public CompiledWeave? Weave { get; init; }
    public TagSet Tags { get; init; } = TagSet.Empty;

    /// <summary>
    /// How deep in a chain this effect runs. Nested deliveries, repeats, delays and casts (of other weaves or
    /// of the host again) run one deeper; <c>effects.max_depth</c> caps it, and damage and status events carry
    /// it so triggers can refuse to chain without limit.
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

    /// <summary>
    /// The point the cast was aimed at. It is the context's point until a delivery moves the point (onto a body
    /// it struck, or to where a flight or a placement is), which keeps the aim here, so the effects of a hit can
    /// still reach where the cast was aimed.
    /// </summary>
    public Vector3 Aim
    {
        get => _aim ?? Point;
        init => _aim = value;
    }

    private readonly Vector3? _aim;

    public Dictionary<string, double> Vars { get; init; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The weave slot the cast came from, when a runtime knows it: effects that act on their host slot
    /// (restoring its uses, commanding its minions) read it. Casts by a <c>cast</c> effect keep their host's.
    /// </summary>
    public WeaveSlot? Slot { get; init; }

    /// <summary>
    /// The weave of the slot a meta, follow-up, trigger or <c>cast</c> runs for, when the running weave is
    /// another: effects that address their host (its minions, its bank) read <c>Host ?? Weave</c>.
    /// </summary>
    public CompiledWeave? Host { get; init; }

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

    private EffectScope? _scope;

    /// <summary>
    /// The scope of the effect's expressions, built when first read: <c>source.*</c> and <c>caster.*</c>
    /// resolve on the source, <c>target.*</c> on the target, <c>weave.*</c> through the weave folded on the
    /// source, the variables as they stand when read (so what an earlier effect in the list wrote, such as a
    /// damage effect's <c>last_damage</c> and <c>amount</c>, reaches the effects after it), and
    /// <c>distance</c>, <c>depth</c> and <c>time</c> as they stood when it was built; every other name
    /// resolves on the source.
    /// </summary>
    public IExprScope Scope => _scope ??= new EffectScope(this);

    public double Eval(Expr? expr, double fallback = 0) => expr is null ? fallback : expr.Evaluate(Scope);

    /// <summary>The context of a hit on a body: the body is the target and the point moves onto it, while the aim stays where the cast was aimed.</summary>
    public EffectContext WithTarget(Entity target) => Copy(target: target, point: Sim.World.TryGet<Transform>(target, out var t) ? t.Position : Point);

    /// <summary>A context aimed at another point: the point and the aim move together.</summary>
    public EffectContext WithPoint(Vector3 point) => Copy(point: point, aim: point);

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

    /// <summary><see cref="Fork"/> with damage effects scaled by a further factor: a fuse's power.</summary>
    public EffectContext Fork(int deeper, double damageScale) => Copy(depth: Depth + deeper, vars: new Dictionary<string, double>(Vars, StringComparer.Ordinal), damageScale: DamageScale * damageScale);

    private EffectContext Copy(Entity? source = null, Entity? target = null, Vector3? point = null, int? depth = null, Dictionary<string, double>? vars = null, TagSet? tags = null, double? damageScale = null, Vector3? aim = null)
    {
        return new EffectContext
        {
            CastId = CastId,
            Origin = _origin.IsNone ? Source : _origin,
            Sim = Sim,
            Source = source ?? Source,
            Target = target ?? Target,
            Point = point ?? Point,
            Aim = aim ?? Aim,
            Direction = Direction,
            Weave = Weave,
            Tags = tags ?? Tags,
            Depth = depth ?? Depth,
            Rng = Rng,
            Potency = Potency,
            DamageScale = damageScale ?? DamageScale,
            From = From,
            Vars = vars ?? Vars,
            Slot = Slot,
            Host = Host,
        };
    }
}

/// <summary>
/// The expression scope of one effect context, resolving what the context mounts without building an
/// overlay: the variables are read from the context's own dictionary when asked, so an effect sees what
/// the effects before it in its list wrote; <c>distance</c>, <c>depth</c> and <c>time</c> are read when it
/// is built; <c>source.</c> and <c>caster.</c> names resolve on the source's stats, <c>target.</c> names on
/// the target's, <c>weave.</c> names through the weave folded on the source with the context's tags, and
/// anything else on the source.
/// </summary>
internal sealed class EffectScope : IExprScope
{
    // "target.life" -> "life": the part after the mount, kept so prefixed lookups do not allocate.
    private static readonly ConcurrentDictionary<string, string> Suffixes = new(StringComparer.Ordinal);
    private const int SuffixLimit = 4096;
    private static int _suffixCount;

    private readonly Rng _rng;
    private readonly Stats? _source;
    private readonly Stats? _target;
    private readonly CompiledWeave? _weave;
    private readonly TagSet _tags;
    private readonly bool _hasDistance;
    private readonly double _distance;
    private readonly double _depth;
    private readonly double _time;
    private readonly Dictionary<string, double> _vars;

    public EffectScope(EffectContext ctx)
    {
        var world = ctx.Sim.World;
        _rng = ctx.Rng;
        _source = world.Get<Stats>(ctx.Source);
        _target = world.Get<Stats>(ctx.Target);
        _weave = ctx.Weave;
        _tags = ctx.Tags;
        if (world.TryGet<Transform>(ctx.Source, out var st) && world.TryGet<Transform>(ctx.Target, out var tt))
        {
            _hasDistance = true;
            _distance = st.DistanceTo(tt);
        }
        _depth = ctx.Depth;
        _time = ctx.Sim.Clock.Time;
        _vars = ctx.Vars;
    }

    public bool TryResolve(string name, out double value)
    {
        if (TryValue(name, out value))
        {
            return true;
        }
        var dot = name.IndexOf('.');
        if (dot > 0)
        {
            switch (dot)
            {
                case 6 when _source is not null && (string.CompareOrdinal(name, 0, "source", 0, 6) == 0 || string.CompareOrdinal(name, 0, "caster", 0, 6) == 0):
                    return _source.TryResolve(Suffix(name, dot), out value);
                case 6 when _target is not null && string.CompareOrdinal(name, 0, "target", 0, 6) == 0:
                    return _target.TryResolve(Suffix(name, dot), out value);
                case 5 when _weave is not null && _source is not null && string.CompareOrdinal(name, 0, "weave", 0, 5) == 0:
                {
                    var rest = Suffix(name, dot);
                    value = rest == "level" ? _weave.Level : _weave.Attr(_source, rest, _tags);
                    return true;
                }
            }
        }
        if (_source is not null)
        {
            return _source.TryResolve(name, out value);
        }
        value = 0;
        return false;
    }

    public bool HasFlag(string flag)
    {
        var dot = flag.IndexOf('.');
        if (dot > 0)
        {
            switch (dot)
            {
                case 6 when _source is not null && (string.CompareOrdinal(flag, 0, "source", 0, 6) == 0 || string.CompareOrdinal(flag, 0, "caster", 0, 6) == 0):
                    return _source.HasFlag(Suffix(flag, dot));
                case 6 when _target is not null && string.CompareOrdinal(flag, 0, "target", 0, 6) == 0:
                    return _target.HasFlag(Suffix(flag, dot));
                case 5 when _weave is not null && _source is not null && string.CompareOrdinal(flag, 0, "weave", 0, 5) == 0:
                {
                    var rest = Suffix(flag, dot);
                    return _weave.Tags.Has(rest) || _weave.Mechanisms.Has(rest);
                }
            }
        }
        return _source?.HasFlag(flag) ?? false;
    }

    public double NextRandom() => _rng.NextDouble();

    /// <summary>The context's own values: the real depth, time and distance win over variables of the same names.</summary>
    private bool TryValue(string name, out double value)
    {
        switch (name)
        {
            case "depth":
                value = _depth;
                return true;
            case "time":
                value = _time;
                return true;
            case "distance" when _hasDistance:
                value = _distance;
                return true;
        }
        if (_vars.Count > 0 && _vars.TryGetValue(name, out value))
        {
            return true;
        }
        value = 0;
        return false;
    }

    internal static string Suffix(string name, int dot)
    {
        if (Suffixes.TryGetValue(name, out var suffix))
        {
            return suffix;
        }
        suffix = name.Substring(dot + 1);
        if (Volatile.Read(ref _suffixCount) < SuffixLimit && Suffixes.TryAdd(name, suffix))
        {
            Interlocked.Increment(ref _suffixCount);
        }
        return suffix;
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

    /// <summary>How many use charges a cast spent from its slot's bank.</summary>
    public const string UsesSpent = "uses_spent";

    /// <summary>The power a trigger banked from its event, and the event's amount (damage, healing, stacks).</summary>
    public const string TriggerPower = "trigger.power";
    public const string TriggerAmount = "trigger.amount";

    /// <summary>Which repeat of a use this is, from one.</summary>
    public const string RepeatIndex = "repeat.index";

    /// <summary>Damage a guard absorbed before its bash.</summary>
    public const string GuardAbsorbed = "guard.absorbed";

    /// <summary>The share of a persisting channel's strength left, from one down to its fade.</summary>
    public const string Persist = "cast.persist";

    /// <summary>Every name above, for validators.</summary>
    public static readonly IReadOnlyList<string> All = new[] { Held, Fill, Stage, ChargesSpent, UsesSpent, TriggerPower, TriggerAmount, RepeatIndex, GuardAbsorbed, Persist };
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
        for (var i = 0; i < effects.Count; i++)
        {
            var e = effects[i];
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
/// may extend (keyed by hook name: <c>on_hit</c>, <c>on_kill</c> ...), which list a wrapper puts the
/// effects it wraps in, which parameters name content, and the checks of its own a node must pass. The weave
/// compiler reads it to append hooks and apply patches; validators read it to reject patches that name
/// parameters the type does not have, names of content that does not exist, and nodes its checks refuse.
/// </summary>
public sealed class EffectSignature
{
    private readonly HashSet<string> _params;
    private readonly Dictionary<string, string> _hooks;
    private readonly List<(string Kind, string Parameter)> _refers = new();
    private readonly List<Func<JsonObject, ContentRegistry, string?>> _checks = new();

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

    /// <summary>
    /// The parameters that name content of a kind (<c>statuses</c>, <c>charges</c>, <c>patterns</c>), each a
    /// string or a list of strings. Validation checks every name against the registry and refuses an unknown
    /// one; a name starting with <c>$</c> is a variable the compiler resolves and is left alone.
    /// </summary>
    public IReadOnlyList<(string Kind, string Parameter)> References => _refers;

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

    /// <summary>Declares that each of <paramref name="parameters"/> names content of <paramref name="kind"/>.</summary>
    public EffectSignature Refers(string kind, params string[] parameters)
    {
        foreach (var parameter in parameters)
        {
            _params.Add(parameter);
            _refers.Add((kind, parameter));
        }
        return this;
    }

    /// <summary>The checks validation runs on every node of the type, each saying why it refuses the node, or null.</summary>
    public IReadOnlyList<Func<JsonObject, ContentRegistry, string?>> Checks => _checks;

    /// <summary>
    /// Declares a check validation runs on every node of the type: given the node's parameters and the content, it
    /// says why the node is refused, or returns null. What the handler would refuse at run time is refused at load
    /// where content writes it as a literal.
    /// </summary>
    public EffectSignature Checking(Func<JsonObject, ContentRegistry, string?> check)
    {
        _checks.Add(check);
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
