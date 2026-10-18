using HollowWake.Core.Ecs;
using HollowWake.Core.Territory;
using HollowWake.Core.Components;
using HollowWake.Core.Combat;
using HollowWake.Core.Sim;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Maths;
using HollowWake.Core.Tags;
using HollowWake.Core.World;
using HollowWake.Core.WorldGen;
using Xunit;

namespace HollowWake.Core.Tests;

public class QuestPopulationAnchorTests
{
    [Fact]
    public void StarterKnotOfferTargetsItsSiteScaledResidents()
    {
        var content = CoreContent.Registry;
        var generator = new WorldGenerator(content, Rng.Hash64("aureth"));
        var zone = generator.Zone(new ZoneCoord(0, 0));
        var quest = Assert.Single(zone.Quests, q => q.Id.Contains(".clear_knot.", StringComparison.Ordinal));
        var knotId = Assert.Single(quest.Objectives, o => o.Type == "reach").Poi;
        Assert.Equal("poi.0_0.knot.200.60", knotId);
        var target = Assert.Single(quest.Objectives, o => o.Type == "kill").Target;
        var residents = generator.Layout.ChunksOf(zone.Coord).SelectMany(c => generator.GenerateChunk(c).Spawns)
            .Where(s => s.Group == knotId && s.Pattern == target).ToArray();
        Assert.NotEmpty(residents);
        var knot = Assert.Single(zone.Pois, p => p.Id == knotId);
        Assert.Equal(3, knot.Level);
        Assert.All(residents, s => Assert.Equal(knot.Level, s.Level));
        Assert.Equal(4, content.Require<PatternDef>("cinder_imp").Level);
        Assert.Equal(6, content.Require<PatternDef>("tangle").Level);
        Assert.All(residents, s => Assert.InRange(s.Level!.Value - quest.Level, 0, 2));
        var foe = content.Require<QuestTemplateDef>("clear_knot").Slots["foe"];
        Assert.Equal("$knot", foe.PopulationAt);
        Assert.True(foe.LocalPopulation);
        Assert.Equal(2, foe.LevelDelta!.Max);
    }

    [Theory]
    [InlineData("cinder_imp")]
    [InlineData("remnant_shade")]
    [InlineData("tangle")]
    public void StarterKnotInitialAndRespawnedResidentsUseTheSitesLevel(string pattern)
    {
        var content = CoreContent.Registry;
        var generator = new WorldGenerator(content, Rng.Hash64("aureth"));
        var coord = new ZoneCoord(0, 0);
        var knot = Assert.Single(generator.Zone(coord).Pois, p => p.Id == "poi.0_0.knot.200.60");
        var sim = ZoneBuilder.CreateZone(content, generator, new WorldState(new GenerationOverlays()), coord);
        ZoneBuilder.LoadZone(sim);
        var spawners = sim.World.Query<Spawner>().Select(s => s.C1)
            .Where(s => s.Group == knot.Id && s.Pattern == pattern).ToArray();
        Assert.NotEmpty(spawners);
        Assert.All(spawners, s => Assert.Equal(knot.Level, s.Level));
        sim.Tick();
        foreach (var spawner in spawners)
        {
            Assert.NotEmpty(spawner.Alive);
            foreach (var resident in spawner.Alive.ToArray())
            {
                Assert.Equal(knot.Level, sim.World.Get<Stats>(resident)!.Level);
                sim.Service<DeathService>().Kill(sim, resident, Entity.None);
            }
            spawner.RespawnAt = sim.Clock.Time;
        }
        sim.Tick();
        Assert.All(spawners, s =>
        {
            Assert.NotEmpty(s.Alive);
            Assert.All(s.Alive, resident => Assert.Equal(knot.Level, sim.World.Get<Stats>(resident)!.Level));
        });
    }

    [Theory]
    [InlineData("$site", true, false)]
    [InlineData("$giver", true, false)]
    [InlineData("$missing", true, true)]
    [InlineData("site", true, true)]
    [InlineData("$later", true, true)]
    [InlineData("$item", true, true)]
    [InlineData("$target", true, true)]
    [InlineData("$site", false, true)]
    public void AnchorsRequireAnEarlierPoiAndALocalPopulationSlot(string anchor, bool local, bool invalid)
    {
        var template = new QuestTemplateDef
        {
            Id = "anchor_validation",
            Slots =
            {
                ["site"] = new QuestSlot { PoiTags = TagSet.Of("knot") },
                ["item"] = new QuestSlot { ItemTags = TagSet.Of("floss") },
                ["target"] = new QuestSlot { PatternTags = TagSet.Of("tangled"), LocalPopulation = local, PopulationAt = anchor },
                ["later"] = new QuestSlot { PoiTags = TagSet.Of("ruin") },
            },
            Objectives = { new ObjectiveDef { Id = "hunt", Type = "kill", Target = "$target" } },
        };
        var report = new ValidationReport();
        Assert.True(ContentKinds.TryGet("quest_templates", out var kind));
        kind!.Validate!(template, new ValidationContext(CoreContent.Registry, report, "quest_templates", template.Id, null));
        Assert.Equal(invalid, report.HasErrors);
        Assert.All(report.Errors, error => Assert.Contains("population_at", error.Message));
    }
}
