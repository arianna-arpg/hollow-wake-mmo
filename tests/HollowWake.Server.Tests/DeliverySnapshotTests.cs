using System.Numerics;
using HollowWake.Core.Components;
using HollowWake.Core.Combat;
using HollowWake.Core.Definitions;
using HollowWake.Core.Patterns;
using HollowWake.Core.Weaves;
using HollowWake.Core.World;
using HollowWake.Core.Content;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Maths;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;
using HollowWake.Server.World;
using Xunit;

namespace HollowWake.Server.Tests;

/// <summary>
/// Deliveries as the wire draws them: every shaped body carries its resolved shape (angles in radians, the hollow in
/// world units, a bowed shape's curvature), its facing and a flight's spin and velocity; tethers are bars laid along
/// their line; telegraph marks are circles that say when they land.
/// </summary>
public class DeliverySnapshotTests
{
    private static readonly Lazy<ContentRegistry> Content = new(() =>
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "content", "core")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        var registry = ContentRegistry.Load(new[] { Path.Combine(dir!, "content", "core") }, out var report);
        Assert.False(report.HasErrors, report.ToString());
        return registry;
    });

    private static Simulation Arena() => ZoneBuilder.CreateArena(Content.Value, 5);

    private static Entity Body(Simulation sim, Vector3 at, float yaw, float radius, string look, TagSet? tags = null)
    {
        var e = sim.World.Create();
        sim.World.Add(e, new Transform { Position = at, Yaw = yaw, Radius = radius });
        sim.World.Add(e, new Identity { Name = look, Look = look, Tags = tags ?? TagSet.Empty });
        sim.Spatial.Update(e, at);
        return e;
    }

    private static EffectContext Context(Simulation sim) => new() { Sim = sim, Rng = new Rng(1) };

    private static Entity Flight(Simulation sim, string shape, ShapeSize size, float heading = 0, float spin = 0)
    {
        var e = Body(sim, new Vector3(4, 4, 1), heading, 0.4f, "bolt");
        sim.World.Add(e, new Projectile
        {
            Context = Context(sim),
            Spec = new ProjectileSpec { Shape = Shapes.Get(shape) },
            Size = size,
            Heading = heading,
            SpinAngle = spin,
            Velocity = new Vector3(4, 2, 0),
        });
        return e;
    }

    [Fact]
    public void AFlightCarriesItsShapeFacingSpinAndVelocity()
    {
        var sim = Arena();
        var cone = Flight(sim, "sector", new ShapeSize(3, 60, 0, 0), heading: 0.3f, spin: 0.5f);
        var state = Snapshots.Capture(sim, cone);
        Assert.False(state.Static);
        Assert.Equal("sector", state.Shape);
        Assert.Equal(3, state.ShapeSize!.Radius);
        Assert.Equal(1.05f, state.ShapeSize.Angle!.Value, 2);
        Assert.Null(state.ShapeSize.Width);
        Assert.Equal(0.3f, state.Heading);
        Assert.Equal(0.5f, state.Spin);
        Assert.Equal((4f, 2f), (state.VX, state.VY));

        // A plain flight is a circle of its collision radius.
        var plain = Body(sim, new Vector3(6, 4, 1), 0, 0.4f, "bolt");
        sim.World.Add(plain, new Projectile { Context = Context(sim), Radius = 0.6f, Velocity = new Vector3(-3, 0, 0) });
        var round = Snapshots.Capture(sim, plain);
        Assert.Equal("circle", round.Shape);
        Assert.Equal(0.6f, round.ShapeSize!.Radius);
        Assert.Equal(-3f, round.VX);

        // A turn of the spin is a change a client sees, carried by a delta.
        var before = Snapshots.Fingerprint(sim, cone);
        sim.World.Get<Projectile>(cone)!.SpinAngle = 1.2f;
        Assert.NotEqual(before, Snapshots.Fingerprint(sim, cone));
        var delta = Snapshots.Delta(state, Snapshots.Capture(sim, cone), out var empty);
        Assert.False(empty);
        Assert.Equal(1.2f, delta!.Spin);
    }

    [Fact]
    public void SizesArriveResolvedWithTheShapesDefaults()
    {
        var sim = Arena();
        // A crescent with no size beyond its reach: 180 degrees, half hollow.
        var crescent = Snapshots.Capture(sim, Flight(sim, "crescent", new ShapeSize(2, 0, 0, 0))).ShapeSize!;
        Assert.Equal(3.14f, crescent.Angle!.Value, 2);
        Assert.Equal(1f, crescent.Inner);
        // An arc and a wave: their angles, a width, and how far each bows.
        var arc = Snapshots.Capture(sim, Flight(sim, "arc", new ShapeSize(2, 0, 0, 0))).ShapeSize!;
        Assert.Equal(1.57f, arc.Angle!.Value, 2);
        Assert.True(arc.Width > 0);
        Assert.Equal(1f, arc.Curvature);
        var wave = Snapshots.Capture(sim, Flight(sim, "wave", new ShapeSize(2, 0, 0, 0))).ShapeSize!;
        Assert.Equal(2.09f, wave.Angle!.Value, 2);
        Assert.Equal(2f, wave.Curvature);
        // A band is as wide as it is long.
        Assert.Equal(2f, Snapshots.Capture(sim, Flight(sim, "band", new ShapeSize(2, 0, 0, 0))).ShapeSize!.Width);
        // A size content resolved passes unchanged: a sector it made 45 degrees stays 45.
        Assert.Equal(0.79f, Snapshots.Capture(sim, Flight(sim, "sector", new ShapeSize(2, 45, 0, 0))).ShapeSize!.Angle!.Value, 2);
    }

    [Fact]
    public void ARingFieldAndATetherAreDrawnByRegisteredShapes()
    {
        var sim = Arena();
        var ring = Body(sim, new Vector3(8, 8, 0), 0.4f, 3, "ring");
        sim.World.Add(ring, new Field { Context = Context(sim), Radius = 3, Size = new ShapeSize(3, 0, 0, 0.5f), Facing = 0.4f });
        var field = Snapshots.Capture(sim, ring);
        Assert.Equal("circle", field.Shape);
        // The hollow travels in world units: half of a radius of three.
        Assert.Equal(1.5f, field.ShapeSize!.Inner);
        Assert.Equal(0.4f, field.Heading);

        // A tether's transform sits at the midpoint along its line; it is drawn as a bar laid along that line.
        var line = Body(sim, new Vector3(10, 10, 0), 0.25f, 4, "tether");
        sim.World.Add(line, new Tether { Context = Context(sim), Width = 1.5f });
        Assert.False(Snapshots.IsStatic(sim, line));
        var tether = Snapshots.Capture(sim, line);
        Assert.Equal("bar", tether.Shape);
        Assert.Equal(4, tether.ShapeSize!.Radius);
        Assert.Equal(1.5f, tether.ShapeSize.Width);
        Assert.Equal(MathF.Round((0.25f + MathF.PI / 2) * 100) / 100, tether.Heading);
    }

    [Fact]
    public void ATelegraphMarkIsSentOnceWithWhenItLands()
    {
        var sim = Arena();
        var mark = Body(sim, new Vector3(12, 12, 0), 0, 2.5f, "telegraph", TagSet.Of("telegraph"));
        sim.World.Add(mark, new Lifetime { ExpiresAt = sim.Clock.Time + 1.5 });
        Assert.True(Snapshots.IsStatic(sim, mark));
        var state = Snapshots.Capture(sim, mark);
        Assert.True(state.Static);
        Assert.Equal("telegraph", state.Look);
        Assert.Equal("circle", state.Shape);
        Assert.Equal(2.5f, state.ShapeSize!.Radius);
        Assert.Equal(Math.Round(sim.Clock.Time + 1.5, 2), state.Until);
        // A changed end cannot ride a delta.
        Assert.Null(Snapshots.Delta(state, state with { Until = state.Until + 1 }, out _));
    }
    public static IEnumerable<object[]> FloorSnapshots() =>
        new[] { "firebolt", "piercing_arrow" }.SelectMany(preset =>
            new[] { -.125f, 0, .125f }.SelectMany(slope =>
                new[] { 0, 1 }.Select(afterContact => new object[] { preset, slope, afterContact })));

    [Theory, MemberData(nameof(FloorSnapshots))]
    public void FloorSnapshotMotionMatchesTheConstrainedPathWithoutChangingPitch(string preset, float slope, int afterContact)
    {
        var (sim, e, p) = FloorFlight(preset, slope);
        sim.Run(afterContact);
        var t = sim.World.Get<Transform>(e)!;
        var raw = p.Velocity;
        var carrier = p.Carrier;
        var traveled = p.Traveled;
        var odometer = p.Odometer;
        var chunks = sim.Terrain.LoadedChunkCount;
        Assert.True(raw.Z < 0);
        var state = Snapshots.Capture(sim, e);
        var fingerprint = Snapshots.Fingerprint(sim, e);
        _ = Snapshots.Delta(state, Snapshots.Capture(sim, e), out var unchanged);
        Assert.True(unchanged);
        Assert.Equal(fingerprint, Snapshots.Fingerprint(sim, e));
        Assert.Equal(raw, p.Velocity);
        Assert.Equal(carrier, p.Carrier);
        Assert.Equal(traveled, p.Traveled);
        Assert.Equal(odometer, p.Odometer);
        Assert.Equal(chunks, sim.Terrain.LoadedChunkCount);
        Assert.InRange(state.VZ - slope * raw.X, -.01f, .01f); // existing centimetre velocity quantization

        // Both clients extrapolate position + wire velocity * elapsed time.
        const float ahead = .1f;
        var projected = new Vector3(state.X + state.VX * ahead, state.Y + state.VY * ahead, state.Z + state.VZ * ahead);
        var floor = sim.Terrain.HeightAt(projected.X, projected.Y) + p.GroundFloor!.Value;
        Assert.InRange(projected.Z - floor, -.02f, .02f); // existing centimetre wire precision
        var next = new Vector3(state.X, state.Y, state.Z) + new Vector3(state.VX, state.VY, state.VZ) * (float)sim.Clock.Dt;
        sim.Run(1);
        Assert.InRange(Vector3.Distance(next, t.Position), 0, .02f);
        Assert.Equal(raw, p.Velocity);
        if (slope < 0) Assert.True(t.Position.Z < carrier.Z);
    }

    [Fact]
    public void FloorVelocityChangesAreCapturedAndFingerprintTheSameWireState()
    {
        var (sim, e, p) = FloorFlight("firebolt", 0);
        var first = Snapshots.Capture(sim, e);
        var fingerprint = Snapshots.Fingerprint(sim, e);
        Assert.Equal(0, first.VZ);
        p.Velocity.Z = 3; // an external deflection lifts the flight from its floor
        var next = Snapshots.Capture(sim, e);
        Assert.Equal(3, next.VZ);
        Assert.NotEqual(fingerprint, Snapshots.Fingerprint(sim, e));
        var delta = Snapshots.Delta(first, next, out var empty);
        Assert.False(empty);
        Assert.Equal(3, delta!.VZ);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void OrdinaryGravityAndReturningSnapshotsKeepTheirRawVelocity(bool gravity, bool returning)
    {
        var sim = Arena();
        var e = Flight(sim, "circle", new ShapeSize(.1f, 0, 0, 0));
        var p = sim.World.Get<Projectile>(e)!;
        p.Velocity = new(12, 0, -8);
        p.Spec.Gravity = gravity;
        p.Returning = returning;
        p.GroundFloor = gravity || returning ? .342f : null;
        Assert.Equal(-8, Snapshots.Capture(sim, e).VZ);
    }

    [Fact]
    public void FloorObservationDoesNotLoadUnseenTerrain()
    {
        var (sim, e, p) = FloorFlight("firebolt", 0);
        var t = sim.World.Get<Transform>(e)!;
        t.Position = new Vector3(10000, 10000, .342f);
        p.Carrier = t.Position;
        var count = sim.Terrain.LoadedChunkCount;
        var raw = p.Velocity;
        var state = Snapshots.Capture(sim, e);
        _ = Snapshots.Fingerprint(sim, e);
        Assert.Equal(count, sim.Terrain.LoadedChunkCount);
        Assert.Equal(raw, p.Velocity);
        Assert.Equal((0f, 0f, 0f), (state.VX, state.VY, state.VZ));
    }

    [Theory]
    [InlineData(-.25, -.25)]
    [InlineData(0, 0)]
    [InlineData(31.75, -.25)]
    public void LoadedTerrainObservationInterpolatesAcrossChunkBoundaries(double x, double y)
    {
        var sim = Arena();
        foreach (var cx in new[] { -1, 0, 1 })
        foreach (var cy in new[] { -1, 0 })
        {
            var chunk = sim.Terrain.GetChunk(new ChunkCoord(cx, cy));
            for (var ly = 0; ly < chunk.Size; ly++)
            for (var lx = 0; lx < chunk.Size; lx++)
                chunk.Height[chunk.Index(lx, ly)] = (cx * chunk.Size + lx + .5f) * .2f + (cy * chunk.Size + ly + .5f) * .3f;
        }
        var count = sim.Terrain.LoadedChunkCount;
        Assert.True(sim.Terrain.TryHeightAt(x, y, out var height));
        Assert.Equal(x * .2 + y * .3, height, 5);
        Assert.Equal(count, sim.Terrain.LoadedChunkCount);
        sim.Terrain.Unload(new ChunkCoord(-1, -1));
        count = sim.Terrain.LoadedChunkCount;
        Assert.False(sim.Terrain.TryHeightAt(-.25, -.25, out _));
        Assert.Equal(count, sim.Terrain.LoadedChunkCount);
    }
    [Theory]
    [InlineData(1.2f, false)]
    [InlineData(3f, false)]
    [InlineData(1.2f, true)]
    [InlineData(3f, true)]
    public void LayeredFloorSnapshotsKeepSupportAndDoNotPullFlightsOntoOverheadDecks(float height, bool underneath)
    {
        var (sim, e, p) = FloorFlight("firebolt", 0);
        var chunk = sim.Terrain.GetChunk(new ChunkCoord(0, 0));
        for (var i = 0; i < chunk.Height.Length; i++)
            chunk.AddLayer(i, new Surface(height, true, TagSet.Of("structure", "bridge")));
        var t = sim.World.Get<Transform>(e)!;
        if (!underneath) t.Position += new Vector3(0, 0, height);
        p.Carrier = t.Position;
        var raw = p.Velocity;
        var carrier = p.Carrier;
        var count = sim.Terrain.LoadedChunkCount;
        var state = Snapshots.Capture(sim, e);
        var hash = Snapshots.Fingerprint(sim, e);
        Assert.Equal(hash, Snapshots.Fingerprint(sim, e));
        _ = Snapshots.Delta(state, Snapshots.Capture(sim, e), out var unchanged);
        Assert.True(unchanged);
        Assert.Equal(raw, p.Velocity);
        Assert.Equal(carrier, p.Carrier);
        Assert.Equal(count, sim.Terrain.LoadedChunkCount);
        Assert.Equal(0, state.VZ);
        sim.Run(1);
        Assert.Equal(carrier.Z, t.Position.Z, 4);
        Assert.Equal(raw, p.Velocity);
    }
    private static (Simulation Sim, Entity Entity, Projectile Flight) FloorFlight(string preset, float slope)
    {
        var sim = Arena();
        var chunk = sim.Terrain.GetChunk(new ChunkCoord(0, 0));
        for (var y = 0; y < chunk.Size; y++)
        for (var x = 0; x < chunk.Size; x++) chunk.Height[chunk.Index(x, y)] = slope * (x + .5f - 10);
        chunk.Modified = true;
        var caster = sim.Service<PatternFactory>().Spawn(sim, "loosed_kindler", new Vector3(10, 10, 0), new SpawnOptions { RollRarity = false });
        sim.World.Remove<Behavior>(caster);
        sim.World.Get<Transform>(caster)!.Height = 1.8f;
        sim.World.Add(caster, new HitBody { Definition = sim.Content.Get<HitBodyDef>(sim.Content.Rules.Text("body.player_hit_profile", ""))! });
        HitBodies.InvalidateBounds(sim);
        var slot = sim.World.Get<WeaveBook>(caster)!.Add(new WeaveSpec { Preset = preset });
        Assert.True(sim.Service<WeaveRuntime>().TryBegin(sim, caster, slot, Entity.None, new(11, 10, slope), Vector2.UnitX, out var reason), reason);
        for (var i = 0; i < 40; i++)
        {
            sim.Run(1);
            foreach (var (e, p) in sim.World.Query<Projectile>())
            {
                var at = sim.World.Get<Transform>(e)!.Position;
                if (p.GroundFloor is { } floor && Math.Abs(at.Z - sim.Terrain.HeightAt(at.X, at.Y) - floor) < .00001f)
                    return (sim, e, p);
            }
        }
        throw new InvalidOperationException("Expected the free shot to reach its floor.");
    }
}
