using System.Numerics;
using System.Reflection;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Maths;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;
using HollowWake.Core.Weaves;
using HollowWake.Core.World;
using Xunit;

namespace HollowWake.Core.Tests;

public class HeldGroundForecastTests
{
    private static readonly Lazy<ContentRegistry> Content = new(() =>
    {
        var content = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out var report);
        Assert.False(report.HasErrors, report.ToString());
        foreach (var targeting in new[] { Targeting.Ground, Targeting.Direction })
            foreach (var mode in new[] { "charge", "over", "timed" })
                content.With(CastForecastContent.Registry.Get<FormDef>("forecast_" + mode)! with
                {
                    Id = Key(targeting, mode), Targeting = targeting,
                    Effects = new() { CastForecastContent.Spec("""{"type":"area","center":"point","radius":2,"effects":[{"type":"event","name":"held_ground_contact"}]}""") },
                });
        return content;
    });

    private static string Key(Targeting targeting, string mode) => $"held_surface_{targeting}_{mode}";

    private static (CastingArena Arena, WeaveSlot Slot) Begin(Targeting targeting, string mode, Vector3 point, bool staleAim = false)
    {
        var a = CastingArena.Create(registry: Content.Value);
        SurfaceAt(a, point, 3);
        if (staleAim)
        {
            Assert.True(a.Runtime.Aim(a.Sim, a.Caster, new(16, 10, 0), Vector2.UnitY));
            a.Sim.Run(1);
        }
        var slot = a.Slot(new WeaveSpec { Form = Key(targeting, mode) });
        Assert.True(a.Runtime.TryBegin(a.Sim, a.Caster, slot, Entity.None, point, Vector2.UnitX, out var reason), reason);
        return (a, slot);
    }

    private static void SurfaceAt(CastingArena a, Vector3 point, float? height)
    {
        var (chunk, index) = a.Sim.Terrain.Locate(new((int)MathF.Floor(point.X), (int)MathF.Floor(point.Y)));
        if (height is { } z) chunk.Layers[index] = new() { new(z, true, TagSet.Of("bridge")) };
        else chunk.Layers.Remove(index);
    }

    [Theory]
    [InlineData(Targeting.Ground, "charge", false)]
    [InlineData(Targeting.Ground, "charge", true)]
    [InlineData(Targeting.Direction, "charge", false)]
    [InlineData(Targeting.Direction, "charge", true)]
    [InlineData(Targeting.Ground, "over", false)]
    [InlineData(Targeting.Ground, "over", true)]
    [InlineData(Targeting.Direction, "over", false)]
    [InlineData(Targeting.Direction, "over", true)]
    public void ChangedLayerIsResolvedAgainWithoutObservationChangingState(Targeting targeting, string mode, bool remove)
    {
        var point = new Vector3(13, 10, 3);
        var (a, slot) = Begin(targeting, mode, point);
        var cast = a.Book.Casting!;
        Assert.True(a.Runtime.Aim(a.Sim, a.Caster, point, Vector2.UnitY));
        Assert.Equal(3, a.Book.AimPoint!.Value.Z);
        cast.AimOffset = new(0, 0, 1.25f);
        SurfaceAt(a, point, remove ? null : 4);
        // Point the terrain memo elsewhere: an observational generating query would move it back.
        a.Sim.Terrain.GetChunk(new(2, 2));
        var expected = point with { Z = remove ? 0 : 4 };
        var forecast = ObservePure(a);
        Assert.Equal(expected, forecast.Context.Point);
        Assert.Equal("primary", forecast.Coverage);
        Assert.Equal(Vector3.Zero, forecast.Context.AimOffset);
        Assert.Equal(new Vector3(0, 0, 1.25f), cast.AimOffset);
        Assert.Equal(point, a.Book.AimPoint);
        Assert.True(a.Runtime.Release(a.Sim, a.Caster, a.SlotIndex(slot)));
        Assert.Equal(expected, Assert.Single(a.Of<WeaveCast>()).Point);
        Assert.Equal(expected, cast.Point);
        Assert.Equal(forecast.Context.Direction, cast.Direction);
        Assert.Equal(Vector3.Zero, cast.AimOffset);
    }

    [Theory]
    [InlineData(Targeting.Ground, false)]
    [InlineData(Targeting.Direction, false)]
    [InlineData(Targeting.Ground, true)]
    [InlineData(Targeting.Direction, true)]
    public void MissingTerrainDeclinesGeometryUntilRuntimeReleaseLoadsIt(Targeting targeting, bool neighbourOnly)
    {
        var point = new Vector3(neighbourOnly ? 32 : 13, 10, 3);
        var (a, slot) = Begin(targeting, "charge", point);
        Assert.True(a.Runtime.Aim(a.Sim, a.Caster, point, Vector2.UnitX));
        SurfaceAt(a, point, 4);
        var terrain = a.Sim.Terrain;
        var missing = terrain.Layout.ChunkOf(new(neighbourOnly ? 31 : 13, 10));
        terrain.Unload(missing);
        if (neighbourOnly) Assert.True(terrain.IsLoaded(terrain.Layout.ChunkOf(new(32, 10))));
        var forecast = ObservePure(a);
        Assert.Equal("unavailable", forecast.Coverage);
        Assert.Equal("unloaded_release_surface", forecast.Delivery.UnavailableReason);
        Assert.NotNull(forecast.ReadyAt);
        Assert.False(terrain.IsLoaded(missing));
        Assert.True(a.Runtime.Release(a.Sim, a.Caster, a.SlotIndex(slot)));
        Assert.True(terrain.IsLoaded(missing));
        Assert.Equal(neighbourOnly ? 4 : 0, Assert.Single(a.Of<WeaveCast>()).Point.Z);
    }

    [Theory]
    [InlineData(Targeting.Ground, true)]
    [InlineData(Targeting.Direction, true)]
    [InlineData(Targeting.Ground, false)]
    [InlineData(Targeting.Direction, false)]
    public void StaleOrAbsentStreamDoesNotResnapTheCommittedPoint(Targeting targeting, bool stale)
    {
        var point = new Vector3(13, 10, 3);
        var (a, slot) = Begin(targeting, "charge", point, staleAim: stale);
        var cast = a.Book.Casting!;
        cast.AimOffset = new(0, 0, 1.25f);
        SurfaceAt(a, point, null);
        a.Sim.Terrain.Clear();
        var forecast = ObservePure(a);
        Assert.Equal("primary", forecast.Coverage);
        Assert.Equal(point, forecast.Context.Point);
        Assert.Equal(cast.AimOffset, forecast.Context.AimOffset);
        Assert.True(a.Runtime.Release(a.Sim, a.Caster, a.SlotIndex(slot)));
        Assert.Equal(point, Assert.Single(a.Of<WeaveCast>()).Point);
    }

    [Theory]
    [InlineData(Targeting.Ground)]
    [InlineData(Targeting.Direction)]
    public void OrdinaryTimedCastsKeepTheirCommittedPointAndDirection(Targeting targeting)
    {
        var point = new Vector3(13, 10, 3);
        var (a, _) = Begin(targeting, "timed", point);
        Assert.True(a.Runtime.Aim(a.Sim, a.Caster, new(16, 10, 0), Vector2.UnitY));
        SurfaceAt(a, point, null);
        var forecast = ObservePure(a);
        Assert.Equal(point, forecast.Context.Point);
        Assert.Equal(Vector2.UnitX, forecast.Context.Direction);
        a.RunSeconds(1);
        Assert.Equal(point, Assert.Single(a.Of<WeaveCast>()).Point);
    }

    private static CastForecast ObservePure(CastingArena a)
    {
        var cast = a.Book.Casting!;
        var memo = typeof(Terrain).GetField("_last", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var before = State(a);
        var last = memo.GetValue(a.Sim.Terrain);
        var events = a.Events.ToArray();
        var scope = Hidden(cast, "Scope");
        var modifiers = Hidden(cast, "_modifiers");
        CastForecast? forecast = null;
        for (var i = 0; i < 4; i++)
        {
            forecast = a.Runtime.DescribeCast(a.Sim, a.Caster);
            Assert.Equal(before, State(a));
            Assert.Same(cast, a.Book.Casting);
            Assert.Same(scope, Hidden(cast, "Scope"));
            Assert.Same(modifiers, Hidden(cast, "_modifiers"));
            Assert.Same(last, memo.GetValue(a.Sim.Terrain));
            Assert.Equal(events, a.Events);
        }
        return Assert.IsType<CastForecast>(forecast);
    }

    private static string State(CastingArena a) => string.Join("|", Scalars(a.Book.Casting!), Scalars(a.Book),
        Scalars(a.Sim.Rng), Hidden(a.Book.Casting!, "Rng") is { } rng ? Scalars(rng) : "no cast RNG",
        a.Sim.Terrain.LoadedChunkCount, a.Sim.Scheduler.Pending,
        string.Join(",", a.Sim.Terrain.LoadedChunks.Select(c => c.Coord)),
        string.Join(",", a.Stats.Resources), string.Join(",", a.Book.ReadyAt));

    private static string Scalars(object value) => string.Join(";", value.GetType()
        .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        .Where(f => f.FieldType.IsValueType || f.FieldType == typeof(string))
        .OrderBy(f => f.Name).Select(f => f.Name + "=" + f.GetValue(value)));

    private static object? Hidden(object value, string name) => value.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(value);
}
