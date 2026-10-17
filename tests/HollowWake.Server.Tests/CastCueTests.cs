using System.Numerics;
using HollowWake.Core.Components;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;
using HollowWake.Core.Patterns;
using HollowWake.Core.Weaves;
using HollowWake.Protocol;
using HollowWake.Server;
using HollowWake.Server.Config;
using HollowWake.Server.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HollowWake.Server.Tests;

public class CastCueTests
{
    private static string ContentDir()
    {
        for (var dir = AppContext.BaseDirectory; dir is not null; dir = Path.GetDirectoryName(dir))
            if (Directory.Exists(Path.Combine(dir, "content/core"))) return Path.Combine(dir, "content/core");
        throw new DirectoryNotFoundException();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cues_are_opt_in_ordered_and_redact_unknown_targets(bool optedIn)
    {
        var transport = new LoopbackTransport();
        var stores = new MemoryStores();
        await using var server = new GameServer(new ServerConfig { ContentLayers = new() { ContentDir() }, WorldEvents = false,
            SnapshotRate = 20 }, NullLoggerFactory.Instance, transport, stores, stores, stores);
        await server.StartAsync(CancellationToken.None);
        var client = transport.Connect();
        client.Send(new HelloMsg("cast-test", ProtocolInfo.Version) { Features = optedIn ? new() { ProtocolInfo.CastCuesFeature } : null });
        client.Send(new LoginMsg("cast-test", ""));
        server.Step();
        var welcome = Assert.Single(client.Drain().OfType<WelcomeMsg>());
        Assert.Equal(optedIn, welcome.Features?.Contains(ProtocolInfo.CastCuesFeature) == true);
        client.Send(new CreateCharacterMsg("Flame", "kindler"));
        server.Step();
        var character = Assert.Single(Assert.Single(client.Drain().OfType<CharacterListMsg>()).Characters);
        client.Send(new SelectCharacterMsg(character.Id));
        server.Step();
        client.Drain();
        var session = Assert.Single(server.Sessions.Sessions);
        var sim = session.Zone!.Sim;
        Assert.Equal(optedIn, sim.EmitCastPresentation);
        var at = sim.World.Get<Transform>(session.Entity)!.Position;
        var enemy = sim.Service<PatternFactory>().Spawn(sim, "hob", at + new Vector3(4, 0, 0));
        sim.World.Remove<Behavior>(enemy);
        server.Step();
        client.Drain();
        var book = sim.World.Get<WeaveBook>(session.Entity)!;
        Assert.True(sim.Service<WeaveRuntime>().TryBegin(sim, session.Entity, book.Slots[0], enemy,
            sim.World.Get<Transform>(enemy)!.Position, Vector2.UnitX, out var reason), reason);
        var castId = book.Casting!.CastId;
        server.Step();
        var start = client.Drain().OfType<CastCueMsg>().Where(c => c.CastId == castId).ToArray();
        if (optedIn) Assert.Equal("start", Assert.Single(start).Phase);
        else Assert.Empty(start);
        // A fresh interest view, also used on zone entry, reconstructs preparation only.
        session.View.Reset();
        server.Step();
        var synced = client.Drain().OfType<CastCueMsg>().Where(c => c.CastId == castId).ToArray();
        if (optedIn) Assert.Equal("sync", Assert.Single(synced).Phase);
        else Assert.Empty(synced);
        for (var tick = 0; tick < 25; tick++) server.Step();
        var ended = client.Drain().OfType<CastCueMsg>().Where(c => c.CastId == castId).ToArray();
        if (optedIn)
        {
            Assert.Contains(ended, c => c.Phase == "release");
            Assert.Contains(ended, c => c.Phase == "end" && c.Fired);
        }
        else Assert.Empty(ended);
        var hidden = sim.World.Create();
        sim.World.Add(hidden, new Transform { Position = at + new Vector3(500, 500, 0) });
        var e = new CastPresentationEvent(42, "start", session.Entity, hidden, "test", "Test", "cast", TagSet.Of("ember"),
            at, at + new Vector3(500, 500, 0), sim.Clock.Time, 1, sim.Clock.Time, false, "");
        sim.Events.Publish(e);
        sim.Events.Publish(e with { Phase = "release", Fired = true });
        sim.Events.Publish(e with { Phase = "impact", Fired = true });
        sim.Events.Publish(e with { Phase = "end", Fired = true });
        server.Step();
        var cues = client.Drain().OfType<CastCueMsg>().Where(c => c.CastId == 42).ToArray();
        if (!optedIn) Assert.Empty(cues);
        else
        {
            Assert.Equal(new[] { "start", "release", "end" }, cues.Select(c => c.Phase));
            Assert.True(cues.Zip(cues.Skip(1), (a, b) => a.Sequence < b.Sequence).All(x => x));
            Assert.All(cues, c => { Assert.Equal(0, c.Target); Assert.Equal(c.X, c.AimX); Assert.Equal(c.Y, c.AimY); });
            // A hidden source is silent even when its damage target is the local player.
            sim.Events.Publish(e with { Caster = hidden, Target = session.Entity, Phase = "impact" });
            server.Step();
            Assert.DoesNotContain(client.Drain(), m => m is CastCueMsg c && c.CastId == 42);
        }
        // Outcomes do not fabricate a successful cast or expose an unknown source/target.
        sim.Events.Publish(new Healed(session.Entity, session.Entity, 5, "life"));
        sim.Events.Publish(new Healed(session.Entity, session.Entity, 0, "life"));
        sim.Events.Publish(new Healed(hidden, session.Entity, 5, "life"));
        sim.Events.Publish(new Healed(session.Entity, hidden, 5, "life"));
        sim.Events.Publish(new DamageDealt(session.Entity, session.Entity, 0, false, TagSet.Empty, new Dictionary<string, double>(), false, true));
        sim.Events.Publish(new DamageDealt(session.Entity, session.Entity, 0, false, TagSet.Empty, new Dictionary<string, double>(), true, false));
        sim.Events.Publish(new DamageDealt(session.Entity, session.Entity, 5, false, TagSet.Empty, new Dictionary<string, double>(), false, false));
        sim.Events.Publish(new DamageDealt(session.Entity, session.Entity, 5, false, TagSet.Empty, new Dictionary<string, double>(), false, false) { CastId = 99 });
        sim.Events.Publish(new StatusApplied(session.Entity, session.Entity, "burning", 1, 1));
        sim.Events.Publish(new StatusExpired(session.Entity, "burning"));
        server.Step();
        var outcomes = client.Drain().OfType<CastCueMsg>().Where(c => c.Outcome).ToArray();
        if (!optedIn) Assert.Empty(outcomes);
        else
        {
            Assert.Equal(new[] { "heal", "block", "evade", "damage", "status", "status_expired" }, outcomes.Select(e => e.Phase));
            Assert.All(outcomes, e => { Assert.Equal(0, e.CastId); Assert.Equal(session.Entity.Key, e.Target); Assert.False(e.Fired); });
            Assert.Contains("harmful", Assert.Single(outcomes, e => e.Phase == "status").Tags);
        }
        session.Zone.Remove(session);
        Assert.False(sim.EmitCastPresentation);
        await server.ShutdownAsync();
    }
}
