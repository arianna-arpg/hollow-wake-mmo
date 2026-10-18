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
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void AQueuedProjectileKeepsTheOwnersResolvedBearing(bool wrapped, bool targetMoves)
    {
        var content = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out _);
        content.With(new FactionDef { Id = "red", Relations = { ["blue"] = Stance.Hostile } });
        content.With(new FactionDef { Id = "blue", Relations = { ["red"] = Stance.Hostile } });
        const string projectile = """{"type":"projectile","speed":12,"range":12,"radius":0.1}""";
        var graph = wrapped ? "{\"type\":\"aim\",\"offset\":0,\"effects\":[" + projectile + "]}" : projectile;
        content.With(CastForecastContent.Registry.Get<FormDef>("forecast_instant_target")! with
        {
            Id = "echo_aim_snapshot_probe", Cooldown = Expr.Constant(0),
            ReleaseCheck = new() { Policy = "body" },
            Effects = new() { CastForecastContent.Spec(graph) },
        });
        var arena = CastingArena.Create(registry: content, distance: 3);
        var minions = new MinionArena { Sim = arena.Sim, Owner = arena.Caster };
        minions.Cast(minions.Learn("shade_echo"));
        var echo = Assert.Single(minions.Minions("echoes"));
        CastForecastContent.Move(arena, echo, arena.Sim.World.Get<Transform>(arena.Caster)!.Position);
        arena.Sim.World.Get<Behavior>(echo)!.NextThinkAt = arena.Now + 10;
        var flights = new List<Projectile>();
        arena.Sim.Events.Subscribe<EntitySpawned>(e =>
        {
            if (arena.Sim.World.Get<Projectile>(e.Entity) is { } flight) flights.Add(flight);
        });
        arena.Begin(arena.Slot(new WeaveSpec { Form = "echo_aim_snapshot_probe" }));
        var ownerFlight = Assert.Single(flights);
        Assert.Equal(1, arena.Sim.World.Get<Minion>(echo)!.PendingReplayCount);
        if (targetMoves) CastForecastContent.Move(arena, arena.Enemy, new Vector3(13, 13, 0));
        arena.Sim.World.Get<Behavior>(echo)!.NextThinkAt = arena.Now;
        arena.Sim.Tick();
        Assert.Equal(2, flights.Count);
        var replay = flights[1];
        Assert.Equal(echo, replay.Context.CastSource);
        Assert.Equal(ownerFlight.Heading, replay.Heading, 5);
    }
}
