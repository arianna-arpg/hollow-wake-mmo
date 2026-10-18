using System.Numerics;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;
using HollowWake.Core.World;
using Xunit;
using Xunit.Abstractions;

namespace HollowWake.Core.Tests;

public class KoboldProjectileTests(ITestOutputHelper output)
{
    public static IEnumerable<object[]> Cases() => KoboldTests.Bodies().SelectMany(body =>
        new[] { -0.5f, 0f, 0.5f }.SelectMany(relief => new[] { "firebolt", "piercing_arrow" }
            .Select(weave => new object[] { body[0], relief, weave })));

    [Theory, MemberData(nameof(Cases))]
    public void RealProjectilesReachAuthoredBodiesOnFlatGroundAndHalfUnitRelief(string pattern, float relief, string preset)
    {
        var a = DeliveryArena.Create(seed: 481);
        // Put the target on real raised/lowered terrain. Neither body height nor flight parameters are altered.
        var chunk = a.Sim.Terrain.GetChunk(new ChunkCoord(0, 0));
        for (var y = 0; y < chunk.Size; y++)
            for (var x = 14; x < chunk.Size; x++) chunk.Height[chunk.Index(x, y)] = relief;
        chunk.Modified = true;
        var target = a.Body(16, 10, pattern: pattern);
        var at = a.Sim.World.Get<Transform>(target)!;
        var authored = a.Sim.Content.Require<PatternDef>(pattern);
        Assert.Equal(authored.Height, at.Height, 5);
        Assert.Equal(relief, at.Position.Z, 5);
        var stats = a.Sim.World.Get<Stats>(target)!;
        stats.Attributes.SetBase("life.max", 100000);
        stats.FillResources();
        var book = a.Sim.World.Get<WeaveBook>(a.Caster)!;
        var slot = book.Add(new WeaveSpec { Preset = preset });
        Assert.True(a.Sim.Service<WeaveRuntime>().TryBegin(a.Sim, a.Caster, slot, target, at.Position, Vector2.UnitX, out var reason), reason);
        a.Tick(80);
        Assert.Contains(a.Events, e => e is EntitySpawned spawned && spawned.Pattern == "projectile");
        var hits = a.Events.OfType<DamageDealt>().Count(e => e.Source == a.Caster && e.Target == target && e.Total > 0);
        output.WriteLine($"{preset} -> {pattern}, height {at.Height:F2}, relief {relief:F2}: {hits} damaging hits");
        Assert.True(hits > 0, $"{preset} missed {pattern} at relief {relief}; keep its authored {authored.Height} m height and fix the shared delivery path.");
    }
}
