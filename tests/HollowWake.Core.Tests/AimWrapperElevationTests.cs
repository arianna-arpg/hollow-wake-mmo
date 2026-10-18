using System.Numerics;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Maths;
using HollowWake.Core.Sim;
using HollowWake.Core.World;
using Xunit;

namespace HollowWake.Core.Tests;

public class AimWrapperElevationTests
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 30)]
    [InlineData(false, -30)]
    [InlineData(true, 0)]
    [InlineData(true, 30)]
    [InlineData(true, -30)]
    public void TurningTheBearingPreservesSampledOrRetainedBodyElevation(bool retainedTarget, int degrees)
    {
        var a = DeliveryArena.Create();
        var target = a.Body(18, 10);
        var transform = a.Sim.World.Get<Transform>(target)!;
        transform.Position.Z = 2;
        a.Sim.Spatial.Update(target, transform.Position);
        EffectContext Context() => new()
        {
            Sim = a.Sim, Source = a.Caster, Target = retainedTarget ? target : Entity.None,
            Point = transform.Position, Direction = Vector2.UnitX, Rng = new Rng(481),
            AimOffset = retainedTarget ? Vector3.Zero : HitBodies.AimPoint(a.Sim, target) - transform.Position,
        };
        const string child = """{"type":"projectile","speed":15,"range":10,"radius":0.1}""";
        var flights = new List<Projectile>();
        a.Sim.Events.Subscribe<EntitySpawned>(e => { if (a.Sim.World.Get<Projectile>(e.Entity) is { } p) flights.Add(p); });
        a.Effect(child).Execute(Context());
        a.Effect("{\"type\":\"aim\",\"offset\":" + degrees + ",\"effects\":[" + child + "]}").Execute(Context());
        Assert.Equal(2, flights.Count);
        Assert.True(flights[0].Velocity.Z > 0);
        Assert.Equal(flights[0].Velocity.Z, flights[1].Velocity.Z, 5);
        Assert.Equal(Entity.None, flights[1].CastTarget);
        Assert.Equal(degrees * Math.PI / 180, flights[1].Heading, 5);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void IdentityBearingSupportStillHitsTheBodyAboveAStep(bool retainedTarget, bool wrapped)
    {
        var a = DeliveryArena.Create();
        var chunk = a.Sim.Terrain.GetChunk(new ChunkCoord(0, 0));
        for (var y = 0; y < chunk.Size; y++)
        for (var x = 14; x < chunk.Size; x++) chunk.Height[y * chunk.Size + x] = 2;
        chunk.Modified = true;
        var target = a.Body(16, 10);
        var transform = a.Sim.World.Get<Transform>(target)!;
        transform.Position.Z = 2;
        a.Sim.Spatial.Update(target, transform.Position);
        const string child = """{"type":"projectile","speed":15,"range":10,"radius":0.1,"on_hit":[{"type":"event","name":"elevated_hit"}]}""";
        var graph = wrapped ? "{\"type\":\"aim\",\"offset\":0,\"effects\":[" + child + "]}" : child;
        a.Effect(graph).Execute(new EffectContext
        {
            Sim = a.Sim, Source = a.Caster, Target = retainedTarget ? target : Entity.None,
            Point = transform.Position, Direction = Vector2.UnitX, Rng = new Rng(481),
            AimOffset = retainedTarget ? Vector3.Zero : HitBodies.AimPoint(a.Sim, target) - transform.Position,
        });
        a.Tick(40);
        Assert.Contains(target, a.Struck("elevated_hit"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BearingTurnsBodyOffsetsAndKeepsTheInheritedLaunchAnchor(bool retainedTarget)
    {
        var a = DeliveryArena.Create();
        var target = a.Body(18, 10);
        a.Sim.World.Add(target, new HitBody { Definition = new HitBodyDef
        {
            Id = "offset-body", Volumes = { new HitVolumeSpec { Forward = .4f, Left = .2f, Up = .75f, Radius = .1f, HalfHeight = .2f } },
        } });
        var point = a.Sim.World.Get<Transform>(target)!.Position;
        var offset = HitBodies.AimPoint(a.Sim, target) - point;
        var flights = new List<Projectile>();
        a.Sim.Events.Subscribe<EntitySpawned>(e => { if (a.Sim.World.Get<Projectile>(e.Entity) is { } p) flights.Add(p); });
        a.Effect("""{"type":"aim","offset":90,"effects":[{"type":"projectile","speed":15,"range":10}]}""").Execute(new EffectContext
        {
            Sim = a.Sim, Source = a.Caster, Origin = target, CastId = 123,
            Target = retainedTarget ? target : Entity.None, Point = point,
            From = new Vector3(10, 10, .9f), LaunchLift = 0,
            Direction = Vector2.UnitX, Rng = new Rng(481), AimOffset = retainedTarget ? Vector3.Zero : offset,
        });
        var flight = Assert.Single(flights);
        Assert.Equal(new Vector3(10, 10, .9f), flight.Origin);
        Assert.Equal(-offset.Y, flight.Context.AimOffset.X, 5);
        Assert.Equal(offset.X, flight.Context.AimOffset.Y, 5);
        Assert.Equal(offset.Z, flight.Context.AimOffset.Z, 5);
        Assert.Equal(0f, flight.Context.LaunchLift);
        Assert.Equal(123, flight.Context.CastId);
        Assert.Equal(a.Caster, flight.Context.Source);
        Assert.Equal(target, flight.Context.Origin);
        Assert.Equal(Entity.None, flight.CastTarget);
    }

    [Fact]
    public void EmptyGroundBearingSupportRemainsHorizontal()
    {
        var a = DeliveryArena.Create();
        Projectile? flight = null;
        a.Sim.Events.Subscribe<EntitySpawned>(e => flight = a.Sim.World.Get<Projectile>(e.Entity) ?? flight);
        a.Run("""{"type":"aim","offset":30,"effects":[{"type":"projectile","speed":15,"range":10}]}""", new Vector3(13, 10, 0), Vector2.UnitX);
        Assert.NotNull(flight);
        Assert.Equal(0f, flight.Velocity.Z);
        Assert.Equal(Entity.None, flight.CastTarget);
    }
}
