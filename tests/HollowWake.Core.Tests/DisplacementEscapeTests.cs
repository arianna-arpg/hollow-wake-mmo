using System.Numerics;
using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Movement;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.Systems;
using HollowWake.Core.World;
using Xunit;

namespace HollowWake.Core.Tests;

public class DisplacementEscapeTests
{
    [Theory]
    [InlineData(false, false, -1)]
    [InlineData(false, false, 1)]
    [InlineData(false, true, -1)]
    [InlineData(false, true, 1)]
    [InlineData(true, false, -1)]
    [InlineData(true, false, 1)]
    [InlineData(true, true, -1)]
    [InlineData(true, true, 1)]
    public void ResidentSpawnedInsideABlockerCanEscapeWithoutACollision(bool terrain, bool nudge, int direction)
    {
        var (arena, body) = ResidentInBlocker(terrain);
        var t = arena.Sim.World.Get<Transform>(body)!;
        var start = t.Position;
        if (nudge)
        {
            // Small field steps must work even before they reach the edge of the blocked home cell.
            for (var i = 0; i < 20; i++)
                Assert.True(DisplacementSystem.Nudge(arena.Sim, body, t, new Vector2(direction * .1f, 0)));
        }
        else
        {
            DisplacementSystem.Shove(arena.Sim, body,
                new Vector2(direction * DisplacementSystem.SpeedFor(arena.Sim, 3), 0));
            arena.Tick(60);
            Assert.Equal(Vector3.Zero, arena.Sim.World.Get<Motion>(body)!.Impulse);
        }
        Assert.InRange(direction * (t.Position.X - start.X), 1.9f, 3.01f);
        Assert.Equal(start.Z, t.Position.Z);
        Assert.DoesNotContain(arena.Of<BodyCollided>(), e => e.Entity == body);
        Assert.Contains(arena.Sim.Spatial.Within(t.Position, .01f), found => found.Entity == body);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void EscapingResidentStillStopsBeforeTheNextBlockedCell(bool terrain, bool nudge)
    {
        var (arena, body) = ResidentInBlocker(terrain);
        var t = arena.Sim.World.Get<Transform>(body)!;
        Block(arena, new CellCoord(32, 10), terrain);
        if (nudge) Assert.True(DisplacementSystem.Nudge(arena.Sim, body, t, new Vector2(10, 0)));
        else
        {
            DisplacementSystem.Shove(arena.Sim, body, new Vector2(200, 0));
            arena.Tick(1);
            Assert.Single(arena.Of<BodyCollided>(), e => e.Entity == body);
            Assert.Equal(Vector3.Zero, arena.Sim.World.Get<Motion>(body)!.Impulse);
        }
        Assert.InRange(t.Position.X, 31.5f, 31.99999f);
        // A later nudge may not exempt the obstruction outside its current (clear) cell.
        var before = t.Position;
        DisplacementSystem.Nudge(arena.Sim, body, t, new Vector2(10, 0));
        Assert.InRange(t.Position.X, before.X, 31.99999f);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void EscapeExemptionDoesNotIncludeAnEndpointOnTheNextCellBoundary(bool terrain, bool north)
    {
        var (arena, body) = ResidentInBlocker(terrain);
        var t = arena.Sim.World.Get<Transform>(body)!;
        Block(arena, north ? new CellCoord(30, 11) : new CellCoord(31, 10), terrain);
        DisplacementSystem.Nudge(arena.Sim, body, t, north ? new Vector2(0, .5f) : new Vector2(.5f, 0));
        Assert.True(north ? t.Position.Y < 11 : t.Position.X < 31);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SweptDisplacementStopsAtTheFarWallOfADeepDip(bool nudge)
    {
        var arena = DeliveryArena.Create();
        var body = arena.Body(30.5f, 10.5f);
        var t = arena.Sim.World.Get<Transform>(body)!;
        for (var x = 32; x <= 33; x++)
        for (var y = 9; y <= 12; y++)
        {
            var (chunk, i) = arena.Sim.Terrain.Locate(new CellCoord(x, y));
            chunk.Height[i] = -2;
        }
        if (nudge) Assert.True(DisplacementSystem.Nudge(arena.Sim, body, t, new Vector2(6, 0)));
        else
        {
            DisplacementSystem.Shove(arena.Sim, body, new Vector2(120, 0));
            arena.Tick(1);
            Assert.Single(arena.Of<BodyCollided>(), e => e.Entity == body);
        }
        Assert.InRange(t.Position.X, 32, 33.99999f);
        // An impulse preserves altitude for the walking system's later gravity pass.
        if (nudge) Assert.True(t.Position.Z < -1);
        else Assert.False(arena.Sim.World.Get<Motion>(body)!.Grounded);
    }
    private static (DeliveryArena Arena, Entity Body) ResidentInBlocker(bool terrain)
    {
        var arena = DeliveryArena.Create();
        Block(arena, new CellCoord(30, 10), terrain);
        var spawnPoint = arena.Sim.World.Create();
        arena.Sim.World.Add(spawnPoint, new Transform { Position = new Vector3(30.5f, 10.5f, 0) });
        var spawner = new Spawner
        {
            Pattern = "goblin_scavenger", Group = "test.blocked_home", Scatter = 0,
            RollRarity = false, RespawnAt = arena.Sim.Clock.Time,
        };
        arena.Sim.World.Add(spawnPoint, spawner);
        new SpawnerSystem().Update(arena.Sim);
        var body = Assert.Single(spawner.Alive);
        arena.Sim.World.Remove<Behavior>(body);
        var t = arena.Sim.World.Get<Transform>(body)!;
        Assert.Equal(new Vector3(30.5f, 10.5f, 0), t.Position);
        Assert.False(MovementSystem.CanOccupy(arena.Sim, arena.Sim.Service<BlockedCells>(),
            t.Position.X, t.Position.Y, t.Position.Z, .6, out _));
        return (arena, body);
    }

    private static void Block(DeliveryArena arena, CellCoord cell, bool terrain)
    {
        if (terrain)
        {
            var (chunk, i) = arena.Sim.Terrain.Locate(cell);
            chunk.Flags[i] = CellFlags.Structure | CellFlags.Blocked;
        }
        else arena.Sim.Service<BlockedCells>().Block(cell, arena.Sim.World.Create());
    }
}
