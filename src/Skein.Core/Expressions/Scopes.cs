using Skein.Core.Maths;

namespace Skein.Core.Expressions;

/// <summary>A scope backed by a dictionary. Handy for tools, tests and content previews.</summary>
public sealed class DictScope : IExprScope
{
    private readonly Dictionary<string, double> _values = new(StringComparer.Ordinal);
    private readonly HashSet<string> _flags = new(StringComparer.Ordinal);
    private readonly Rng? _rng;

    public DictScope(Rng? rng = null)
    {
        _rng = rng;
    }

    public DictScope Set(string name, double value)
    {
        _values[name] = value;
        return this;
    }

    public DictScope Flag(string flag, bool on = true)
    {
        if (on)
        {
            _flags.Add(flag);
        }
        else
        {
            _flags.Remove(flag);
        }
        return this;
    }

    public bool TryResolve(string name, out double value) => _values.TryGetValue(name, out value);
    public bool HasFlag(string flag) => _flags.Contains(flag);
    public double NextRandom() => _rng?.NextDouble() ?? 0.5;
}

/// <summary>
/// Layers a set of overrides over a parent scope, and optionally mounts child scopes under a prefix
/// (<c>target.life</c> resolves through the scope mounted as "target").
/// </summary>
public sealed class ScopeOverlay : IExprScope
{
    private readonly IExprScope? _parent;
    private readonly Dictionary<string, double> _values = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IExprScope> _mounts = new(StringComparer.Ordinal);
    private readonly HashSet<string> _flags = new(StringComparer.Ordinal);
    private readonly Rng? _rng;

    public ScopeOverlay(IExprScope? parent = null, Rng? rng = null)
    {
        _parent = parent;
        _rng = rng;
    }

    public ScopeOverlay Set(string name, double value)
    {
        _values[name] = value;
        return this;
    }

    public ScopeOverlay Flag(string flag)
    {
        _flags.Add(flag);
        return this;
    }

    public ScopeOverlay Mount(string prefix, IExprScope scope)
    {
        _mounts[prefix] = scope;
        return this;
    }

    public bool TryResolve(string name, out double value)
    {
        if (_values.TryGetValue(name, out value))
        {
            return true;
        }
        var dot = name.IndexOf('.');
        if (dot > 0 && _mounts.TryGetValue(name.Substring(0, dot), out var mounted))
        {
            return mounted.TryResolve(name.Substring(dot + 1), out value);
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
        if (_flags.Contains(flag))
        {
            return true;
        }
        var dot = flag.IndexOf('.');
        if (dot > 0 && _mounts.TryGetValue(flag.Substring(0, dot), out var mounted))
        {
            return mounted.HasFlag(flag.Substring(dot + 1));
        }
        return _parent?.HasFlag(flag) ?? false;
    }

    public double NextRandom() => _rng?.NextDouble() ?? _parent?.NextRandom() ?? 0.5;
}

public sealed class EmptyScope : IExprScope
{
    public static readonly EmptyScope Instance = new();
    public bool TryResolve(string name, out double value) { value = 0; return false; }
    public bool HasFlag(string flag) => false;
    public double NextRandom() => 0.5;
}
