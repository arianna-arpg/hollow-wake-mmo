using System.Numerics;
using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;
using HollowWake.Core.World;
using HollowWake.Core.Definitions;
using HollowWake.Core.Patterns;
using Xunit;

namespace HollowWake.Core.Tests;

public class KinProjectileTests
{
    public static IEnumerable<object[]> Cases() => DeliveryArena.Registry.Each<PatternDef>()
        .Where(p => p.Extends is "gnoll_body" or "beastkin_body").OrderBy(p => p.Id, StringComparer.Ordinal)
        .SelectMany(p => new[] { -.5f, 0, .5f }.SelectMany(relief =>
            new[] { "firebolt", "piercing_arrow", "glaive", "orb", "volley", "quiver", "drawn_shot" }
                .Select(form => new object[] { p.Id, relief, form })));

    [Theory, MemberData(nameof(Cases))]
    public void AllKinTakeRealFlightsAcrossReliefWithoutEnlargingTheirBodies(string pattern, float relief, string form)
    {
        // Gnolls and Beastkin currently uses the shared cylinder fallback; this is not authored anatomy coverage.
        Assert.False(new PatternFactory(DeliveryArena.Registry).Resolve(pattern).Components.ContainsKey("hit_body"));
        HitBodyProjectileTests.AssertRealCastContact(pattern, relief, form);
    }

    public static IEnumerable<object[]> OrbChildCases() => DeliveryArena.Registry.Each<PatternDef>()
        .Where(p => p.Extends is "gnoll_body" or "beastkin_body").OrderBy(p => p.Id, StringComparer.Ordinal)
        .SelectMany(p => new[] { -.5f, 0, .5f }.SelectMany(relief =>
            new[] { "tide_orb", "frozen_orb" }.Select(preset => new object[] { p.Id, relief, preset })));

    [Theory, MemberData(nameof(OrbChildCases))]
    public void AuthoredOrbChildrenHitKinAtTheirOriginalSizes(string pattern, float relief, string preset)
    {
        var a = DeliveryArena.Create(seed: 481);
        var chunk = a.Sim.Terrain.GetChunk(new ChunkCoord(0, 0));
        for (var y = 0; y < chunk.Size; y++)
        for (var x = 11; x < chunk.Size; x++) chunk.Height[chunk.Index(x, y)] = relief;
        chunk.Modified = true;
        var authored = a.Sim.Service<PatternFactory>().Resolve(pattern);
        var bodies = new List<Entity>();
        for (var x = 12; x <= 18; x += 2)
        foreach (var y in new[] { 8f, 10f, 12f })
        {
            var body = a.Body(x, y, pattern: pattern);
            var transform = a.Sim.World.Get<Transform>(body)!;
            Assert.Equal(authored.Def.Height, transform.Height, 5);
            Assert.Equal(authored.Def.Radius, transform.Radius, 5);
            Assert.Equal(relief, transform.Position.Z, 5);
            var stats = a.Sim.World.Get<Stats>(body)!;
            stats.Attributes.SetBase("life.max", 100000);
            stats.FillResources();
            bodies.Add(body);
        }
        var flights = new List<Projectile>();
        a.Sim.Events.Subscribe<EntitySpawned>(e =>
        {
            if (a.Sim.World.Get<Projectile>(e.Entity) is { } projectile) flights.Add(projectile);
        });
        var slot = a.Sim.World.Get<WeaveBook>(a.Caster)!.Add(new WeaveSpec { Preset = preset });
        Assert.True(a.Sim.Service<WeaveRuntime>().TryBegin(a.Sim, a.Caster, slot, Entity.None,
            new Vector3(20, 10, 0), Vector2.UnitX, out var reason), reason);
        a.Tick(120);
        var parent = Assert.Single(flights, p => p.Spec.Emits);
        var children = flights.Where(p => !p.Spec.Emits).ToArray();
        Assert.NotEmpty(children);
        // A parent-orb hit cannot satisfy this assertion; only emitted child flights qualify.
        Assert.Contains(children, p => bodies.Any(body => p.Hits.Holds(body, a.Sim.Clock.Time, 0)));
        Assert.All(children, p => Assert.Equal(p.Context.From!.Value.Z, p.Origin.Z, 4));
        Assert.All(children, p => Assert.Equal(parent.Context.CastId, p.Context.CastId));
        Assert.All(children, p => Assert.Equal(a.Caster, p.Context.Source));
    }
}
