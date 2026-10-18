using HollowWake.Core.Content;
using HollowWake.Core.Maths;
using HollowWake.Core.Tags;
using HollowWake.Core.World;
using HollowWake.Core.WorldGen;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>
/// Golden hashes of generated output. Generation is a pure function of seed, content, overrides and
/// overlays, and the client generates the same terrain as the server, so an optimization that moves a
/// single height bit is a bug. These pin every chunk array, interned table, layer, stamp, spawn point and
/// flora placement of a few zones, the planned zone info, climate samples, regions and an overlaid world.
/// When generation or the core content changes on purpose, update the constants from the failure message.
/// </summary>
public class WorldGenGoldenTests
{
    private const string Seed = "aureth";

    // A private registry: the golden output must not depend on what other tests add to the shared one.
    private static readonly Lazy<ContentRegistry> Registry = new(() =>
    {
        var registry = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out var report);
        if (report.HasErrors)
        {
            throw new InvalidOperationException(report.ToString());
        }
        return registry;
    });

    [Fact]
    public void StartZoneIsTheOrigin()
    {
        var g = new WorldGenerator(Registry.Value, Rng.Hash64(Seed));
        Assert.Equal(new ZoneCoord(0, 0), g.StartZone());
    }

    // Corrected foundation plus Kobold choices/spacing: named nearby-site deltas in experiments/kobolds/evidence/world-integrated-summary.json.
    [Theory]
    [InlineData(0, 0, "D2F6FA64DD42E7B7", "384BA03BE0D34C0A")]
    [InlineData(1, 0, "33C9B160B66B9604", "DF735AA7610989B3")]
    [InlineData(0, -1, "8AD7370958FA7D7B", "D785D8F084A1F919")]
    [InlineData(3, 2, "BAFEA2C1CCA0DEC0", "8C46ACF595DB3A42")]
    public void ZoneMatchesGoldenHashes(int x, int y, string infoHash, string chunksHash)
    {
        var g = new WorldGenerator(Registry.Value, Rng.Hash64(Seed));
        var zc = new ZoneCoord(x, y);
        var info = new Hasher();
        HashZone(info, g, zc);
        var chunks = new Hasher();
        foreach (var cc in g.Layout.ChunksOf(zc))
        {
            HashChunk(chunks, g.GenerateChunk(cc));
        }
        Golden($"{zc} info and chunks", $"{infoHash} {chunksHash}", $"{info.Hex} {chunks.Hex}");
    }

    [Fact]
    public void ClimateSamplesMatchGoldenHash()
    {
        var g = new WorldGenerator(Registry.Value, Rng.Hash64(Seed));
        var h = new Hasher();
        // A long diagonal through several regions (and their blends), then the benchmark's points.
        for (var i = 0; i < 2000; i++)
        {
            HashPoint(h, g, -6000 + i * 6.37, 4200 - i * 4.11, i % 10 == 0);
        }
        for (var i = 0; i < 500; i++)
        {
            HashPoint(h, g, 100 + i * 0.37, 50 + i * 0.11, i % 50 == 0);
        }
        for (var ry = -3; ry <= 3; ry++)
        {
            for (var rx = -3; rx <= 3; rx++)
            {
                HashRegion(h, g.Region(new RegionCoord(rx, ry)));
            }
        }
        Golden("climate samples and regions", "999D90D6110B1CDD", h.Hex);
    }

    [Fact]
    public void OverlaidWorldMatchesGoldenHash()
    {
        var overlays = Overlays();
        var g = new WorldGenerator(Registry.Value, Rng.Hash64(Seed), overlays);
        var h = new Hasher();
        foreach (var zc in new[] { new ZoneCoord(0, 0), new ZoneCoord(1, 0), new ZoneCoord(0, -1) })
        {
            HashZone(h, g, zc);
        }
        foreach (var zc in new[] { new ZoneCoord(0, 0), new ZoneCoord(1, 0) })
        {
            foreach (var cc in g.Layout.ChunksOf(zc))
            {
                HashChunk(h, g.GenerateChunk(cc));
            }
        }
        for (var i = 0; i < 400; i++)
        {
            HashPoint(h, g, 40 + i * 0.83, 20 + i * 0.61, i % 20 == 0);
        }
        Golden("overlaid world", "AC3C2BFA30192BBC", h.Hex);

        // Removing an overlay must regenerate exactly what a generator that never had it produces.
        overlays.Remove("fount");
        var fresh = new WorldGenerator(Registry.Value, Rng.Hash64(Seed), WithoutFount());
        foreach (var cc in new[] { new ChunkCoord(4, 3), new ChunkCoord(3, 4), new ChunkCoord(9, 2) })
        {
            var a = new Hasher();
            HashChunk(a, g.GenerateChunk(cc), version: false);
            var b = new Hasher();
            HashChunk(b, fresh.GenerateChunk(cc), version: false);
            Assert.Equal(b.Hex, a.Hex);
        }
        var za = new Hasher();
        HashZone(za, g, new ZoneCoord(0, 0));
        var zb = new Hasher();
        HashZone(zb, fresh, new ZoneCoord(0, 0));
        Assert.Equal(zb.Hex, za.Hex);
    }

    private static GenerationOverlays Overlays()
    {
        var overlays = WithoutFount();
        overlays.Add(new GenerationOverlay
        {
            Id = "fount",
            Circle = (140, 120, 70),
            Priority = 2,
            Params = { ["thread.ember"] = 0.45, ["moisture_offset"] = 0.2, ["elevation_multiplier"] = 1.15 },
        });
        return overlays;
    }

    private static GenerationOverlays WithoutFount()
    {
        var overlays = new GenerationOverlays();
        overlays.Add(new GenerationOverlay
        {
            Id = "season",
            Global = true,
            Params = { ["temperature_offset"] = -0.08, ["elevation_offset"] = 0.75 },
        });
        overlays.Add(new GenerationOverlay
        {
            Id = "flood",
            Zones = { new ZoneCoord(1, 0) },
            Priority = 1,
            Params = { ["water_level"] = 2.5, ["thread.void"] = 0.35 },
        });
        overlays.Add(new GenerationOverlay
        {
            Id = "decree",
            Regions = { new RegionCoord(0, -1) },
            Params = { ["poi_bonus"] = 1.5, ["thread.void"] = 0.5 },
        });
        return overlays;
    }

    // ------------------------------------------------------------ hashing

    private static void Golden(string what, string expected, string actual) =>
        Assert.True(expected == actual, $"{what}: expected {expected}, generated {actual}. Generation output changed; update the constant only if that was intended.");

    private static void HashPoint(Hasher h, WorldGenerator g, double x, double y, bool waterDistance)
    {
        var s = g.Sample(x, y);
        h.F64(s.Elevation);
        h.F64(s.Temperature);
        h.F64(s.Moisture);
        h.F64(s.Slope);
        h.I32(s.Threads.Count);
        foreach (var (id, v) in s.Threads)
        {
            h.Str(id);
            h.F64(v);
        }
        h.Bool(s.Water);
        h.F64(s.WaterDepth);
        h.Str(g.Biome(s, g.Region(g.Layout.RegionOf(x, y)))?.Id);
        h.F64(g.Elevation(x, y));
        h.F64(g.WaterLevel(x, y));
        if (waterDistance)
        {
            h.F64(g.WaterDistance(x, y));
        }
    }

    private static void HashRegion(Hasher h, RegionInfo r)
    {
        h.I32(r.Coord.X);
        h.I32(r.Coord.Y);
        h.Str(r.Archetype.Id);
        h.Str(r.Name);
        h.Str(r.Faction);
        h.I32(r.Level);
        h.I32(r.Distance);
        h.I32(r.Threads.Count);
        foreach (var (id, v) in r.Threads)
        {
            h.Str(id);
            h.F64(v);
        }
    }

    private static void HashPoi(Hasher h, PoiInstance p)
    {
        h.Str(p.Id);
        h.Str(p.Def.Id);
        h.Str(p.Name);
        h.I32(p.Zone.X);
        h.I32(p.Zone.Y);
        h.F64(p.X);
        h.F64(p.Y);
        h.F32(p.BaseHeight);
        h.Str(p.Landmark);
        h.Str(p.Faction);
        h.I32(p.Level);
    }

    private static void HashZone(Hasher h, WorldGenerator g, ZoneCoord zc)
    {
        var z = g.Zone(zc);
        h.I32(z.Coord.X);
        h.I32(z.Coord.Y);
        h.Str(z.Name);
        h.I32(z.Level);
        h.Bool(z.Safe);
        h.Bool(z.PlayerStart);
        HashRegion(h, z.Region);
        h.I32(z.Pois.Count);
        foreach (var p in z.Pois)
        {
            HashPoi(h, p);
        }
        h.I32(z.Quests.Count);
        foreach (var q in z.Quests)
        {
            h.Str(q.Id);
            h.Str(q.Name);
            h.Str(q.Description);
            h.Tags(q.Tags);
            h.I32(q.Level);
            h.Str(q.Giver);
            h.Str(q.Zone);
            h.Str(q.Faction);
            h.Bool(q.Repeatable);
            h.I32(q.Objectives.Count);
            foreach (var o in q.Objectives)
            {
                h.Str(o.Id);
                h.Str(o.Type);
                h.Str(o.Target);
                h.Tags(o.Tags);
                h.I32(o.Count);
                h.Str(o.Zone);
                h.Str(o.Poi);
                h.Str(o.Text);
                h.Bool(o.Optional);
            }
            h.I32(q.Text.Count);
            foreach (var (k, v) in q.Text)
            {
                h.Str(k);
                h.Str(v);
            }
        }
        foreach (var p in g.PoisAround(zc))
        {
            HashPoi(h, p);
        }
        var (sx, sy) = g.StartPoint(zc);
        h.F64(sx);
        h.F64(sy);
    }

    private static void HashChunk(Hasher h, ChunkData c, bool version = true)
    {
        h.I32(c.Coord.X);
        h.I32(c.Coord.Y);
        h.I32(c.Size);
        if (version)
        {
            h.I32(c.GenVersion);
        }
        h.Bool(c.Modified);
        h.F32(c.WaterLevel);
        for (var i = 0; i < c.Height.Length; i++)
        {
            h.F32(c.Height[i]);
            h.Byte((byte)c.Flags[i]);
            h.I32(c.SurfaceTag[i]);
            h.I32(c.BiomeIndex[i]);
        }
        h.I32(c.TagTable.Count);
        foreach (var t in c.TagTable)
        {
            h.Tags(t);
        }
        h.I32(c.BiomeTable.Count);
        foreach (var b in c.BiomeTable)
        {
            h.Str(b);
        }
        h.I32(c.Layers.Count);
        foreach (var (index, surfaces) in c.Layers)
        {
            h.I32(index);
            h.I32(surfaces.Count);
            foreach (var s in surfaces)
            {
                h.F32(s.Height);
                h.Bool(s.Walkable);
                h.Tags(s.Tags);
            }
        }
        h.I32(c.Stamps.Count);
        foreach (var s in c.Stamps)
        {
            h.Str(s.PoiId);
            h.Str(s.Landmark);
            h.I32(s.CenterX);
            h.I32(s.CenterY);
            h.F32(s.BaseHeight);
        }
        h.I32(c.Spawns.Count);
        foreach (var s in c.Spawns)
        {
            h.Str(s.Pattern);
            h.F32(s.X);
            h.F32(s.Y);
            h.I32(s.PackSize);
            h.F64(s.RespawnSeconds);
            h.Str(s.Group);
            h.I32(s.Level ?? 0);
        }
        h.I32(c.Flora.Count);
        foreach (var f in c.Flora)
        {
            h.Str(f.Pattern);
            h.F32(f.X);
            h.F32(f.Y);
            h.F32(f.Scale);
        }
    }

    /// <summary>FNV-1a over the exact bits of every value, so any change at all shows.</summary>
    private sealed class Hasher
    {
        private ulong _h = 14695981039346656037UL;

        public string Hex => _h.ToString("X16");

        public void Byte(byte b)
        {
            _h ^= b;
            _h *= 1099511628211UL;
        }

        public void I32(int v)
        {
            for (var i = 0; i < 32; i += 8)
            {
                Byte((byte)(v >> i));
            }
        }

        public void I64(long v)
        {
            for (var i = 0; i < 64; i += 8)
            {
                Byte((byte)(v >> i));
            }
        }

        public void F32(float v) => I32(BitConverter.SingleToInt32Bits(v));
        public void F64(double v) => I64(BitConverter.DoubleToInt64Bits(v));
        public void Bool(bool v) => Byte(v ? (byte)1 : (byte)0);

        public void Str(string? s)
        {
            if (s is null)
            {
                I32(-1);
                return;
            }
            I32(s.Length);
            foreach (var ch in s)
            {
                Byte((byte)ch);
                Byte((byte)(ch >> 8));
            }
        }

        public void Tags(TagSet t)
        {
            I32(t.Count);
            foreach (var s in t.Items)
            {
                Str(s);
            }
        }
    }
}
