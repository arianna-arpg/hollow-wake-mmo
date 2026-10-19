using System.Numerics;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;
using HollowWake.Core.World;
using Xunit;
using Xunit.Abstractions;

namespace HollowWake.Core.Tests;

public class ProjectileLaunchRegressionTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("firebolt", 1f)]
    [InlineData("firebolt", 3f)]
    [InlineData("firebolt", 8f)]
    [InlineData("piercing_arrow", 1f)]
    [InlineData("piercing_arrow", 3f)]
    [InlineData("piercing_arrow", 8f)]
    public void EmptyGroundDirectionPressTravelsItsAuthoredRange(string preset, float cursorDistance)
    {
        var a = DeliveryArena.Create(seed: 481);
        var flights = Capture(a);
        Press(a, preset, new Vector3(10 + cursorDistance, 10, 0));
        a.Tick(150);
        var flight = Assert.Single(flights);
        output.WriteLine($"{preset}, cursor {cursorDistance}: origin {flight.Origin}, traveled {flight.Traveled:F3}/{flight.Range:F3}, final {flight.Carrier}");
        Assert.True(flight.Traveled >= flight.Range, $"Cursor {cursorDistance} ended flight at {flight.Traveled:F3} before range {flight.Range:F3}");
    }

    [Fact]
    public void EmptyGroundDirectionCanStrikeABodyBeyondTheCursor()
    {
        var a = DeliveryArena.Create(seed: 481);
        var body = a.Body(18, 10, pattern: "hob");
        Press(a, "firebolt", new Vector3(13, 10, 0));
        a.Tick(100);
        Assert.Contains(a.Events.OfType<DamageDealt>(), h => h.Target == body && h.Total > 0);
    }

    public static IEnumerable<object[]> Emissions() =>
        new[] { "on_tick", "on_travel", "cast", "cascade", "delay", "salvo", "aim" }.SelectMany(path =>
            new[] { -.5f, 0, .5f }.SelectMany(ground =>
                new[] { "hob", "skeleton_warrior" }.Select(body => new object[] { path, ground, body })));

    [Theory, MemberData(nameof(Emissions))]
    public void NestedFlightsKeepTheirEmissionHeightAndReachHumanoids(string path, float ground, string pattern)
    {
        var a = DeliveryArena.Create(seed: 481);
        Ground(a, ground);
        var body = a.Body(18, 10, pattern: pattern);
        var flights = Capture(a);
        const string child = """{"type":"projectile","speed":15,"range":10,"radius":0.1,"on_hit":[{"type":"event","name":"child_hit"}]}""";
        var emitted = path switch
        {
            "cast" => """{"type":"cast","weave":"frostbolt","at":"point","aim":"onward"}""",
            "aim" => "{\"type\":\"aim\",\"offset\":0,\"effects\":[" + child + "]}",
            "delay" => "{\"type\":\"delay\",\"seconds\":0.1,\"effects\":[" + child + "]}",
            "salvo" => child.Replace("\"speed\":15", "\"speed\":15,\"fire\":\"salvo\",\"count\":2,\"salvo_interval\":0.15"),
            _ => child,
        };
        var hook = path == "on_travel" ? "on_travel" : "on_tick";
        var parent = "{\"type\":\"projectile\",\"aim\":\"horizontal\",\"speed\":2,\"duration\":1,\"radius\":0.05,\"tick\":0.2,\"travel\":0.4,\"" + hook + "\":[" + emitted + "]}";
        var graph = path == "cascade" ? "{\"type\":\"cascade\",\"count\":2,\"dir\":\"forward\",\"step\":1,\"interval\":0.1,\"effects\":[" + parent + "]}" : parent;
        a.Run(graph, new Vector3(12, 10, ground), Vector2.UnitX);
        a.Tick(80);
        var children = flights.Where(p => !p.Spec.Emits).ToList();
        Assert.NotEmpty(children);
        var parents = flights.Where(p => p.Spec.Emits).ToList();
        Assert.Equal(ground + .9f, parents[0].Origin.Z, 4);
        if (path == "cascade") Assert.All(parents.Skip(1), p => Assert.Equal(ground + 1, p.Origin.Z, 4));
        output.WriteLine($"{path}/{pattern}, ground {ground}: {children.Count} children, heights {string.Join(",", children.Select(p => p.Origin.Z.ToString("F3")))}");
        Assert.All(children, p => Assert.Equal(p.Context.From!.Value.Z, p.Origin.Z, 4));
        Assert.Contains(children, p => p.Hits.Holds(body, a.Sim.Clock.Time, 0));
        if (path == "cast") Assert.Contains(a.Events.OfType<DamageDealt>(), h => h.Source == a.Caster && h.Target == body && h.Total > 0);
        else Assert.Contains(body, a.Struck("child_hit"));
    }

    [Theory]
    [InlineData("tide_orb")]
    [InlineData("frozen_orb")]
    public void AuthoredOrbsShedChildrenAtTheParentHeightAndHitBodies(string preset)
    {
        var a = DeliveryArena.Create(seed: 481);
        var bodies = new List<Entity>();
        for (var x = 12; x <= 18; x += 2)
        foreach (var y in new[] { 8f, 10f, 12f })
        {
            var body = a.Body(x, y, pattern: "hob");
            a.Sim.World.Get<Stats>(body)!.Attributes.SetBase("life.max", 100000);
            a.Sim.World.Get<Stats>(body)!.FillResources();
            bodies.Add(body);
        }
        var flights = Capture(a);
        var emissionPositions = new Dictionary<Projectile, Vector3>();
        Projectile? emittingParent = null;
        a.Sim.Events.Subscribe<EntitySpawned>(e =>
        {
            if (a.Sim.World.Get<Projectile>(e.Entity) is not { } p) return;
            if (p.Spec.Emits) emittingParent = p;
            else emissionPositions[p] = emittingParent!.Carrier;
        });
        Press(a, preset, new Vector3(20, 10, 0));
        a.Tick(120);
        var parent = Assert.Single(flights, p => p.Spec.Emits);
        var children = flights.Where(p => !p.Spec.Emits).ToList();
        Assert.NotEmpty(children);
        output.WriteLine($"{preset}: parent height {parent.Origin.Z:F3}, {children.Count} children at {string.Join(",", children.Select(p => p.Origin.Z.ToString("F3")))}");
        Assert.All(children, p => Assert.Equal(emissionPositions[p], p.Origin));
        Assert.Contains(children, p => bodies.Any(b => p.Hits.Holds(b, a.Sim.Clock.Time, 0)));
        Assert.All(children, p => Assert.Equal(a.Caster, p.Context.Source));
        Assert.All(children, p => Assert.Equal(parent.Context.CastId, p.Context.CastId));
    }

    [Theory]
    [InlineData("point", -1)]
    [InlineData("horizontal", 0)]
    public void ExplicitElevationChoicesRemainAvailable(string aim, int verticalSign)
    {
        var a = DeliveryArena.Create();
        var flights = Capture(a);
        a.Run("{\"type\":\"projectile\",\"aim\":\"" + aim + "\",\"speed\":10,\"range\":10}", new Vector3(13, 10, 0), Vector2.UnitX);
        Assert.Equal(verticalSign, Math.Sign(Assert.Single(flights).Velocity.Z));
    }

    [Fact]
    public void ExplicitPointOriginAtANewAnchorUsesItsGroundLift()
    {
        var a = DeliveryArena.Create();
        var flights = Capture(a);
        a.Effect("""{"type":"projectile","origin":"point","aim":"horizontal","range":10}""").Execute(new EffectContext
        {
            Sim = a.Sim,
            Source = a.Caster,
            Point = new Vector3(15, 10, 0),
            From = new Vector3(12, 10, .9f),
            LaunchLift = 0,
            Direction = Vector2.UnitX,
            Rng = new HollowWake.Core.Maths.Rng(481),
        });
        Assert.Equal(new Vector3(15, 10, 1), Assert.Single(flights).Origin);
    }
    private static List<Projectile> Capture(DeliveryArena a)
    {
        var flights = new List<Projectile>();
        a.Sim.Events.Subscribe<EntitySpawned>(e => { if (a.Sim.World.Get<Projectile>(e.Entity) is { } p) flights.Add(p); });
        return flights;
    }

    private static void Press(DeliveryArena a, string preset, Vector3 point)
    {
        var slot = a.Sim.World.Get<WeaveBook>(a.Caster)!.Add(new WeaveSpec { Preset = preset });
        Assert.True(a.Sim.Service<WeaveRuntime>().TryBegin(a.Sim, a.Caster, slot, Entity.None, point, Vector2.UnitX, out var reason), reason);
    }

    private static void Ground(DeliveryArena a, float height)
    {
        var chunk = a.Sim.Terrain.GetChunk(new ChunkCoord(0, 0));
        Array.Fill(chunk.Height, height);
        chunk.Modified = true;
        var caster = a.Sim.World.Get<Transform>(a.Caster)!;
        caster.Position.Z = height;
        a.Sim.Spatial.Update(a.Caster, caster.Position);
    }
}
