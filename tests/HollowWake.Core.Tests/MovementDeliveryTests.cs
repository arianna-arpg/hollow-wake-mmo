using System.Numerics;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Maths;
using HollowWake.Core.Movement;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.Systems;
using HollowWake.Core.Tags;
using HollowWake.Core.Weaves;
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
    public void DiagonalStepsPassAPlacedBlockersCornerAsWalkingDoes()
    {
        var terrain = Flat();
        var blocked = new BlockedCells();
        blocked.Block(new CellCoord(6, 5), new Entity(1, 1));
        var into = new List<Vector3>();
        // A diagonal step from (5, 5) to (6, 6) never enters (6, 5), so walking allows it and so does the planner.
        Assert.Equal(PathResult.Complete, Planner(terrain, blocked).FindPath(new Vector3(5.5f, 5.5f, 0), new Vector3(6.5f, 6.5f, 0), into, 100));
        Assert.Equal(new[] { new Vector3(6.5f, 6.5f, 0) }, into);
        // Two cells apart, straight across the blocked cell: the way goes round it.
        Assert.Equal(PathResult.Complete, Planner(terrain, blocked).FindPath(new Vector3(5.5f, 5.5f, 0), new Vector3(7.5f, 5.5f, 0), into, 100));
        Assert.False(Touches(into, blocked), string.Join(" ", into));
        Assert.Equal(2, into.Count);
    }

    [Fact]
    public void AGoalInsideAPlacedBlockerIsReachedFromBesideIt()
    {
        var terrain = Flat();
        var blocked = new BlockedCells();
        blocked.Block(new CellCoord(10, 5), new Entity(1, 1));
        var into = new List<Vector3>();
        var planner = Planner(terrain, blocked, maxNodes: 500);
        Assert.Equal(PathResult.Complete, planner.FindPath(new Vector3(4.5f, 5.5f, 0), new Vector3(10.5f, 5.5f, 0), into, 500));
        Assert.Equal(new Vector3(9.5f, 5.5f, 0), into[^1]);
        Assert.False(Touches(into, blocked));
        // Arriving beside it costs the cells on the way, not the whole budget.
        var before = planner.Expanded;
        planner.FindPath(new Vector3(4.5f, 5.5f, 0), new Vector3(10.5f, 5.5f, 0), into, 500);
        Assert.True(planner.Expanded - before < 20, $"expanded {planner.Expanded - before} cells");
        // Standing beside it already is arriving.
        Assert.Equal(PathResult.Complete, planner.FindPath(new Vector3(9.5f, 5.5f, 0), new Vector3(10.5f, 5.5f, 0), into, 500));
        Assert.Equal(new[] { new Vector3(9.5f, 5.5f, 0) }, into);
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

/// <summary>Shoves: knockbacks, pulls and buffets ride a decaying impulse, and walls stop them with a collision.</summary>
public class ShoveTests
{
    private static Vector3 At(DeliveryArena arena, Entity e) => arena.Sim.World.Get<Transform>(e)!.Position;

    private static void Block(Simulation sim, int x, int fromY, int toY)
    {
        for (var y = fromY; y <= toY; y++)
        {
            var (chunk, i) = sim.Terrain.Locate(new CellCoord(x, y));
            chunk.Flags[i] |= CellFlags.Blocked;
        }
    }

    [Fact]
    public void KnockbackCarriesItsDistanceThenSettles()
    {
        var arena = DeliveryArena.Create();
        var boar = arena.Body(12, 10);
        arena.Run("{\"type\": \"move\", \"kind\": \"knockback\", \"distance\": 3}", At(arena, boar), target: boar);
        Assert.True(arena.Sim.World.Has<Displaced>(boar));
        arena.Tick(40);
        Assert.Equal(15, At(arena, boar).X, 0);
        Assert.Equal(10, At(arena, boar).Y, 3);
        Assert.False(arena.Sim.World.Has<Displaced>(boar));
        Assert.Equal(Vector3.Zero, arena.Sim.World.Get<Motion>(boar)!.Impulse);
        Assert.Empty(arena.Of<BodyCollided>());
    }

    [Fact]
    public void AWallStopsAShoveAndTheBodyCollides()
    {
        var arena = DeliveryArena.Create();
        var boar = arena.Body(12.5f, 10.5f);
        Block(arena.Sim, 14, 6, 14);
        var hub = arena.Sim.Service<TriggerHub>();
        var collisions = new List<Entity>();
        hub.Subscribe("collision", Entity.None, (in TriggerEvent e) => collisions.Add(e.Owner));
        arena.Run("{\"type\": \"move\", \"kind\": \"knockback\", \"distance\": 4, \"on_collide\": [{\"type\": \"apply_status\", \"status\": \"stunned\", \"duration\": 1}]}", At(arena, boar), target: boar);
        arena.Tick(20);
        Assert.True(At(arena, boar).X < 14, $"the boar went through the wall to {At(arena, boar)}");
        var collided = Assert.Single(arena.Of<BodyCollided>());
        Assert.Equal(boar, collided.Entity);
        Assert.Equal(arena.Caster, collided.Source);
        Assert.Contains(arena.Of<StatusApplied>(), s => s.Target == boar && s.Status == "stunned");
        Assert.Equal(new[] { boar, arena.Caster }, collisions);
    }

    [Fact]
    public void PullsDrawTowardTheSourceAndStopAUnitShort()
    {
        var arena = DeliveryArena.Create();
        var boar = arena.Body(16, 10);
        arena.Run("{\"type\": \"move\", \"kind\": \"pull\", \"distance\": 20, \"status\": \"stunned\"}", At(arena, boar), target: boar);
        Assert.Contains(arena.Of<StatusApplied>(), s => s.Target == boar && s.Status == "stunned");
        arena.Tick(40);
        Assert.Equal(11, At(arena, boar).X, 0);
    }

    [Fact]
    public void ImmovableBodiesAreNotShoved()
    {
        var arena = DeliveryArena.Create();
        var boar = arena.Body(12, 10);
        arena.Sim.World.Get<Stats>(boar)!.Flags.Add("immovable");
        arena.Run("{\"type\": \"move\", \"kind\": \"knockback\", \"distance\": 3}", At(arena, boar), target: boar);
        arena.Tick(20);
        Assert.Equal(new Vector3(12, 10, 0), At(arena, boar));
    }

    [Fact]
    public void BuffetsShoveTheSameWayForTheSameSeed()
    {
        Vector3 Buffet(ulong seed)
        {
            var arena = DeliveryArena.Create();
            var boar = arena.Body(14, 10);
            arena.Effect("{\"type\": \"move\", \"kind\": \"buffet\", \"distance\": 2}").Execute(arena.Context(At(arena, boar), target: boar, rng: seed));
            arena.Tick(30);
            return At(arena, boar);
        }
        var first = Buffet(1);
        Assert.Equal(first, Buffet(1));
        Assert.NotEqual(first, Buffet(2));
        Assert.Equal(2, Vector2.Distance(new Vector2(first.X, first.Y), new Vector2(14, 10)), 0);
    }

    [Fact]
    public void ABashThrowsAFoeIntoAWallAndStunsIt()
    {
        var arena = DeliveryArena.Create();
        var boar = arena.Body(11.5f, 10.5f);
        Block(arena.Sim, 13, 6, 14);
        var book = arena.Sim.World.Get<WeaveBook>(arena.Caster)!;
        var slot = book.Add(new WeaveSpec { Preset = "shield_bash" });
        Assert.True(arena.Sim.Service<WeaveRuntime>().TryBegin(arena.Sim, arena.Caster, slot, boar, At(arena, boar), Vector2.Zero, out var reason), reason);
        arena.Tick(40);
        Assert.Contains(arena.Of<DamageDealt>(), d => d.Target == boar);
        Assert.Single(arena.Of<BodyCollided>());
        Assert.Contains(arena.Of<StatusApplied>(), s => s.Target == boar && s.Status == "stunned");
    }
}

/// <summary>Displacements over time: dashes through a corridor, leaps through the air, blinks, marks and recalls.</summary>
public class DisplacementTests
{
    private static Vector3 At(DeliveryArena arena, Entity e) => arena.Sim.World.Get<Transform>(e)!.Position;

    private static Stats StatsOf(DeliveryArena arena, Entity e) => arena.Sim.World.Get<Stats>(e)!;

    [Fact]
    public void DashesTravelOverTimeAndStrikeTheirCorridorOnce()
    {
        var arena = DeliveryArena.Create();
        var inWay = arena.Body(13, 10.5f);
        var aside = arena.Body(13, 12.2f);
        arena.Run("{\"type\": \"move\", \"kind\": \"dash\", \"distance\": 6, \"speed\": 10, \"width\": 1.5, \"on_contact\": [{\"type\": \"event\", \"name\": \"t_hit\"}], \"on_end\": [{\"type\": \"event\", \"name\": \"t_end\"}]}", new Vector3(16, 10, 0));
        Assert.True(StatsOf(arena, arena.Caster).HasFlag("dashing"));
        Assert.Single(arena.Of<DisplacementStarted>());
        arena.Tick(4);
        var x = At(arena, arena.Caster).X;
        Assert.InRange(x, 11.5f, 12.5f);
        arena.Tick(10);
        Assert.Equal(16, At(arena, arena.Caster).X, 2);
        Assert.Equal(new[] { inWay }, arena.Struck());
        Assert.Single(arena.Struck("t_end"));
        Assert.False(StatsOf(arena, arena.Caster).HasFlag("dashing"));
        Assert.True(Assert.Single(arena.Of<DisplacementEnded>()).Arrived);
        _ = aside;
    }

    [Fact]
    public void DashesStopShortOfWallsAndOfTreesUnlessTheyPhase()
    {
        var arena = DeliveryArena.Create();
        for (var y = 6; y <= 14; y++)
        {
            arena.Sim.Service<PatternFactory>().Spawn(arena.Sim, "oak", new Vector3(13.5f, y + 0.5f, 0));
        }
        arena.Run("{\"type\": \"move\", \"kind\": \"dash\", \"distance\": 6, \"speed\": 20}", new Vector3(16, 10, 0));
        arena.Tick(20);
        Assert.True(At(arena, arena.Caster).X < 13, $"dashed into the trees to {At(arena, arena.Caster)}");
        var phasing = DeliveryArena.Create();
        for (var y = 6; y <= 14; y++)
        {
            phasing.Sim.Service<PatternFactory>().Spawn(phasing.Sim, "oak", new Vector3(13.5f, y + 0.5f, 0));
        }
        phasing.Run("{\"type\": \"move\", \"kind\": \"dash\", \"distance\": 6, \"speed\": 20, \"phase\": true}", new Vector3(16, 10, 0));
        phasing.Tick(20);
        Assert.Equal(16, At(phasing, phasing.Caster).X, 2);
    }

    [Fact]
    public void LeapsAreUntargetableInTheAirThenLand()
    {
        var arena = DeliveryArena.Create();
        var foe = arena.Body(16.5f, 10);
        arena.Run("{\"type\": \"move\", \"kind\": \"leap\", \"distance\": 6, \"air_time\": 0.5, \"arc\": 2, \"on_land\": [{\"type\": \"area\", \"radius\": 2, \"center\": \"point\", \"effects\": [{\"type\": \"event\", \"name\": \"t_hit\"}]}]}", new Vector3(16, 10, 0));
        arena.Tick(5);
        var mid = At(arena, arena.Caster);
        Assert.True(mid.Z > 1.5f, $"the leap is only {mid.Z} high");
        Assert.True(StatsOf(arena, arena.Caster).HasFlag("untargetable"));
        // A foe's area cannot reach the leaper in the air.
        var other = DeliveryArenaFoe(arena, foe);
        Assert.Empty(other);
        arena.Tick(8);
        Assert.Equal(16, At(arena, arena.Caster).X, 2);
        Assert.Equal(0, At(arena, arena.Caster).Z, 2);
        Assert.False(StatsOf(arena, arena.Caster).HasFlag("untargetable"));
        Assert.Equal(new[] { foe }, arena.Struck());
    }

    /// <summary>The foe strikes everything around it; returns whom it struck.</summary>
    private static List<Entity> DeliveryArenaFoe(DeliveryArena arena, Entity foe)
    {
        var struck = new List<Entity>();
        var area = arena.Effect("{\"type\": \"area\", \"radius\": 30, \"center\": \"source\", \"effects\": [{\"type\": \"event\", \"name\": \"t_foe\"}]}");
        area.Execute(new EffectContext { Sim = arena.Sim, Source = foe, Point = At(arena, foe), Rng = new Rng(1) });
        struck.AddRange(arena.Struck("t_foe"));
        return struck;
    }

    [Fact]
    public void BlinksArriveBehindTheirTargetAfterTheirDelay()
    {
        var arena = DeliveryArena.Create();
        var foe = arena.Body(16, 10);
        var ft = arena.Sim.World.Get<Transform>(foe)!;
        ft.Yaw = MathF.PI;
        arena.Run("{\"type\": \"move\", \"kind\": \"blink\", \"behind\": true, \"delay\": 0.3, \"distance\": 20}", At(arena, foe), target: foe);
        arena.Tick(3);
        Assert.Equal(10, At(arena, arena.Caster).X, 2);
        arena.Tick(4);
        // Behind a body facing west is east of it.
        Assert.True(At(arena, arena.Caster).X > 16.5f, $"arrived at {At(arena, arena.Caster)}");
    }

    [Fact]
    public void MarksAreRecalledOnce()
    {
        var arena = DeliveryArena.Create();
        arena.Run("{\"type\": \"mark\", \"key\": \"t_home\"}", new Vector3(10, 10, 0));
        arena.Run("{\"type\": \"move\", \"kind\": \"teleport\", \"distance\": 5}", new Vector3(15, 10, 0));
        Assert.Equal(15, At(arena, arena.Caster).X, 2);
        arena.Run("{\"type\": \"move\", \"kind\": \"recall\", \"mark\": \"t_home\"}", new Vector3(15, 10, 0));
        Assert.Equal(10, At(arena, arena.Caster).X, 2);
        arena.Run("{\"type\": \"move\", \"kind\": \"teleport\", \"distance\": 5}", new Vector3(15, 10, 0));
        arena.Run("{\"type\": \"move\", \"kind\": \"recall\", \"mark\": \"t_home\"}", new Vector3(15, 10, 0));
        Assert.Equal(15, At(arena, arena.Caster).X, 2);
    }
}
