using System.Numerics;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;
using HollowWake.Core.Territory;
using HollowWake.Core.Weaves;
using HollowWake.Core.World;
using HollowWake.Core.WorldGen;
using Xunit;
using Xunit.Abstractions;

namespace HollowWake.Core.Tests;

public class ElementalProjectileTests(ITestOutputHelper output)
{
    public static IEnumerable<object[]> Targets() => ElementalTests.Bodies()
        .SelectMany(row => new[] { -.5f, 0f, .5f }.Select(relief => new object[] { row[0], relief }));

    [Theory, MemberData(nameof(Targets))]
    public void TargetedBoltHitsAuthoredBodyOnFlatGroundAndRelief(string pattern, float relief)
    {
        var layout = WorldLayout.Default;
        var terrain = new Terrain(layout, new Relief(layout, relief));
        var sim = new Simulation(CoreContent.Registry, terrain, 40, 20, layout);
        ZoneBuilder.Wire(sim, new WorldState(new GenerationOverlays()));
        var factory = sim.Service<PatternFactory>();
        var from = new Vector3(10, 10, terrain.HeightAt(10, 10));
        var to = new Vector3(16, 10, terrain.HeightAt(16, 10));
        Assert.Equal(relief, to.Z - from.Z, 5);
        var caster = factory.Spawn(sim, "loosed_kindler", from, new SpawnOptions { RollRarity = false });
        var target = factory.Spawn(sim, pattern, to, new SpawnOptions { RollRarity = false });
        sim.World.Remove<Behavior>(caster);
        sim.World.Remove<Behavior>(target);
        var events = new List<GameEvent>();
        sim.Events.SubscribeAll(events.Add);
        var slot = sim.World.Get<WeaveBook>(caster)!.Add(new WeaveSpec { Preset = "ember_bolt" });
        Assert.True(sim.Service<WeaveRuntime>().TryBegin(sim, caster, slot, target, to, Vector2.UnitX, out var reason), reason);
        sim.RunSeconds(3);
        var flights = events.OfType<EntitySpawned>().Count(e => e.Pattern == "projectile");
        var hits = events.OfType<DamageDealt>().Count(e => e.Source == caster && e.Target == target && e.Total > 0);
        output.WriteLine($"{pattern}: relief={relief}, authored height={sim.World.Get<Transform>(target)?.Height}, flights={flights}, hits={hits}");
        Assert.True(flights > 0, "The real weave must launch a projectile.");
        Assert.True(hits > 0, $"{pattern} was missed by the real bolt at relief {relief}.");
    }

    private sealed class Relief(WorldLayout layout, float delta) : IChunkProvider
    {
        public ChunkData Load(ChunkCoord coord)
        {
            var chunk = new FlatChunkProvider(layout.ChunkSize, 0, TagSet.Of("ground", "arena")).Load(coord);
            var origin = layout.ChunkOrigin(coord);
            for (var y = 0; y < chunk.Size; y++)
                for (var x = 0; x < chunk.Size; x++)
                    chunk.Height[chunk.Index(x, y)] = 2 + (origin.X + x - 10) * delta / 6;
            return chunk;
        }
    }
}
