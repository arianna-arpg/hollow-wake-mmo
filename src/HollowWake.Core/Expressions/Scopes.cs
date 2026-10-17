using System.Collections.Concurrent;
using HollowWake.Core.Maths;

namespace HollowWake.Core.Expressions;

/// <summary>
/// The named values of a small scope. Scopes are built per hit, per roll and per effect with a handful of
/// names (accuracy and evasion, item level and tier), so names sit in a short array and are found by
/// comparison; a dictionary takes over past <see cref="InlineLimit"/> names. Setting a name again
/// replaces its value.
/// </summary>
internal struct ScopeValues
{
    private const int InlineLimit = 8;

    private struct Pair
    {
        public string Name;
        public double Value;
    }

    private Pair[]? _pairs;
    private int _count;
    private Dictionary<string, double>? _map;

    public void Set(string name, double value)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (_map is not null)
        {
            _map[name] = value;
            return;
        }
        var pairs = _pairs;
        for (var i = 0; i < _count; i++)
        {
            if (string.Equals(pairs![i].Name, name))
            {
                pairs[i].Value = value;
                return;
            }
        }
        if (pairs is null)
        {
            _pairs = pairs = new Pair[4];
        }
        else if (_count == pairs.Length)
        {
            if (_count >= InlineLimit)
            {
                var map = new Dictionary<string, double>(_count * 2, StringComparer.Ordinal);
                for (var i = 0; i < _count; i++)
                {
                    map[pairs[i].Name] = pairs[i].Value;
                }
                map[name] = value;
                _map = map;
                _pairs = null;
                _count = 0;
                return;
            }
            Array.Resize(ref _pairs, pairs.Length * 2);
            pairs = _pairs;
        }
        pairs[_count++] = new Pair { Name = name, Value = value };
    }

    public readonly bool TryGet(string name, out double value)
    {
        if (_map is not null)
        {
            return _map.TryGetValue(name, out value);
        }
        var pairs = _pairs;
        for (var i = 0; i < _count; i++)
        {
            ref readonly var pair = ref pairs![i];
            if (string.Equals(pair.Name, name))
            {
                value = pair.Value;
                return true;
            }
        }
        value = 0;
        return false;
    }
}

/// <summary>A scope of named values and flags. Handy for tools, tests and content previews, and for one-off formulas such as a hit's chance.</summary>
public sealed class DictScope : IExprScope
{
    private ScopeValues _values;
    private HashSet<string>? _flags;
    private readonly Rng? _rng;

    public DictScope(Rng? rng = null)
    {
        _rng = rng;
    }

    public DictScope Set(string name, double value)
    {
        _values.Set(name, value);
        return this;
    }

    public DictScope Flag(string flag, bool on = true)
    {
        if (on)
        {
            (_flags ??= new HashSet<string>(StringComparer.Ordinal)).Add(flag);
        }
        else
        {
            _flags?.Remove(flag);
        }
        return this;
    }

    public bool TryResolve(string name, out double value) => _values.TryGet(name, out value);
    public bool HasFlag(string flag) => _flags is not null && _flags.Contains(flag);
    public double NextRandom() => _rng?.NextDouble() ?? 0.5;
}

/// <summary>
/// Layers a set of overrides over a parent scope, and optionally mounts child scopes under a prefix
/// (<c>target.life</c> resolves through the scope mounted as "target").
/// </summary>
public sealed class ScopeOverlay : IExprScope
{
    // "target.life" -> "life": the part a mounted scope reads, kept so dotted lookups do not allocate.
    // Names come from formulas, so the set is small; the limit only guards against runaway generated names.
    private static readonly ConcurrentDictionary<string, string> Suffixes = new(StringComparer.Ordinal);
    private const int SuffixLimit = 4096;
    private static int _suffixCount;

    private readonly IExprScope? _parent;
    private ScopeValues _values;
    private (string Prefix, IExprScope Scope)[]? _mounts;
    private int _mountCount;
    private HashSet<string>? _flags;
    private readonly Rng? _rng;

    public ScopeOverlay(IExprScope? parent = null, Rng? rng = null)
    {
        _parent = parent;
        _rng = rng;
    }

    public ScopeOverlay Set(string name, double value)
    {
        _values.Set(name, value);
        return this;
    }

    public ScopeOverlay Flag(string flag)
    {
        (_flags ??= new HashSet<string>(StringComparer.Ordinal)).Add(flag);
        return this;
    }

    public ScopeOverlay Mount(string prefix, IExprScope scope)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        for (var i = 0; i < _mountCount; i++)
        {
            if (string.Equals(_mounts![i].Prefix, prefix))
            {
                _mounts[i].Scope = scope;
                return this;
            }
        }
        if (_mounts is null)
        {
            _mounts = new (string, IExprScope)[4];
        }
        else if (_mountCount == _mounts.Length)
        {
            Array.Resize(ref _mounts, _mounts.Length * 2);
        }
        _mounts[_mountCount++] = (prefix, scope);
        return this;
    }

    public bool TryResolve(string name, out double value)
    {
        if (_values.TryGet(name, out value))
        {
            return true;
        }
        if (_mountCount > 0)
        {
            var dot = name.IndexOf('.');
            if (dot > 0 && MountAt(name, dot) is { } mounted)
            {
                return mounted.TryResolve(Suffix(name, dot), out value);
            }
        }
        if (_parent is not null)
        {
            return _parent.TryResolve(name, out value);
        }
        value = 0;
        return false;
    }

    public bool HasFlag(string flag)
    {
        if (_flags is not null && _flags.Contains(flag))
        {
            return true;
        }
        if (_mountCount > 0)
        {
            var dot = flag.IndexOf('.');
            if (dot > 0 && MountAt(flag, dot) is { } mounted)
            {
                return mounted.HasFlag(Suffix(flag, dot));
            }
        }
        return _parent?.HasFlag(flag) ?? false;
    }

    public double NextRandom() => _rng?.NextDouble() ?? _parent?.NextRandom() ?? 0.5;

    /// <summary>The scope mounted under the text before the first dot, if any.</summary>
    private IExprScope? MountAt(string name, int dot)
    {
        var mounts = _mounts!;
        for (var i = 0; i < _mountCount; i++)
        {
            var prefix = mounts[i].Prefix;
            if (prefix.Length == dot && string.CompareOrdinal(name, 0, prefix, 0, dot) == 0)
            {
                return mounts[i].Scope;
            }
        }
        return null;
    }

    private static string Suffix(string name, int dot)
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

public sealed class EmptyScope : IExprScope
{
    public static readonly EmptyScope Instance = new();
    public bool TryResolve(string name, out double value) { value = 0; return false; }
    public bool HasFlag(string flag) => false;
    public double NextRandom() => 0.5;
}
