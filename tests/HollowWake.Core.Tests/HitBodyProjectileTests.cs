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

public class HitBodyProjectileTests
{
    [Fact]
    public void DirectionCastKeepsItsPressedAimAndCanHitABystander()
    {
        var a = DeliveryArena.Create(seed: 481);
        var target = a.Body(16, 10, pattern: "goblin_skirmisher");
        var at = a.Sim.World.Get<Transform>(target)!;
        var slot = a.Sim.World.Get<WeaveBook>(a.Caster)!.Add(new WeaveSpec { Preset = "firebolt" });
        Assert.True(a.Sim.Service<WeaveRuntime>().TryBegin(a.Sim, a.Caster, slot, target, at.Position, Vector2.UnitX, out var reason), reason);
        at.Position.Y += 4;
        a.Sim.Spatial.Update(target, at.Position);
        var bystander = a.Body(16, 10, pattern: "goblin_skirmisher");
        a.Tick(80);
        Assert.DoesNotContain(a.Events, e => e is DamageDealt hit && hit.Target == target);
        Assert.Contains(a.Events, e => e is DamageDealt hit && hit.Target == bystander && hit.Total > 0);
    }

    public static IEnumerable<object[]> Cases()
    {
        var factory = new PatternFactory(DeliveryArena.Registry);
        return DeliveryArena.Registry.Each<PatternDef>()
            .Where(p => factory.Resolve(p.Id).Components.ContainsKey("hit_body"))
            .OrderBy(p => p.Id, StringComparer.Ordinal)
            .SelectMany(p => new[] { -.5f, 0, .5f }.SelectMany(relief =>
                new[] { "firebolt", "piercing_arrow", "glaive", "orb", "volley", "quiver", "drawn_shot" }
                    .Select(form => new object[] { p.Id, relief, form })));
    }

    [Theory, MemberData(nameof(Cases))]
    public void RealCastsReachAuthoredBodiesAcrossRelief(string pattern, float relief, string form) =>
        AssertRealCastContact(pattern, relief, form);

    internal static void AssertRealCastContact(string pattern, float relief, string form)
    {
        var a = DeliveryArena.Create(seed: 481);
        var chunk = a.Sim.Terrain.GetChunk(new ChunkCoord(0, 0));
        for (var y = 0; y < chunk.Size; y++)
        for (var x = 14; x < chunk.Size; x++) chunk.Height[chunk.Index(x, y)] = relief;
        chunk.Modified = true;
        var target = a.Body(16, 10, pattern: pattern);
        var at = a.Sim.World.Get<Transform>(target)!;
        var authored = a.Sim.Service<PatternFactory>().Resolve(pattern);
        Assert.Equal(authored.Def.Height, at.Height, 5);
        Assert.Equal(relief, at.Position.Z, 5);
        a.Sim.World.Get<Stats>(target)!.Attributes.SetBase("life.max", 100000);
        a.Sim.World.Get<Stats>(target)!.FillResources();
        var book = a.Sim.World.Get<WeaveBook>(a.Caster)!;
        var spec = form is "firebolt" or "piercing_arrow" ? new WeaveSpec { Preset = form } :
            new WeaveSpec { Form = form, Threads = { "ember" } };
        var slot = book.Add(spec);
        var flights = new List<Projectile>();
        a.Sim.Events.Subscribe<EntitySpawned>(e =>
        {
            if (a.Sim.World.Get<Projectile>(e.Entity) is { } p) flights.Add(p);
        });
        Assert.True(a.Sim.Service<WeaveRuntime>().TryBegin(a.Sim, a.Caster, slot, target, at.Position, Vector2.UnitX, out var reason), reason);
        a.Tick(100);
        Assert.Contains(a.Events, e => e is EntitySpawned spawned && spawned.Pattern == "projectile");
        Assert.Contains(a.Events, e => e is DamageDealt hit && hit.Source == a.Caster && hit.Target == target && hit.Total > 0);
        Assert.Contains(flights, p => p.Hits.Holds(target, a.Sim.Clock.Time, 0));
    }
}
