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

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void IdentitySupportHitsRetainedOffsetAnatomyLikeThePlainChild(bool wrapped, bool stalePoint)
    {
        var a = DeliveryArena.Create();
        var target = a.Body(18, 10);
        a.Sim.World.Get<Transform>(target)!.Yaw = 0;
        a.Sim.World.Add(target, new HitBody { Definition = new HitBodyDef
        {
            Id = "offset-only", Volumes = { new HitVolumeSpec { Left = 1, Up = .8f, Radius = .025f, HalfHeight = .1f } },
        } });
        var point = stalePoint ? new Vector3(16, 10, 0) : new Vector3(18, 10, 0);
        const string child = """{"type":"projectile","speed":15,"range":12,"radius":0.02,"on_hit":[{"type":"event","name":"offset_hit"}]}""";
        Projectile? flight = null;
        a.Sim.Events.Subscribe<EntitySpawned>(e => flight = a.Sim.World.Get<Projectile>(e.Entity) ?? flight);
        var context = a.Context(point, Vector2.UnitX, target);
        a.Effect(wrapped ? "{\"type\":\"aim\",\"offset\":0,\"effects\":[" + child + "]}" : child).Execute(context);
        Assert.NotNull(flight);
        var aim = HitBodies.AimPoint(a.Sim, target);
        var distance = Vector2.Distance(new(flight.Origin.X, flight.Origin.Y), new(aim.X, aim.Y));
        a.Tick(30);
        Assert.Contains(target, a.Struck("offset_hit"));
        Assert.Equal(MathF.Atan2(aim.Y - flight.Origin.Y, aim.X - flight.Origin.X), flight.Heading, 5);
        Assert.Equal((aim.Z - flight.Origin.Z) * 15 / distance, flight.Velocity.Z, 5);
        Assert.Equal(wrapped ? Entity.None : target, flight.CastTarget);
        Assert.Equal(point, context.Point);
        Assert.Equal(point, context.Aim);
    }

    [Theory]
    [InlineData(false, -30)]
    [InlineData(false, 0)]
    [InlineData(false, 30)]
    [InlineData(true, -30)]
    [InlineData(true, 0)]
    [InlineData(true, 30)]
    public void TurningRetainedAnatomyDoesNotRetargetAnAlreadyCommittedDirection(bool retainedTarget, int degrees)
    {
        var a = DeliveryArena.Create();
        var target = a.Body(18, 10);
        a.Sim.World.Get<Transform>(target)!.Yaw = 0;
        a.Sim.World.Add(target, new HitBody { Definition = new HitBodyDef
        {
            Id = "off-center", Volumes = { new HitVolumeSpec { Left = 1, Up = .5f, Radius = .025f, HalfHeight = .1f } },
        } });
        var point = new Vector3(18, 10, 0);
        var offset = HitBodies.AimPoint(a.Sim, target) - point;
        Projectile? flight = null;
        a.Sim.Events.Subscribe<EntitySpawned>(e => flight = a.Sim.World.Get<Projectile>(e.Entity) ?? flight);
        a.Effect("{\"type\":\"aim\",\"offset\":" + degrees + ",\"effects\":[{\"type\":\"projectile\",\"speed\":15,\"range\":12}]}").Execute(new EffectContext
        {
            Sim = a.Sim, Source = a.Caster, Target = retainedTarget ? target : Entity.None,
            Point = point, Direction = Vector2.UnitX, Rng = new Rng(481), AimOffset = retainedTarget ? Vector3.Zero : offset,
        });
        Assert.NotNull(flight);
        var baseAngle = retainedTarget ? MathF.Atan2(offset.Y, 8 + offset.X) : 0;
        Assert.Equal(baseAngle + degrees * Math.PI / 180, flight.Heading, 5);
        Assert.Equal(Entity.None, flight.CastTarget);
    }

    [Theory]
    [InlineData(false, 0, 0)]
    [InlineData(false, 30, -30)]
    [InlineData(false, 15, 20)]
    [InlineData(true, 0, 0)]
    [InlineData(true, 30, -30)]
    [InlineData(true, 15, 20)]
    public void NestedBearingSupportsComposeWithoutRealigningTheSampledPoint(bool retainedTarget, int first, int second)
    {
        var a = DeliveryArena.Create();
        var target = a.Body(18, 10);
        a.Sim.World.Get<Transform>(target)!.Yaw = 0;
        a.Sim.World.Add(target, new HitBody { Definition = new HitBodyDef
        {
            Id = "nested-offset", Volumes = { new HitVolumeSpec { Left = 1, Up = .8f, Radius = .025f, HalfHeight = .1f } },
        } });
        var offset = HitBodies.AimPoint(a.Sim, target) - new Vector3(18, 10, 0);
        const string child = """{"type":"projectile","speed":15,"range":12,"radius":0.02,"on_hit":[{"type":"event","name":"nested_hit"}]}""";
        static string Wrap(int turn, string effects) => "{\"type\":\"aim\",\"offset\":" + turn + ",\"effects\":[" + effects + "]}";
        EffectContext Context() => new()
        {
            Sim = a.Sim, Source = a.Caster, Target = retainedTarget ? target : Entity.None,
            Point = new Vector3(16, 10, 0), Direction = Vector2.Normalize(new Vector2(1, .2f)),
            From = new Vector3(10, 10, .9f), LaunchLift = 0,
            Rng = new Rng(481), AimOffset = retainedTarget ? Vector3.Zero : offset,
        };
        var flights = new List<Projectile>();
        a.Sim.Events.Subscribe<EntitySpawned>(e => { if (a.Sim.World.Get<Projectile>(e.Entity) is { } p) flights.Add(p); });
        a.Effect(Wrap(first + second, child)).Execute(Context());
        a.Effect(Wrap(first, Wrap(second, child))).Execute(Context());
        var single = flights[0]; var nested = flights[1];
        Assert.True(Vector3.Distance(single.Context.Point, nested.Context.Point) < 0.00001f);
        Assert.True(Vector3.Distance(single.Context.AimOffset, nested.Context.AimOffset) < 0.00001f);
        Assert.True(Vector3.Distance(single.Velocity, nested.Velocity) < 0.00001f);
        if (first + second == 0)
        {
            a.Effect(child).Execute(Context());
            Assert.True(Vector3.Distance(flights[2].Velocity, nested.Velocity) < 0.00001f);
            Assert.True(Vector3.Distance(retainedTarget ? new Vector3(18, 10, 0) : Context().Point, nested.Context.Point) < 0.00001f);
            a.Tick(30);
            if (retainedTarget)
            {
                Assert.Equal(3, a.Struck("nested_hit").Count(e => e == target));
                Assert.All(flights, p => Assert.True(p.Hits.Holds(target, a.Sim.Clock.Time, 0)));
            }
        }
        Assert.Equal(Entity.None, nested.CastTarget);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IdentitySupportsPreservePointPlacedAreaAndProjectileContacts(bool projectile)
    {
        var a = DeliveryArena.Create();
        var point = new Vector3(16, 10, 0);
        var direction = Vector2.Normalize(new Vector2(1, .2f));
        var targetPoint = projectile ? point + new Vector3(direction * 4, 0) : point;
        var target = a.Body(targetPoint.X, targetPoint.Y);
        var child = projectile
            ? """{"type":"projectile","origin":"point","aim":"horizontal","speed":15,"range":6,"radius":0.1,"on_hit":[{"type":"event","name":"point_hit"}]}"""
            : """{"type":"area","center":"point","radius":0.1,"effects":[{"type":"event","name":"point_hit"}]}""";
        static string Wrap(string effects) => "{\"type\":\"aim\",\"offset\":0,\"effects\":[" + effects + "]}";
        var flights = new List<Projectile>();
        a.Sim.Events.Subscribe<EntitySpawned>(e => { if (a.Sim.World.Get<Projectile>(e.Entity) is { } p) flights.Add(p); });
        foreach (var graph in new[] { child, Wrap(child), Wrap(Wrap(child)) })
            a.Effect(graph).Execute(a.Context(point, direction));
        a.Tick(30);
        Assert.Equal(3, a.Struck("point_hit").Count(e => e == target));
        if (projectile)
        {
            Assert.Equal(3, flights.Count);
            Assert.All(flights, p => Assert.Equal(new Vector3(16, 10, 1), p.Origin));
        }
    }
}
