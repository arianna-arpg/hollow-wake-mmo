using System.Numerics;
using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Maths;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;
using HollowWake.Core.World;
using HollowWake.Core.WorldGen;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>
/// The terrain queries and the pathfinder were rewritten to avoid allocation. These tests hold them to
/// the original algorithms, kept here verbatim as references, over generated land and over synthetic
/// stacked surfaces with every edge case of layers.
/// </summary>
public class NavigationEquivalenceTests
{
    private static readonly Lazy<(Terrain Terrain, CellCoord Origin)> StartZone = new(() =>
    {
        var generator = new WorldGenerator(CoreContent.Registry, Rng.Hash64("aureth"));
        var terrain = new Terrain(generator.Layout, new GeneratedChunkProvider(generator));
        var origin = generator.Layout.ZoneOrigin(generator.StartZone());
        var first = generator.Layout.ChunkOf(origin);
        for (var cy = -1; cy <= generator.Layout.ZoneChunks; cy++)
        {
            for (var cx = -1; cx <= generator.Layout.ZoneChunks; cx++)
            {
                terrain.GetChunk(new ChunkCoord(first.X + cx, first.Y + cy));
            }
        }
        return (terrain, origin);
    });

    [Fact]
    public void TerrainQueriesMatchTheReferenceOnStackedSurfaces()
    {
        foreach (var size in new[] { 32, 7 })
        {
            var layout = new WorldLayout { ChunkSize = size };
            var terrain = new Terrain(layout, new StackedChunkProvider(size));
            var reference = new ReferenceTerrain(terrain);
            var rng = new Rng(17);
            for (var i = 0; i < 40_000; i++)
            {
                var x = rng.Range(-40.0, 40.0);
                var y = rng.Range(-40.0, 40.0);
                var z = rng.Range(-5.0, 12.0);
                var stepUp = new[] { 0, 0.6, 1.2, 3 }[rng.Next(4)];
                CompareAt(terrain, reference, x, y, z, stepUp);
            }
        }
    }

    [Fact]
    public void TerrainQueriesMatchTheReferenceOnGeneratedLand()
    {
        var (terrain, origin0) = StartZone.Value;
        var reference = new ReferenceTerrain(terrain);
        var rng = new Rng(23);
        // Every cell with layers (bridges, floors) several times when the zone has any, then random land.
        // Stacked surfaces of every kind are covered by the synthetic land above.
        var layered = new List<(double X, double Y)>();
        foreach (var chunk in terrain.LoadedChunks)
        {
            var origin = terrain.Layout.ChunkOrigin(chunk.Coord);
            foreach (var index in chunk.Layers.Keys)
            {
                layered.Add((origin.X + index % chunk.Size, origin.Y + index / chunk.Size));
            }
        }
        foreach (var (cx, cy) in layered)
        {
            for (var k = 0; k < 6; k++)
            {
                CompareAt(terrain, reference, cx + rng.NextDouble(), cy + rng.NextDouble(), rng.Range(-5.0, 30.0), new[] { 0.6, 1.2, 3 }[rng.Next(3)]);
            }
        }
        for (var i = 0; i < 20_000; i++)
        {
            var x = origin0.X - 16 + rng.NextDouble() * (terrain.Layout.ZoneSize + 32);
            var y = origin0.Y - 16 + rng.NextDouble() * (terrain.Layout.ZoneSize + 32);
            var z = reference.HeightAt(x, y) + rng.Range(-1.0, 2.0);
            CompareAt(terrain, reference, x, y, z, 0.6);
        }
    }

    [Fact]
    public void PathsMatchTheReferenceSearch()
    {
        var (terrain, origin) = StartZone.Value;
        var reference = new ReferenceTerrain(terrain);
        Func<CellCoord, double> cost = cell => terrain.TagsAt(cell).Has("road") ? 0.6 : terrain.TagsAt(cell).Has("bog") ? 2.5 : 1.0;
        var pathfinder = new Pathfinder(terrain) { StepUp = 0.6, MaxDrop = 3, MaxNodes = 4000, CellCost = cost };
        var rng = new Rng(5);
        var into = new List<Vector3>();
        var outcomes = new Dictionary<PathResult, int>();
        foreach (var (budget, count) in new[] { (4000, 40), (500, 120), (40, 120) })
        {
            for (var i = 0; i < count; i++)
            {
                var fx = origin.X + 8 + rng.NextDouble() * (terrain.Layout.ZoneSize - 16);
                var fy = origin.Y + 8 + rng.NextDouble() * (terrain.Layout.ZoneSize - 16);
                var tx = fx + rng.Range(-30.0, 30.0);
                var ty = fy + rng.Range(-30.0, 30.0);
                var from = new Vector3((float)fx, (float)fy, reference.HeightAt(fx, fy));
                var to = new Vector3((float)tx, (float)ty, reference.HeightAt(tx, ty));
                var expected = ReferencePath(reference, from, to, 0.6, 3, budget, cost);
                var result = pathfinder.FindPath(from, to, into, budget);
                Assert.Equal(expected, into);
                var kind = expected.Count == 0 ? PathResult.None : expected[^1].X == to.X && expected[^1].Y == to.Y ? PathResult.Complete : PathResult.Partial;
                Assert.Equal(kind, result);
                outcomes[result] = outcomes.GetValueOrDefault(result) + 1;
                if (budget == 4000)
                {
                    Assert.Equal(expected, pathfinder.FindPath(from, to));
                }
            }
        }
        // The sample exercises complete and partial searches, not one kind only.
        Assert.True(outcomes.GetValueOrDefault(PathResult.Complete) > 20, string.Join(", ", outcomes));
        Assert.True(outcomes.GetValueOrDefault(PathResult.Partial) > 20, string.Join(", ", outcomes));
    }

    [Fact]
    public void PathsMatchTheReferenceOverStackedSurfaces()
    {
        var terrain = new Terrain(WorldLayout.Default, new StackedChunkProvider(32));
        var reference = new ReferenceTerrain(terrain);
        Func<CellCoord, double> cost = cell => terrain.TagsAt(cell).Has("road") ? 0.6 : 1.0;
        var pathfinder = new Pathfinder(terrain) { StepUp = 1.2, MaxDrop = 3, MaxNodes = 2000, CellCost = cost };
        var rng = new Rng(9);
        var into = new List<Vector3>();
        for (var i = 0; i < 150; i++)
        {
            var from = new Vector3((float)rng.Range(-30.0, 30.0), (float)rng.Range(-30.0, 30.0), (float)rng.Range(-2.0, 8.0));
            var to = new Vector3((float)rng.Range(-30.0, 30.0), (float)rng.Range(-30.0, 30.0), 0);
            var budget = rng.Next(2) == 0 ? 2000 : 80;
            var expected = ReferencePath(reference, from, to, 1.2, 3, budget, cost);
            pathfinder.FindPath(from, to, into, budget);
            Assert.Equal(expected, into);
        }
    }

    [Fact]
    public void RepeatedSearchesDoNotAllocate()
    {
        var (terrain, origin) = StartZone.Value;
        var reference = new ReferenceTerrain(terrain);
        Func<CellCoord, double> cost = cell => terrain.TagsAt(cell).Has("road") ? 0.6 : 1.0;
        var pathfinder = new Pathfinder(terrain) { CellCost = cost };
        var from = new Vector3(origin.X + 100.3f, origin.Y + 100.6f, 0);
        from.Z = reference.HeightAt(from.X, from.Y);
        var to = new Vector3(origin.X + 124.2f, origin.Y + 87.9f, 0);
        var into = new List<Vector3>(256);
        pathfinder.FindPath(from, to, into, 4000);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 20; i++)
        {
            pathfinder.FindPath(from, to, into, 4000);
            terrain.TryFindGround(from.X, from.Y, from.Z, 0.6, out _, out _);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static void CompareAt(Terrain terrain, ReferenceTerrain reference, double x, double y, double z, double stepUp)
    {
        Assert.Equal(reference.HeightAt(x, y), terrain.HeightAt(x, y));
        var expected = reference.TryFindGround(x, y, z, stepUp, out var g1, out var a1);
        var actual = terrain.TryFindGround(x, y, z, stepUp, out var g2, out var a2, out var flags);
        Assert.Equal(expected, actual);
        Assert.Equal(g1, g2);
        Assert.Equal(a1, a2);
        var cell = CellCoord.Of(x, y);
        Assert.Equal(reference.FlagsAt(cell), flags);
        Assert.Equal(reference.FlagsAt(cell), terrain.FlagsAt(cell));
        Assert.Equal(reference.GroundHeight(cell), terrain.GroundHeight(cell));
        Assert.Equal(reference.TagsAt(cell), terrain.TagsAt(cell));
        foreach (var (dx, dy) in new[] { (1, 0), (0, -1), (-1, 1) })
        {
            var next = cell.Offset(dx, dy);
            Assert.Equal(reference.CanStep(cell, next, z, stepUp, 3), terrain.CanStep(cell, next, z, stepUp, 3));
        }
    }

    /// <summary>The terrain queries as they were first written: located through the layout, surfaces through the chunk's iterator.</summary>
    private sealed class ReferenceTerrain
    {
        private readonly Terrain _terrain;

        public ReferenceTerrain(Terrain terrain)
        {
            _terrain = terrain;
        }

        private (ChunkData Chunk, int Index) Locate(CellCoord cell) => (_terrain.GetChunk(_terrain.Layout.ChunkOf(cell)), _terrain.Layout.LocalIndex(cell));

        public float GroundHeight(CellCoord cell)
        {
            var (chunk, i) = Locate(cell);
            return chunk.Height[i];
        }

        public CellFlags FlagsAt(CellCoord cell)
        {
            var (chunk, i) = Locate(cell);
            return chunk.Flags[i];
        }

        public TagSet TagsAt(CellCoord cell)
        {
            var (chunk, i) = Locate(cell);
            return chunk.TagsAt(i);
        }

        public float HeightAt(double x, double y)
        {
            var fx = x - 0.5;
            var fy = y - 0.5;
            var x0 = (int)Math.Floor(fx);
            var y0 = (int)Math.Floor(fy);
            var tx = (float)(fx - x0);
            var ty = (float)(fy - y0);
            var h00 = GroundHeight(new CellCoord(x0, y0));
            var h10 = GroundHeight(new CellCoord(x0 + 1, y0));
            var h01 = GroundHeight(new CellCoord(x0, y0 + 1));
            var h11 = GroundHeight(new CellCoord(x0 + 1, y0 + 1));
            var a = h00 + (h10 - h00) * tx;
            var b = h01 + (h11 - h01) * tx;
            return a + (b - a) * ty;
        }

        public bool TryFindGround(double x, double y, double z, double stepUp, out Surface ground, out bool airborne)
        {
            var cell = CellCoord.Of(x, y);
            Surface? best = null;
            var smoothGround = HeightAt(x, y);
            var (chunk, index) = Locate(cell);
            foreach (var s in chunk.SurfacesAt(index))
            {
                var h = s.Tags.Has("ground") || !IsLayer(s, cell) ? smoothGround : s.Height;
                var candidate = new Surface(h, s.Walkable, s.Tags);
                if (!candidate.Walkable)
                {
                    continue;
                }
                if (candidate.Height <= z + stepUp)
                {
                    if (best is null || candidate.Height > best.Value.Height)
                    {
                        best = candidate;
                    }
                }
            }
            if (best is null)
            {
                ground = default;
                airborne = true;
                return false;
            }
            ground = best.Value;
            airborne = z > ground.Height + 0.001;
            return true;
        }

        private bool IsLayer(Surface s, CellCoord cell)
        {
            var (chunk, i) = Locate(cell);
            return chunk.Layers.TryGetValue(i, out var layers) && layers.Contains(s);
        }

        public bool CanStep(CellCoord from, CellCoord to, double z, double stepUp, double maxDrop)
        {
            if (!TryFindGround(to.X + 0.5, to.Y + 0.5, z, stepUp, out var ground, out _))
            {
                return false;
            }
            var (chunk, i) = Locate(to);
            if ((chunk.Flags[i] & CellFlags.Blocked) != 0 && ground.Tags.Has("ground"))
            {
                return false;
            }
            return z - ground.Height <= maxDrop;
        }
    }

    private static readonly (int dx, int dy, double cost)[] Neighbors =
    {
        (1, 0, 1), (-1, 0, 1), (0, 1, 1), (0, -1, 1),
        (1, 1, 1.4142), (-1, 1, 1.4142), (1, -1, 1.4142), (-1, -1, 1.4142),
    };

    /// <summary>The A* search as it was first written.</summary>
    private static List<Vector3> ReferencePath(ReferenceTerrain terrain, Vector3 from, Vector3 to, double stepUp, double maxDrop, int maxNodes, Func<CellCoord, double> cellCost)
    {
        var start = CellCoord.Of(from.X, from.Y);
        var goal = CellCoord.Of(to.X, to.Y);
        if (start == goal)
        {
            return new List<Vector3> { to };
        }
        var open = new PriorityQueue<(CellCoord cell, double z), double>();
        var gScore = new Dictionary<CellCoord, double> { [start] = 0 };
        var cameFrom = new Dictionary<CellCoord, CellCoord>();
        var heightAt = new Dictionary<CellCoord, double> { [start] = from.Z };
        open.Enqueue((start, from.Z), Heuristic(start, goal));
        var closed = new HashSet<CellCoord>();
        var best = start;
        var bestH = Heuristic(start, goal);
        var expanded = 0;
        while (open.Count > 0 && expanded < maxNodes)
        {
            var (current, z) = open.Dequeue();
            if (!closed.Add(current))
            {
                continue;
            }
            expanded++;
            var h = Heuristic(current, goal);
            if (h < bestH)
            {
                bestH = h;
                best = current;
            }
            if (current == goal)
            {
                return Reconstruct(cameFrom, current, heightAt, to);
            }
            foreach (var (dx, dy, stepCost) in Neighbors)
            {
                var next = current.Offset(dx, dy);
                if (closed.Contains(next))
                {
                    continue;
                }
                if (dx != 0 && dy != 0)
                {
                    if (!terrain.CanStep(current, current.Offset(dx, 0), z, stepUp, maxDrop) || !terrain.CanStep(current, current.Offset(0, dy), z, stepUp, maxDrop))
                    {
                        continue;
                    }
                }
                if (!terrain.TryFindGround(next.X + 0.5, next.Y + 0.5, z, stepUp, out var ground, out _))
                {
                    continue;
                }
                if (!ground.Walkable || z - ground.Height > maxDrop)
                {
                    continue;
                }
                if ((terrain.FlagsAt(next) & CellFlags.Blocked) != 0)
                {
                    continue;
                }
                var tentative = gScore[current] + stepCost * cellCost(next) + Math.Abs(ground.Height - z) * 0.5;
                if (!gScore.TryGetValue(next, out var known) || tentative < known)
                {
                    gScore[next] = tentative;
                    cameFrom[next] = current;
                    heightAt[next] = ground.Height;
                    open.Enqueue((next, ground.Height), tentative + Heuristic(next, goal));
                }
            }
        }
        return best == start ? new List<Vector3>() : Reconstruct(cameFrom, best, heightAt, null);
    }

    private static double Heuristic(CellCoord a, CellCoord b)
    {
        var dx = Math.Abs(a.X - b.X);
        var dy = Math.Abs(a.Y - b.Y);
        return Math.Max(dx, dy) + 0.4142 * Math.Min(dx, dy);
    }

    private static List<Vector3> Reconstruct(Dictionary<CellCoord, CellCoord> cameFrom, CellCoord end, Dictionary<CellCoord, double> heightAt, Vector3? exact)
    {
        var cells = new List<CellCoord> { end };
        while (cameFrom.TryGetValue(cells[^1], out var prev))
        {
            cells.Add(prev);
        }
        cells.Reverse();
        var path = new List<Vector3>(cells.Count);
        for (var i = 1; i < cells.Count; i++)
        {
            var c = cells[i];
            path.Add(new Vector3(c.X + 0.5f, c.Y + 0.5f, (float)heightAt[c]));
        }
        if (exact is { } e && path.Count > 0)
        {
            path[^1] = new Vector3(e.X, e.Y, path[^1].Z);
        }
        return path;
    }

    /// <summary>
    /// Rough land with every kind of stacked surface: decks tagged ground, flat floors, unwalkable layers,
    /// layers below the base, and layers identical to the base surface.
    /// </summary>
    private sealed class StackedChunkProvider : IChunkProvider
    {
        private readonly int _size;

        public StackedChunkProvider(int size)
        {
            _size = size;
        }

        public ChunkData Load(ChunkCoord coord)
        {
            var chunk = new ChunkData(coord, _size);
            var rng = new Rng(Rng.Combine((ulong)(coord.X + 1000), (ulong)(coord.Y + 5000)));
            var ground = chunk.Intern(TagSet.Of("ground"));
            var road = chunk.Intern(TagSet.Of("ground", "road"));
            var stone = chunk.Intern(TagSet.Of("stone"));
            for (var i = 0; i < chunk.Height.Length; i++)
            {
                chunk.Height[i] = (float)Math.Round(rng.Range(-2.0, 4.0), 1);
                var roll = rng.NextDouble();
                chunk.Flags[i] = roll < 0.72 ? CellFlags.Walkable : roll < 0.84 ? CellFlags.Walkable | CellFlags.Blocked : roll < 0.94 ? CellFlags.None : CellFlags.Walkable | CellFlags.Road;
                var tagRoll = rng.NextDouble();
                chunk.SurfaceTag[i] = tagRoll < 0.75 ? ground : tagRoll < 0.9 ? road : stone;
                if (rng.NextDouble() < 0.25)
                {
                    var layers = rng.Range(1, 3);
                    for (var l = 0; l < layers; l++)
                    {
                        if (rng.NextDouble() < 0.15)
                        {
                            chunk.AddLayer(i, new Surface(chunk.Height[i], chunk.IsWalkable(i), chunk.TagsAt(i)));
                        }
                        else
                        {
                            var height = (float)Math.Round(chunk.Height[i] + rng.Range(-3.0, 6.0), 1);
                            var tags = rng.NextDouble() < 0.3 ? TagSet.Of("ground", "deck") : TagSet.Of("structure", "floor");
                            chunk.AddLayer(i, new Surface(height, rng.NextDouble() < 0.8, tags));
                        }
                    }
                }
            }
            return chunk;
        }
    }
}

/// <summary>Monsters still close in and hit through the AI's planning shortcuts: straight walks, kept plans, budgets.</summary>
public class AiNavigationTests
{
    private static (Simulation Sim, Entity Hunter, Entity Prey) Arena(string hunter, Vector3 at, Vector3 preyAt, ulong seed = 3)
    {
        var sim = ZoneBuilder.CreateArena(CoreContent.Registry, seed);
        var factory = sim.Service<PatternFactory>();
        var h = factory.Spawn(sim, hunter, at, new SpawnOptions { Faction = "red" });
        var p = factory.Spawn(sim, "boar", preyAt, new SpawnOptions { Faction = "blue" });
        // The prey stands still so the test measures the hunter alone.
        sim.World.Remove<Behavior>(p);
        return (sim, h, p);
    }

    private static void Wall(Simulation sim, int x, int fromY, int toY)
    {
        for (var y = fromY; y <= toY; y++)
        {
            Block(sim, x, y);
        }
    }

    private static void Block(Simulation sim, int x, int y)
    {
        var (chunk, i) = sim.Terrain.Locate(new CellCoord(x, y));
        chunk.Flags[i] |= CellFlags.Blocked;
    }

    private static Func<bool> HitBy(Simulation sim, Entity source, Entity target)
    {
        var hit = false;
        sim.Events.Subscribe<DamageDealt>(d =>
        {
            if (d.Source == source && d.Target == target)
            {
                hit = true;
            }
        });
        return () => hit;
    }

    private static bool RunUntil(Simulation sim, double seconds, Func<bool> done)
    {
        for (var i = 0; i < seconds * sim.Clock.TicksPerSecond; i++)
        {
            sim.Tick();
            if (done())
            {
                return true;
            }
        }
        return false;
    }

    [Theory]
    [InlineData("wolf")]
    [InlineData("hob")]
    [InlineData("loosed_kindler")]
    public void MonstersInAnArenaReachAndHitATarget(string hunter)
    {
        var (sim, h, p) = Arena(hunter, new Vector3(10.2f, 10.4f, 0), new Vector3(18.7f, 12.1f, 0));
        var hit = HitBy(sim, h, p);
        Assert.True(RunUntil(sim, 10, hit), $"{hunter} never hit its target");
    }

    [Fact]
    public void MonstersPathAroundWallsToReachATarget()
    {
        var (sim, h, p) = Arena("wolf", new Vector3(10.5f, 10.5f, 0), new Vector3(18.5f, 10.5f, 0));
        Wall(sim, 14, 2, 18);
        var hit = HitBy(sim, h, p);
        var t = sim.World.Get<Transform>(h)!;
        var throughWall = false;
        var reached = RunUntil(sim, 15, () =>
        {
            throughWall |= CellCoord.Of(t.Position.X, t.Position.Y).X == 14 && t.Position.Y >= 2 && t.Position.Y < 19;
            return hit();
        });
        Assert.True(reached, "the wolf never got around the wall");
        Assert.False(throughWall, "the wolf walked through the wall");
    }

    [Fact]
    public void ChasesKeepTheirPlanUntilItGoesStale()
    {
        var (sim, h, _) = Arena("wolf", new Vector3(10.5f, 10.5f, 0), new Vector3(18.5f, 10.5f, 0));
        Wall(sim, 14, 2, 18);
        var b = sim.World.Get<Behavior>(h)!;
        var m = sim.World.Get<Motion>(h)!;
        Assert.True(RunUntil(sim, 2, () => b.State == "chase" && b.Planned && m.Path.Count > 0), "no path was planned around the wall");
        var plannedAt = b.PlannedAt;
        var repath = CoreContent.Registry.Rules.Number("move.ai_repath_seconds", 1.5);
        // Many thinks pass while the target stands still and the path lasts: no new search.
        sim.RunSeconds(repath * 0.6);
        Assert.True(m.Path.Count > 0, "the detour should still be under way");
        Assert.Equal(plannedAt, b.PlannedAt);
        // A plan older than the repath interval is renewed.
        sim.RunSeconds(repath * 0.6);
        Assert.True(b.PlannedAt > plannedAt, "the plan was never renewed");
    }

    [Fact]
    public void UnreachableTargetsAreNotSearchedEveryThink()
    {
        var (sim, h, _) = Arena("wolf", new Vector3(12.5f, 10.5f, 0), new Vector3(20.5f, 10.5f, 0));
        // The prey stands in a closed pen.
        for (var x = 18; x <= 22; x++)
        {
            Block(sim, x, 8);
            Block(sim, x, 12);
        }
        Wall(sim, 18, 9, 11);
        Wall(sim, 22, 9, 11);
        var b = sim.World.Get<Behavior>(h)!;
        Assert.True(RunUntil(sim, 8, () => b.State == "chase" && b.PlanFailed), "the wolf never concluded the prey was out of reach");
        var plannedAt = b.PlannedAt;
        var repath = CoreContent.Registry.Rules.Number("move.ai_repath_seconds", 1.5);
        sim.RunSeconds(repath * 0.6);
        Assert.Equal(plannedAt, b.PlannedAt);
        sim.RunSeconds(repath * 0.6);
        Assert.True(b.PlannedAt > plannedAt, "the plan was never renewed");
        var t = sim.World.Get<Transform>(h)!;
        Assert.False(t.Position.X >= 18 && t.Position.X < 23 && t.Position.Y >= 8 && t.Position.Y < 13, "the wolf got into the pen");
    }

    [Fact]
    public void ChasersReplanWhenTheTargetMovesAway()
    {
        var (sim, h, p) = Arena("wolf", new Vector3(10.5f, 10.5f, 0), new Vector3(17.5f, 10.5f, 0));
        var b = sim.World.Get<Behavior>(h)!;
        Assert.True(RunUntil(sim, 2, () => b.State == "chase" && b.Planned), "the wolf never planned a chase");
        // The prey leaps well beyond the planned goal: the next think plans toward where it is now.
        var pt = sim.World.Get<Transform>(p)!;
        pt.Position = new Vector3(16.5f, 3.5f, 0);
        sim.Spatial.Update(p, pt.Position);
        sim.RunSeconds(0.3);
        Assert.True(Vector2.Distance(new Vector2(b.PlanGoal.X, b.PlanGoal.Y), new Vector2(pt.Position.X, pt.Position.Y)) < 0.01f, $"still heading for {b.PlanGoal}");
        var hit = HitBy(sim, h, p);
        Assert.True(RunUntil(sim, 10, hit), "the wolf never caught up with the moved prey");
        var t = sim.World.Get<Transform>(h)!;
        Assert.True(t.GroundDistanceTo(pt.Position) < 4, $"the wolf hit from {t.Position}");
    }
}

/// <summary>The allocation-free plumbing keeps the semantics the systems rely on.</summary>
public class SimulationPlumbingTests
{
    private sealed class Tagged : IComponent
    {
        public int Value;
    }

    private sealed class Marked : IComponent
    {
    }

    private abstract record Shout : GameEvent;

    private sealed record LoudShout(string Text) : Shout;

    [Fact]
    public void QueriesVisitEveryMatchFromTheSmallestStore()
    {
        var world = new Ecs.World();
        var expected = new HashSet<Entity>();
        for (var i = 0; i < 200; i++)
        {
            var e = world.Create();
            world.Add(e, new Tagged { Value = i });
            if (i % 7 == 0)
            {
                world.Add(e, new Marked());
                expected.Add(e);
            }
        }
        var seen = world.Query<Tagged, Marked>().Select(q => q.Entity).ToHashSet();
        Assert.Equal(expected, seen);
        Assert.Equal(expected, world.Query<Marked, Tagged>().Select(q => q.Entity).ToHashSet());
        Assert.Empty(world.Query<Tagged, Lifetime>());
    }

    [Fact]
    public void QueriesSkipWhatIsDestroyedAndIgnoreWhatIsCreatedWhileIterating()
    {
        var world = new Ecs.World();
        var all = new List<Entity>();
        for (var i = 0; i < 50; i++)
        {
            var e = world.Create();
            world.Add(e, new Tagged { Value = i });
            world.Add(e, new Marked());
            all.Add(e);
        }
        var visited = new List<int>();
        foreach (var (e, tagged, _) in world.Query<Tagged, Marked>())
        {
            visited.Add(tagged.Value);
            if (tagged.Value == 10)
            {
                // Created first so it takes a fresh index rather than the destroyed one's.
                var born = world.Create();
                world.Add(born, new Tagged { Value = 1000 });
                world.Add(born, new Marked());
                world.Destroy(all[11]);
            }
        }
        Assert.DoesNotContain(11, visited);
        Assert.DoesNotContain(1000, visited);
        Assert.Equal(49, visited.Count);
    }

    [Fact]
    public void WithinFindsWhatABruteForceScanFindsAndRefusesChangesMidway()
    {
        var grid = new SpatialGrid();
        var rng = new Rng(4);
        var positions = new Dictionary<Entity, Vector3>();
        for (var i = 0; i < 400; i++)
        {
            var e = new Entity(i, 1);
            var p = new Vector3((float)rng.Range(-60.0, 60.0), (float)rng.Range(-60.0, 60.0), (float)rng.Range(-3.0, 3.0));
            grid.Update(e, p);
            positions[e] = p;
        }
        for (var q = 0; q < 50; q++)
        {
            var center = new Vector3((float)rng.Range(-50.0, 50.0), (float)rng.Range(-50.0, 50.0), 0);
            var radius = (float)rng.Range(1.0, 25.0);
            var expected = positions.Where(kv => Vector2.Distance(new Vector2(kv.Value.X, kv.Value.Y), new Vector2(center.X, center.Y)) <= radius && Math.Abs(kv.Value.Z - center.Z) <= 2f).Select(kv => kv.Key).ToHashSet();
            var found = new HashSet<Entity>();
            foreach (var (e, p, d) in grid.Within(center, radius, 2f))
            {
                Assert.Equal(positions[e], p);
                Assert.Equal(Vector2.Distance(new Vector2(p.X, p.Y), new Vector2(center.X, center.Y)), d, 3);
                found.Add(e);
            }
            // Distances are compared squared inside the grid; allow the boundary to differ by rounding only.
            found.SymmetricExceptWith(expected);
            Assert.All(found, e => Assert.InRange(Vector2.Distance(new Vector2(positions[e].X, positions[e].Y), new Vector2(center.X, center.Y)), radius - 0.001f, radius + 0.001f));
        }
        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (var (e, _, _) in grid.Within(Vector3.Zero, 100, 10))
            {
                grid.Update(e, new Vector3(-500, -500, 0));
            }
        });
    }

    [Fact]
    public void EventsReachExactThenBaseThenAllSubscribersWithSnapshots()
    {
        var bus = new EventBus();
        var calls = new List<string>();
        Action<LoudShout>? late = null;
        Action<LoudShout> second = _ => calls.Add("second");
        bus.SubscribeAll(e => calls.Add("all"));
        bus.Subscribe<GameEvent>(e => calls.Add("game"));
        bus.Subscribe<Shout>(e => calls.Add("shout"));
        bus.Subscribe<LoudShout>(e =>
        {
            calls.Add("loud:" + e.Text);
            // Changes made while dispatching apply from the next publish.
            bus.Unsubscribe(second);
            late ??= _ => calls.Add("late");
            bus.Subscribe(late);
        });
        bus.Subscribe(second);
        bus.Publish(new LoudShout("a"));
        Assert.Equal(new[] { "loud:a", "second", "shout", "game", "all" }, calls);
        calls.Clear();
        bus.Publish(new LoudShout("b"));
        Assert.Equal(new[] { "loud:b", "late", "shout", "game", "all" }, calls);
        Assert.Equal(2, bus.Log.Count);
    }
}
