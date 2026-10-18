using System.Runtime.CompilerServices;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Maths;
using HollowWake.Core.Tags;
using HollowWake.Core.World;

namespace HollowWake.Core.WorldGen;

/// <summary>Macro facts about a region, decided once from the seed and the overrides.</summary>
public sealed record RegionInfo(
    RegionCoord Coord,
    RegionArchetypeDef Archetype,
    string Name,
    string? Faction,
    int Level,
    int Distance,
    IReadOnlyDictionary<string, double> Threads);

/// <summary>Climate and shape at a point. Everything the biome table and the point-of-interest rules read.</summary>
public readonly record struct ClimateSample(
    double Elevation,
    double Temperature,
    double Moisture,
    double Slope,
    IReadOnlyDictionary<string, double> Threads,
    bool Water,
    double WaterDepth);

/// <summary>A placed point of interest, deterministic from the seed, pinned to world cell coordinates.</summary>
public sealed record PoiInstance(
    string Id,
    PoiDef Def,
    string Name,
    ZoneCoord Zone,
    double X,
    double Y,
    float BaseHeight,
    string? Landmark,
    string? Faction,
    int Level)
{
    public TagSet Tags => Def.Tags.With(Def.Id, "poi");
}

/// <summary>What a zone is: its region, its points of interest, its quests, its level, its flags.</summary>
public sealed record ZoneInfo(
    ZoneCoord Coord,
    RegionInfo Region,
    IReadOnlyList<PoiInstance> Pois,
    IReadOnlyList<QuestDef> Quests,
    int Level,
    bool Safe,
    bool PlayerStart,
    string Name);

/// <summary>
/// The deterministic world: any region, zone or chunk is a pure function of the seed, the content,
/// the hand-placed overrides and the active overlays. Nothing is stored except caches, so the world is
/// infinite and seamless, and two servers with the same seed and content generate the same land.
/// </summary>
/// <remarks>
/// Generation must be bit-for-bit reproducible, so every fast path computes exactly the reference
/// arithmetic and only removes repeated work: region parameters are cached as numbers, the biome table
/// is compiled once, a sample's five elevation points share their noise lattice lookups, and chunks
/// evaluate their climate on a grid (<see cref="ClimateGrid"/>). Golden tests pin the output.
/// </remarks>
public sealed class WorldGenerator
{
    private readonly CoordCache<RegionEntry> _regions = new();
    private readonly CoordCache<ZoneInfo> _zones = new();
    private readonly CoordCache<IReadOnlyList<PoiInstance>> _poisByZone = new();
    private readonly CoordCache<PoiInstance[]> _poisAround = new();
    private readonly Func<int, int, PoiInstance[]> _gatherAround;
    private readonly Func<int, int, RegionEntry> _buildRegion;
    private readonly Func<int, int, ZoneInfo> _buildZone;
    private readonly Func<int, int, IReadOnlyList<PoiInstance>> _placePois;
    private readonly Noise2D _continent;
    private readonly Noise2D _detail;
    private readonly Noise2D _ridges;
    private readonly Noise2D[] _threadNoise;
    private readonly string[] _threadOverlayKeys;
    private readonly List<RegionArchetypeDef> _archetypes;
    private readonly Dictionary<(int, int), RegionOverrideDef> _regionOverrides = new();
    private readonly Dictionary<(int, int), ZoneOverrideDef> _zoneOverrides = new();
    private readonly ZoneCoord _startZone;
    private readonly double _regionSize;
    // 1 / region size when the size is a power of two (so the reciprocal is exact), else 0.
    private readonly double _regionReciprocal;
    private readonly double _continentScale;
    private readonly double _ridgeScale;
    private readonly double _detailScale;
    private readonly double _continentAmplitude;
    private readonly double _ridgeAmplitude;
    private readonly double _detailAmplitude;
    private RegionQuad? _lastQuad;
    private int _overlayVersionSeen;

    // Read by the chunk passes and the climate grid; fixed at construction.
    internal readonly ThreadKeys Threads;
    internal readonly BiomeTable Biomes;
    internal readonly Noise2D TemperatureNoise;
    internal readonly Noise2D MoistureNoise;
    internal readonly Noise2D FloraClump;
    internal readonly double SeaLevel;
    internal readonly double LapseRate;
    internal readonly double TemperatureScale;
    internal readonly double MoistureScale;
    internal readonly double ThreadScale;

    public ContentRegistry Content { get; }
    public WorldLayout Layout { get; }
    public ulong Seed { get; }
    public GenerationOverlays Overlays { get; }
    public GenParams Params { get; }

    public WorldGenerator(ContentRegistry content, ulong seed, GenerationOverlays? overlays = null, WorldLayout? layout = null)
    {
        Content = content;
        Seed = seed;
        Overlays = overlays ?? new GenerationOverlays();
        Params = GenParams.FromRules(content.Rules);
        Layout = layout ?? new WorldLayout
        {
            ChunkSize = (int)content.Rules.Number("world.chunk_size", 32),
            ZoneChunks = (int)content.Rules.Number("world.zone_chunks", 8),
            RegionZones = (int)content.Rules.Number("world.region_zones", 8),
        };
        _continent = new Noise2D(Rng.Combine(seed, 1));
        _detail = new Noise2D(Rng.Combine(seed, 2));
        TemperatureNoise = new Noise2D(Rng.Combine(seed, 3));
        MoistureNoise = new Noise2D(Rng.Combine(seed, 4));
        _ridges = new Noise2D(Rng.Combine(seed, 5));
        var threadIds = new List<string>();
        foreach (var t in content.Each<ThreadDef>())
        {
            threadIds.Add(t.Id);
        }
        Threads = new ThreadKeys(threadIds);
        _threadNoise = new Noise2D[Threads.Count];
        _threadOverlayKeys = new string[Threads.Count];
        for (var k = 0; k < _threadNoise.Length; k++)
        {
            _threadNoise[k] = new Noise2D(Rng.Combine(seed, (ulong)(10 + k)));
            _threadOverlayKeys[k] = "thread." + Threads.Ids[k];
        }
        var biomeDepth = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var b in content.Each<BiomeDef>())
        {
            biomeDepth[b.Id] = Depth(b, content);
        }
        var biomesBySpecificity = content.Each<BiomeDef>()
            .OrderByDescending(b => biomeDepth[b.Id])
            .ThenByDescending(b => b.Priority)
            .ThenBy(b => b.Id, StringComparer.Ordinal)
            .ToList();
        Biomes = new BiomeTable(biomesBySpecificity, content, Threads);
        _archetypes = content.Each<RegionArchetypeDef>().OrderBy(a => a.Id, StringComparer.Ordinal).ToList();
        foreach (var o in content.Each<RegionOverrideDef>())
        {
            _regionOverrides[(o.X, o.Y)] = o;
        }
        foreach (var o in content.Each<ZoneOverrideDef>())
        {
            _zoneOverrides[(o.X, o.Y)] = o;
        }
        var start = _zoneOverrides.Values.FirstOrDefault(z => z.PlayerStart);
        _startZone = start is null ? new ZoneCoord(0, 0) : new ZoneCoord(start.X, start.Y);
        _overlayVersionSeen = Overlays.Version;

        _regionSize = Layout.RegionSize;
        _regionReciprocal = Layout.RegionSize > 0 && System.Numerics.BitOperations.IsPow2(Layout.RegionSize) ? 1.0 / Layout.RegionSize : 0;
        _continentScale = Params.ContinentScale;
        _ridgeScale = Params.ContinentScale * 0.35;
        _detailScale = Params.DetailScale;
        _continentAmplitude = Params.ContinentAmplitude;
        _ridgeAmplitude = Params.RidgeAmplitude;
        _detailAmplitude = Params.DetailAmplitude;
        SeaLevel = Params.SeaLevel;
        LapseRate = Params.LapseRate;
        TemperatureScale = Params.TemperatureScale;
        MoistureScale = Params.MoistureScale;
        ThreadScale = Params.ThreadScale;
        FloraClump = new Noise2D(Rng.Combine(seed, 77));

        _buildRegion = BuildRegion;
        _buildZone = BuildZone;
        _placePois = PlacePois;
        _gatherAround = GatherAround;
    }

    private static int Depth(BiomeDef b, ContentRegistry content)
    {
        var depth = 0;
        var cur = b;
        var guard = 0;
        while (cur.Parent is not null && content.TryGet<BiomeDef>(cur.Parent, out var p) && p is not null && guard++ < 16)
        {
            depth++;
            cur = p;
        }
        return depth;
    }

    internal Noise2D ThreadNoise(int thread) => _threadNoise[thread];
    internal string ThreadOverlayKey(int thread) => _threadOverlayKeys[thread];

    /// <summary>Drops cached zones when overlays changed. Chunks are owned by terrains, which ask us for the version.</summary>
    public void RefreshIfOverlaysChanged()
    {
        if (_overlayVersionSeen != Overlays.Version)
        {
            _overlayVersionSeen = Overlays.Version;
            _zones.Clear();
            _poisByZone.Clear();
            _poisAround.Clear();
        }
    }

    public int GenVersion => Overlays.Version;

    // ------------------------------------------------------------ regions

    public RegionInfo Region(RegionCoord rc) => _regions.GetOrAdd(rc.X, rc.Y, _buildRegion).Info;

    internal RegionEntry RegionEntryAt(RegionCoord rc) => _regions.GetOrAdd(rc.X, rc.Y, _buildRegion);

    private RegionEntry BuildRegion(int regionX, int regionY)
    {
        var rc = new RegionCoord(regionX, regionY);
        var rng = new Rng(Rng.Combine(Seed, Rng.Hash64($"region:{rc.X}:{rc.Y}")));
        var distance = Math.Max(Math.Abs(rc.X), Math.Abs(rc.Y));
        _regionOverrides.TryGetValue((rc.X, rc.Y), out var ovr);

        RegionArchetypeDef? archetype = null;
        if (ovr?.Archetype is not null)
        {
            archetype = Content.Get<RegionArchetypeDef>(ovr.Archetype);
        }
        if (archetype is null)
        {
            var candidates = new List<RegionArchetypeDef>();
            foreach (var a in _archetypes)
            {
                if (distance >= a.MinDistance && distance <= a.MaxDistance)
                {
                    candidates.Add(a);
                }
            }
            if (candidates.Count == 0)
            {
                candidates = _archetypes;
            }
            if (candidates.Count == 0)
            {
                archetype = new RegionArchetypeDef { Id = "default", Name = "Wilds" };
            }
            else
            {
                var weights = new double[candidates.Count];
                for (var i = 0; i < weights.Length; i++)
                {
                    weights[i] = candidates[i].Weight;
                }
                var idx = rng.PickWeighted(weights);
                archetype = candidates[Math.Max(0, idx)];
            }
        }

        var threads = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var t in Content.Each<ThreadDef>())
        {
            var bias = archetype.Threads.TryGetValue(t.Id, out var b) ? b : 0;
            if (ovr is not null && ovr.Threads.TryGetValue(t.Id, out var ob))
            {
                bias = ob;
            }
            threads[t.Id] = bias;
        }

        var level = ovr?.Level ?? Math.Max(1, 1 + distance * (int)Params.LevelPerRegion + archetype.LevelOffset);
        string? faction = ovr?.Faction;
        if (faction is null && !archetype.Factions.IsEmpty && rng.Chance(0.7))
        {
            faction = rng.Pick(archetype.Factions.Items);
        }
        var name = ovr?.Name is { Length: > 0 } n ? n : NameRegion(archetype, rng);
        var info = new RegionInfo(rc, archetype, name, faction, level, distance, threads);

        var biases = new double[Threads.Count];
        for (var k = 0; k < biases.Length; k++)
        {
            biases[k] = threads.TryGetValue(Threads.Ids[k], out var v) ? v : 0;
        }
        return new RegionEntry(info, biases, Biomes.RegionMask(archetype));
    }

    private static string NameRegion(RegionArchetypeDef archetype, Rng rng)
    {
        if (archetype.Names.Count > 0)
        {
            return rng.Pick(archetype.Names);
        }
        return archetype.DisplayName;
    }

    // ------------------------------------------------------------ climate

    /// <summary>
    /// The four nearest region centers of a point and its smoothstep weights between them. Region
    /// parameters blend bilinearly across them so borders never seam. <paramref name="quad"/> carries the
    /// four regions from point to point so they are looked up only when a point leaves them.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Blend(double x, double y, ref RegionQuad? quad, out double tx, out double ty)
    {
        // Dividing by a power of two and multiplying by its reciprocal give the same exact value, rounded the same way.
        var fx = (_regionReciprocal != 0 ? x * _regionReciprocal : x / _regionSize) - 0.5;
        var fy = (_regionReciprocal != 0 ? y * _regionReciprocal : y / _regionSize) - 0.5;
        var x0 = (int)Math.Floor(fx);
        var y0 = (int)Math.Floor(fy);
        var bx = fx - x0;
        var by = fy - y0;
        tx = bx * bx * (3 - 2 * bx);
        ty = by * by * (3 - 2 * by);
        if (quad is null || quad.X0 != x0 || quad.Y0 != y0)
        {
            quad = QuadAt(x0, y0);
        }
    }

    // Not inlined: points leave their quad rarely, and the hot callers should stay small.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private RegionQuad QuadAt(int x0, int y0)
    {
        // Any published quad is complete and immutable, so racing threads at worst rebuild one.
        var last = Volatile.Read(ref _lastQuad);
        if (last is not null && last.X0 == x0 && last.Y0 == y0)
        {
            return last;
        }
        var quad = new RegionQuad(
            x0,
            y0,
            _regions.GetOrAdd(x0, y0, _buildRegion),
            _regions.GetOrAdd(x0 + 1, y0, _buildRegion),
            _regions.GetOrAdd(x0, y0 + 1, _buildRegion),
            _regions.GetOrAdd(x0 + 1, y0 + 1, _buildRegion));
        Volatile.Write(ref _lastQuad, quad);
        return quad;
    }

    /// <summary>Bilinear blend of four corner values.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double W(double a, double b, double c, double d, double tx, double ty) =>
        (a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty;

    /// <summary>Elevation before overlays: region lift and scale, continental, ridge and detail noise.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal double ElevationBase(double x, double y, ref RegionQuad? quad)
    {
        var continental = _continent.Fbm(x / _continentScale, y / _continentScale, 4, 2.0, 0.5);
        var ridge = _ridges.Fbm(x / _ridgeScale, y / _ridgeScale, 3, 2.2, 0.5);
        var detail = _detail.Fbm(x / _detailScale, y / _detailScale, 4, 2.0, 0.5);
        return ShapeAt(x, y, continental, ridge, detail, ref quad, out _, out _);
    }

    /// <summary>
    /// The elevation formula at a point from its three noise values: region lift and scale, ridge,
    /// shape. Out of line so every caller shares one compiled copy; reports the region blend weights.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal double ShapeAt(double x, double y, double continental, double ridgeNoise, double detail, ref RegionQuad? quad, out double tx, out double ty)
    {
        Blend(x, y, ref quad, out tx, out ty);
        var q = quad!;
        var lift = W(q.R00.Lift, q.R10.Lift, q.R01.Lift, q.R11.Lift, tx, ty);
        var scale = W(q.R00.Scale, q.R10.Scale, q.R01.Scale, q.R11.Scale, tx, ty);
        var ridge = 1 - Math.Abs(ridgeNoise);
        var baseHeight = continental * _continentAmplitude + ridge * ridge * _ridgeAmplitude * Math.Max(0, scale - 0.5);
        return SeaLevel + lift + baseHeight * scale + detail * _detailAmplitude;
    }

    public double Elevation(double x, double y)
    {
        RegionQuad? quad = null;
        return Elevation(x, y, ref quad);
    }

    private double Elevation(double x, double y, ref RegionQuad? quad)
    {
        var e = ElevationBase(x, y, ref quad);
        return WithElevationOverlays(e, x, y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double WithElevationOverlays(double e, double x, double y)
    {
        e += Overlays.Sum(Layout, "elevation_offset", x, y);
        e *= Overlays.Param(Layout, "elevation_multiplier", x, y, 1);
        return e;
    }

    public double WaterLevel(double x, double y) => Overlays.Param(Layout, "water_level", x, y, SeaLevel);

    // The climate formulas, shared by every path that samples a climate so they cannot drift apart.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double Temperature(double noise, double bias, double elevation, double water, double lapseRate, double offset)
    {
        var t = 0.5 + 0.5 * noise;
        t += bias - Math.Max(0, elevation - water) * lapseRate;
        t += offset;
        return Math.Clamp(t, 0, 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double Moisture(double noise, double bias, double offset)
    {
        var m = 0.5 + 0.5 * noise;
        m += bias + offset;
        return Math.Clamp(m, 0, 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double ThreadDensity(double noise, double bias, double offset)
    {
        var v = 0.5 + 0.5 * noise;
        v += bias;
        v += offset;
        return Math.Clamp(v, 0, 1);
    }

    /// <summary>Elevation relative to water as the biome table reads it: the local water when under it, else sea level.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal double RelativeElevation(double elevation, double water)
    {
        var isWater = elevation < water;
        double depth = isWater ? water - elevation : 0;
        return elevation - (isWater ? elevation + depth : SeaLevel);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public ClimateSample Sample(double x, double y)
    {
        // Elevation at the point and its four neighbors (for the slope), the noise of all five at once.
        var xw = x - 1;
        var xe = x + 1;
        var ys = y - 1;
        var yn = y + 1;
        var cs = _continentScale;
        var rs = _ridgeScale;
        var ds = _detailScale;
        _continent.FbmCross(xw / cs, x / cs, xe / cs, ys / cs, y / cs, yn / cs, 4, 2.0, 0.5, out var cc, out var cw, out var ce, out var csouth, out var cn);
        _ridges.FbmCross(xw / rs, x / rs, xe / rs, ys / rs, y / rs, yn / rs, 3, 2.2, 0.5, out var rc, out var rw, out var re, out var rsouth, out var rn);
        _detail.FbmCross(xw / ds, x / ds, xe / ds, ys / ds, y / ds, yn / ds, 4, 2.0, 0.5, out var dc, out var dw, out var de, out var dsouth, out var dn);
        RegionQuad? quad = null;
        var elevation = WithElevationOverlays(ShapeAt(x, y, cc, rc, dc, ref quad, out var tx, out var ty), x, y);
        var q = quad!;
        var east = WithElevationOverlays(ShapeAt(xe, y, ce, re, de, ref quad, out _, out _), xe, y);
        var west = WithElevationOverlays(ShapeAt(xw, y, cw, rw, dw, ref quad, out _, out _), xw, y);
        var north = WithElevationOverlays(ShapeAt(x, yn, cn, rn, dn, ref quad, out _, out _), x, yn);
        var south = WithElevationOverlays(ShapeAt(x, ys, csouth, rsouth, dsouth, ref quad, out _, out _), x, ys);
        var water = WaterLevel(x, y);
        var ex = east - west;
        var ey = north - south;
        var slope = Math.Sqrt(ex * ex + ey * ey) / 2.0;

        Noise2D.FbmPair(TemperatureNoise, x / TemperatureScale, y / TemperatureScale, MoistureNoise, x / MoistureScale, y / MoistureScale, 3, 2.0, 0.5, out var tNoise, out var mNoise);
        var temperature = Temperature(
            tNoise,
            W(q.R00.TempBias, q.R10.TempBias, q.R01.TempBias, q.R11.TempBias, tx, ty),
            elevation,
            water,
            LapseRate,
            Overlays.Sum(Layout, "temperature_offset", x, y));
        var moisture = Moisture(mNoise, W(q.R00.MoistBias, q.R10.MoistBias, q.R01.MoistBias, q.R11.MoistBias, tx, ty), Overlays.Sum(Layout, "moisture_offset", x, y));

        // Thread densities are worked out when first read; only the overlay offsets can change, so take them now.
        double[]? offsets = null;
        if (Overlays.Count > 0)
        {
            offsets = new double[_threadNoise.Length];
            for (var k = 0; k < offsets.Length; k++)
            {
                offsets[k] = Overlays.Sum(Layout, _threadOverlayKeys[k], x, y);
            }
        }
        var isWater = elevation < water;
        return new ClimateSample(elevation, temperature, moisture, slope, new ThreadDensities(this, x, y, offsets), isWater, isWater ? water - elevation : 0);
    }

    /// <summary>The Thread densities at a point, given the overlay offsets taken when it was sampled.</summary>
    internal double[] ThreadDensitiesAt(double x, double y, double[]? offsets)
    {
        RegionQuad? quad = null;
        Blend(x, y, ref quad, out var tx, out var ty);
        var q = quad!;
        var densities = new double[_threadNoise.Length];
        for (var k = 0; k < densities.Length; k++)
        {
            densities[k] = ThreadDensity(
                _threadNoise[k].Fbm(x / ThreadScale, y / ThreadScale, 2, 2.0, 0.5),
                W(q.R00.Threads[k], q.R10.Threads[k], q.R01.Threads[k], q.R11.Threads[k], tx, ty),
                offsets is null ? 0 : offsets[k]);
        }
        return densities;
    }

    public BiomeDef? Biome(in ClimateSample s, RegionInfo region)
    {
        Span<double> values = stackalloc double[Biomes.Width];
        values[BiomeTable.RelativeElevation] = s.Elevation - WaterLevelOrSea(s);
        values[BiomeTable.Temperature] = s.Temperature;
        values[BiomeTable.Moisture] = s.Moisture;
        values[BiomeTable.Slope] = s.Slope;
        if (s.Threads is null)
        {
            values[BiomeTable.FirstSlot..].Fill(double.NaN);
        }
        else
        {
            Biomes.ReadSlots(s.Threads, Threads, values);
        }
        var rule = _regions.TryGet(region.Coord.X, region.Coord.Y, out var entry) && ReferenceEquals(entry.Info, region)
            ? Biomes.Match(values, entry.Biomes)
            : Biomes.Match(values, region.Archetype);
        return rule < 0 ? null : Biomes.Biome(rule);
    }

    private double WaterLevelOrSea(in ClimateSample s) => s.Water ? s.Elevation + s.WaterDepth : Params.SeaLevel;

    /// <summary>Approximate distance to water by probing rings, in cells. Capped at the probe limit.</summary>
    public double WaterDistance(double x, double y, int limit = 64)
    {
        RegionQuad? quad = null;
        if (Elevation(x, y, ref quad) < WaterLevel(x, y))
        {
            return 0;
        }
        for (var d = 4; d <= limit; d *= 2)
        {
            for (var k = 0; k < 8; k++)
            {
                var ang = k * Math.PI / 4;
                var px = x + Math.Cos(ang) * d;
                var py = y + Math.Sin(ang) * d;
                if (Elevation(px, py, ref quad) < WaterLevel(px, py))
                {
                    return d;
                }
            }
        }
        return limit * 2;
    }

    // ------------------------------------------------------------ zones

    public ZoneInfo Zone(ZoneCoord zc)
    {
        RefreshIfOverlaysChanged();
        return _zones.GetOrAdd(zc.X, zc.Y, _buildZone);
    }

    public IReadOnlyList<PoiInstance> PoisOf(ZoneCoord zc)
    {
        RefreshIfOverlaysChanged();
        return _poisByZone.GetOrAdd(zc.X, zc.Y, _placePois);
    }

    /// <summary>Points of interest in a zone and its eight neighbors.</summary>
    public IEnumerable<PoiInstance> PoisAround(ZoneCoord zc) => new List<PoiInstance>(PoisAroundCached(zc));

    /// <summary>
    /// <see cref="PoisAround"/>, in the same order, cached per zone (and dropped with the other zone caches
    /// when overlays change): every chunk of a zone reads it twice. Callers must not modify it.
    /// </summary>
    internal PoiInstance[] PoisAroundCached(ZoneCoord zc)
    {
        RefreshIfOverlaysChanged();
        return _poisAround.GetOrAdd(zc.X, zc.Y, _gatherAround);
    }

    private PoiInstance[] GatherAround(int zoneX, int zoneY)
    {
        var all = new List<PoiInstance>();
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                all.AddRange(PoisOf(new ZoneCoord(zoneX + dx, zoneY + dy)));
            }
        }
        return all.ToArray();
    }

    private IReadOnlyList<PoiInstance> PlacePois(int zoneX, int zoneY) => new ZonePlanner(this).PlacePois(new ZoneCoord(zoneX, zoneY));

    public ZoneOverrideDef? ZoneOverride(ZoneCoord zc) => _zoneOverrides.TryGetValue((zc.X, zc.Y), out var o) ? o : null;

    private ZoneInfo BuildZone(int zoneX, int zoneY)
    {
        var zc = new ZoneCoord(zoneX, zoneY);
        var region = Region(Layout.RegionOf(zc));
        var pois = PoisOf(zc);
        var ovr = ZoneOverride(zc);
        var planner = new ZonePlanner(this);
        var quests = planner.GenerateQuests(zc, pois);
        var level = PopulationLevel(zc);
        var anySafe = false;
        PoiInstance? hub = null;
        foreach (var p in pois)
        {
            anySafe |= p.Def.Safe;
            if (hub is null && p.Def.Hub)
            {
                hub = p;
            }
        }
        var safe = ovr?.Safe ?? anySafe;
        var name = ovr?.Name is { Length: > 0 } n ? n : hub?.Name ?? region.Name;
        return new ZoneInfo(zc, region, pois, quests, level, safe, ovr?.PlayerStart ?? false, name);
    }

    /// <summary>The designated starting zone (the first zone override flagged player_start), or the origin.</summary>
    public ZoneCoord StartZone() => _startZone;

    /// <summary>A sensible spawn point in a zone: the hub if any, else the zone center snapped to walkable land.</summary>
    public (double X, double Y) StartPoint(ZoneCoord zc)
    {
        var info = Zone(zc);
        PoiInstance? hub = null;
        foreach (var p in info.Pois)
        {
            if (p.Def.Hub)
            {
                hub = p;
                break;
            }
        }
        hub ??= info.Pois.Count > 0 ? info.Pois[0] : null;
        if (hub is not null)
        {
            // Just south of the landmark's footprint, where hamlet stamps put their road.
            var half = hub.Def.Radius + 2;
            if (hub.Landmark is not null && Content.Get<LandmarkDef>(hub.Landmark) is { } landmark && landmark.Rows.Count > 0)
            {
                half = Math.Max(half, Math.Max(landmark.Rows.Count, ChunkPasses.Width(landmark)) / 2.0 + 3);
            }
            return (hub.X + 1, hub.Y - half);
        }
        return Layout.ZoneCenter(zc);
    }

    // ------------------------------------------------------------ chunks

    /// <summary>Population difficulty independent of quest planning, avoiding recursive chunk/zone generation.</summary>
    internal int PopulationLevel(ZoneCoord zone) => Math.Max(1, Region(Layout.RegionOf(zone)).Level);

    public ChunkData GenerateChunk(ChunkCoord cc)
    {
        RefreshIfOverlaysChanged();
        var chunk = new ChunkData(cc, Layout.ChunkSize) { GenVersion = GenVersion };
        var passes = new ChunkPasses(this);
        passes.Terrain(chunk);
        passes.Landmarks(chunk);
        passes.Flora(chunk);
        passes.Spawns(chunk);
        return chunk;
    }

    public Rng RngFor(string salt) => new(Rng.Combine(Seed, Rng.Hash64(salt)));
}

/// <summary>A region with its blend parameters as plain numbers and the biomes its archetype allows.</summary>
internal sealed class RegionEntry
{
    public readonly RegionInfo Info;
    public readonly double Lift;
    public readonly double Scale;
    public readonly double TempBias;
    public readonly double MoistBias;
    /// <summary>Thread bias by the generator's Thread index.</summary>
    public readonly double[] Threads;
    /// <summary>Whether each biome rule's region restrictions admit this region.</summary>
    public readonly bool[] Biomes;

    public RegionEntry(RegionInfo info, double[] threads, bool[] biomes)
    {
        Info = info;
        Lift = info.Archetype.ElevationLift;
        Scale = info.Archetype.ElevationScale;
        TempBias = info.Archetype.TemperatureBias;
        MoistBias = info.Archetype.MoistureBias;
        Threads = threads;
        Biomes = biomes;
    }
}

/// <summary>The four regions around a point (the nearest region centers), immutable once built.</summary>
internal sealed class RegionQuad
{
    public readonly int X0;
    public readonly int Y0;
    public readonly RegionEntry R00;
    public readonly RegionEntry R10;
    public readonly RegionEntry R01;
    public readonly RegionEntry R11;

    public RegionQuad(int x0, int y0, RegionEntry r00, RegionEntry r10, RegionEntry r01, RegionEntry r11)
    {
        X0 = x0;
        Y0 = y0;
        R00 = r00;
        R10 = r10;
        R01 = r01;
        R11 = r11;
    }
}

/// <summary>Tunables for the generator, read from rules under <c>world.gen</c>.</summary>
public sealed record GenParams
{
    public double SeaLevel { get; init; } = 0;
    public double ContinentScale { get; init; } = 2400;
    public double ContinentAmplitude { get; init; } = 28;
    public double RidgeAmplitude { get; init; } = 40;
    public double DetailScale { get; init; } = 40;
    public double DetailAmplitude { get; init; } = 3;
    public double TemperatureScale { get; init; } = 2200;
    public double MoistureScale { get; init; } = 1600;
    public double ThreadScale { get; init; } = 900;
    public double LapseRate { get; init; } = 0.006;
    public double LevelPerRegion { get; init; } = 4;
    public double PoiCell { get; init; } = 64;
    public double PoiMargin { get; init; } = 20;
    public double PoiTarget { get; init; } = 4;
    public double FlattenFalloff { get; init; } = 6;
    public double MaxWalkableSlope { get; init; } = 1.1;
    public double ShallowDepth { get; init; } = 0.6;
    public double FloraJitter { get; init; } = 0.4;

    public static GenParams FromRules(Rules r) => new()
    {
        SeaLevel = r.Number("world.sea_level", 0),
        ContinentScale = r.Number("world.gen.continent_scale", 2400),
        ContinentAmplitude = r.Number("world.gen.continent_amplitude", 28),
        RidgeAmplitude = r.Number("world.gen.ridge_amplitude", 40),
        DetailScale = r.Number("world.gen.detail_scale", 40),
        DetailAmplitude = r.Number("world.gen.detail_amplitude", 3),
        TemperatureScale = r.Number("world.gen.temperature_scale", 2200),
        MoistureScale = r.Number("world.gen.moisture_scale", 1600),
        ThreadScale = r.Number("world.gen.thread_scale", 900),
        LapseRate = r.Number("world.gen.lapse_rate", 0.006),
        LevelPerRegion = r.Number("world.gen.level_per_region", 4),
        PoiCell = r.Number("world.gen.poi_cell", 64),
        PoiMargin = r.Number("world.gen.poi_margin", 20),
        PoiTarget = r.Number("world.gen.poi_target", 4),
        FlattenFalloff = r.Number("world.gen.flatten_falloff", 6),
        MaxWalkableSlope = r.Number("world.gen.max_walkable_slope", 1.1),
        ShallowDepth = r.Number("world.gen.shallow_depth", 0.6),
        FloraJitter = r.Number("world.gen.flora_jitter", 0.4),
    };
}

/// <summary>Chunk provider backed by the generator. The server wraps it with a delta store for modified chunks.</summary>
public sealed class GeneratedChunkProvider : IChunkProvider
{
    private readonly WorldGenerator _generator;

    public GeneratedChunkProvider(WorldGenerator generator)
    {
        _generator = generator;
    }

    public ChunkData Load(ChunkCoord coord) => _generator.GenerateChunk(coord);
}
