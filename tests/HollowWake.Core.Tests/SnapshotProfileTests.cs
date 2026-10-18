using System.Numerics;
using System.Text.Json;
using HollowWake.Core.Characters;
using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Items;
using HollowWake.Core.Sim;
using HollowWake.Protocol;
using HollowWake.Server.World;
using HollowWake.Tools;
using Xunit;

namespace HollowWake.Core.Tests;

public sealed class SnapshotProfileTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void ProfilingPreservesPayloadOrderBytesCadenceAndViewState(bool deltas, bool panels, bool extra)
    {
        // Independent worlds retain identical lazy compilation/read history. Sharing one would let
        // the reference Self call compile slots before the diagnostic's first fingerprint sees them.
        var (sim, hero, body) = Scene();
        var (reference, referenceHero, referenceBody) = Scene();
        var builder = new SnapshotBuilder(sim, 48);
        var referenceBuilder = new SnapshotBuilder(reference, 48);
        var ordinary = new SnapshotView { SendDeltas = deltas, SendInterface = panels };
        var profiled = new SnapshotView { SendDeltas = deltas, SendInterface = panels };
        var expected = new List<Message>();
        var actual = new List<Message>();
        var extension = extra ? new ExtraStream() : null;
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        using var profile = new SnapshotProfile(path, "tick");
        profile.Prepare(new SnapshotProfile.Workload("test", 0, 1, 0, 0, false, 16));
        profile.BeginPhase("test", sim.Clock.Tick, new[] { profiled });
        for (var round = 0; round < 16; round++)
        {
            sim.Clock.Advance(); sim.Clock.Advance();
            reference.Clock.Advance(); reference.Clock.Advance();
            var position = new Vector3(round == 7 ? 1000 : round, 0, 0);
            sim.World.Get<Transform>(body)!.Position = position;
            sim.Spatial.Update(body, position);
            reference.World.Get<Transform>(referenceBody)!.Position = position;
            reference.Spatial.Update(referenceBody, position);
            var tick = sim.Clock.Tick;
            var entityCount = sim.World.Count;
            var eventCount = sim.Events.Log.Count;
            var state = MessageSerializer.ToJson(new EntitySpawnMsg(Snapshots.Capture(sim, hero)));
            var before = SnapshotProfile.Counts.Read(ordinary);

            expected.Clear();
            referenceBuilder.Entities(ordinary, referenceHero, expected);
            var entityEnd = expected.Count;
            if (round % 5 == 0 && referenceBuilder.Self(ordinary, referenceHero) is { } self) expected.Add(self);
            var selfEnd = expected.Count;
            if (panels) referenceBuilder.Panels(ordinary, referenceHero, expected);
            var expectedBytes = expected.Select(m => (long)MessageSerializer.Encode(m).Length).ToArray();
            var extraMessage = extra ? new EntityDespawnMsg(-5) : null;
            var extraBytes = extraMessage is null ? 0 : MessageSerializer.Encode(extraMessage).Length;

            var frame = profile.Measure(sim, builder, profiled, hero, actual, round, 0, extension, out var actualExtra);
            Assert.Equal(expected.Select(MessageSerializer.ToJson), actual.Select(MessageSerializer.ToJson));
            Assert.Equal(extraMessage is null ? null : MessageSerializer.ToJson(extraMessage),
                actualExtra is null ? null : MessageSerializer.ToJson(actualExtra));
            Assert.Equal(expectedBytes.Sum() + extraBytes, frame.Total.EncodedBytes);
            Assert.Equal(expectedBytes.Take(entityEnd).Sum(), frame.EntityEncoding.EncodedBytes);
            Assert.Equal(expectedBytes.Skip(entityEnd).Take(selfEnd - entityEnd).Sum(), frame.SelfEncoding.EncodedBytes);
            Assert.Equal(expectedBytes.Skip(selfEnd).Sum(), frame.PanelEncoding.EncodedBytes);
            Assert.Equal(extraBytes, frame.ExtensionEncoding.EncodedBytes);
            Assert.Equal(expected.Count + (extra ? 1 : 0), frame.Total.Messages);
            Assert.Equal(entityEnd, frame.Entities.Messages);
            Assert.Equal(selfEnd - entityEnd, frame.Self.Messages);
            Assert.Equal(expected.Count - selfEnd, frame.Panels.Messages);
            Assert.Equal(frame.Entities.Messages, frame.EntityEncoding.Messages);
            Assert.Equal(frame.Self.Messages, frame.SelfEncoding.Messages);
            Assert.Equal(frame.Panels.Messages, frame.PanelEncoding.Messages);
            Assert.Equal(extra ? 3 : 0, frame.Observations);
            Assert.Equal(extra ? "test-extra" : null, frame.Extension);
            Assert.Equal(round % 5 == 0, frame.SelfDue);
            if (!frame.SelfDue) Assert.Equal(default, frame.Self);
            if (!panels) Assert.Equal(default, frame.Panels);
            Assert.Equal(SnapshotProfile.Counts.Read(ordinary).Since(before), frame.Emitted);
            Assert.Equal(SnapshotProfile.Counts.Read(ordinary), SnapshotProfile.Counts.Read(profiled));
            Assert.Equal(ordinary.Frames, profiled.Frames);
            Assert.Equal(ordinary.Known.Order(), profiled.Known.Order());
            Assert.Equal(ordinary.Known.Count, frame.KnownAfter);
            Assert.Equal(tick, sim.Clock.Tick);
            Assert.Equal(entityCount, sim.World.Count);
            Assert.Equal(eventCount, sim.Events.Log.Count);
            Assert.Equal(state, MessageSerializer.ToJson(new EntitySpawnMsg(Snapshots.Capture(sim, hero))));
        }
        profile.EndPhase(sim.Clock.Tick);
        Assert.Equal(extra ? 16 : 0, extension?.Calls ?? 0);
        Assert.Equal(16, profile.Samples.Length);
        Assert.True(profiled.Spawns > 0);
        Assert.True(profiled.Despawns > 0);
        Assert.Equal(4, profiled.SelfStates + profiled.SelfStatesSkipped);
        Assert.Equal(panels, profiled.Panels > 0);
        Assert.True(deltas ? profiled.Deltas > 0 : profiled.Updates > 0);
        Assert.False(File.Exists(path)); // Measurement does not write during a phase.
    }

    private static (Simulation Sim, Entity Hero, Entity Body) Scene()
    {
        var sim = ZoneBuilder.CreateArena(CoreContent.Registry, 5);
        var characters = sim.Service<CharacterFactory>();
        var sheet = characters.Create(sim, "profile", "profile", "Profile", "holdfast", sim.Service<ItemGenerator>());
        var hero = characters.Materialize(sim, sheet);
        var body = sim.World.Create();
        sim.World.Add(body, new Transform());
        sim.World.Add(body, new Motion());
        sim.Spatial.Update(body, Vector3.Zero);
        return (sim, hero, body);
    }

    [Fact]
    public void ReportPreservesRawSamplesAndRefusesToOverwriteEvidence()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            using var profile = new SnapshotProfile(path, "tick");
            profile.Prepare(new SnapshotProfile.Workload("test", 0, 1, 1, 0, false, 1));
            var sim = ZoneBuilder.CreateArena(CoreContent.Registry);
            var body = sim.World.Create();
            sim.World.Add(body, new Transform());
            sim.Spatial.Update(body, Vector3.Zero);
            var view = new SnapshotView();
            profile.BeginPhase("full", sim.Clock.Tick, new[] { view });
            var frame = profile.Measure(sim, new SnapshotBuilder(sim, 48), view, body, new(), 0, 0, null, out _);
            Assert.Throws<InvalidOperationException>(profile.Save);
            Assert.False(File.Exists(path));
            profile.EndPhase(sim.Clock.Tick);
            profile.Save();
            var saved = File.ReadAllText(path);
            using var report = JsonDocument.Parse(saved);
            var root = report.RootElement;
            Assert.True(root.GetProperty("DiagnosticOnly").GetBoolean());
            Assert.Equal(1, root.GetProperty("Samples").GetArrayLength());
            Assert.Equal(frame.Total.EncodedBytes, root.GetProperty("Samples")[0].GetProperty("Total").GetProperty("EncodedBytes").GetInt64());
            Assert.Equal(1, root.GetProperty("Phases")[0].GetProperty("Samples").GetInt32());
            Assert.Throws<IOException>(profile.Save);
            Assert.Throws<IOException>(() => new SnapshotProfile(path, "tick"));
            Assert.Equal(saved, File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("", "tick")]
    [InlineData("--json", "tick")]
    [InlineData("unused.json", "expressions")]
    public void InvalidInvocationFailsBeforeAnyMeasurement(string path, string only) =>
        Assert.Throws<ArgumentException>(() => new SnapshotProfile(path, only));

    private sealed class ExtraStream : ISnapshotProfileExtension
    {
        public string Name => "test-extra";
        public int Calls { get; private set; }
        public SnapshotProfileExtra Build(SnapshotView view, Entity viewer)
        {
            Calls++;
            return new SnapshotProfileExtra(new EntityDespawnMsg(-5), 3);
        }
    }
}
