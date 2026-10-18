using System.Numerics;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Effects;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;
using HollowWake.Core.World;
using Xunit;

namespace HollowWake.Core.Tests;

// Promoted from the supplemental fixture after the complete shared base was integrated.
// These entity patterns currently use #36's legacy cylinder fallback, not authored anatomy.
public class GolemProjectileContactTests
{
    public static IEnumerable<object[]> Incoming() => GolemContentTests.Bodies().Concat(GolemPrototypeTests.Bodies())
        .SelectMany(row => new[] { -.5f, 0, .5f }.SelectMany(relief =>
            new[] { "firebolt", "piercing_arrow", "glaive", "orb", "volley", "quiver", "drawn_shot" }
                .Select(form => new object[] { row[0], relief, form })));

    [Theory, MemberData(nameof(Incoming))]
    public void IncomingFlightsReachEveryGolemAcrossRelief(string pattern, float relief, string form)
    {
        var a = DeliveryArena.Create(seed: 539);
        Relief(a, relief);
        var target = a.Body(16, 10, pattern: pattern);
        var body = a.Sim.World.Get<Transform>(target)!;
        var def = a.Sim.Service<PatternFactory>().Resolve(pattern);
        Assert.Equal(def.Def.Height, body.Height, 5);
        Assert.Equal(def.Def.Radius, body.Radius, 5);
        Assert.Equal(relief, body.Position.Z, 5);
        Assert.False(def.Components.ContainsKey("hit_body"), "Update the fallback coverage record when authored golem anatomy is added.");
        var spec = form is "firebolt" or "piercing_arrow" ? new WeaveSpec { Preset = form } : new WeaveSpec { Form = form, Threads = { "ember" } };
        CastAndAssertContact(a, target, spec);
    }

    public static IEnumerable<object[]> Outgoing() => new[]
        { ("fire_golem", "firebolt"), ("ice_golem", "frostbolt"), ("lightning_golem", "storm_bolt") }
        .SelectMany(pair => new[] { -.5f, 0, .5f }.SelectMany(relief =>
            new[] { "goblin_scavenger", "wolf", "skeleton_warrior" }
                .Select(target => new object[] { pair.Item1, pair.Item2, relief, target })));

    [Theory, MemberData(nameof(Outgoing))]
    public void NativeGolemBoltsReachSmallAndHumanoidBodiesAcrossRelief(string pattern, string weave, float relief, string targetPattern)
    {
        var a = DeliveryArena.Create(seed: 540);
        Relief(a, relief);
        a.Caster = a.Sim.Service<PatternFactory>().Spawn(a.Sim, pattern, new Vector3(10, 20, 0), new SpawnOptions { Faction = "red" });
        a.Sim.World.Remove<Behavior>(a.Caster);
        var target = a.Body(16, 20, pattern: targetPattern);
        Assert.Equal(relief, a.Sim.World.Get<Transform>(target)!.Position.Z, 5);
        CastAndAssertContact(a, target, new WeaveSpec { Preset = weave });
    }

    private static void Relief(DeliveryArena a, float height)
    {
        var chunk = a.Sim.Terrain.GetChunk(new ChunkCoord(0, 0));
        for (var y = 0; y < chunk.Size; y++)
            for (var x = 14; x < chunk.Size; x++) chunk.Height[chunk.Index(x, y)] = height;
        chunk.Modified = true;
    }

    private static void CastAndAssertContact(DeliveryArena a, HollowWake.Core.Ecs.Entity target, WeaveSpec spec)
    {
        var stats = a.Sim.World.Get<Stats>(target)!;
        stats.Attributes.SetBase("life.max", 100000); stats.FillResources();
        var slot = a.Sim.World.Get<WeaveBook>(a.Caster)!.Add(spec);
        var flights = new List<Projectile>();
        a.Sim.Events.Subscribe<EntitySpawned>(e =>
        {
            if (a.Sim.World.Get<Projectile>(e.Entity) is { } p) flights.Add(p);
        });
        Assert.True(a.Sim.Service<WeaveRuntime>().TryBegin(a.Sim, a.Caster, slot, target,
            a.Sim.World.Get<Transform>(target)!.Position, Vector2.UnitX, out var reason), reason);
        a.Tick(100);
        Assert.NotEmpty(flights);
        Assert.Contains(a.Events, e => e is DamageDealt hit && hit.Source == a.Caster && hit.Target == target && hit.Total > 0);
        Assert.Contains(flights, p => p.Hits.Holds(target, a.Sim.Clock.Time, 0));
    }
}
