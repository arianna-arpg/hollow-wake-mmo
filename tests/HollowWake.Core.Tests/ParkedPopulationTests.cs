using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Maths;
using HollowWake.Core.World;
using HollowWake.Core.WorldGen;
using Xunit;

namespace HollowWake.Core.Tests;

// Opt in only in authoring tests. Play.cmd and the shipped server load content/core alone.
internal static class ParkedPopulationContent
{
    private static readonly Lazy<ContentRegistry> Content = new(Create);
    public static ContentRegistry Registry => Content.Value;

    public static ContentRegistry Create()
    {
        var core = CoreContent.FindContentDir();
        var parked = Path.GetFullPath(Path.Combine(core, "..", "..", "experiments", "parked-populations", "content"));
        var registry = ContentRegistry.Load(new[] { core, parked }, out var report, strict: true);
        Assert.Empty(report.Issues);
        return registry;
    }
}

public class ParkedPopulationTests
{
    [Fact]
    public void PlaytestDoesNotLoadParkedSitesOrWildlifeTables()
    {
        var live = CoreContent.Registry;
        var parked = ParkedPopulationContent.Registry;
        var sites = parked.Each<PoiDef>().Where(p => p.Tags.Has("orc_warbands")).ToArray();
        var tables = parked.Each<SpawnTableDef>().Where(t => t.Id.StartsWith("fauna_", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(sites);
        Assert.NotEmpty(tables);
        Assert.All(sites, p => Assert.False(live.Has<PoiDef>(p.Id)));
        Assert.All(tables, t => Assert.False(live.Has<SpawnTableDef>(t.Id)));
        Assert.DoesNotContain(live.Each<BiomeDef>().SelectMany(b => b.SpawnTables),
            id => tables.Any(t => t.Id == id));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(0, -1)]
    [InlineData(3, 2)]
    public void ParkedBodiesCannotBecomePlaytestSpawnsOrQuestTargets(int x, int y)
    {
        var content = CoreContent.Registry;
        // These three wildlife bodies already existed on main before the population expansion.
        var existing = new HashSet<string> { "wolf", "boar", "grove_stag" };
        var parked = content.Each<PatternDef>()
            .Where(p => p.Extends == "orc_body" || p.Extends == "fauna_body" && !existing.Contains(p.Id))
            .Select(p => p.Id).ToHashSet();
        parked.UnionWith(new[] { "reed_gnasher", "thornback_gnasher", "great_gnasher", "saddle_gnasher", "ironjaw_gnasher", "brood_gnasher" });
        Assert.NotEmpty(parked);
        var generator = new WorldGenerator(content, Rng.Hash64("aureth"));
        var zone = new ZoneCoord(x, y);
        Assert.DoesNotContain(generator.Layout.ChunksOf(zone).SelectMany(c => generator.GenerateChunk(c).Spawns),
            spawn => parked.Contains(spawn.Pattern));
        Assert.DoesNotContain(generator.Zone(zone).Quests.SelectMany(q => q.Objectives),
            objective => parked.Contains(objective.Target ?? ""));
    }
}
