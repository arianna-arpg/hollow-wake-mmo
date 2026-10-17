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

    public void Terrain(ChunkData chunk)
    {
        var origin = _layout.ChunkOrigin(chunk.Coord);
        var region = _gen.Region(_layout.RegionOf(origin));
        var ground = TagSet.Of("ground");
        var tagCache = new Dictionary<(string, int), ushort>();
        chunk.WaterLevel = (float)_gen.WaterLevel(origin.X + chunk.Size / 2.0, origin.Y + chunk.Size / 2.0);

        for (var ly = 0; ly < chunk.Size; ly++)
        {
            for (var lx = 0; lx < chunk.Size; lx++)
            {
                var wx = origin.X + lx + 0.5;
                var wy = origin.Y + ly + 0.5;
                var s = _gen.Sample(wx, wy);
                var biome = _gen.Biome(s, region);
                var h = s.Elevation;
                if (biome is not null && biome.Terrace > 0)
                {
                    h = Math.Floor(h / biome.Terrace) * biome.Terrace;
                }
                var i = chunk.Index(lx, ly);
                chunk.Height[i] = (float)h;
                chunk.BiomeIndex[i] = chunk.InternBiome(biome?.Id ?? "");

                var flags = CellFlags.Walkable;
                var tags = ground.Union(biome?.Surface ?? TagSet.Empty);
                var water = _gen.WaterLevel(wx, wy);
                if (h < water)
                {
                    flags |= CellFlags.Water;
                    var depth = water - h;
                    if (depth > _p.ShallowDepth)
                    {
                        flags &= ~CellFlags.Walkable;
                        tags = tags.With("water", "deep");
                    }
                    else
                    {
                        tags = tags.With("water", "shallow");
                    }
                }
                else if (s.Slope > _p.MaxWalkableSlope && (biome?.Terrace ?? 0) <= 0)
                {
                    flags |= CellFlags.Blocked;
                    tags = tags.With("cliff");
                }
                chunk.Flags[i] = flags;
                var key = (tags.ToString(), 0);
                if (!tagCache.TryGetValue(key, out var idx))
                {
                    idx = chunk.Intern(tags);
                    tagCache[key] = idx;
                }
                chunk.SurfaceTag[i] = idx;
            }
        }
    }

    /// <summary>Flattens around and stamps the landmarks of every point of interest reaching into this chunk.</summary>
    public void Landmarks(ChunkData chunk)
    {
        var origin = _layout.ChunkOrigin(chunk.Coord);
        var zc = _layout.ZoneOf(chunk.Coord);
        var reach = 0.0;
        var pois = new List<PoiInstance>();
        foreach (var poi in _gen.PoisAround(zc))
        {
            var landmark = poi.Landmark is null ? null : _gen.Content.Get<LandmarkDef>(poi.Landmark);
            var half = landmark is null ? 0 : Math.Max(landmark.Rows.Count, landmark.Rows.Max(r => r.Length)) / 2.0 + 1;
            var r = Math.Max(poi.Def.Radius + _p.FlattenFalloff, half);
            if (poi.X + r < origin.X || poi.X - r > origin.X + chunk.Size || poi.Y + r < origin.Y || poi.Y - r > origin.Y + chunk.Size)
            {
                continue;
            }
            pois.Add(poi);
            reach = Math.Max(reach, r);
        }
        if (pois.Count == 0)
        {
            return;
        }

        // Flatten footprints first so every stamp sits on level ground.
        foreach (var poi in pois)
        {
            if (!poi.Def.Flatten)
            {
                continue;
            }
            var radius = poi.Def.Radius;
            var falloff = _p.FlattenFalloff;
            for (var ly = 0; ly < chunk.Size; ly++)
            {
                for (var lx = 0; lx < chunk.Size; lx++)
                {
                    var wx = origin.X + lx + 0.5;
                    var wy = origin.Y + ly + 0.5;
                    var d = Math.Sqrt((wx - poi.X) * (wx - poi.X) + (wy - poi.Y) * (wy - poi.Y));
                    if (d > radius + falloff)
                    {
                        continue;
                    }
                    var i = chunk.Index(lx, ly);
                    var t = d <= radius ? 1.0 : 1.0 - (d - radius) / falloff;
                    t = t * t * (3 - 2 * t);
                    chunk.Height[i] = (float)(chunk.Height[i] + (poi.BaseHeight - chunk.Height[i]) * t);
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

        foreach (var poi in pois)
        {
            if (poi.Landmark is null || _gen.Content.Get<LandmarkDef>(poi.Landmark) is not { } landmark)
            {
                continue;
            }
            Stamp(chunk, origin, poi, landmark);
        }
    }

    private void Stamp(ChunkData chunk, CellCoord origin, PoiInstance poi, LandmarkDef landmark)
    {
        var rows = landmark.Rows;
        var height = rows.Count;
        var width = rows.Max(r => r.Length);
        var ax = landmark.AnchorX > 0 ? landmark.AnchorX : width / 2;
        var ay = landmark.AnchorY > 0 ? landmark.AnchorY : height / 2;
        var cx = (int)Math.Floor(poi.X);
        var cy = (int)Math.Floor(poi.Y);
        var stampedAny = false;
        var structureTags = TagSet.Of("structure");

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
                if (!landmark.Legend.TryGetValue(ch.ToString(), out var cell))
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
                var i = chunk.Index(lx, ly);
                var h = (float)(poi.BaseHeight + cell.Height);
                var tags = cell.Tags.Union(structureTags);
                if (cell.Layer)
                {
                    chunk.AddLayer(i, new Surface(h, cell.Walkable, tags));
                }
                else if (cell.Surface)
                {
                    chunk.Height[i] = h;
                    chunk.Flags[i] = CellFlags.Structure | (cell.Walkable ? CellFlags.Walkable : CellFlags.Blocked);
                    chunk.SurfaceTag[i] = chunk.Intern(tags.Union(TagSet.Of("ground")));
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

    public void Flora(ChunkData chunk)
    {
        var origin = _layout.ChunkOrigin(chunk.Coord);
        var rng = _gen.RngFor($"flora:{chunk.Coord.X}:{chunk.Coord.Y}");
        var clump = new Noise2D(Rng.Combine(_gen.Seed, 77));
        var biomeCache = new Dictionary<string, BiomeDef?>(StringComparer.Ordinal);
        for (var ly = 0; ly < chunk.Size; ly++)
        {
            for (var lx = 0; lx < chunk.Size; lx++)
            {
                var i = chunk.Index(lx, ly);
                if (!chunk.IsWalkable(i) || (chunk.Flags[i] & (CellFlags.Structure | CellFlags.Water)) != 0)
                {
                    continue;
                }
                var biomeId = chunk.BiomeAt(i);
                if (!biomeCache.TryGetValue(biomeId, out var biome))
                {
                    biome = _gen.Content.Get<BiomeDef>(biomeId);
                    biomeCache[biomeId] = biome;
                }
                if (biome is null)
                {
                    continue;
                }
                var tags = chunk.TagsAt(i);
                foreach (var f in AllFlora(biome))
                {
                    if (!f.On.IsEmpty && !tags.HasAll(f.On))
                    {
                        continue;
                    }
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
                        var jx = (float)(rng.Range(-_p.FloraJitter, _p.FloraJitter));
                        var jy = (float)(rng.Range(-_p.FloraJitter, _p.FloraJitter));
                        chunk.Flora.Add(new FloraPlacement(f.Pattern, origin.X + lx + 0.5f + jx, origin.Y + ly + 0.5f + jy, (float)rng.Range(0.8, 1.25)));
                        break;
                    }
                }
            }
        }
    }

    private IEnumerable<FloraSpec> AllFlora(BiomeDef biome)
    {
        var cur = biome;
        var guard = 0;
        while (cur is not null && guard++ < 16)
        {
            foreach (var f in cur.Flora)
            {
                yield return f;
            }
            cur = cur.Parent is null ? null : _gen.Content.Get<BiomeDef>(cur.Parent);
        }
    }

    private IEnumerable<SpawnSpec> AllSpawns(BiomeDef biome)
    {
        var cur = biome;
        var guard = 0;
        while (cur is not null && guard++ < 16)
        {
            foreach (var s in cur.Spawns)
            {
                yield return s;
            }
            foreach (var tableId in cur.SpawnTables)
            {
                if (_gen.Content.TryGet<SpawnTableDef>(tableId, out var table) && table is not null)
                {
                    foreach (var s in table.Spawns)
                    {
                        yield return s;
                    }
                }
            }
            cur = cur.Parent is null ? null : _gen.Content.Get<BiomeDef>(cur.Parent);
        }
    }

    /// <summary>Spawn points for packs: biome densities per thousand walkable cells, plus point-of-interest inhabitants.</summary>
    public void Spawns(ChunkData chunk)
    {
        var origin = _layout.ChunkOrigin(chunk.Coord);
        var zc = _layout.ZoneOf(chunk.Coord);
        var zone = _gen.Zone(zc);
        var rng = _gen.RngFor($"spawns:{chunk.Coord.X}:{chunk.Coord.Y}");
        var walkableByBiome = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (var i = 0; i < chunk.Height.Length; i++)
        {
            if (!chunk.IsWalkable(i) || (chunk.Flags[i] & (CellFlags.Structure | CellFlags.Water)) != 0)
            {
                continue;
            }
            var b = chunk.BiomeAt(i);
            if (!walkableByBiome.TryGetValue(b, out var list))
            {
                list = new List<int>();
                walkableByBiome[b] = list;
            }
            list.Add(i);
        }
        // Keep spawns away from safe hubs.
        var safeCircles = _gen.PoisAround(zc).Where(p => p.Def.Safe).Select(p => (p.X, p.Y, p.Def.Radius + 12)).ToList();

        foreach (var (biomeId, cells) in walkableByBiome)
        {
            var biome = _gen.Content.Get<BiomeDef>(biomeId);
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
                var expected = spec.Density * cells.Count / 1000.0;
                var count = (int)Math.Floor(expected);
                if (rng.Chance(expected - count))
                {
                    count++;
                }
                for (var k = 0; k < count; k++)
                {
                    var i = cells[rng.Next(cells.Count)];
                    if (!spec.On.IsEmpty && !chunk.TagsAt(i).HasAll(spec.On))
                    {
                        continue;
                    }
                    var wx = origin.X + i % chunk.Size + 0.5f;
                    var wy = origin.Y + i / chunk.Size + 0.5f;
                    if (safeCircles.Any(c => (wx - c.X) * (wx - c.X) + (wy - c.Y) * (wy - c.Y) < c.Item3 * c.Item3))
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
}
