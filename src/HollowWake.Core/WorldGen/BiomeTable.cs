using System.Collections;
using System.Runtime.CompilerServices;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Tags;

namespace HollowWake.Core.WorldGen;

/// <summary>
/// The biome table compiled once per generator. Each biome, in specificity order, carries every window
/// of its parent chain as plain comparisons, so matching a cell is a scan with no lookups, no recursion
/// and no allocation. Thread windows read "slots": the Thread ids any biome names, in first-seen order,
/// so a cell only needs the densities a biome can actually read. The surface tag sets the terrain pass
/// writes (plain, deep and shallow water, cliff) are built here once instead of per cell.
/// </summary>
internal sealed class BiomeTable
{
    /// <summary>Dimensions of the value vector a match reads; Thread slots follow.</summary>
    public const int RelativeElevation = 0;
    public const int Temperature = 1;
    public const int Moisture = 2;
    public const int Slope = 3;
    public const int FirstSlot = 4;

    public const int VariantDry = 0;
    public const int VariantDeep = 1;
    public const int VariantShallow = 2;
    public const int VariantCliff = 3;
    public const int Variants = 4;

    private readonly Rule[] _rules;
    private readonly string[] _slotThreads;
    private readonly int[] _slotThreadIndex;
    private readonly TagSet[] _surfaces;
    private readonly int[] _surfaceKeys;
    // When no biome restricts its regions every region allows every rule, and they all share this mask.
    private readonly bool[]? _allowAll;

    public BiomeTable(IReadOnlyList<BiomeDef> bySpecificity, ContentRegistry content, ThreadKeys threads)
    {
        var slots = new List<string>();
        _rules = new Rule[bySpecificity.Count];
        var restricted = false;
        for (var r = 0; r < _rules.Length; r++)
        {
            _rules[r] = Compile(bySpecificity[r], content, slots);
            restricted |= _rules[r].Regions.Length > 0;
        }
        if (!restricted)
        {
            _allowAll = new bool[_rules.Length];
            Array.Fill(_allowAll, true);
        }
        _slotThreads = slots.ToArray();
        _slotThreadIndex = new int[_slotThreads.Length];
        for (var s = 0; s < _slotThreads.Length; s++)
        {
            _slotThreadIndex[s] = threads.IndexOf(_slotThreads[s]);
        }

        // Surface tags per (biome or none, variant), exactly as the terrain pass composes them, and for
        // each the first entry with the same text, which is what the pass keys its per-chunk cache on.
        var ground = TagSet.Of("ground");
        _surfaces = new TagSet[(_rules.Length + 1) * Variants];
        for (var r = -1; r < _rules.Length; r++)
        {
            var dry = ground.Union(r < 0 ? TagSet.Empty : _rules[r].Biome.Surface);
            var at = (r + 1) * Variants;
            _surfaces[at + VariantDry] = dry;
            _surfaces[at + VariantDeep] = dry.With("water", "deep");
            _surfaces[at + VariantShallow] = dry.With("water", "shallow");
            _surfaces[at + VariantCliff] = dry.With("cliff");
        }
        _surfaceKeys = new int[_surfaces.Length];
        var texts = new string[_surfaces.Length];
        for (var k = 0; k < _surfaces.Length; k++)
        {
            texts[k] = _surfaces[k].ToString();
            _surfaceKeys[k] = k;
            for (var j = 0; j < k; j++)
            {
                if (string.Equals(texts[j], texts[k], StringComparison.Ordinal))
                {
                    _surfaceKeys[k] = j;
                    break;
                }
            }
        }
    }

    public int Count => _rules.Length;

    /// <summary>Length of the value vector: four climate dimensions plus one per Thread slot.</summary>
    public int Width => FirstSlot + _slotThreads.Length;

    public int SlotCount => _slotThreads.Length;

    /// <summary>The generator's index of the Thread behind a slot, or -1 when no such Thread exists.</summary>
    public int SlotThreadIndex(int slot) => _slotThreadIndex[slot];

    public BiomeDef Biome(int rule) => _rules[rule].Biome;

    /// <summary>The surface tags for a rule (-1 for no biome) and variant.</summary>
    public TagSet Surface(int rule, int variant) => _surfaces[(rule + 1) * Variants + variant];

    /// <summary>A cache key for <see cref="Surface"/>: equal for entries whose tag sets print the same.</summary>
    public int SurfaceKey(int rule, int variant) => _surfaceKeys[(rule + 1) * Variants + variant];

    public int SurfaceKeyCount => _surfaceKeys.Length;

    /// <summary>Which rules a region archetype allows, for every rule's chain of region restrictions. Callers must not modify it.</summary>
    public bool[] RegionMask(RegionArchetypeDef archetype)
    {
        if (_allowAll is not null)
        {
            return _allowAll;
        }
        var mask = new bool[_rules.Length];
        for (var r = 0; r < mask.Length; r++)
        {
            mask[r] = AllowsRegion(_rules[r], archetype);
        }
        return mask;
    }

    private static bool AllowsRegion(Rule rule, RegionArchetypeDef archetype)
    {
        foreach (var regions in rule.Regions)
        {
            if (!regions.Has(archetype.Id) && !archetype.Tags.HasAny(regions))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// The first rule in specificity order whose windows all hold, or -1. Inlined into the terrain pass,
    /// which calls it per cell; the planner's few calls run the shared copy.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Match(ReadOnlySpan<double> values, bool[] regionOk)
    {
        var rules = _rules;
        for (var r = 0; r < rules.Length; r++)
        {
            if (!regionOk[r])
            {
                continue;
            }
            var checks = rules[r].Checks;
            var ok = true;
            for (var c = 0; c < checks.Length; c++)
            {
                var v = values[checks[c].Dimension];
                if (!(v >= checks[c].Min && v <= checks[c].Max))
                {
                    ok = false;
                    break;
                }
            }
            if (ok)
            {
                return r;
            }
        }
        return -1;
    }

    /// <summary>Matches a sample from anywhere, region restrictions evaluated for this region.</summary>
    public int Match(ReadOnlySpan<double> values, RegionArchetypeDef archetype)
    {
        for (var r = 0; r < _rules.Length; r++)
        {
            if (!AllowsRegion(_rules[r], archetype))
            {
                continue;
            }
            var checks = _rules[r].Checks;
            var ok = true;
            for (var c = 0; c < checks.Length; c++)
            {
                var v = values[checks[c].Dimension];
                if (!(v >= checks[c].Min && v <= checks[c].Max))
                {
                    ok = false;
                    break;
                }
            }
            if (ok)
            {
                return r;
            }
        }
        return -1;
    }

    /// <summary>Fills the Thread slots of a value vector from any density map; a missing Thread reads as NaN, which no window holds.</summary>
    public void ReadSlots(IReadOnlyDictionary<string, double> threads, ThreadKeys keys, Span<double> values)
    {
        if (threads is ThreadDensities own && ReferenceEquals(own.Keys, keys))
        {
            for (var s = 0; s < _slotThreads.Length; s++)
            {
                var k = _slotThreadIndex[s];
                values[FirstSlot + s] = k < 0 ? double.NaN : own[k];
            }
            return;
        }
        for (var s = 0; s < _slotThreads.Length; s++)
        {
            values[FirstSlot + s] = threads.TryGetValue(_slotThreads[s], out var v) ? v : double.NaN;
        }
    }

    private static Rule Compile(BiomeDef biome, ContentRegistry content, List<string> slots)
    {
        var checks = new List<Check>();
        var regions = new List<TagSet>();
        var cur = biome;
        // A child must satisfy its parents: every window up the chain joins the rule.
        for (var depth = 0; cur is not null && depth < 64; depth++)
        {
            Add(checks, RelativeElevation, cur.Elevation);
            Add(checks, Temperature, cur.Temperature);
            Add(checks, Moisture, cur.Moisture);
            Add(checks, Slope, cur.Slope);
            foreach (var (thread, window) in cur.Threads)
            {
                var slot = slots.IndexOf(thread);
                if (slot < 0)
                {
                    slots.Add(thread);
                    slot = slots.Count - 1;
                }
                Add(checks, FirstSlot + slot, window);
            }
            if (!cur.Regions.IsEmpty)
            {
                regions.Add(cur.Regions);
            }
            cur = cur.Parent is not null && content.TryGet<BiomeDef>(cur.Parent, out var parent) ? parent : null;
        }
        return new Rule(biome, checks.ToArray(), regions.ToArray());
    }

    private static void Add(List<Check> checks, int dimension, Window? window)
    {
        if (window is not null)
        {
            checks.Add(new Check(dimension, window.Min, window.Max));
        }
    }

    private readonly struct Check
    {
        public readonly int Dimension;
        public readonly double Min;
        public readonly double Max;

        public Check(int dimension, double min, double max)
        {
            Dimension = dimension;
            Min = min;
            Max = max;
        }
    }

    private sealed class Rule
    {
        public readonly BiomeDef Biome;
        public readonly Check[] Checks;
        public readonly TagSet[] Regions;

        public Rule(BiomeDef biome, Check[] checks, TagSet[] regions)
        {
            Biome = biome;
            Checks = checks;
            Regions = regions;
        }
    }
}

/// <summary>The generator's Thread ids in content order, with their indices.</summary>
internal sealed class ThreadKeys
{
    private readonly Dictionary<string, int> _index = new(StringComparer.Ordinal);

    public ThreadKeys(IEnumerable<string> ids)
    {
        var list = new List<string>();
        foreach (var id in ids)
        {
            if (_index.TryAdd(id, list.Count))
            {
                list.Add(id);
            }
        }
        Ids = list.ToArray();
        ReadOnlyIds = Array.AsReadOnly(Ids);
    }

    public string[] Ids { get; }
    public IReadOnlyList<string> ReadOnlyIds { get; }
    public int Count => Ids.Length;

    public int IndexOf(string id) => _index.TryGetValue(id, out var i) ? i : -1;
}

/// <summary>
/// Thread densities at a point, keyed by Thread id in the content's Thread order. Lookups, enumeration
/// order and missing-key behavior match the dictionary a sample used to carry. The densities are
/// computed on first read: they depend only on the point, the noise and the regions, which never
/// change, and on overlay offsets, which are captured when the sample is taken. So a later read gives
/// exactly what an eager sample would have held, and a caller that never reads them pays nothing.
/// </summary>
internal sealed class ThreadDensities : IReadOnlyDictionary<string, double>
{
    private readonly WorldGenerator _gen;
    private readonly double _x;
    private readonly double _y;
    private readonly double[]? _offsets;
    private double[]? _densities;

    public ThreadDensities(WorldGenerator gen, double x, double y, double[]? offsets)
    {
        _gen = gen;
        _x = x;
        _y = y;
        _offsets = offsets;
    }

    public ThreadKeys Keys => _gen.Threads;

    // Racing first reads compute the same values; either array may win.
    private double[] Densities => Volatile.Read(ref _densities) ?? Compute();

    private double[] Compute()
    {
        var densities = _gen.ThreadDensitiesAt(_x, _y, _offsets);
        Volatile.Write(ref _densities, densities);
        return densities;
    }

    public double this[int index] => Densities[index];

    public double this[string key] => TryGetValue(key, out var v) ? v : throw new KeyNotFoundException($"The given key '{key}' was not present in the dictionary.");

    public int Count => Keys.Count;

    IEnumerable<string> IReadOnlyDictionary<string, double>.Keys => Keys.ReadOnlyIds;

    public IEnumerable<double> Values => Array.AsReadOnly(Densities);

    public bool ContainsKey(string key) => Keys.IndexOf(key) >= 0;

    public bool TryGetValue(string key, out double value)
    {
        ArgumentNullException.ThrowIfNull(key);
        var i = Keys.IndexOf(key);
        if (i < 0)
        {
            value = default;
            return false;
        }
        value = Densities[i];
        return true;
    }

    public IEnumerator<KeyValuePair<string, double>> GetEnumerator()
    {
        var ids = Keys.Ids;
        var densities = Densities;
        for (var i = 0; i < densities.Length; i++)
        {
            yield return new KeyValuePair<string, double>(ids[i], densities[i]);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
