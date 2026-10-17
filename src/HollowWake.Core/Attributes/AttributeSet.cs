using HollowWake.Core.Expressions;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Attributes;

public sealed class AttributeCycleException : Exception
{
    public AttributeCycleException(string attribute) : base($"Attribute '{attribute}' depends on itself.")
    {
    }
}

/// <summary>Full accounting of how a value was produced, for tooltips, tests and balance tools.</summary>
public sealed record AttributeBreakdown(
    string Attribute,
    double Base,
    double Flat,
    double Increased,
    double More,
    double? Override,
    double Result,
    IReadOnlyList<Modifier> Applied);

/// <summary>
/// The modifier stack of one owner: a character, a monster, a weave, an item, a holding.
/// Values are computed on demand from base, formula and modifiers, optionally scoped by a tag context
/// and joined by "extra" modifiers local to one query (a weave's glyphs taking part in the caster's
/// damage fold), and cached until anything changes.
/// </summary>
public sealed class AttributeSet
{
    private readonly AttributeCatalog _catalog;
    private readonly Dictionary<string, List<Modifier>> _byAttribute = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _baseOverrides = new(StringComparer.Ordinal);
    private readonly Dictionary<(string, TagSet), double> _cache = new();
    private readonly HashSet<string> _resolving = new(StringComparer.Ordinal);

    /// <summary>Increments on every structural change; consumers can use it to invalidate derived data.</summary>
    public int Version { get; private set; }

    public AttributeCatalog Catalog => _catalog;

    public AttributeSet(AttributeCatalog? catalog = null)
    {
        _catalog = catalog ?? AttributeCatalog.Empty;
    }

    // ------------------------------------------------------------ mutation

    /// <summary>Sets this owner's base for an attribute, replacing the catalog default or formula.</summary>
    public void SetBase(string attribute, double value)
    {
        _baseOverrides[attribute] = value;
        Touch();
    }

    public bool ClearBase(string attribute)
    {
        var removed = _baseOverrides.Remove(attribute);
        if (removed)
        {
            Touch();
        }
        return removed;
    }

    public bool TryGetBaseOverride(string attribute, out double value) => _baseOverrides.TryGetValue(attribute, out value);

    public IEnumerable<KeyValuePair<string, double>> BaseOverrides => _baseOverrides;

    public void Add(Modifier modifier)
    {
        if (!_byAttribute.TryGetValue(modifier.Attribute, out var list))
        {
            list = new List<Modifier>();
            _byAttribute[modifier.Attribute] = list;
        }
        list.Add(modifier);
        Touch();
    }

    public void AddRange(IEnumerable<Modifier> modifiers, string? source = null)
    {
        foreach (var m in modifiers)
        {
            Add(source is null ? m : m.WithSource(source));
        }
    }

    /// <summary>Removes every modifier granted by a source. Returns how many were removed.</summary>
    public int RemoveSource(string source)
    {
        var removed = 0;
        foreach (var list in _byAttribute.Values)
        {
            removed += list.RemoveAll(m => string.Equals(m.Source, source, StringComparison.Ordinal));
        }
        if (removed > 0)
        {
            Touch();
        }
        return removed;
    }

    public bool HasSource(string source) => _byAttribute.Values.Any(l => l.Any(m => m.Source == source));

    public bool Remove(Modifier modifier)
    {
        if (_byAttribute.TryGetValue(modifier.Attribute, out var list) && list.Remove(modifier))
        {
            Touch();
            return true;
        }
        return false;
    }

    public void Clear()
    {
        _byAttribute.Clear();
        _baseOverrides.Clear();
        Touch();
    }

    /// <summary>Call when something outside the set that formulas read (level, flags, gauges) has changed.</summary>
    public void Touch()
    {
        Version++;
        _cache.Clear();
    }

    // ------------------------------------------------------------ queries

    public IEnumerable<Modifier> ModifiersFor(string attribute) =>
        _byAttribute.TryGetValue(attribute, out var list) ? list : Enumerable.Empty<Modifier>();

    public IEnumerable<Modifier> AllModifiers => _byAttribute.Values.SelectMany(l => l);

    public IEnumerable<string> ModifiedAttributes => _byAttribute.Where(kv => kv.Value.Count > 0).Select(kv => kv.Key);

    public bool HasModifiers(string attribute) => _byAttribute.TryGetValue(attribute, out var l) && l.Count > 0;

    /// <summary>Computes the attribute with no tag context (only unscoped modifiers apply).</summary>
    public double Get(string attribute, IExprScope owner) => Get(attribute, owner, TagSet.Empty);

    /// <summary>
    /// Computes the attribute for a tag context. Scoped modifiers apply when the context contains all
    /// of their scope tags. The result is cached per (attribute, context) until the set changes.
    /// </summary>
    public double Get(string attribute, IExprScope owner, TagSet context)
    {
        var key = (attribute, context);
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }
        var result = Compute(attribute, BaseFor(attribute, owner), owner, context, null, applyBounds: true, noLinks: false, out _);
        _cache[key] = result;
        return result;
    }

    /// <summary>
    /// Computes the attribute with query-local extra modifiers joining the same fold. Not cached.
    /// A weave's glyph modifiers join the caster's stack this way so "increased" from gear and
    /// "increased" from glyphs add together as one layer.
    /// </summary>
    public double Get(string attribute, IExprScope owner, TagSet context, IReadOnlyList<Modifier>? extra, double? baseValue = null)
    {
        if (extra is null or { Count: 0 } && baseValue is null)
        {
            return Get(attribute, owner, context);
        }
        return Compute(attribute, baseValue ?? BaseFor(attribute, owner), owner, context, extra, applyBounds: true, noLinks: false, out _);
    }

    /// <summary>
    /// Applies this owner's modifier stack for an attribute to an externally supplied base, without
    /// catalog bounds. The damage pipeline uses it: the weave produces a base amount, then the caster's
    /// <c>damage</c> stack (flat, increased, more) scoped by the hit's tags scales it.
    /// </summary>
    public double Apply(string attribute, double baseValue, IExprScope owner, TagSet context, IReadOnlyList<Modifier>? extra = null) =>
        Compute(attribute, baseValue, owner, context, extra, applyBounds: false, noLinks: false, out _);

    /// <summary>Only the multiplicative part of the stack: (1 + increased) * more.</summary>
    public double Multiplier(string attribute, IExprScope owner, TagSet context, IReadOnlyList<Modifier>? extra = null)
    {
        Compute(attribute, 0, owner, context, extra, applyBounds: false, noLinks: false, out var parts);
        return (1 + parts.Increased) * parts.More;
    }

    public double FlatBonus(string attribute, IExprScope owner, TagSet context, IReadOnlyList<Modifier>? extra = null)
    {
        Compute(attribute, 0, owner, context, extra, applyBounds: false, noLinks: false, out var parts);
        return parts.Flat;
    }

    public AttributeBreakdown Explain(string attribute, IExprScope owner, TagSet? context = null, IReadOnlyList<Modifier>? extra = null)
    {
        context ??= TagSet.Empty;
        var baseValue = BaseFor(attribute, owner);
        var result = Compute(attribute, baseValue, owner, context, extra, applyBounds: true, noLinks: false, out var parts);
        return new AttributeBreakdown(attribute, baseValue, parts.Flat, parts.Increased, parts.More, parts.Override, result, parts.Applied);
    }

    private double BaseFor(string attribute, IExprScope owner)
    {
        if (_baseOverrides.TryGetValue(attribute, out var b))
        {
            return b;
        }
        if (_catalog.TryGet(attribute, out var def) && def is not null)
        {
            if (def.Formula is not null)
            {
                return Guarded(attribute, () => def.Formula.Evaluate(owner));
            }
            return def.Base;
        }
        return 0;
    }

    private struct Parts
    {
        public double Flat;
        public double Increased;
        public double More;
        public double? Override;
        public List<Modifier> Applied;
    }

    private double Compute(string attribute, double baseValue, IExprScope owner, TagSet context, IReadOnlyList<Modifier>? extra, bool applyBounds, bool noLinks, out Parts parts)
    {
        parts = new Parts { More = 1, Applied = new List<Modifier>() };
        var overridePriority = int.MinValue;
        List<(Modifier mod, double ratio)>? links = null;

        void ApplyOne(Modifier m, ref Parts p, ref int overridePri, ref List<(Modifier, double)>? linkList)
        {
            if (!string.Equals(m.Attribute, attribute, StringComparison.Ordinal))
            {
                return;
            }
            if (!m.Scope.IsEmpty && !context.HasAll(m.Scope))
            {
                return;
            }
            if (m.Condition is not null && !GuardedIsTrue(attribute, m.Condition, owner))
            {
                return;
            }
            var v = m.ValueExpr is not null ? Guarded(attribute, () => m.ValueExpr.Evaluate(owner)) : m.Value;
            if (m.Gauge is not null)
            {
                var g = owner.TryResolve(m.Gauge, out var gauge) ? gauge : 0;
                if (m.GaugeAt is { } at)
                {
                    if (g < at)
                    {
                        return;
                    }
                }
                else
                {
                    if (g == 0)
                    {
                        return;
                    }
                    v *= g;
                }
            }
            p.Applied.Add(m);
            switch (m.Form)
            {
                case ModifierForm.Flat:
                    p.Flat += v;
                    break;
                case ModifierForm.Increased:
                    p.Increased += v;
                    break;
                case ModifierForm.More:
                    p.More *= 1 + v;
                    break;
                case ModifierForm.Override:
                    if (p.Override is null || m.Priority >= overridePri)
                    {
                        p.Override = v;
                        overridePri = m.Priority;
                    }
                    break;
                case ModifierForm.Link:
                    if (!noLinks && !string.IsNullOrEmpty(m.From))
                    {
                        (linkList ??= new List<(Modifier, double)>()).Add((m, v));
                    }
                    break;
            }
        }

        if (_byAttribute.TryGetValue(attribute, out var list))
        {
            foreach (var m in list)
            {
                ApplyOne(m, ref parts, ref overridePriority, ref links);
            }
        }
        if (extra is not null)
        {
            foreach (var m in extra)
            {
                ApplyOne(m, ref parts, ref overridePriority, ref links);
            }
        }

        if (links is not null)
        {
            foreach (var (mod, ratio) in links)
            {
                var source = Guarded(attribute, () => Compute(mod.From!, BaseFor(mod.From!, owner), owner, context, extra, applyBounds: true, noLinks: true, out _));
                parts.Flat += ratio * source;
            }
        }

        var result = parts.Override ?? (baseValue + parts.Flat) * (1 + parts.Increased) * parts.More;

        if (applyBounds && _catalog.TryGet(attribute, out var def) && def is not null)
        {
            if (def.Min is { } min && result < min)
            {
                result = min;
            }
            if (def.Max is { } max && result > max)
            {
                result = max;
            }
            if (def.Decimals is { } decimals)
            {
                result = Math.Round(result, Math.Clamp(decimals, 0, 10), MidpointRounding.AwayFromZero);
            }
        }
        return result;
    }

    private double Guarded(string attribute, Func<double> compute)
    {
        if (!_resolving.Add(attribute))
        {
            throw new AttributeCycleException(attribute);
        }
        try
        {
            return compute();
        }
        finally
        {
            _resolving.Remove(attribute);
        }
    }

    private bool GuardedIsTrue(string attribute, Expr expr, IExprScope owner) => Guarded(attribute, () => expr.Evaluate(owner)) != 0;
}
