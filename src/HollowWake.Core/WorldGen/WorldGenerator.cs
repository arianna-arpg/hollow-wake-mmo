using System.Collections.Concurrent;
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
public sealed class WorldGenerator
{
    private readonly ConcurrentDictionary<RegionCoord, RegionInfo> _regions = new();
    private readonly ConcurrentDictionary<ZoneCoord, ZoneInfo> _zones = new();
    private readonly ConcurrentDictionary<ZoneCoord, IReadOnlyList<PoiInstance>> _poisByZone = new();
    private readonly Noise2D _continent;
    private readonly Noise2D _detail;
    private readonly Noise2D _temperature;
    private readonly Noise2D _moisture;
    private readonly Noise2D _ridges;
    private readonly Dictionary<string, Noise2D> _threadNoise = new(StringComparer.Ordinal);
    private readonly List<BiomeDef> _biomesBySpecificity;
    private readonly Dictionary<string, int> _biomeDepth = new(StringComparer.Ordinal);
    private readonly List<RegionArchetypeDef> _archetypes;
    private readonly Dictionary<(int, int), RegionOverrideDef> _regionOverrides = new();
    private readonly Dictionary<(int, int), ZoneOverrideDef> _zoneOverrides = new();
    private int _overlayVersionSeen;

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
        _temperature = new Noise2D(Rng.Combine(seed, 3));
        _moisture = new Noise2D(Rng.Combine(seed, 4));
        _ridges = new Noise2D(Rng.Combine(seed, 5));
        var i = 10;
        foreach (var t in content.Each<ThreadDef>())
        {
            _threadNoise[t.Id] = new Noise2D(Rng.Combine(seed, (ulong)i++));
        }
        foreach (var b in content.Each<BiomeDef>())
        {
            _biomeDepth[b.Id] = Depth(b, content);
        }
        _biomesBySpecificity = content.Each<BiomeDef>()
            .OrderByDescending(b => _biomeDepth[b.Id])
            .ThenByDescending(b => b.Priority)
            .ThenBy(b => b.Id, StringComparer.Ordinal)
            .ToList();
        _archetypes = content.Each<RegionArchetypeDef>().OrderBy(a => a.Id, StringComparer.Ordinal).ToList();
        foreach (var o in content.Each<RegionOverrideDef>())
        {
            _regionOverrides[(o.X, o.Y)] = o;
        }
        foreach (var o in content.Each<ZoneOverrideDef>())
        {
            _zoneOverrides[(o.X, o.Y)] = o;
        }
        _overlayVersionSeen = Overlays.Version;
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

    /// <summary>Drops cached zones when overlays changed. Chunks are owned by terrains, which ask us for the version.</summary>
    public void RefreshIfOverlaysChanged()
    {
        if (_overlayVersionSeen != Overlays.Version)
        {
            _overlayVersionSeen = Overlays.Version;
            _zones.Clear();
            _poisByZone.Clear();
        }
    }

    public int GenVersion => Overlays.Version;

    // ------------------------------------------------------------ regions

    public RegionInfo Region(RegionCoord rc) => _regions.GetOrAdd(rc, BuildRegion);

    private RegionInfo BuildRegion(RegionCoord rc)
    {
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
            var candidates = _archetypes.Where(a => distance >= a.MinDistance && distance <= a.MaxDistance).ToList();
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
                var idx = rng.PickWeighted(candidates.Select(a => a.Weight).ToList());
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
        return new RegionInfo(rc, archetype, name, faction, level, distance, threads);
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

    /// <summary>Region parameters blended across the four nearest region centers so borders never seam.</summary>
    private (double Lift, double Scale, double TempBias, double MoistBias, IReadOnlyDictionary<string, double> Threads) BlendedRegion(double x, double y)
    {
        var size = (double)Layout.RegionSize;
        var fx = x / size - 0.5;
        var fy = y / size - 0.5;
        var x0 = (int)Math.Floor(fx);
        var y0 = (int)Math.Floor(fy);
        var tx = fx - x0;
        var ty = fy - y0;
        tx = tx * tx * (3 - 2 * tx);
        ty = ty * ty * (3 - 2 * ty);
        var r00 = Region(new RegionCoord(x0, y0));
        var r10 = Region(new RegionCoord(x0 + 1, y0));
        var r01 = Region(new RegionCoord(x0, y0 + 1));
        var r11 = Region(new RegionCoord(x0 + 1, y0 + 1));
        double W(double a, double b, double c, double d) => (a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty;
        var lift = W(r00.Archetype.ElevationLift, r10.Archetype.ElevationLift, r01.Archetype.ElevationLift, r11.Archetype.ElevationLift);
        var scale = W(r00.Archetype.ElevationScale, r10.Archetype.ElevationScale, r01.Archetype.ElevationScale, r11.Archetype.ElevationScale);
        var temp = W(r00.Archetype.TemperatureBias, r10.Archetype.TemperatureBias, r01.Archetype.TemperatureBias, r11.Archetype.TemperatureBias);
        var moist = W(r00.Archetype.MoistureBias, r10.Archetype.MoistureBias, r01.Archetype.MoistureBias, r11.Archetype.MoistureBias);
        var threads = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var key in r00.Threads.Keys)
        {
            threads[key] = W(r00.Threads[key], r10.Threads[key], r01.Threads[key], r11.Threads[key]);
        }
        return (lift, scale, temp, moist, threads);
    }

    public double Elevation(double x, double y)
    {
        var (lift, scale, _, _, _) = BlendedRegion(x, y);
        var continental = _continent.Fbm(x / Params.ContinentScale, y / Params.ContinentScale, 4, 2.0, 0.5);
        var ridge = 1 - Math.Abs(_ridges.Fbm(x / (Params.ContinentScale * 0.35), y / (Params.ContinentScale * 0.35), 3, 2.2, 0.5));
        var detail = _detail.Fbm(x / Params.DetailScale, y / Params.DetailScale, 4, 2.0, 0.5);
        var baseHeight = continental * Params.ContinentAmplitude + ridge * ridge * Params.RidgeAmplitude * Math.Max(0, scale - 0.5);
        var e = Params.SeaLevel + lift + baseHeight * scale + detail * Params.DetailAmplitude;
        e += Overlays.Sum(Layout, "elevation_offset", x, y);
        e *= Overlays.Param(Layout, "elevation_multiplier", x, y, 1);
        return e;
    }

    public double WaterLevel(double x, double y) => Overlays.Param(Layout, "water_level", x, y, Params.SeaLevel);

    public ClimateSample Sample(double x, double y)
    {
        var (_, _, tempBias, moistBias, regionThreads) = BlendedRegion(x, y);
        var elevation = Elevation(x, y);
        var water = WaterLevel(x, y);
        var ex = Elevation(x + 1, y) - Elevation(x - 1, y);
        var ey = Elevation(x, y + 1) - Elevation(x, y - 1);
        var slope = Math.Sqrt(ex * ex + ey * ey) / 2.0;
        var t = 0.5 + 0.5 * _temperature.Fbm(x / Params.TemperatureScale, y / Params.TemperatureScale, 3, 2.0, 0.5);
        t += tempBias - Math.Max(0, elevation - water) * Params.LapseRate;
        t += Overlays.Sum(Layout, "temperature_offset", x, y);
        var m = 0.5 + 0.5 * _moisture.Fbm(x / Params.MoistureScale, y / Params.MoistureScale, 3, 2.0, 0.5);
        m += moistBias + Overlays.Sum(Layout, "moisture_offset", x, y);
        var threads = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (id, noise) in _threadNoise)
        {
            var v = 0.5 + 0.5 * noise.Fbm(x / Params.ThreadScale, y / Params.ThreadScale, 2, 2.0, 0.5);
            v += regionThreads.TryGetValue(id, out var rb) ? rb : 0;
            v += Overlays.Sum(Layout, "thread." + id, x, y);
            threads[id] = Math.Clamp(v, 0, 1);
        }
        var isWater = elevation < water;
        return new ClimateSample(elevation, Math.Clamp(t, 0, 1), Math.Clamp(m, 0, 1), slope, threads, isWater, isWater ? water - elevation : 0);
    }

    public BiomeDef? Biome(in ClimateSample s, RegionInfo region)
    {
        foreach (var b in _biomesBySpecificity)
        {
            if (Matches(b, s, region))
            {
                return b;
            }
        }
        return null;
    }

    private bool Matches(BiomeDef b, in ClimateSample s, RegionInfo region)
    {
        if (b.Elevation is { } e && !e.Contains(s.Elevation - WaterLevelOrSea(s)))
        {
            return false;
        }
        if (b.Temperature is { } t && !t.Contains(s.Temperature))
        {
            return false;
        }
        if (b.Moisture is { } m && !m.Contains(s.Moisture))
        {
            return false;
        }
        if (b.Slope is { } sl && !sl.Contains(s.Slope))
        {
            return false;
        }
        foreach (var (thread, window) in b.Threads)
        {
            if (!s.Threads.TryGetValue(thread, out var v) || !window.Contains(v))
            {
                return false;
            }
        }
        if (!b.Regions.IsEmpty && !b.Regions.Has(region.Archetype.Id) && !region.Archetype.Tags.HasAny(b.Regions))
        {
            return false;
        }
        // Parent windows must hold as well.
        if (b.Parent is not null && Content.TryGet<BiomeDef>(b.Parent, out var p) && p is not null)
        {
            return Matches(p, s, region);
        }
        return true;
    }

    private double WaterLevelOrSea(in ClimateSample s) => s.Water ? s.Elevation + s.WaterDepth : Params.SeaLevel;

    /// <summary>Approximate distance to water by probing rings, in cells. Capped at the probe limit.</summary>
    public double WaterDistance(double x, double y, int limit = 64)
    {
        if (Sample(x, y).Water)
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
                if (Elevation(px, py) < WaterLevel(px, py))
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
        return _zones.GetOrAdd(zc, BuildZone);
    }

    public IReadOnlyList<PoiInstance> PoisOf(ZoneCoord zc)
    {
        RefreshIfOverlaysChanged();
        return _poisByZone.GetOrAdd(zc, c => new ZonePlanner(this).PlacePois(c));
    }

    /// <summary>Points of interest in a zone and its eight neighbors.</summary>
    public IEnumerable<PoiInstance> PoisAround(ZoneCoord zc)
    {
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                foreach (var p in PoisOf(new ZoneCoord(zc.X + dx, zc.Y + dy)))
                {
                    yield return p;
                }
            }
        }
    }

    public ZoneOverrideDef? ZoneOverride(ZoneCoord zc) => _zoneOverrides.TryGetValue((zc.X, zc.Y), out var o) ? o : null;

    private ZoneInfo BuildZone(ZoneCoord zc)
    {
        var region = Region(Layout.RegionOf(zc));
        var pois = PoisOf(zc);
        var ovr = ZoneOverride(zc);
        var planner = new ZonePlanner(this);
        var quests = planner.GenerateQuests(zc, pois);
        var level = Math.Max(1, region.Level + (ovr is not null && ovr.PlayerStart ? 0 : 0));
        var safe = ovr?.Safe ?? pois.Any(p => p.Def.Safe);
        var name = ovr?.Name is { Length: > 0 } n ? n : pois.FirstOrDefault(p => p.Def.Hub)?.Name ?? region.Name;
        return new ZoneInfo(zc, region, pois, quests, level, safe, ovr?.PlayerStart ?? false, name);
    }

    /// <summary>The designated starting zone (the first zone override flagged player_start), or the origin.</summary>
    public ZoneCoord StartZone()
    {
        var start = _zoneOverrides.Values.FirstOrDefault(z => z.PlayerStart);
        return start is null ? new ZoneCoord(0, 0) : new ZoneCoord(start.X, start.Y);
    }

    /// <summary>A sensible spawn point in a zone: the hub if any, else the zone center snapped to walkable land.</summary>
    public (double X, double Y) StartPoint(ZoneCoord zc)
    {
        var info = Zone(zc);
        var hub = info.Pois.FirstOrDefault(p => p.Def.Hub) ?? info.Pois.FirstOrDefault();
        if (hub is not null)
        {
            // Just south of the landmark's footprint, where hamlet stamps put their road.
            var half = hub.Def.Radius + 2;
            if (hub.Landmark is not null && Content.Get<LandmarkDef>(hub.Landmark) is { } landmark && landmark.Rows.Count > 0)
            {
                half = Math.Max(half, Math.Max(landmark.Rows.Count, landmark.Rows.Max(r => r.Length)) / 2.0 + 3);
            }
            return (hub.X + 1, hub.Y - half);
        }
        return Layout.ZoneCenter(zc);
    }

    // ------------------------------------------------------------ chunks

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
