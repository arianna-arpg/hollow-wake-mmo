using System.Numerics;
using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Maths;
using HollowWake.Core.Movement;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;
using HollowWake.Core.World;
using HollowWake.Core.WorldGen;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>
/// The planner respects the cells placed things block (trees, veins), as the movement system does, while
/// keeping the allocation-free search and the reference paths wherever nothing is placed.
/// </summary>
public class PlacedBlockerPlanningTests
{
    private static Terrain Flat() => new(WorldLayout.Default, new FlatChunkProvider(WorldLayout.Default.ChunkSize, 0, TagSet.Of("ground")));

    private static Pathfinder Planner(Terrain terrain, BlockedCells? blocked, int maxNodes = 4000) => new(terrain)
    {
        StepUp = 0.6,
        MaxDrop = 3,
        MaxNodes = maxNodes,
        Blocked = blocked is null ? null : blocked.IsBlocked,
    };

    private static bool Touches(IEnumerable<Vector3> path, BlockedCells blocked) => path.Any(p => blocked.IsBlocked(CellCoord.Of(p.X, p.Y)));

    [Fact]
    public void PathsGoAroundPlacedBlockers()
    {
        var terrain = Flat();
        var blocked = new BlockedCells();
        var tree = new Entity(1, 1);
        for (var y = 2; y <= 18; y++)
        {
            blocked.Block(new CellCoord(10, y), tree);
        }
        var into = new List<Vector3>();
        var result = Planner(terrain, blocked).FindPath(new Vector3(5.5f, 10.5f, 0), new Vector3(15.5f, 10.5f, 0), into, 4000);
        Assert.Equal(PathResult.Complete, result);
        Assert.False(Touches(into, blocked), string.Join(" ", into));
        // The same search without the index walks straight through the row.
        Planner(terrain, null).FindPath(new Vector3(5.5f, 10.5f, 0), new Vector3(15.5f, 10.5f, 0), into, 4000);
        Assert.True(Touches(into, blocked));
    }

    [Fact]
    public void DiagonalStepsDoNotCutPastAPlacedBlocker()
    {
        var terrain = Flat();
        var blocked = new BlockedCells();
        blocked.Block(new CellCoord(6, 5), new Entity(1, 1));
        var into = new List<Vector3>();
        Assert.Equal(PathResult.Complete, Planner(terrain, blocked).FindPath(new Vector3(5.5f, 5.5f, 0), new Vector3(6.5f, 6.5f, 0), into, 100));
        // Around the corner through (5, 6), not diagonally past the blocked cell.
        Assert.Equal(new[] { new Vector3(5.5f, 6.5f, 0), new Vector3(6.5f, 6.5f, 0) }, into);
        Assert.Equal(PathResult.Complete, Planner(terrain, null).FindPath(new Vector3(5.5f, 5.5f, 0), new Vector3(6.5f, 6.5f, 0), into, 100));
        Assert.Single(into);
    }

    [Fact]
    public void AnUnblockedIndexKeepsTheSamePathsOnGeneratedLand()
    {
        var generator = new WorldGenerator(CoreContent.Registry, Rng.Hash64("aureth"));
        var terrain = new Terrain(generator.Layout, new GeneratedChunkProvider(generator));
        var origin = generator.Layout.ZoneOrigin(generator.StartZone());
        var plain = Planner(terrain, null);
        var indexed = Planner(terrain, new BlockedCells());
        var rng = new Rng(41);
        var a = new List<Vector3>();
        var b = new List<Vector3>();
        for (var i = 0; i < 60; i++)
        {
            var fx = origin.X + 8 + rng.NextDouble() * (terrain.Layout.ZoneSize - 16);
            var fy = origin.Y + 8 + rng.NextDouble() * (terrain.Layout.ZoneSize - 16);
            var from = new Vector3((float)fx, (float)fy, terrain.HeightAt(fx, fy));
            var to = new Vector3((float)(fx + rng.Range(-25.0, 25.0)), (float)(fy + rng.Range(-25.0, 25.0)), 0);
            to.Z = terrain.HeightAt(to.X, to.Y);
            var budget = i % 2 == 0 ? 4000 : 120;
            Assert.Equal(plain.FindPath(from, to, a, budget), indexed.FindPath(from, to, b, budget));
            Assert.Equal(a, b);
        }
    }

    [Fact]
    public void RepeatedSearchesThroughAGroveDoNotAllocate()
    {
        var terrain = Flat();
        var blocked = new BlockedCells();
        var rng = new Rng(8);
        for (var i = 0; i < 120; i++)
        {
            blocked.Block(new CellCoord(rng.Range(2, 40), rng.Range(2, 40)), new Entity(i, 1));
        }
        var planner = Planner(terrain, blocked);
        var into = new List<Vector3>(256);
        var from = new Vector3(1.5f, 1.5f, 0);
        var to = new Vector3(41.5f, 40.5f, 0);
        planner.FindPath(from, to, into, 4000);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 20; i++)
        {
            planner.FindPath(from, to, into, 4000);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.False(Touches(into, blocked));
    }

    [Fact]
    public void AZonePlannerKnowsThePlacedProps()
    {
        var sim = ZoneBuilder.CreateArena(CoreContent.Registry, 5);
        var factory = sim.Service<PatternFactory>();
        for (var y = 4; y <= 16; y++)
        {
            factory.Spawn(sim, "oak", new Vector3(12.5f, y + 0.5f, 0));
        }
        var blocked = sim.Service<BlockedCells>();
        Assert.True(blocked.IsBlocked(new CellCoord(12, 10)));
        var into = new List<Vector3>();
        Assert.Equal(PathResult.Complete, sim.Service<Pathfinder>().FindPath(new Vector3(8.5f, 10.5f, 0), new Vector3(16.5f, 10.5f, 0), into, 4000));
        Assert.False(Touches(into, blocked), string.Join(" ", into));
    }

    [Fact]
    public void AChaserReachesItsPreyAroundARowOfTreesWithoutEnteringOne()
    {
        var sim = ZoneBuilder.CreateArena(CoreContent.Registry, 3);
        var factory = sim.Service<PatternFactory>();
        for (var y = 3; y <= 17; y++)
        {
            factory.Spawn(sim, "oak", new Vector3(14.5f, y + 0.5f, 0));
        }
        var wolf = factory.Spawn(sim, "wolf", new Vector3(10.5f, 10.5f, 0), new SpawnOptions { Faction = "red" });
        var boar = factory.Spawn(sim, "boar", new Vector3(18.5f, 10.5f, 0), new SpawnOptions { Faction = "blue" });
        sim.World.Remove<Behavior>(boar);
        var blocked = sim.Service<BlockedCells>();
        var hit = false;
        sim.Events.Subscribe<DamageDealt>(d => hit |= d.Source == wolf && d.Target == boar);
        var t = sim.World.Get<Transform>(wolf)!;
        var entered = false;
        for (var i = 0; i < 20 * 15 && !hit; i++)
        {
            sim.Tick();
            entered |= blocked.IsBlocked(CellCoord.Of(t.Position.X, t.Position.Y));
        }
        Assert.True(hit, "the wolf never got around the trees");
        Assert.False(entered, "the wolf stood inside a tree");
    }
}
