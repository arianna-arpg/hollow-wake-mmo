using System.Numerics;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Expressions;
using HollowWake.Core.Sim;
using Xunit;

namespace HollowWake.Core.Tests;

public class ConstructEchoAimSnapshotTests
{
    private static (CastingArena Arena, Entity Echo, List<(Entity Entity, Projectile Flight)> Flights) Setup(
        bool wrapped, bool salvo = false, bool direction = false, bool moveDuringOwnerCast = false)
    {
        var content = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out _);
        content.With(new FactionDef { Id = "red", Relations = { ["blue"] = Stance.Hostile } });
        content.With(new FactionDef { Id = "blue", Relations = { ["red"] = Stance.Hostile } });
        var projectile = """{"type":"projectile","speed":12,"range":12,"radius":0.1,"on_hit":[{"type":"event","name":"echo_snapshot_hit"}]}""";
        if (salvo) projectile = projectile.Replace("\"speed\":12", "\"speed\":12,\"fire\":\"salvo\",\"count\":3,\"salvo_interval\":0.15");
        var graph = wrapped ? "{\"type\":\"aim\",\"offset\":0,\"effects\":[" + projectile + "]}" : projectile;
        var form = CastForecastContent.Registry.Get<FormDef>("forecast_instant_target")! with
        {
            Id = "echo_aim_snapshot_probe", Cooldown = Expr.Constant(0),
            ReleaseCheck = new() { Policy = "body" },
            Effects = new() { CastForecastContent.Spec(graph) },
        };
        if (direction) form = form with { Targeting = Targeting.Direction, Target = null };
        content.With(form);
        var arena = CastingArena.Create(registry: content, distance: 3);
        var minions = new MinionArena { Sim = arena.Sim, Owner = arena.Caster };
        minions.Cast(minions.Learn("shade_echo"));
        var echo = Assert.Single(minions.Minions("echoes"));
        CastForecastContent.Move(arena, echo, arena.Sim.World.Get<Transform>(arena.Caster)!.Position);
        arena.Sim.World.Get<Behavior>(echo)!.NextThinkAt = arena.Now + 10;
        if (direction) CastForecastContent.Move(arena, arena.Enemy, new Vector3(13, 11, 2));
        var flights = new List<(Entity Entity, Projectile Flight)>();
        arena.Sim.Events.Subscribe<EntitySpawned>(e =>
        {
            if (arena.Sim.World.Get<Projectile>(e.Entity) is { } flight)
            {
                flights.Add((e.Entity, flight));
                if (moveDuringOwnerCast && flights.Count == 1)
                    CastForecastContent.Move(arena, arena.Enemy, new Vector3(13, 13, 0));
            }
        });
        arena.Begin(arena.Slot(new WeaveSpec { Form = "echo_aim_snapshot_probe" }));
        Assert.Single(flights);
        Assert.Equal(1, arena.Sim.World.Get<Minion>(echo)!.PendingReplayCount);
        return (arena, echo, flights);
    }

    private static void Drain(CastingArena arena, Entity echo)
    {
        arena.Sim.World.Get<Behavior>(echo)!.NextThinkAt = arena.Now;
        arena.Sim.Tick();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void AQueuedProjectileKeepsTheOwnersResolvedBearing(bool wrapped, bool targetMoves)
    {
        var (arena, echo, flights) = Setup(wrapped);
        var ownerFlight = Assert.Single(flights).Flight;
        if (targetMoves) CastForecastContent.Move(arena, arena.Enemy, new Vector3(13, 13, 0));
        Drain(arena, echo);
        Assert.Equal(2, flights.Count);
        var replay = flights[1].Flight;
        Assert.Equal(echo, replay.Context.CastSource);
        Assert.Equal(ownerFlight.Heading, replay.Heading, 5);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AReplayHitsTheBodyInItsCapturedLineRatherThanTheMovedTarget(bool wrapped)
    {
        var (arena, echo, flights) = Setup(wrapped);
        var ownerFlight = Assert.Single(flights);
        // Isolate the replay's actual contact from the already resolved owner's projectile.
        arena.Sim.Spatial.Remove(ownerFlight.Entity);
        arena.Sim.World.Destroy(ownerFlight.Entity);
        CastForecastContent.Move(arena, arena.Enemy, new Vector3(13, 13, 0));
        var replacement = arena.Spawn("hob", new Vector3(13, 10, 0));
        arena.Events.Clear();
        Drain(arena, echo);
        arena.RunSeconds(1);
        var hit = Assert.Single(arena.Custom("echo_snapshot_hit"));
        Assert.Equal(replacement, hit.Target);
        Assert.Equal(arena.Caster, hit.Source);
        Assert.DoesNotContain(arena.Custom("echo_snapshot_hit"), e => e.Target == arena.Enemy);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheAimSamplePrecedesPayloadSideEffects(bool wrapped)
    {
        var (arena, echo, flights) = Setup(wrapped, moveDuringOwnerCast: true);
        Assert.Equal(new Vector3(13, 13, 0), arena.Sim.World.Get<Transform>(arena.Enemy)!.Position);
        Assert.Equal(0, flights[0].Flight.Heading, 5);
        Drain(arena, echo);
        Assert.Equal(flights[0].Flight.Heading, flights[1].Flight.Heading, 5);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DirectionReplaysRetainTheirSampledBodyElevationWithoutAcquiringATarget(bool wrapped)
    {
        var (arena, echo, flights) = Setup(wrapped, direction: true);
        var ownerFlight = Assert.Single(flights).Flight;
        var aim = ProjectileAims.Get("target")(ownerFlight.Context);
        Assert.NotNull(aim);
        Assert.True(aim.Value.Z > 2);
        CastForecastContent.Move(arena, arena.Enemy, new Vector3(13, 13, 0));
        Drain(arena, echo);
        var replay = flights[1].Flight;
        Assert.Equal(Entity.None, replay.CastTarget);
        Assert.Null(replay.Context.SampledTargetPoint);
        Assert.Equal(Vector2.UnitX, replay.Context.Direction);
        Assert.Equal(ownerFlight.Heading, replay.Heading, 5);
        Assert.Equal(aim, ProjectileAims.Get("target")(replay.Context));
        Assert.True(replay.Velocity.Z > 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LaterReplaySalvoShotsIgnoreTheOwnersNewCursorAndTargetPosition(bool wrapped)
    {
        var (arena, echo, flights) = Setup(wrapped, salvo: true);
        var originalHeading = Assert.Single(flights).Flight.Heading;
        CastForecastContent.Move(arena, arena.Enemy, new Vector3(13, 13, 0));
        var replacement = arena.Spawn("hob", new Vector3(13, 10, 0));
        Drain(arena, echo);
        Assert.True(arena.Runtime.Aim(arena.Sim, arena.Caster, new Vector3(13, 13, 0), Vector2.UnitY));
        arena.RunSeconds(1);
        var replayFlights = flights.Select(p => p.Flight).Where(p => p.Context.CastSource == echo).ToArray();
        Assert.Equal(3, replayFlights.Length);
        Assert.All(replayFlights, flight =>
        {
            Assert.Equal(originalHeading, flight.Heading, 5);
            Assert.True(flight.Hits.Contains(replacement));
            Assert.False(flight.Hits.Contains(arena.Enemy));
        });
    }
}
