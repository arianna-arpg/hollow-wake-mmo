using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HollowWake.Core.Definitions;
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
/// <remarks>
/// Every attribute the set has seen has one entry: its modifiers in insertion order, a flat view of them
/// the fold walks (rebuilt after a change), its catalog definition, the owner's base override, cached
/// results and its cycle guard. A fold allocates nothing; only <see cref="Explain"/> collects the
/// modifiers it applied.
/// </remarks>
public sealed class AttributeSet
{
    private readonly AttributeCatalog _catalog;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    // Entries in the order they first received a modifier: the order AllModifiers and ModifiedAttributes report.
    private readonly List<Entry> _modified = new();
    private readonly Dictionary<string, double> _baseOverrides = new(StringComparer.Ordinal);
    // Cached results hold while their epoch matches; Touch moves the epoch rather than clearing anything.
    private long _epoch = 1;
    // The last entry looked up: repeated queries of one attribute (a hit's threads all fold "damage") skip the hash.
    private Entry? _recent;
    // A links list kept for the next fold that needs one.
    private List<(Modifier Mod, double Ratio)>? _spareLinks;

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
        var entry = EntryFor(attribute);
        entry.HasBase = true;
        entry.Base = value;
        Recount(entry);
        Touch();
    }

    public bool ClearBase(string attribute)
    {
        var removed = _baseOverrides.Remove(attribute);
        if (removed)
        {
            if (_entries.TryGetValue(attribute, out var entry))
            {
                entry.HasBase = false;
                Recount(entry);
            }
            Touch();
        }
        return removed;
    }

    public bool TryGetBaseOverride(string attribute, out double value) => _baseOverrides.TryGetValue(attribute, out value);

    public IEnumerable<KeyValuePair<string, double>> BaseOverrides => _baseOverrides;

    public void Add(Modifier modifier)
    {
        var entry = EntryFor(modifier.Attribute);
        entry.Append(modifier);
        if (!entry.Listed)
        {
            entry.Listed = true;
            _modified.Add(entry);
        }
        Recount(entry);
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
        foreach (var entry in _modified)
        {
            var n = entry.RemoveSource(source);
            if (n > 0)
            {
                removed += n;
                Recount(entry);
            }
        }
        if (removed > 0)
        {
            Touch();
        }
        return removed;
    }

    public bool HasSource(string source)
    {
        foreach (var entry in _modified)
        {
            var mods = entry.Mods;
            for (var i = 0; i < entry.Count; i++)
            {
                if (mods[i].Source == source)
                {
                    return true;
                }
            }
        }
        return false;
    }

    public bool Remove(Modifier modifier)
    {
        if (_entries.TryGetValue(modifier.Attribute, out var entry) && entry.RemoveFirst(modifier))
        {
            Recount(entry);
            Touch();
            return true;
        }
        return false;
    }

    public void Clear()
    {
        foreach (var entry in _modified)
        {
            entry.ClearModifiers();
            entry.Listed = false;
            Recount(entry);
        }
        _modified.Clear();
        foreach (var attribute in _baseOverrides.Keys)
        {
            if (_entries.TryGetValue(attribute, out var entry))
            {
                entry.HasBase = false;
                Recount(entry);
            }
        }
        _baseOverrides.Clear();
        Touch();
    }

    // How many members of each attribute family the owner carries (has modifiers on or a base of its own).
    private Dictionary<string, int>? _families;

    /// <summary>
    /// Whether the owner carries any member of an attribute family: modifiers on it or a base of its own. Hot
    /// paths ask before walking a family member by member (added damage per Thread, a chance per status, leech
    /// per resource), so an owner that touches none of it costs one lookup.
    /// </summary>
    public bool CarriesFamily(string family) => _families is not null && _families.TryGetValue(family, out var n) && n > 0;

    /// <summary>Keeps the family counts in step with whether an entry carries anything.</summary>
    private void Recount(Entry entry)
    {
        var carrying = entry.Count > 0 || entry.HasBase;
        if (carrying == entry.Carrying)
        {
            return;
        }
        entry.Carrying = carrying;
        if (entry.Def?.Family is not { } family)
        {
            return;
        }
        _families ??= new Dictionary<string, int>(StringComparer.Ordinal);
        _families[family] = (_families.TryGetValue(family, out var n) ? n : 0) + (carrying ? 1 : -1);
    }

    /// <summary>Call when something outside the set that formulas read (level, flags, gauges) has changed.</summary>
    public void Touch()
    {
        Version++;
        _epoch++;
    }

    // ------------------------------------------------------------ queries

    public IEnumerable<Modifier> ModifiersFor(string attribute) =>
        _entries.TryGetValue(attribute, out var entry) && entry.Listed ? entry.Modifiers() : Enumerable.Empty<Modifier>();

    public IEnumerable<Modifier> AllModifiers
    {
        get
        {
            foreach (var entry in _modified)
            {
                for (var i = 0; i < entry.Count; i++)
                {
                    yield return entry.Mods[i];
                }
            }
        }
    }

    public IEnumerable<string> ModifiedAttributes
    {
        get
        {
            foreach (var entry in _modified)
            {
                if (entry.Count > 0)
                {
                    yield return entry.Attribute;
                }
            }
        }
    }

    public bool HasModifiers(string attribute) => _entries.TryGetValue(attribute, out var entry) && entry.Count > 0;

    /// <summary>
    /// Whether the attribute can be anything but zero for this owner: it has modifiers, a base of the owner's
    /// own, or a catalog base or formula. Hot paths ask before folding attributes most owners never touch
    /// (added damage per Thread, status chances, leech), so an untouched one costs a lookup and no fold.
    /// </summary>
    public bool Carries(string attribute)
    {
        if (_entries.TryGetValue(attribute, out var entry))
        {
            if (entry.Count > 0 || entry.HasBase)
            {
                return true;
            }
            return entry.Def is { } def && (def.Formula is not null || def.Base != 0);
        }
        return _catalog.Get(attribute) is { } d && (d.Formula is not null || d.Base != 0);
    }

    /// <summary>Computes the attribute with no tag context (only unscoped modifiers apply).</summary>
    public double Get(string attribute, IExprScope owner) => Get(attribute, owner, TagSet.Empty);

    /// <summary>
    /// Computes the attribute for a tag context. Scoped modifiers apply when the context contains all
    /// of their scope tags. The result is cached per (attribute, context) until the set changes.
    /// </summary>
    public double Get(string attribute, IExprScope owner, TagSet context)
    {
        var entry = EntryFor(attribute);
        context ??= TagSet.Empty;
        if (context.IsEmpty)
        {
            if (entry.EmptyEpoch == _epoch)
            {
                return entry.EmptyValue;
            }
            var value = Compute(entry, BaseFor(entry, owner), owner, context, null, applyBounds: true, noLinks: false, null, out _);
            // Read the epoch after the fold: a Touch during it leaves the result cached for the new epoch, as a cleared cache would.
            entry.EmptyValue = value;
            entry.EmptyEpoch = _epoch;
            return value;
        }
        if (entry.LastEpoch == _epoch && ReferenceEquals(entry.LastContext, context))
        {
            return entry.LastValue;
        }
        return GetInContext(entry, owner, context);
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
        var entry = EntryFor(attribute);
        return Compute(entry, baseValue ?? BaseFor(entry, owner), owner, context ?? TagSet.Empty, extra, applyBounds: true, noLinks: false, null, out _);
    }

    /// <summary>
    /// Applies this owner's modifier stack for an attribute to an externally supplied base, without
    /// catalog bounds. The damage pipeline uses it: the weave produces a base amount, then the caster's
    /// <c>damage</c> stack (flat, increased, more) scoped by the hit's tags scales it.
    /// </summary>
    public double Apply(string attribute, double baseValue, IExprScope owner, TagSet context, IReadOnlyList<Modifier>? extra = null) =>
        Compute(EntryFor(attribute), baseValue, owner, context ?? TagSet.Empty, extra, applyBounds: false, noLinks: false, null, out _);

    /// <summary>Only the multiplicative part of the stack: (1 + increased) * more.</summary>
    public double Multiplier(string attribute, IExprScope owner, TagSet context, IReadOnlyList<Modifier>? extra = null)
    {
        Compute(EntryFor(attribute), 0, owner, context ?? TagSet.Empty, extra, applyBounds: false, noLinks: false, null, out var fold);
        return (1 + fold.Increased) * fold.More;
    }

    public double FlatBonus(string attribute, IExprScope owner, TagSet context, IReadOnlyList<Modifier>? extra = null)
    {
        Compute(EntryFor(attribute), 0, owner, context ?? TagSet.Empty, extra, applyBounds: false, noLinks: false, null, out var fold);
        return fold.Flat;
    }

    public AttributeBreakdown Explain(string attribute, IExprScope owner, TagSet? context = null, IReadOnlyList<Modifier>? extra = null)
    {
        context ??= TagSet.Empty;
        var entry = EntryFor(attribute);
        var baseValue = BaseFor(entry, owner);
        var applied = new List<Modifier>();
        var result = Compute(entry, baseValue, owner, context, extra, applyBounds: true, noLinks: false, applied, out var fold);
        return new AttributeBreakdown(attribute, baseValue, fold.Flat, fold.Increased, fold.More, fold.HasOverride ? fold.Override : null, result, applied);
    }

    // ------------------------------------------------------------ the fold

    private Entry EntryFor(string attribute)
    {
        var recent = _recent;
        if (recent is not null && string.Equals(recent.Attribute, attribute, StringComparison.Ordinal))
        {
            return recent;
        }
        ref var slot = ref CollectionsMarshal.GetValueRefOrAddDefault(_entries, attribute, out _);
        slot ??= new Entry(attribute, _catalog.Get(attribute));
        _recent = slot;
        return slot;
    }

    private double GetInContext(Entry entry, IExprScope owner, TagSet context)
    {
        if (entry.ContextsEpoch == _epoch && entry.Contexts!.TryGetValue(context, out var cached))
        {
            entry.Remember(context, cached, _epoch);
            return cached;
        }
        var value = Compute(entry, BaseFor(entry, owner), owner, context, null, applyBounds: true, noLinks: false, null, out _);
        var contexts = entry.Contexts ??= new Dictionary<TagSet, double>();
        if (entry.ContextsEpoch != _epoch)
        {
            contexts.Clear();
            entry.ContextsEpoch = _epoch;
        }
        contexts[context] = value;
        entry.Remember(context, value, _epoch);
        return value;
    }

    private static double BaseFor(Entry entry, IExprScope owner)
    {
        if (entry.HasBase)
        {
            return entry.Base;
        }
        if (entry.Def is { } def)
        {
            return def.Formula is { } formula ? Guarded(entry, formula, owner) : def.Base;
        }
        return 0;
    }

    private struct Fold
    {
        public double Flat;
        public double Increased;
        public double More;
        public double Override;
        public bool HasOverride;
        public int OverridePriority;
        public List<(Modifier Mod, double Ratio)>? Links;
    }

    // The fold's own steps (this loop, FoldExtra, TryEvaluate, Guarded) are compiled optimized on first
    // use instead of starting in the unoptimized tier: every hit runs them, and a fresh process or zone
    // would otherwise fold several times slower for its first fraction of a second. The formulas they
    // evaluate still tier normally, so profile-guided optimization keeps working where it pays.
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private double Compute(Entry entry, double baseValue, IExprScope owner, TagSet context, IReadOnlyList<Modifier>? extra, bool applyBounds, bool noLinks, List<Modifier>? applied, out Fold fold)
    {
        fold = default;
        fold.More = 1;

        if (entry.Stale)
        {
            entry.Rebuild();
        }
        var slots = entry.Slots;
        if (slots.Length > 0)
        {
            var scopes = entry.Scopes;
            // Each distinct scope is matched once per context; past 64 of them, each modifier matches on its own.
            var masked = scopes.Length <= 64;
            var matched = masked && scopes.Length > 0 ? Matches(entry, scopes, context) : 0UL;
            for (var i = 0; i < slots.Length; i++)
            {
                ref readonly var slot = ref slots[i];
                if (slot.Scope >= 0 && (masked ? (matched & (1UL << slot.Scope)) == 0 : !context.HasAll(scopes[slot.Scope])))
                {
                    continue;
                }
                double v;
                if (!slot.Dynamic)
                {
                    v = slot.Value;
                }
                else if (!TryEvaluate(entry, slot.Modifier, owner, out v))
                {
                    continue;
                }
                applied?.Add(slot.Modifier);
                Accumulate(ref fold, slot.Modifier, slot.Form, slot.Priority, v, noLinks);
            }
        }

        if (extra is not null)
        {
            if (extra is List<Modifier> list)
            {
                foreach (var m in CollectionsMarshal.AsSpan(list))
                {
                    FoldExtra(entry, m, owner, context, noLinks, applied, ref fold);
                }
            }
            else if (extra is Modifier[] array)
            {
                foreach (var m in array)
                {
                    FoldExtra(entry, m, owner, context, noLinks, applied, ref fold);
                }
            }
            else
            {
                for (var i = 0; i < extra.Count; i++)
                {
                    FoldExtra(entry, extra[i], owner, context, noLinks, applied, ref fold);
                }
            }
        }

        if (fold.Links is { } links)
        {
            // Link sources are read after this attribute's own modifiers, with links disabled (single hop).
            for (var i = 0; i < links.Count; i++)
            {
                var (mod, ratio) = links[i];
                var source = EntryFor(mod.From!);
                if (entry.Resolving)
                {
                    throw new AttributeCycleException(entry.Attribute);
                }
                entry.Resolving = true;
                double sourceValue;
                try
                {
                    sourceValue = Compute(source, BaseFor(source, owner), owner, context, extra, applyBounds: true, noLinks: true, null, out _);
                }
                finally
                {
                    entry.Resolving = false;
                }
                fold.Flat += ratio * sourceValue;
            }
            fold.Links = null;
            links.Clear();
            _spareLinks = links;
        }

        var result = fold.HasOverride ? fold.Override : (baseValue + fold.Flat) * (1 + fold.Increased) * fold.More;

        if (applyBounds && entry.Def is { } def)
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

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void FoldExtra(Entry entry, Modifier m, IExprScope owner, TagSet context, bool noLinks, List<Modifier>? applied, ref Fold fold)
    {
        if (!string.Equals(m.Attribute, entry.Attribute, StringComparison.Ordinal))
        {
            return;
        }
        if (m.Scope is { IsEmpty: false } scope && !context.HasAll(scope))
        {
            return;
        }
        double v;
        if (m.Condition is null && m.ValueExpr is null && m.Gauge is null)
        {
            v = m.Value;
        }
        else if (!TryEvaluate(entry, m, owner, out v))
        {
            return;
        }
        applied?.Add(m);
        Accumulate(ref fold, m, m.Form, m.Priority, v, noLinks);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Accumulate(ref Fold fold, Modifier m, ModifierForm form, int priority, double v, bool noLinks)
    {
        switch (form)
        {
            case ModifierForm.Flat:
                fold.Flat += v;
                break;
            case ModifierForm.Increased:
                fold.Increased += v;
                break;
            case ModifierForm.More:
                fold.More *= 1 + v;
                break;
            case ModifierForm.Override:
                // The highest priority wins; among equals, the later one.
                if (!fold.HasOverride || priority >= fold.OverridePriority)
                {
                    fold.Override = v;
                    fold.OverridePriority = priority;
                    fold.HasOverride = true;
                }
                break;
            case ModifierForm.Link:
                if (!noLinks && !string.IsNullOrEmpty(m.From))
                {
                    var links = fold.Links;
                    if (links is null)
                    {
                        links = _spareLinks ?? new List<(Modifier, double)>();
                        _spareLinks = null;
                        fold.Links = links;
                    }
                    links.Add((m, v));
                }
                break;
        }
    }

    /// <summary>The condition, value expression and gauge of a modifier. False when it does not apply.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static bool TryEvaluate(Entry entry, Modifier m, IExprScope owner, out double value)
    {
        value = 0;
        if (m.Condition is { } condition && Guarded(entry, condition, owner) == 0)
        {
            return false;
        }
        var v = m.ValueExpr is { } expr ? Guarded(entry, expr, owner) : m.Value;
        if (m.Gauge is { } gauge)
        {
            var g = owner.TryResolve(gauge, out var reading) ? reading : 0;
            if (m.GaugeAt is { } at)
            {
                if (g < at)
                {
                    return false;
                }
            }
            else
            {
                if (g == 0)
                {
                    return false;
                }
                v *= g;
            }
        }
        value = v;
        return true;
    }

    /// <summary>Evaluates a formula on behalf of an attribute; reaching the same attribute again while it runs is a cycle.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static double Guarded(Entry entry, Expr expr, IExprScope owner)
    {
        if (entry.Resolving)
        {
            throw new AttributeCycleException(entry.Attribute);
        }
        entry.Resolving = true;
        try
        {
            return expr.Evaluate(owner);
        }
        finally
        {
            entry.Resolving = false;
        }
    }

    /// <summary>Which of an entry's distinct scopes a context satisfies, remembered for the last context (tag sets are immutable).</summary>
    private static ulong Matches(Entry entry, TagSet[] scopes, TagSet context)
    {
        if (ReferenceEquals(entry.MatchedContext, context))
        {
            return entry.MatchedMask;
        }
        var mask = 0UL;
        if (!context.IsEmpty)
        {
            for (var k = 0; k < scopes.Length; k++)
            {
                if (context.HasAll(scopes[k]))
                {
                    mask |= 1UL << k;
                }
            }
        }
        entry.MatchedContext = context;
        entry.MatchedMask = mask;
        return mask;
    }

    /// <summary>One modifier as the fold reads it: the fields it needs copied flat, its scope as an index.</summary>
    private struct Slot
    {
        public Modifier Modifier;
        public double Value;
        public ModifierForm Form;
        public int Priority;
        /// <summary>Index into the entry's distinct scopes, or -1 when unscoped.</summary>
        public int Scope;
        /// <summary>Has a condition, a value expression or a gauge, so it is evaluated per fold.</summary>
        public bool Dynamic;
    }

    private sealed class Entry
    {
        public readonly string Attribute;
        public readonly AttributeDef? Def;

        // Modifiers in insertion order. Appends write past the end; removals swap in a new array, so a fold
        // in progress keeps a consistent view.
        public Modifier[] Mods = Array.Empty<Modifier>();
        public int Count;
        public bool Listed;

        // The view the fold walks, rebuilt (into new arrays) after a change.
        public bool Stale;
        public Slot[] Slots = Array.Empty<Slot>();
        public TagSet[] Scopes = Array.Empty<TagSet>();
        public TagSet? MatchedContext;
        public ulong MatchedMask;

        public bool HasBase;
        public double Base;

        /// <summary>Counted in its family as carried: it has modifiers or a base of its own.</summary>
        public bool Carrying;

        public bool Resolving;

        // Cached results, valid while their epoch is the set's.
        public long EmptyEpoch;
        public double EmptyValue;
        public long LastEpoch;
        public TagSet? LastContext;
        public double LastValue;
        public long ContextsEpoch;
        public Dictionary<TagSet, double>? Contexts;

        public Entry(string attribute, AttributeDef? def)
        {
            Attribute = attribute;
            Def = def;
        }

        public void Remember(TagSet context, double value, long epoch)
        {
            LastContext = context;
            LastValue = value;
            LastEpoch = epoch;
        }

        public IEnumerable<Modifier> Modifiers()
        {
            for (var i = 0; i < Count; i++)
            {
                yield return Mods[i];
            }
        }

        public void Append(Modifier modifier)
        {
            if (Count == Mods.Length)
            {
                var grown = new Modifier[Math.Max(4, Count * 2)];
                Array.Copy(Mods, grown, Count);
                Mods = grown;
            }
            Mods[Count++] = modifier;
            Stale = true;
        }

        public bool RemoveFirst(Modifier modifier)
        {
            for (var i = 0; i < Count; i++)
            {
                if (EqualityComparer<Modifier>.Default.Equals(Mods[i], modifier))
                {
                    var kept = new Modifier[Mods.Length];
                    Array.Copy(Mods, kept, i);
                    Array.Copy(Mods, i + 1, kept, i, Count - i - 1);
                    Mods = kept;
                    Count--;
                    Stale = true;
                    return true;
                }
            }
            return false;
        }

        public int RemoveSource(string source)
        {
            var matches = 0;
            for (var i = 0; i < Count; i++)
            {
                if (string.Equals(Mods[i].Source, source, StringComparison.Ordinal))
                {
                    matches++;
                }
            }
            if (matches == 0)
            {
                return 0;
            }
            var kept = new Modifier[Mods.Length];
            var n = 0;
            for (var i = 0; i < Count; i++)
            {
                if (!string.Equals(Mods[i].Source, source, StringComparison.Ordinal))
                {
                    kept[n++] = Mods[i];
                }
            }
            Mods = kept;
            Count = n;
            Stale = true;
            return matches;
        }

        public void ClearModifiers()
        {
            Mods = Array.Empty<Modifier>();
            Count = 0;
            Stale = true;
        }

        public void Rebuild()
        {
            var slots = Count == 0 ? Array.Empty<Slot>() : new Slot[Count];
            List<TagSet>? scopes = null;
            for (var i = 0; i < Count; i++)
            {
                var m = Mods[i];
                var scope = -1;
                if (m.Scope is { IsEmpty: false } tags)
                {
                    scopes ??= new List<TagSet>();
                    scope = scopes.IndexOf(tags);
                    if (scope < 0)
                    {
                        scope = scopes.Count;
                        scopes.Add(tags);
                    }
                }
                slots[i] = new Slot
                {
                    Modifier = m,
                    Value = m.Value,
                    Form = m.Form,
                    Priority = m.Priority,
                    Scope = scope,
                    Dynamic = m.Condition is not null || m.ValueExpr is not null || m.Gauge is not null,
                };
            }
            Slots = slots;
            Scopes = scopes is null ? Array.Empty<TagSet>() : scopes.ToArray();
            MatchedContext = null;
            Stale = false;
        }
    }
}
