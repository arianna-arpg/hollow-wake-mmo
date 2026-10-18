using System.Numerics;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Maths;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;
using Xunit;

namespace HollowWake.Core.Tests;

public class SampledAimPrecedenceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void LiveInputBelongsToPhysicalCaster(bool guided, bool physicalInput)
    {
        var a = DeliveryArena.Create();
        var physical = a.Body(10, 10, faction: "red");
        var context = new EffectContext
        {
            Sim = a.Sim, Source = a.Caster, CastSource = physical, From = new(10, 10, 0),
            Point = new(25, 10, 0), Direction = Vector2.UnitX, Rng = new Rng(71),
            SampledTargetPoint = new(25, 10, 0), AimOffset = Vector3.UnitZ,
        };
        var flights = new List<Projectile>();
        a.Sim.Events.Subscribe<EntitySpawned>(e => { if (a.Sim.World.Get<Projectile>(e.Entity) is { } p) flights.Add(p); });
        a.Effect(guided
            ? """{"type":"projectile","speed":10,"range":30,"guide":720}"""
            : """{"type":"projectile","speed":10,"range":30,"fire":"salvo","count":3,"salvo_interval":0.2}""")
            .Execute(context);
        a.Tick();
        var input = a.Sim.World.GetOrAdd<WeaveBook>(physicalInput ? physical : a.Caster);
        input.AimPoint = new(10, 30, 0);
        input.AimDirection = Vector2.UnitY;
        input.AimAt = a.Sim.Clock.Time;
        a.Tick(10);
        Assert.Equal(guided ? 1 : 3, flights.Count);
        foreach (var flight in guided ? flights : flights.Skip(1))
        {
            if (physicalInput) Assert.True(flight.Heading > 1, $"physical caster input must turn the flight: {flight.Heading}");
            else Assert.Equal(0f, flight.Heading, 5);
        }
        if (!guided && physicalInput)
            Assert.All(flights.Skip(1), p => Assert.Null(p.Context.SampledTargetPoint));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void FrozenGeometryAndLiveBodyEligibilityStayIndependent(bool frozen, bool wrapped, bool salvo)
    {
        var a = DeliveryArena.Create();
        Entity OffsetBody(float y)
        {
            var body = a.Body(20, y);
            a.Sim.World.Get<Transform>(body)!.Yaw = 0;
            a.Sim.World.Add(body, new HitBody { Definition = new HitBodyDef
            {
                Id = "snapshot-body", Volumes = { new HitVolumeSpec { Left = 1, Up = .5f, Radius = .025f, HalfHeight = .1f } },
            } });
            return body;
        }
        var target = OffsetBody(10);
        var capturedPoint = new Vector3(20, 10, 0);
        var capturedOffset = HitBodies.AimPoint(a.Sim, target) - capturedPoint;
        var transform = a.Sim.World.Get<Transform>(target)!;
        transform.Position.Y = 14;
        a.Sim.Spatial.Update(target, transform.Position);
        var bystander = OffsetBody(10);
        var context = new EffectContext
        {
            Sim = a.Sim, Source = a.Caster, Target = target, Point = new(16, 10, 0),
            Direction = Vector2.UnitX, Rng = new Rng(481), AimOffset = capturedOffset,
            SampledTargetPoint = frozen ? capturedPoint : null,
        };
        var child = """{"type":"projectile","origin":"point","speed":15,"range":12,"radius":0.02,"on_hit":[{"type":"event","name":"snapshot_hit"}]}""";
        if (salvo) child = child.Replace("\"speed\":15", "\"speed\":15,\"fire\":\"salvo\",\"count\":3,\"salvo_interval\":0.1");
        var flights = new List<Projectile>();
        a.Sim.Events.Subscribe<EntitySpawned>(e => { if (a.Sim.World.Get<Projectile>(e.Entity) is { } p) flights.Add(p); });
        a.Effect(wrapped ? "{\"type\":\"aim\",\"offset\":0,\"effects\":[" + child + "]}" : child).Execute(context);
        // A direct body payload still receives the live selected entity, independently of geometry.
        a.Effect("""{"type":"event","name":"body_payload"}""").Execute(context);
        Assert.Equal(target, Assert.Single(a.Struck("body_payload")));
        a.Tick(40);
        var expected = frozen ? bystander : target;
        var excluded = frozen ? target : bystander;
        Assert.Equal(salvo ? 3 : 1, flights.Count);
        Assert.Equal(flights.Count, a.Struck("snapshot_hit").Count(e => e == expected));
        Assert.DoesNotContain(excluded, a.Struck("snapshot_hit"));
        Assert.All(flights, p =>
        {
            Assert.Equal(new Vector3(16, 10, 1), p.Origin);
            Assert.Equal(frozen ? capturedPoint : transform.Position, p.AimPoint);
            Assert.Equal(wrapped ? Entity.None : target, p.Context.Target);
        });
    }
}
