using System.Numerics;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Maths;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;
using HollowWake.Core.World;
using HollowWake.Core.WorldGen;
using Xunit;
using Xunit.Abstractions;

namespace HollowWake.Core.Tests;

public class LayeredProjectileGroundTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("watch_tower", "firebolt")]
    [InlineData("watch_tower", "piercing_arrow")]
    [InlineData("wreck_hull", "firebolt")]
    [InlineData("wreck_hull", "piercing_arrow")]
    public void RealShippedFloorsSupportFreeShotsBeyondANearCursor(string landmark, string preset)
    {
        var (a, start, floor) = Arena(landmark);
        var target = a.Body(start.X, start.Y + 2.25f, pattern: "goblin_skirmisher");
        Move(a, target, new(start.X, start.Y + 2.25f, floor));
        var flights = Capture(a);
        Press(a, preset, start + new Vector3(0, .35f, 0));
        var minimum = float.PositiveInfinity;
        for (var i = 0; i < 45; i++)
        {
            a.Tick();
            foreach (var (_, t, p) in a.Sim.World.Query<Transform, Projectile>())
                if (!p.Returning && t.Position.Y <= start.Y + 2.25f) minimum = MathF.Min(minimum, t.Position.Z);
        }
        var flight = Assert.Single(flights);
        output.WriteLine($"{landmark}/{preset}: deck={floor}, start={flight.Origin}, minimum={minimum}");
        Assert.True(minimum >= floor, $"Flight fell below the real {landmark} floor: {minimum} < {floor}");
        Assert.Contains(a.Events.OfType<DamageDealt>(), hit => hit.Target == target && hit.Total > 0);
    }

    [Theory]
    [InlineData("watch_tower")]
    [InlineData("wreck_hull")]
    public void DirectionInputHeightIsResolvedToAnActualSurface(string landmark)
    {
        var (a, start, floor) = Arena(landmark);
        var flights = Capture(a);
        Press(a, "firebolt", start + new Vector3(0, .35f, 1000));
        for (var i = 0; i < 40 && flights.Count == 0; i++) a.Tick();
        var p = Assert.Single(flights);
        Assert.Equal(floor, p.Context.Point.Z, 4);
        Assert.True(p.Velocity.Z < 0);
    }

    [Theory]
    [InlineData("watch_tower")]
    [InlineData("wreck_hull")]
    public void StreamedAimUsesTheSameSurfaceResolution(string landmark)
    {
        var (a, start, floor) = Arena(landmark);
        Assert.True(a.Sim.Service<WeaveRuntime>().Aim(a.Sim, a.Caster, start + new Vector3(0, .35f, 1000), Vector2.UnitY));
        Assert.Equal(floor, a.Sim.World.Get<WeaveBook>(a.Caster)!.AimPoint!.Value.Z, 4);
    }

    [Fact]
    public void HoveredAimHeightComesFromTheBodyAndInvalidInputIsRefused()
    {
        var (a, start, floor) = Arena("watch_tower");
        var target = a.Body(start.X, start.Y + 2);
        Move(a, target, start + new Vector3(0, 2, 1)); // a body above the surface has authoritative elevation too
        var slot = a.Sim.World.Get<WeaveBook>(a.Caster)!.Add(new WeaveSpec { Preset = "firebolt" });
        var runtime = a.Sim.Service<WeaveRuntime>();
        Assert.False(runtime.TryBegin(a.Sim, a.Caster, slot, Entity.None, new(float.NaN, start.Y, floor), Vector2.UnitY, out var reason));
        Assert.Equal("invalid_aim", reason);
        Assert.True(runtime.TryBegin(a.Sim, a.Caster, slot, target, start + new Vector3(0, 2, 1000), Vector2.UnitY, out reason), reason);
        Assert.Equal(floor + 1, a.Sim.World.Get<WeaveBook>(a.Caster)!.Casting!.Point.Z, 4);
    }
    private static (DeliveryArena Arena, Vector3 Start, float Floor) Arena(string landmark)
    {
        // Pin the unmodified shipped landmark, then use the production generation pass to stamp it.
        var content = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out _);
        content.With(new PoiDef { Id = "ground_review", Weight = 0, Radius = 8, Landmarks = { landmark } });
        content.With(new ZoneOverrideDef { Id = "ninewells", X = 0, Y = 0,
            Pois = { new PoiPlacement { Poi = "ground_review", X = 16, Y = 16, Landmark = landmark } } });
        var generated = new WorldGenerator(content, Rng.Hash64("ground-review")).GenerateChunk(new ChunkCoord(0, 0));
        Assert.Contains(generated.Stamps, stamp => stamp.Landmark == landmark);
        var a = DeliveryArena.Create(seed: 481);
        var chunk = a.Sim.Terrain.GetChunk(new ChunkCoord(0, 0));
        Array.Copy(generated.Height, chunk.Height, chunk.Height.Length);
        Array.Copy(generated.Flags, chunk.Flags, chunk.Flags.Length);
        Array.Copy(generated.SurfaceTag, chunk.SurfaceTag, chunk.SurfaceTag.Length);
        chunk.TagTable.Clear(); chunk.TagTable.AddRange(generated.TagTable);
        foreach (var (index, surfaces) in generated.Layers)
            foreach (var surface in surfaces) chunk.AddLayer(index, surface);
        chunk.Modified = true;
        var layers = chunk.Layers.SelectMany(pair => pair.Value
            .Where(surface => surface.Tags.Has(landmark == "watch_tower" ? "tower" : "deck"))
            .Select(surface => (X: pair.Key % chunk.Size, Y: pair.Key / chunk.Size, surface.Height))).ToArray();
        Assert.NotEmpty(layers);
        var floor = layers[0].Height;
        var start = new Vector3(layers.Min(l => l.X) + .5f, layers.Min(l => l.Y) + .2f, floor);
        Move(a, a.Caster, start);
        a.Sim.World.Get<Transform>(a.Caster)!.Height = 1.8f;
        a.Sim.World.Add(a.Caster, new HitBody { Definition = a.Sim.Content.Get<HitBodyDef>(a.Sim.Content.Rules.Text("body.player_hit_profile", ""))! });
        HitBodies.InvalidateBounds(a.Sim);
        Assert.True(a.Sim.Terrain.TryFindGround(start.X, start.Y, floor, 0, out var support, out _));
        Assert.Equal(floor, support.Height);
        return (a, start, floor);
    }

    private static void Move(DeliveryArena a, Entity entity, Vector3 at)
    {
        a.Sim.World.Get<Transform>(entity)!.Position = at;
        a.Sim.Spatial.Update(entity, at);
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
        Assert.True(a.Sim.Service<WeaveRuntime>().TryBegin(a.Sim, a.Caster, slot, Entity.None, point, Vector2.UnitY, out var reason), reason);
    }
}
