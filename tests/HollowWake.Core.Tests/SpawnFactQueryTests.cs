using HollowWake.Core.Maths;
using HollowWake.Core.World;
using HollowWake.Core.WorldGen;
using Xunit;

namespace HollowWake.Core.Tests;

public class SpawnFactQueryTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(0, -1)]
    [InlineData(3, 2)]
    public void PopulationFactsMatchFullChunksWithAndAfterOverlays(int x, int y)
    {
        var overlays = new GenerationOverlays();
        overlays.Add(new GenerationOverlay { Id = "season", Global = true, Params = { ["temperature_offset"] = -0.08, ["elevation_offset"] = 0.75 } });
        overlays.Add(new GenerationOverlay { Id = "flood", Zones = { new ZoneCoord(1, 0) }, Priority = 1, Params = { ["water_level"] = 2.5, ["thread.void"] = 0.35 } });
        var generator = new WorldGenerator(CoreContent.Registry, Rng.Hash64("aureth"), overlays);
        for (var stage = 0; stage < 2; stage++)
        {
            foreach (var chunk in generator.Layout.ChunksOf(new ZoneCoord(x, y)))
            {
                // Query first so normal generation cannot prime any query-only state.
                var facts = generator.GenerateSpawnFacts(chunk);
                Assert.Equal(generator.GenerateChunk(chunk).Spawns.ToArray(), facts);
                foreach (var pattern in facts.Select(s => s.Pattern).Distinct())
                    Assert.True(generator.CouldContainPopulation(chunk, new HashSet<string> { pattern }), $"False rejection: {chunk} / {pattern}");
                Assert.False(generator.CouldContainPopulation(chunk, new HashSet<string> { "absent_population_fixture" }));
            }
            overlays.Remove("season");
            overlays.Remove("flood");
        }
    }
}
