namespace Skein.Core.Maths;

/// <summary>
/// Deterministic random number generator (xoshiro256**). Every roll in the simulation flows through
/// an <see cref="Rng"/> seeded from the zone, entity or action, so a fight, a drop or a generated
/// Knot can be replayed exactly from its seed. Never use <see cref="System.Random"/> in gameplay code.
/// </summary>
public sealed class Rng
{
    private ulong _s0, _s1, _s2, _s3;

    public ulong Seed { get; }

    public Rng(ulong seed)
    {
        Seed = seed;
        var sm = seed;
        _s0 = SplitMix64(ref sm);
        _s1 = SplitMix64(ref sm);
        _s2 = SplitMix64(ref sm);
        _s3 = SplitMix64(ref sm);
        if ((_s0 | _s1 | _s2 | _s3) == 0)
        {
            _s0 = 0x9E3779B97F4A7C15UL;
        }
    }

    public Rng(string seed) : this(Hash64(seed))
    {
    }

    public ulong NextUInt64()
    {
        var result = RotL(_s1 * 5, 7) * 9;
        var t = _s1 << 17;
        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = RotL(_s3, 45);
        return result;
    }

    /// <summary>Uniform double in [0, 1).</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    /// <summary>Uniform integer in [0, maxExclusive).</summary>
    public int Next(int maxExclusive)
    {
        if (maxExclusive <= 0)
        {
            return 0;
        }
        return (int)(NextDouble() * maxExclusive);
    }

    /// <summary>Uniform integer in [min, max], inclusive on both ends.</summary>
    public int Range(int minInclusive, int maxInclusive)
    {
        if (maxInclusive <= minInclusive)
        {
            return minInclusive;
        }
        return minInclusive + Next(maxInclusive - minInclusive + 1);
    }

    public double Range(double min, double max) => max <= min ? min : min + NextDouble() * (max - min);

    public bool Chance(double probability) => probability >= 1 || (probability > 0 && NextDouble() < probability);

    public T Pick<T>(IReadOnlyList<T> items)
    {
        if (items.Count == 0)
        {
            throw new InvalidOperationException("Cannot pick from an empty list.");
        }
        return items[Next(items.Count)];
    }

    /// <summary>Returns the index chosen by weight, or -1 when every weight is zero or negative.</summary>
    public int PickWeighted(IReadOnlyList<double> weights)
    {
        double total = 0;
        foreach (var w in weights)
        {
            if (w > 0)
            {
                total += w;
            }
        }
        if (total <= 0)
        {
            return -1;
        }
        var roll = NextDouble() * total;
        for (var i = 0; i < weights.Count; i++)
        {
            var w = weights[i];
            if (w <= 0)
            {
                continue;
            }
            if (roll < w)
            {
                return i;
            }
            roll -= w;
        }
        return weights.Count - 1;
    }

    /// <summary>Gaussian sample via Box-Muller, clamped to roughly plus or minus four sigma.</summary>
    public double Gaussian(double mean = 0, double stdDev = 1)
    {
        var u1 = 1.0 - NextDouble();
        var u2 = NextDouble();
        var z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        z = Math.Clamp(z, -4, 4);
        return mean + z * stdDev;
    }

    public void Shuffle<T>(IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    /// <summary>
    /// Derives an independent generator from this generator's seed and a salt, without consuming state.
    /// Use it to give each entity, zone or action its own stream: <c>zoneRng.Fork("spawns")</c>.
    /// </summary>
    public Rng Fork(ulong salt)
    {
        var mixed = Seed ^ (salt + 0x9E3779B97F4A7C15UL + (Seed << 6) + (Seed >> 2));
        return new Rng(SplitMix64(ref mixed));
    }

    public Rng Fork(string salt) => Fork(Hash64(salt));

    /// <summary>FNV-1a 64-bit hash of a string; stable across processes and platforms.</summary>
    public static ulong Hash64(string text)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var h = offset;
        foreach (var c in text)
        {
            h ^= c;
            h *= prime;
        }
        return h;
    }

    public static ulong Combine(ulong a, ulong b)
    {
        var x = a ^ (b + 0x9E3779B97F4A7C15UL + (a << 6) + (a >> 2));
        return SplitMix64(ref x);
    }

    private static ulong SplitMix64(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        var z = state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    private static ulong RotL(ulong x, int k) => (x << k) | (x >> (64 - k));
}
