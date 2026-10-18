using System.Text.Json.Nodes;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Maths;
using HollowWake.Core.Tags;
using HollowWake.Core.World;
using HollowWake.Core.WorldGen;
using Xunit;

namespace HollowWake.Core.Tests;

public class LocalPopulationSearchTests
{
    [Theory]
    [InlineData(0, 1, 160, false)]
    [InlineData(1, 1, 160, false)]
    [InlineData(64, 1, 160, true)]
    [InlineData(64, 9, 160, false)]
    [InlineData(64, 1, 40, false)]
    public void BudgetLimitsDistantOffersWithoutWeakeningSpawnLevelOrRadius(int chunks, int siteLevel, double reach, bool offered)
    {
        var (generator, planner, giver) = Scenario(chunks, siteLevel, reach);
        // The resident exists even when budget/radius/level makes it unavailable to this offer.
        // Its pattern level is deliberately high; an eligible site's effective level is authoritative.
        var spawn = Assert.Single(generator.GenerateChunk(new ChunkCoord(3, 2)).Spawns, s => s.Pattern == "budget_target");
        Assert.Equal(siteLevel, spawn.Level);
        var quests = planner.GenerateQuests(giver.Zone, new[] { giver });
        Assert.Equal(offered, quests.Count == 1);
        if (offered) Assert.Equal("budget_target", Assert.Single(Assert.Single(quests).Objectives).Target);
    }

    [Fact]
    public void CachedDistantResidentCannotExtendAnotherSlotsBudget()
    {
        var (_, planner, giver) = Scenario(1, 1, 160);
        Assert.Empty(planner.GenerateQuests(giver.Zone, new[] { giver }));
        // Another giver fills the cache with the distant resident's chunk.
        var near = giver with { Id = "near", X = 110.5, Y = 80.5 };
        Assert.Single(planner.GenerateQuests(near.Zone, new[] { near }));
        Assert.Empty(planner.GenerateQuests(giver.Zone, new[] { giver }));
    }

    [Theory]
    [InlineData(0, 1, false)]
    [InlineData(1, 1, true)]
    [InlineData(1, 9, false)]
    public void ResolvedPoiAnchorsTheSameBoundedSearchAndRetainsLevelEligibility(int chunks, int siteLevel, bool offered)
    {
        var (generator, planner, giver) = Scenario(chunks, siteLevel, 160);
        var content = generator.Content;
        content.With(content.Require<PoiDef>("budget_site") with { Tags = TagSet.Of("budget_site") });
        var quest = content.Require<QuestTemplateDef>("budget_quest");
        content.With(quest with
        {
            Slots = new()
            {
                ["site"] = new QuestSlot { PoiTags = TagSet.Of("budget_site"), Within = 160 },
                ["target"] = quest.Slots["target"] with { PopulationAt = "$site" },
            },
        });
        var quests = planner.GenerateQuests(giver.Zone, new[] { giver });
        Assert.Equal(offered, quests.Count == 1);
        if (offered) Assert.Equal("budget_target", Assert.Single(Assert.Single(quests).Objectives).Target);
    }

    [Fact]
    public void InvalidProgrammaticAnchorDoesNotFallBackToTheGiver()
    {
        var (generator, planner, giver) = Scenario(64, 1, 160);
        var quest = generator.Content.Require<QuestTemplateDef>("budget_quest");
        generator.Content.With(quest with { Slots = new() { ["target"] = quest.Slots["target"] with { PopulationAt = "$missing" } } });
        Assert.Empty(planner.GenerateQuests(giver.Zone, new[] { giver }));
    }

    private static (WorldGenerator Generator, ZonePlanner Planner, PoiInstance Giver) Scenario(int chunks, int siteLevel, double reach)
    {
        var content = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out _);
        var values = (JsonObject)content.Require<RulesDef>("world").Values!.DeepClone();
        values["gen"]!["quest_population_chunks"] = chunks;
        content.With(new RulesDef { Id = "world", Values = values });
        foreach (var poi in content.Each<PoiDef>().ToArray()) content.With(poi with { Weight = 0 });
        content.With(content.Require<PatternDef>("skeleton_warrior") with { Id = "budget_target", Level = 30, Tags = TagSet.Of("budget_target") });
        content.With(new PoiDef
        {
            Id = "budget_site", Weight = 0, Level = siteLevel - 1,
            Inhabitants = { new SpawnSpec { Pattern = "budget_target", UseSiteLevel = true } },
        });
        content.With(new RegionOverrideDef { Id = "hallows_home", X = 0, Y = 0, Archetype = "hallows", Level = 1 });
        content.With(new ZoneOverrideDef
        {
            Id = "ninewells", X = 0, Y = 0,
            Pois = { new PoiPlacement { Poi = "budget_site", X = 110, Y = 80 } },
        });
        content.With(new QuestTemplateDef
        {
            Id = "budget_quest",
            Slots = { ["target"] = new QuestSlot { PatternTags = TagSet.Of("budget_target"), LocalPopulation = true, Within = reach, LevelDelta = new Window { Max = 2 } } },
            Objectives = { new ObjectiveDef { Id = "hunt", Type = "kill", Target = "$target" } },
        });
        var generator = new WorldGenerator(content, Rng.Hash64("aureth"));
        var giver = new PoiInstance("giver", new PoiDef { Id = "giver", Quests = { "budget_quest" } }, "Giver", new ZoneCoord(0, 0), 16.5, 80.5, 0, null, null, 1);
        return (generator, new ZonePlanner(generator), giver);
    }
}
