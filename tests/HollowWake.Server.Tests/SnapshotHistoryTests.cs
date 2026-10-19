using System.Numerics;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Ecs;
using HollowWake.Core.Sim;
using HollowWake.Protocol;
using HollowWake.Server.Config;
using HollowWake.Server.World;
using Xunit;

namespace HollowWake.Server.Tests;

public class SnapshotHistoryTests
{
    private static readonly Lazy<ContentRegistry> Content = new(() =>
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "content", "core")))
            dir = Path.GetDirectoryName(dir);
        var registry = ContentRegistry.Load(new[] { Path.Combine(dir!, "content", "core") }, out var report);
        Assert.False(report.HasErrors, report.ToString());
        return registry;
    });

    private static (Simulation Sim, Entity Body, SnapshotBuilder Builder, SnapshotView A, SnapshotView B, EntityState Initial) Scene()
    {
        var sim = ZoneBuilder.CreateArena(Content.Value);
        var body = sim.World.Create();
        sim.World.Add(body, new Transform());
        sim.World.Add(body, new Motion());
        sim.Spatial.Update(body, Vector3.Zero);
        var builder = new SnapshotBuilder(sim, 48);
        // Keep these small history scenarios away from repair slots.
        var settings = new SnapshotSettings { RefreshSeconds = 1_000_000 };
        var a = new SnapshotView { SendDeltas = true, Settings = settings };
        var b = new SnapshotView { SendDeltas = true, Settings = settings };
        var initial = Assert.IsType<EntitySpawnMsg>(Assert.Single(Frame(builder, a, body))).State;
        Assert.IsType<EntitySpawnMsg>(Assert.Single(Frame(builder, b, body)));
        return (sim, body, builder, a, b, initial);
    }

    private static List<Message> Frame(SnapshotBuilder builder, SnapshotView view, Entity body)
    {
        var messages = new List<Message>();
        builder.Entities(view, body, messages);
        return messages;
    }

    private static EntityDelta Delta(SnapshotBuilder builder, SnapshotView view, Entity body) =>
        Assert.Single(Assert.IsType<EntityDeltaMsg>(Assert.Single(Frame(builder, view, body))).Deltas);

    private static void Move(Simulation sim, Entity body, float x, float y)
    {
        var at = new Vector3(x, y, 0);
        sim.World.Get<Transform>(body)!.Position = at;
        sim.Spatial.Update(body, at);
    }

    private static void Matches(Simulation sim, Entity body, EntityState state) =>
        Assert.Equal(MessageSerializer.ToJson(new EntitySpawnMsg(Snapshots.Capture(sim, body))),
            MessageSerializer.ToJson(new EntitySpawnMsg(state)));

    [Fact]
    public void IdenticalHistoriesShareAnUnchangedDeltaAcrossViewers()
    {
        var (sim, body, builder, a, b, initial) = Scene();
        sim.Clock.Advance();
        Move(sim, body, 1, 0);
        var first = Delta(builder, a, body);
        var second = Delta(builder, b, body);
        Assert.Same(first, second);
        Matches(sim, body, first.ApplyTo(initial));
        Matches(sim, body, second.ApplyTo(initial));

        sim.Clock.Advance();
        Move(sim, body, 2, 0);
        var later = Delta(builder, a, body);
        Assert.Equal(1, first.X); // A later frame never mutates a queued message.
        Assert.Equal(2, later.X);
    }

    [Fact]
    public void AViewerThatSkippedAFrameStillReceivesEveryMissingField()
    {
        var (sim, body, builder, a, b, initial) = Scene();
        sim.Clock.Advance();
        Move(sim, body, 1, 0);
        var advanced = Delta(builder, a, body).ApplyTo(initial);
        sim.Clock.Advance();
        Move(sim, body, 1, 2);
        var current = Delta(builder, a, body);
        var catchingUp = Delta(builder, b, body);
        Assert.Null(current.X);
        Assert.Equal(1, catchingUp.X);
        Matches(sim, body, current.ApplyTo(advanced));
        Matches(sim, body, catchingUp.ApplyTo(initial));
    }

    [Fact]
    public void DiagnosticViewCannotReuseAnOrdinaryViewsDeltaWithTheSameHistory()
    {
        var (sim, body, builder, a, b, initial) = Scene();
        sim.World.Add(body, new HollowWake.Core.Combat.HitBody { Definition = new HollowWake.Core.Definitions.HitBodyDef
            { Id = "diagnostic", Volumes = new() { new() { Shape = "cylinder", Up = .5f, Radius = .2f, HalfHeight = .5f } } } });
        sim.Clock.Advance();
        Move(sim, body, 1, 0);
        b.SendHitBodies = true;
        var ordinary = Delta(builder, a, body);
        var diagnostic = Delta(builder, b, body);
        Assert.Null(ordinary.ApplyTo(initial).HitBody);
        Assert.Equal("diagnostic", diagnostic.ApplyTo(initial).HitBody);
        Assert.NotSame(ordinary, diagnostic);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidationOrPrecisionChangeCannotReuseAnEarlierStateInTheSameTick(bool precisionChange)
    {
        var (sim, body, builder, a, b, initial) = Scene();
        sim.Clock.Advance();
        Move(sim, body, 1, 0);
        var advanced = Delta(builder, a, body).ApplyTo(initial);
        Move(sim, body, 1, 2);
        if (precisionChange)
            b.Settings = b.Settings with { CastSteps = b.Settings.CastSteps + 1 };
        else
            builder.Invalidate();

        var other = Delta(builder, b, body);
        Assert.Equal(2, other.Y);
        Matches(sim, body, other.ApplyTo(initial));
        Matches(sim, body, Delta(builder, a, body).ApplyTo(advanced));
    }
}
