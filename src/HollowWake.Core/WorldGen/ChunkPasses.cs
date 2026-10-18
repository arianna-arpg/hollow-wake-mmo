using System.Runtime.CompilerServices;
using HollowWake.Core.Definitions;
using HollowWake.Core.Maths;
using HollowWake.Core.Tags;
using HollowWake.Core.World;

namespace HollowWake.Core.WorldGen;

/// <summary>
/// The per-chunk generation passes: terrain from climate and biome, landmark stamps from nearby
/// points of interest, flora scatter, and spawn points. Each pass is a pure function of the seed and
/// the chunk coordinate so neighboring chunks agree at their edges.
/// </summary>
/// <remarks>
/// The passes are hot, so per-cell work is only what varies per cell: the climate comes from a
/// <see cref="ClimateGrid"/>, biomes from the compiled <see cref="BiomeTable"/>, and anything that
/// depends only on a cell's biome or surface tags (tag sets, interned indices, flora lists) is resolved
/// once per chunk for each distinct value. Random draws happen in exactly the reference order.
/// </remarks>
public sealed class ChunkPasses
{
    private readonly WorldGenerator _gen;
    private readonly WorldLayout _layout;
    private readonly GenParams _p;

    public ChunkPasses(WorldGenerator gen)
    {
        _gen = gen;
        _layout = gen.Layout;
        _p = gen.Params;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void Terrain(ChunkData chunk)
    {
        var origin = _layout.ChunkOrigin(chunk.Coord);
        var region = _gen.RegionEntryAt(_layout.RegionOf(origin));
        chunk.WaterLevel = (float)_gen.WaterLevel(origin.X + chunk.Size / 2.0, origin.Y + chunk.Size / 2.0);

        var biomes = _gen.Biomes;
        var climate = new ClimateGrid(_gen, origin, chunk.Size);
        Span<double> values = stackalloc double[biomes.Width];
        // Interned indices per biome rule (offset by one for "no biome") and per surface tag key,
        // filled the first time each appears; zero means not yet interned.
        var biomeIndex = new int[biomes.Count + 1];
        var tagIndex = new int[biomes.SurfaceKeyCount];
        var shallowDepth = _p.ShallowDepth;
        var maxSlope = _p.MaxWalkableSlope;

        for (var ly = 0; ly < chunk.Size; ly++)
        {
            for (var lx = 0; lx < chunk.Size; lx++)
            {
                var elevation = climate.Cell(lx, ly, values, out var slope, out var water);
                var rule = biomes.Match(values, region.Biomes);
                var biome = rule < 0 ? null : biomes.Biome(rule);
                var h = elevation;
                if (biome is not null && biome.Terrace > 0)
                {
                    h = Math.Floor(h / biome.Terrace) * biome.Terrace;
                }
                var i = chunk.Index(lx, ly);
                chunk.Height[i] = (float)h;
                var b = biomeIndex[rule + 1];
                if (b == 0)
                {
                    b = chunk.InternBiome(biome?.Id ?? "") + 1;
                    biomeIndex[rule + 1] = b;
                }
                chunk.BiomeIndex[i] = (ushort)(b - 1);

                var flags = CellFlags.Walkable;
                var variant = BiomeTable.VariantDry;
                if (h < water)
                {
                    flags |= CellFlags.Water;
                    var depth = water - h;
                    if (depth > shallowDepth)
                    {
                        flags &= ~CellFlags.Walkable;
                        variant = BiomeTable.VariantDeep;
                    }
                    else
                    {
                        variant = BiomeTable.VariantShallow;
                    }
                }
                else if (slope > maxSlope && (biome?.Terrace ?? 0) <= 0)
                {
                    flags |= CellFlags.Blocked;
                    variant = BiomeTable.VariantCliff;
                }
                chunk.Flags[i] = flags;
                var key = biomes.SurfaceKey(rule, variant);
                var t = tagIndex[key];
                if (t == 0)
                {
                    t = chunk.Intern(biomes.Surface(rule, variant)) + 1;
                    tagIndex[key] = t;
                }
                chunk.SurfaceTag[i] = (ushort)(t - 1);
            }
        }
    }

    /// <summary>Flattens around and stamps the landmarks of every point of interest reaching into this chunk.</summary>
    public void Landmarks(ChunkData chunk)
    {
        var origin = _layout.ChunkOrigin(chunk.Coord);
        var zc = _layout.ZoneOf(chunk.Coord);
        List<PoiInstance>? pois = null;
        foreach (var poi in _gen.PoisAroundCached(zc))
        {
            var landmark = poi.Landmark is null ? null : _gen.Content.Get<LandmarkDef>(poi.Landmark);
            var half = landmark is null ? 0 : Math.Max(landmark.Rows.Count, Width(landmark)) / 2.0 + 1;
            var r = Math.Max(poi.Def.Radius + _p.FlattenFalloff, half);
            if (poi.X + r < origin.X || poi.X - r > origin.X + chunk.Size || poi.Y + r < origin.Y || poi.Y - r > origin.Y + chunk.Size)
            {
                continue;
            }
            (pois ??= new List<PoiInstance>()).Add(poi);
        }
        if (pois is null)
        {
            return;
        }

        // Flatten footprints first so every stamp sits on level ground.
        foreach (var poi in pois)
        {
            if (poi.Def.Flatten)
            {
                Flatten(chunk, origin, poi.X, poi.Y, poi.Def.Radius, _p.FlattenFalloff, poi.BaseHeight);
            }
        }

        foreach (var poi in pois)
        {
            if (poi.Landmark is null || _gen.Content.Get<LandmarkDef>(poi.Landmark) is not { } landmark)
            {
                continue;
            }
            Stamp(chunk, origin, poi, landmark);
        }
    }

    /// <summary>Eases heights toward a point's base height over its radius plus the falloff, and clears its footprint.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void Flatten(ChunkData chunk, CellCoord origin, double px, double py, double radius, double falloff, float baseHeight)
    {
        for (var ly = 0; ly < chunk.Size; ly++)
        {
            for (var lx = 0; lx < chunk.Size; lx++)
            {
                var wx = origin.X + lx + 0.5;
                var wy = origin.Y + ly + 0.5;
                var d = Math.Sqrt((wx - px) * (wx - px) + (wy - py) * (wy - py));
                if (d > radius + falloff)
                {
                    continue;
                }
                var i = chunk.Index(lx, ly);
                var t = d <= radius ? 1.0 : 1.0 - (d - radius) / falloff;
                t = t * t * (3 - 2 * t);
                chunk.Height[i] = (float)(chunk.Height[i] + (baseHeight - chunk.Height[i]) * t);
                if (d <= radius)
                {
                    chunk.Flags[i] |= CellFlags.Flattened;
                    chunk.Flags[i] &= ~CellFlags.Blocked;
                    if (chunk.Height[i] >= chunk.WaterLevel)
                    {
                        chunk.Flags[i] &= ~CellFlags.Water;
                        chunk.Flags[i] |= CellFlags.Walkable;
                    }
                }
            }
        }
    }

    /// <summary>The widest row of a landmark. Like the sequence maximum it stands for, it throws for a landmark without rows.</summary>
    internal static int Width(LandmarkDef landmark)
    {
        var rows = landmark.Rows;
        if (rows.Count == 0)
        {
            throw new InvalidOperationException("Sequence contains no elements");
        }
        var width = rows[0].Length;
        for (var r = 1; r < rows.Count; r++)
        {
            width = Math.Max(width, rows[r].Length);
        }
        return width;
    }

    private void Stamp(ChunkData chunk, CellCoord origin, PoiInstance poi, LandmarkDef landmark)
    {
        var rows = landmark.Rows;
        var height = rows.Count;
        var width = Width(landmark);
        var ax = landmark.AnchorX > 0 ? landmark.AnchorX : width / 2;
        var ay = landmark.AnchorY > 0 ? landmark.AnchorY : height / 2;
        var cx = (int)Math.Floor(poi.X);
        var cy = (int)Math.Floor(poi.Y);
        var stampedAny = false;
        TagSet? structureTags = null;
        TagSet? groundTags = null;
        // Tag sets per legend cell, composed once per stamp instead of per stamped cell.
        Dictionary<LegendCell, (TagSet Layer, TagSet Surface)>? composed = null;

        for (var ry = 0; ry < height; ry++)
        {
            var row = rows[ry];
            for (var rx = 0; rx < row.Length; rx++)
            {
                var ch = row[rx];
                if (ch == ' ' || ch == '.')
                {
                    continue;
                }
                var wx = cx + (rx - ax);
                var wy = cy + (height - 1 - ry - ay);
                var lx = wx - origin.X;
                var ly = wy - origin.Y;
                if (lx < 0 || ly < 0 || lx >= chunk.Size || ly >= chunk.Size)
                {
                    continue;
                }
                if (!landmark.Legend.TryGetValue(ch.ToString(), out var cell))
                {
                    continue;
                }
                composed ??= new Dictionary<LegendCell, (TagSet, TagSet)>(ReferenceEqualityComparer.Instance);
                if (!composed.TryGetValue(cell, out var tags))
                {
                    structureTags ??= TagSet.Of("structure");
                    groundTags ??= TagSet.Of("ground");
                    var layer = cell.Tags.Union(structureTags);
                    tags = (layer, layer.Union(groundTags));
                    composed[cell] = tags;
                }
                var i = chunk.Index(lx, ly);
                var h = (float)(poi.BaseHeight + cell.Height);
                if (cell.Layer)
                {
                    chunk.AddLayer(i, new Surface(h, cell.Walkable, tags.Layer));
                }
                else if (cell.Surface)
                {
                    chunk.Height[i] = h;
                    chunk.Flags[i] = CellFlags.Structure | (cell.Walkable ? CellFlags.Walkable : CellFlags.Blocked);
                    chunk.SurfaceTag[i] = chunk.Intern(tags.Surface);
                }
                if (cell.Spawn is not null)
                {
                    chunk.Spawns.Add(new SpawnPoint(cell.Spawn, wx + 0.5f, wy + 0.5f, 1, 300, poi.Id));
                }
                stampedAny = true;
            }
        }
        if (stampedAny)
        {
            chunk.Stamps.Add(new PoiStamp(poi.Id, landmark.Id, cx, cy, poi.BaseHeight));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void Flora(ChunkData chunk)
    {
        var origin = _layout.ChunkOrigin(chunk.Coord);
        var rng = _gen.RngFor($"flora:{chunk.Coord.X}:{chunk.Coord.Y}");
        var clump = _gen.FloraClump;
        var jitter = _p.FloraJitter;
        var biomeCount = chunk.BiomeTable.Count;
        var tagCount = chunk.TagTable.Count;
        // Per biome entry, the biome definition (resolved on first use); per (biome, surface tags), the
        // flora of the biome and its parents whose surface requirement those tags meet, in order.
        var biomes = new BiomeDef?[biomeCount];
        var resolved = new bool[biomeCount];
        var candidates = new FloraSpec[]?[biomeCount * tagCount];
        for (var ly = 0; ly < chunk.Size; ly++)
        {
            for (var lx = 0; lx < chunk.Size; lx++)
            {
                var i = chunk.Index(lx, ly);
                if (!IsOpenGround(chunk, i))
                {
                    continue;
                }
                var b = chunk.BiomeIndex[i];
                if (!resolved[b])
                {
                    biomes[b] = _gen.Content.Get<BiomeDef>(chunk.BiomeTable[b]);
                    resolved[b] = true;
                }
                var biome = biomes[b];
                if (biome is null)
                {
                    continue;
                }
                var at = b * tagCount + chunk.SurfaceTag[i];
                var specs = candidates[at] ??= FloraFor(biome, chunk.TagTable[chunk.SurfaceTag[i]]);
                foreach (var f in specs)
                {
                    var p = f.Density / 100.0;
                    if (f.Clumping > 0)
                    {
                        var wx = origin.X + lx;
                        var wy = origin.Y + ly;
                        var n = 0.5 + 0.5 * clump.Sample(wx * f.Clumping, wy * f.Clumping);
                        p *= n * n * 2;
                    }
                    if (rng.Chance(p))
                    {
                        var jx = (float)(rng.Range(-jitter, jitter));
                        var jy = (float)(rng.Range(-jitter, jitter));
                        chunk.Flora.Add(new FloraPlacement(f.Pattern, origin.X + lx + 0.5f + jx, origin.Y + ly + 0.5f + jy, (float)rng.Range(0.8, 1.25)));
                        break;
                    }
                }
            }
        }
    }

    /// <summary>The flora of a biome and its parents, in order, whose surface requirement the tags meet.</summary>
    private FloraSpec[] FloraFor(BiomeDef biome, TagSet tags)
    {
        var specs = new List<FloraSpec>();
        var cur = biome;
        var guard = 0;
        while (cur is not null && guard++ < 16)
        {
            foreach (var f in cur.Flora)
            {
                if (f.On.IsEmpty || tags.HasAll(f.On))
                {
                    specs.Add(f);
                }
            }
            cur = cur.Parent is null ? null : _gen.Content.Get<BiomeDef>(cur.Parent);
        }
        return specs.ToArray();
    }

    /// <summary>The spawns of a biome and its parents, each followed by its spawn tables, in order.</summary>
    private List<SpawnSpec> AllSpawns(BiomeDef biome)
    {
        var specs = new List<SpawnSpec>();
        var cur = biome;
        var guard = 0;
        while (cur is not null && guard++ < 16)
        {
            specs.AddRange(cur.Spawns);
            foreach (var tableId in cur.SpawnTables)
            {
                if (_gen.Content.TryGet<SpawnTableDef>(tableId, out var table) && table is not null)
                {
                    specs.AddRange(table.Spawns);
                }
            }
            cur = cur.Parent is null ? null : _gen.Content.Get<BiomeDef>(cur.Parent);
        }
        return specs;
    }

    /// <summary>Spawn points for packs: biome densities per thousand walkable cells, plus point-of-interest inhabitants.</summary>
    public void Spawns(ChunkData chunk)
    {
        var origin = _layout.ChunkOrigin(chunk.Coord);
        var zc = _layout.ZoneOf(chunk.Coord);
        var zone = _gen.Zone(zc);
        var rng = _gen.RngFor($"spawns:{chunk.Coord.X}:{chunk.Coord.Y}");
        // Walkable open cells grouped by biome entry in one buffer: group b is cells[start[b] .. start[b] + count[b]),
        // in cell order, and the groups are visited in the order their first cell appears.
        var cells = t_cells is { } scratch && scratch.Length >= chunk.Height.Length ? scratch : t_cells = new int[chunk.Height.Length];
        var groupStart = new int[chunk.BiomeTable.Count];
        var groupCount = new int[chunk.BiomeTable.Count];
        var order = new List<int>();
        GroupWalkable(chunk, cells, groupStart, groupCount, order);
        // Keep spawns away from safe hubs.
        var safeCircles = SafeCircles(_gen.PoisAroundCached(zc));

        foreach (var b in order)
        {
            var start = groupStart[b];
            var walkable = groupCount[b];
            var biome = _gen.Content.Get<BiomeDef>(chunk.BiomeTable[b]);
            if (biome is null)
            {
                continue;
            }
            foreach (var spec in AllSpawns(biome))
            {
                if (spec.Level is { } lvl && !lvl.Contains(zone.Level))
                {
                    continue;
                }
                var expected = spec.Density * walkable / 1000.0;
                var count = (int)Math.Floor(expected);
                if (rng.Chance(expected - count))
                {
                    count++;
                }
                for (var k = 0; k < count; k++)
                {
                    var i = cells[start + rng.Next(walkable)];
                    if (!spec.On.IsEmpty && !chunk.TagsAt(i).HasAll(spec.On))
                    {
                        continue;
                    }
                    var wx = origin.X + i % chunk.Size + 0.5f;
                    var wy = origin.Y + i / chunk.Size + 0.5f;
                    if (InAny(safeCircles, wx, wy))
                    {
                        continue;
                    }
                    var pack = rng.Range((int)spec.PackSize[0], (int)spec.PackSize[Math.Min(1, spec.PackSize.Length - 1)]);
                    chunk.Spawns.Add(new SpawnPoint(spec.Pattern, wx, wy, Math.Max(1, pack), spec.Respawn, null));
                }
            }
        }

        foreach (var poi in _gen.PoisOf(zc))
        {
            var cx = (int)Math.Floor(poi.X);
            var cy = (int)Math.Floor(poi.Y);
            if (cx < origin.X || cy < origin.Y || cx >= origin.X + chunk.Size || cy >= origin.Y + chunk.Size)
            {
                continue;
            }
            var prng = _gen.RngFor($"inhabitants:{poi.Id}");
            foreach (var spec in poi.Def.Inhabitants)
            {
                // The site level includes its authored offset from the region.
                if (spec.Level is { } level && !level.Contains(poi.Level))
                {
                    continue;
                }
                var count = Math.Max(1, (int)Math.Round(spec.Density));
                for (var k = 0; k < count; k++)
                {
                    var ang = prng.NextDouble() * Math.PI * 2;
                    var dist = prng.Range(1.0, Math.Max(1.5, poi.Def.Radius - 1));
                    var pack = prng.Range((int)spec.PackSize[0], (int)spec.PackSize[Math.Min(1, spec.PackSize.Length - 1)]);
                    chunk.Spawns.Add(new SpawnPoint(spec.Pattern, (float)(poi.X + Math.Cos(ang) * dist), (float)(poi.Y + Math.Sin(ang) * dist), Math.Max(1, pack), spec.Respawn, poi.Id));
                }
            }
        }
    }

    // Scratch for grouping one chunk's cells at a time on a thread; fully rewritten before use.
    [ThreadStatic]
    private static int[]? t_cells;

    private static bool IsOpenGround(ChunkData chunk, int i) =>
        chunk.IsWalkable(i) && (chunk.Flags[i] & (CellFlags.Structure | CellFlags.Water)) == 0;

    /// <summary>
    /// Walkable open cells grouped by biome entry into <paramref name="cells"/> (group b starts at
    /// <paramref name="start"/>[b] and holds <paramref name="count"/>[b] cells, in cell order), with the
    /// groups listed in the order their first cell appears.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void GroupWalkable(ChunkData chunk, int[] cells, int[] start, int[] count, List<int> order)
    {
        for (var i = 0; i < chunk.Height.Length; i++)
        {
            if (IsOpenGround(chunk, i))
            {
                var b = chunk.BiomeIndex[i];
                if (count[b]++ == 0)
                {
                    order.Add(b);
                }
            }
        }
        var next = 0;
        foreach (var b in order)
        {
            start[b] = next;
            next += count[b];
        }
        var fill = (int[])start.Clone();
        for (var i = 0; i < chunk.Height.Length; i++)
        {
            if (IsOpenGround(chunk, i))
            {
                cells[fill[chunk.BiomeIndex[i]]++] = i;
            }
        }
    }

    /// <summary>The circles spawns keep out of, as (x, y, radius) triples: each safe point of interest plus a margin.</summary>
    private static double[] SafeCircles(PoiInstance[] around)
    {
        var count = 0;
        foreach (var p in around)
        {
            if (p.Def.Safe)
            {
                count++;
            }
        }
        var circles = new double[count * 3];
        var at = 0;
        foreach (var p in around)
        {
            if (p.Def.Safe)
            {
                circles[at++] = p.X;
                circles[at++] = p.Y;
                circles[at++] = p.Def.Radius + 12;
            }
        }
        return circles;
    }

    private static bool InAny(double[] circles, float wx, float wy)
    {
        for (var c = 0; c < circles.Length; c += 3)
        {
            var x = circles[c];
            var y = circles[c + 1];
            var r = circles[c + 2];
            if ((wx - x) * (wx - x) + (wy - y) * (wy - y) < r * r)
            {
                return true;
            }
        }
        return false;
    }
}
