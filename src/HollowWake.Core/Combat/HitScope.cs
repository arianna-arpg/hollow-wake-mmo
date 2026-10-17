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
/// <c>source.*</c> the attacker and <c>distance</c> the distance between them; every other name resolves
/// on the fold's owner exactly as the owner's own scope would, so the attacker's <c>damage</c> fold can
/// scale by the victim's stacks without the attribute cache ever seeing a target. Scopes are pooled per
/// thread; a hit raised inside another hit (an ailment's on-apply damage) rents its own.
/// </summary>
public sealed class HitScope
{
    [ThreadStatic]
    private static Stack<HitScope>? _pool;

    private readonly View _attacker;
    private readonly View _victim;
    private double _distance;
    private int _distanceState; // 0 unknown, 1 known, 2 unavailable

    private HitScope()
    {
        _attacker = new View(this, attacker: true);
        _victim = new View(this, attacker: false);
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

    /// <summary>Loads the packet's Threads into the working arrays, in packet order.</summary>
    public void LoadThreads()
    {
        var amounts = Packet.Amounts;
        if (ThreadIds.Length < amounts.Count)
        {
            var size = Math.Max(amounts.Count, ThreadIds.Length * 2);
            ThreadIds = new string[size];
            Amounts = new double[size];
            Live = new bool[size];
        }
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
