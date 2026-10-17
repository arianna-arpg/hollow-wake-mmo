using System.Numerics;
using HollowWake.Core.Components;
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
}
