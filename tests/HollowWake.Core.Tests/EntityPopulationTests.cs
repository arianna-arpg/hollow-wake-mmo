using System.Numerics;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Maths;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;
using HollowWake.Core.Territory;
using HollowWake.Core.World;
using HollowWake.Core.WorldGen;
using Xunit;

namespace HollowWake.Core.Tests;

public class EntityPopulationTests
{
    [Theory]
    [InlineData("undead_body")]
    [InlineData("goblinkin_body")]
    [InlineData("orc_body")]
    [InlineData("fauna_body")]
    [InlineData("gnoll_body")]
    [InlineData("beastkin_body")]
    public void EveryEntityHabitatHasAMatchingPopulationReference(string template)
    {
        var content = CoreContent.Registry;
        foreach (var body in content.Each<PatternDef>().Where(p => p.Extends == template))
        foreach (var tag in body.Tags.Where(t => t.StartsWith("habitat.", StringComparison.Ordinal)))
        {
            var habitat = tag["habitat.".Length..];
            var poiMatch = content.Each<PoiDef>().Any(p => (p.Id == habitat || p.Tags.Has(habitat) || p.Biomes.Has(habitat))
                && p.Inhabitants.Any(s => s.Pattern == body.Id));
            var biomeMatch = content.Each<BiomeDef>().Any(b => (b.Id == habitat || b.Tags.Has(habitat))
                && b.Spawns.Concat(b.SpawnTables.SelectMany(id => content.Require<SpawnTableDef>(id).Spawns))
                    .Any(s => s.Pattern == body.Id));
            Assert.True(poiMatch || biomeMatch, $"{body.Id} claims {tag} without a matching population reference");
        }
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(8, true)]
    public void NaturalSitesRequireEligibleInhabitants(int level, bool eligible)
    {
        var content = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out _);
        foreach (var def in content.Each<PoiDef>().ToArray()) content.With(def with { Weight = 0 });
        content.With(content.Require<PoiDef>("ossuary") with
        {
            Id = "test_site", Weight = 100, Level = 0, Slope = null, MinSpacing = 30,
            Inhabitants = new() { new SpawnSpec { Pattern = "skeleton_warrior", UseSiteLevel = true, Level = new Window { Min = 5 } } },
        });
        content.With(new RegionOverrideDef { Id = "hallows_home", X = 0, Y = 0, Archetype = "hallows", Level = level });
        content.With(new ZoneOverrideDef { Id = "ninewells", X = 0, Y = 0 });
        var generator = new WorldGenerator(content, Rng.Hash64("aureth"));
        var sites = generator.PoisOf(new ZoneCoord(0, 0));
        Assert.Equal(eligible, sites.Count > 0);
        foreach (var site in sites)
        {
            Assert.Equal("test_site", site.Def.Id);
            var spawns = generator.GenerateChunk(generator.Layout.ChunkOf(site.X, site.Y)).Spawns;
            Assert.Contains(spawns, s => s.Group == site.Id && s.Level == level);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothSiteSpawnPathsPreservePatternLevelUnlessExplicitlyScaled(bool useSiteLevel)
    {
        var content = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out _);
        content.With(new RegionOverrideDef { Id = "hallows_home", X = 0, Y = 0, Archetype = "hallows", Level = 8 });
        content.With(new LandmarkDef { Id = "level_marker", Rows = { "S" }, Legend = { ["S"] = new LegendCell { Spawn = "skeleton_warrior", UseSiteLevel = useSiteLevel } } });
        content.With(new PoiDef { Id = "level_site", Weight = 0, Level = 2, Landmarks = { "level_marker" }, Inhabitants = { new SpawnSpec { Pattern = "skeleton_warrior", UseSiteLevel = useSiteLevel } } });
        content.With(new ZoneOverrideDef { Id = "ninewells", X = 0, Y = 0,
            Pois = { new PoiPlacement { Poi = "level_site", X = 50, Y = 50 } } });
        var generator = new WorldGenerator(content, Rng.Hash64("aureth"));
        var sim = ZoneBuilder.CreateZone(content, generator, new WorldState(new GenerationOverlays()), new ZoneCoord(0, 0));
        ZoneBuilder.LoadZone(sim);
        var spawners = sim.World.Query<Spawner>().Where(s => s.C1.Group?.Contains("level_site", StringComparison.Ordinal) == true).Select(s => s.C1).ToArray();
        Assert.Equal(2, spawners.Length);
        var expected = useSiteLevel ? 10 : content.Require<PatternDef>("skeleton_warrior").Level;
        Assert.All(spawners, spawner => Assert.Equal(useSiteLevel ? (int?)10 : null, spawner.Level));
        sim.Tick();
        foreach (var spawner in spawners)
        {
            var first = Assert.Single(spawner.Alive);
            Assert.Equal(expected, sim.World.Get<Stats>(first)!.Level);
            sim.Service<DeathService>().Kill(sim, first, Entity.None);
            spawner.RespawnAt = sim.Clock.Time;
        }
        sim.Tick();
        Assert.All(spawners, spawner => Assert.Equal(expected, sim.World.Get<Stats>(Assert.Single(spawner.Alive))!.Level));
    }

    [Theory]
    [InlineData(0, 0, "spindlewatch_guard", 5)]
    [InlineData(0, 0, "tangle", 6)]
    [InlineData(0, -1, "remnant_shade", 2)]
    public void LegacyResidentsKeepTheirAuthoredLevels(int x, int y, string pattern, int level)
    {
        var content = CoreContent.Registry;
        var generator = new WorldGenerator(content, Rng.Hash64("aureth"));
        var sim = ZoneBuilder.CreateZone(content, generator, new WorldState(new GenerationOverlays()), new ZoneCoord(x, y));
        ZoneBuilder.LoadZone(sim);
        var spawners = sim.World.Query<Spawner>().Select(s => s.C1).Where(s => s.Pattern == pattern && s.Group is not null).ToArray();
        Assert.NotEmpty(spawners);
        Assert.All(spawners, s => Assert.Null(s.Level));
        sim.Tick();
        foreach (var spawner in spawners)
        {
            Assert.NotEmpty(spawner.Alive);
            Assert.All(spawner.Alive, e => Assert.Equal(level, sim.World.Get<Stats>(e)!.Level));
        }
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(0, -1)]
    [InlineData(3, 2)]
    public void GeneratedCombatTargetsExistNearbyAndRespectQuestLevel(int x, int y)
    {
        var content = CoreContent.Registry;
        var generator = new WorldGenerator(content, Rng.Hash64("aureth"));
        var zone = generator.Zone(new ZoneCoord(x, y));
        var around = generator.PoisAround(zone.Coord).ToDictionary(p => p.Id);
        foreach (var quest in zone.Quests)
        {
            var giver = around[quest.Giver!];
            foreach (var objective in quest.Objectives)
            {
                if (objective.Poi is { } poiId && around.TryGetValue(poiId, out var poi))
                    Assert.True(poi.Level <= quest.Level + 2, $"{quest.Id} targets level {poi.Level}");
                if (objective.Type != "kill") continue;
                const double reach = 160;
                var first = generator.Layout.ChunkOf(giver.X - reach, giver.Y - reach);
                var last = generator.Layout.ChunkOf(giver.X + reach, giver.Y + reach);
                var found = false;
                for (var cy = first.Y; cy <= last.Y && !found; cy++)
                for (var cx = first.X; cx <= last.X && !found; cx++)
                    found = generator.GenerateChunk(new ChunkCoord(cx, cy)).Spawns.Any(s =>
                        s.Pattern == objective.Target
                        && Vector2.Distance(new((float)giver.X, (float)giver.Y), new(s.X, s.Y)) <= reach
                        && (s.Level ?? content.Require<PatternDef>(s.Pattern).Level) <= quest.Level + 2);
                Assert.True(found, $"{quest.Id} selected absent or overleveled {objective.Target}");
            }
        }
        if (x == 0 && y == 0)
        {
            Assert.Contains(zone.Quests, q => q.Id.Contains("cull_beasts", StringComparison.Ordinal));
            Assert.DoesNotContain(zone.Quests.SelectMany(q => q.Objectives), o => o.Target == "barrow_hound");
        }
    }
}
