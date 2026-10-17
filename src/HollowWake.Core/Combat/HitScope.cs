using System.Collections.Concurrent;
using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Effects;
using HollowWake.Core.Expressions;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Combat;

/// <summary>
/// One hit as it walks the pipeline: its context, packet, result and per-Thread working amounts, and the
/// expression scopes its uncached folds read. In those scopes <c>target.*</c> is the struck body,
/// <c>source.*</c> the attacker, <c>distance</c> the distance between them and <c>angle</c> how far round the
/// struck body the attacker stands (zero in front, 180 behind); every other name resolves on the fold's owner
/// exactly as the owner's own scope would, so the attacker's <c>damage</c> fold can scale by the victim's stacks
/// without the attribute cache ever seeing a target. Stages read rule formulas through <see cref="Values"/>, a
/// small reusable scope, so a hit allocates no scopes. Scopes are pooled per thread; a hit raised inside another
/// hit (an ailment's on-apply damage) rents its own.
/// </summary>
public sealed class HitScope
{
    [ThreadStatic]
    private static Stack<HitScope>? _pool;

    private readonly View _attacker;
    private readonly View _victim;
    private double _distance;
    private int _distanceState; // 0 unknown, 1 known, 2 unavailable
    private double _angle;
    private int _angleState;

    private HitScope()
    {
        _attacker = new View(this, attacker: true);
        _victim = new View(this, attacker: false);
        Values = new StageValues(this);
    }

    /// <summary>Takes a scope from this thread's pool for a hit. Return it with <see cref="Return"/>.</summary>
    public static HitScope Rent(EffectContext context, DamagePacket packet, HitResult? result = null)
    {
        var pool = _pool ??= new Stack<HitScope>();
        var hit = pool.Count > 0 ? pool.Pop() : new HitScope();
        hit.Context = context;
        hit.Packet = packet;
        hit.Result = result ?? Empty;
        hit.Source = context.SourceStats;
        hit.Target = context.TargetStats;
        hit.Plan = HitPlan.For(context.Sim.Content.Rules);
        hit.BlockMultiplier = 1;
        hit.Total = 0;
        hit.ThreadCount = 0;
        hit._distanceState = 0;
        hit._angleState = 0;
        hit._matchedKnown = false;
        hit.Values.Clear();
        return hit;
    }

    /// <summary>Gives the scope back to the pool. It must not be used afterwards.</summary>
    public void Return()
    {
        Context = null!;
        Packet = null!;
        Result = Empty;
        Source = null;
        Target = null;
        for (var i = 0; i < ThreadCount; i++)
        {
            ThreadIds[i] = null!;
        }
        ThreadCount = 0;
        VictimTags.Clear();
        Values.Clear();
        (_pool ??= new Stack<HitScope>()).Push(this);
    }

    private static readonly HitResult Empty = new();

    public EffectContext Context { get; private set; } = null!;
    public Simulation Sim => Context.Sim;
    public DamagePacket Packet { get; private set; } = null!;
    public HitResult Result { get; private set; } = Empty;
    /// <summary>The attacker's stats, when the hit has an attacker with stats.</summary>
    public Stats? Source { get; private set; }
    /// <summary>The struck body's stats.</summary>
    public Stats? Target { get; private set; }
    /// <summary>The rules every hit reads, resolved once per ruleset.</summary>
    public HitPlan Plan { get; private set; } = null!;
    /// <summary>The hit's tags: its own, its Threads', <c>hit</c>, <c>damage</c> and the victim tags.</summary>
    public TagSet Tags => Packet.Tags;
    /// <summary>The weave's local modifiers, joining the attacker's folds.</summary>
    public IReadOnlyList<Modifier>? Extra => Context.Weave?.Modifiers;

    /// <summary>A reusable scope of named numbers for the rule formulas a stage evaluates (armor, accuracy, block value).</summary>
    public StageValues Values { get; }

    /// <summary>The fraction of each Thread's amount a block lets through; one when the hit was not blocked.</summary>
    public double BlockMultiplier;

    /// <summary>The damage that landed, summed over Threads once damage taken has applied.</summary>
    public double Total;

    // Per-Thread working amounts, in the packet's order, as mitigation and damage taken change them.
    public int ThreadCount;
    public string[] ThreadIds = new string[4];
    public double[] Amounts = new double[4];
    /// <summary>False for a Thread that carried nothing, which later stages skip.</summary>
    public bool[] Live = new bool[4];

    /// <summary>A reusable list for building the victim tags.</summary>
    internal readonly List<string> VictimTags = new();

    /// <summary>The scope of the attacker's folds: unprefixed names resolve on the attacker.</summary>
    public IExprScope AttackerScope => _attacker;

    /// <summary>The scope of the struck body's folds: unprefixed names resolve on the struck body.</summary>
    public IExprScope VictimScope => _victim;

    private ulong _matched;
    private bool _matchedKnown;

    /// <summary>The skip entries of rule <c>combat.stage_skips</c> this hit's tags match, worked out once per hit.</summary>
    public ulong MatchedSkips
    {
        get
        {
            if (!_matchedKnown)
            {
                _matched = Plan.MatchSkips(Tags);
                _matchedKnown = true;
            }
            return _matched;
        }
    }

    /// <summary>
    /// Whether this hit's tags skip a stage, a declared step or a pool (rule <c>combat.stage_skips</c>). Ask only
    /// once the hit's tags are final: the match is worked out on the first question.
    /// </summary>
    public bool Skips(string name) => Plan.Skips(MatchedSkips, name);

    /// <summary>Loads the packet's Threads into the working arrays, in packet order.</summary>
    public void LoadThreads()
    {
        var amounts = Packet.Amounts;
        Ensure(amounts.Count);
        var i = 0;
        foreach (var (thread, amount) in amounts)
        {
            ThreadIds[i] = thread;
            Amounts[i] = amount;
            Live[i] = amount > 0;
            i++;
        }
        ThreadCount = i;
    }

    /// <summary>Adds an amount to a Thread's working slot, opening a slot for a Thread the hit did not carry.</summary>
    public void AddToThread(string thread, double amount)
    {
        for (var i = 0; i < ThreadCount; i++)
        {
            if (string.Equals(ThreadIds[i], thread, StringComparison.Ordinal))
            {
                Amounts[i] += amount;
                Live[i] = Amounts[i] > 0;
                return;
            }
        }
        Ensure(ThreadCount + 1);
        ThreadIds[ThreadCount] = thread;
        Amounts[ThreadCount] = amount;
        Live[ThreadCount] = amount > 0;
        ThreadCount++;
    }

    private void Ensure(int count)
    {
        if (ThreadIds.Length >= count)
        {
            return;
        }
        var size = Math.Max(count, ThreadIds.Length * 2);
        Array.Resize(ref ThreadIds, size);
        Array.Resize(ref Amounts, size);
        Array.Resize(ref Live, size);
    }

    /// <summary>Scales what was received (each Thread and the total) by a factor: a cap or a stagger taking part of it.</summary>
    public void ScaleReceived(double factor)
    {
        var received = Result.Received;
        for (var i = 0; i < ThreadCount; i++)
        {
            var thread = ThreadIds[i];
            if (received.TryGetValue(thread, out var v))
            {
                received[thread] = v * factor;
            }
            Amounts[i] *= factor;
        }
        Total *= factor;
        Result.Total = Total;
    }

    /// <summary>Publishes the hit as it stands: for stages that end a hit having decided it (an evade).</summary>
    public void Publish() => DamagePipeline.Publish(Sim, Context, Result, Packet);

    private bool TryDistance(out double distance)
    {
        if (_distanceState == 0)
        {
            var world = Context.Sim.World;
            if (world.TryGet<Transform>(Context.Source, out var st) && world.TryGet<Transform>(Context.Target, out var tt))
            {
                _distance = st.DistanceTo(tt);
                _distanceState = 1;
            }
            else
            {
                _distanceState = 2;
            }
        }
        distance = _distance;
        return _distanceState == 1;
    }

    /// <summary>Degrees between the struck body's facing and the way to the attacker: zero in front, 180 behind.</summary>
    private bool TryAngle(out double angle)
    {
        if (_angleState == 0)
        {
            var world = Context.Sim.World;
            if (world.TryGet<Transform>(Context.Source, out var st) && world.TryGet<Transform>(Context.Target, out var tt))
            {
                var dx = st.Position.X - tt.Position.X;
                var dy = st.Position.Y - tt.Position.Y;
                if (dx * dx + dy * dy < 1e-8f)
                {
                    _angle = 0;
                }
                else
                {
                    var toward = Math.Atan2(dy, dx);
                    var diff = Math.Abs(Math.IEEERemainder(toward - tt.Yaw, Math.PI * 2));
                    _angle = diff * 180 / Math.PI;
                }
                _angleState = 1;
            }
            else
            {
                _angleState = 2;
            }
        }
        angle = _angle;
        return _angleState == 1;
    }

    // "target.life_pct" -> "life_pct": kept so prefixed lookups do not allocate.
    private static readonly ConcurrentDictionary<string, string> Suffixes = new(StringComparer.Ordinal);

    private static string After(string name, int prefix) => Suffixes.GetOrAdd(name, static (n, p) => n.Substring(p), prefix);

    /// <summary>One side's view of the hit: the hit's names first, then the owner's.</summary>
    private sealed class View : IExprScope
    {
        private readonly HitScope _hit;
        private readonly bool _attacker;

        public View(HitScope hit, bool attacker)
        {
            _hit = hit;
            _attacker = attacker;
        }

        private Stats? Owner => _attacker ? _hit.Source : _hit.Target;

        public bool TryResolve(string name, out double value)
        {
            if (name.Length > 7 && name[6] == '.' && name.StartsWith("target", StringComparison.Ordinal))
            {
                if (_hit.Target is { } t)
                {
                    return t.TryResolve(After(name, 7), out value);
                }
                value = 0;
                return false;
            }
            if (name.Length > 7 && name[6] == '.' && name.StartsWith("source", StringComparison.Ordinal))
            {
                if (_hit.Source is { } s)
                {
                    return s.TryResolve(After(name, 7), out value);
                }
                value = 0;
                return false;
            }
            if (name == "distance")
            {
                return _hit.TryDistance(out value);
            }
            if (name == "angle")
            {
                return _hit.TryAngle(out value);
            }
            if (Owner is { } owner)
            {
                return owner.TryResolve(name, out value);
            }
            value = 0;
            return false;
        }

        public bool HasFlag(string flag)
        {
            if (flag.Length > 7 && flag[6] == '.' && flag.StartsWith("target", StringComparison.Ordinal))
            {
                return _hit.Target?.HasFlag(After(flag, 7)) ?? false;
            }
            if (flag.Length > 7 && flag[6] == '.' && flag.StartsWith("source", StringComparison.Ordinal))
            {
                return _hit.Source?.HasFlag(After(flag, 7)) ?? false;
            }
            return Owner?.HasFlag(flag) ?? false;
        }

        public double NextRandom() => Owner?.NextRandom() ?? 0.5;
    }

    /// <summary>
    /// A few named numbers for a stage's rule formula (<c>armor</c>, <c>raw</c>, <c>level</c>), set and read
    /// without allocating. Random draws come from the hit's own stream, as a fresh scope's would.
    /// </summary>
    public sealed class StageValues : IExprScope
    {
        private readonly HitScope _hit;
        private string[] _names = new string[6];
        private double[] _values = new double[6];
        private int _count;

        internal StageValues(HitScope hit)
        {
            _hit = hit;
        }

        public StageValues Clear()
        {
            for (var i = 0; i < _count; i++)
            {
                _names[i] = null!;
            }
            _count = 0;
            return this;
        }

        public StageValues Set(string name, double value)
        {
            for (var i = 0; i < _count; i++)
            {
                if (string.Equals(_names[i], name, StringComparison.Ordinal))
                {
                    _values[i] = value;
                    return this;
                }
            }
            if (_count == _names.Length)
            {
                Array.Resize(ref _names, _count * 2);
                Array.Resize(ref _values, _count * 2);
            }
            _names[_count] = name;
            _values[_count] = value;
            _count++;
            return this;
        }

        public bool TryResolve(string name, out double value)
        {
            for (var i = 0; i < _count; i++)
            {
                if (string.Equals(_names[i], name, StringComparison.Ordinal))
                {
                    value = _values[i];
                    return true;
                }
            }
            value = 0;
            return false;
        }

        public bool HasFlag(string flag) => false;

        public double NextRandom() => _hit.Context.Rng.NextDouble();
    }
}

/// <summary>
/// Attributes whose folds run in a hit with the hit scope as their owner, so modifiers on them may read
/// <c>target.*</c> and <c>distance</c>. Everywhere else a modifier reading the target has nothing to read,
/// so content validation warns about target-reading modifiers on attributes not declared here. Packages
/// declare the attributes they fold through the hit scope.
/// </summary>
public static class HitFolds
{
    private static readonly ConcurrentDictionary<string, bool> Attributes = new(StringComparer.Ordinal);

    static HitFolds()
    {
        Declare("damage");
        Declare("damage.taken");
    }

    public static void Declare(string attribute) => Attributes[attribute] = true;

    public static bool Contains(string attribute) => Attributes.ContainsKey(attribute);

    public static IEnumerable<string> All => Attributes.Keys.OrderBy(k => k, StringComparer.Ordinal);
}
