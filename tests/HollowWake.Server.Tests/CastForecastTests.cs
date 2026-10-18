using System.Numerics;
using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Patterns;
using HollowWake.Core.Weaves;
using HollowWake.Protocol;
using HollowWake.Server;
using HollowWake.Server.Config;
using HollowWake.Server.Net;
using HollowWake.Server.World;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HollowWake.Server.Tests;

public class CastForecastTests
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
    public async Task Forecasts_use_resolved_live_geometry_and_repair_interest_without_exposing_hidden_targets(bool optedIn)
    {
        var transport = new LoopbackTransport(); var stores = new MemoryStores();
        await using var server = new GameServer(new ServerConfig { ContentLayers = new() { ContentDir() }, WorldEvents = false, SnapshotRate = 20 },
            NullLoggerFactory.Instance, transport, stores, stores, stores);
        await server.StartAsync(CancellationToken.None);
        var client = transport.Connect();
        client.Send(new HelloMsg("forecast", ProtocolInfo.Version) { Features = optedIn ? new() { ProtocolInfo.CastForecastFeature } : null });
        client.Send(new LoginMsg("forecast", "")); server.Step();
        var welcome = Assert.Single(client.Drain().OfType<WelcomeMsg>());
        Assert.Equal(optedIn, welcome.Features?.Contains(ProtocolInfo.CastForecastFeature) == true);
        client.Send(new CreateCharacterMsg("Forecast", "kindler")); server.Step();
        var character = Assert.Single(Assert.Single(client.Drain().OfType<CharacterListMsg>()).Characters);
        client.Send(new SelectCharacterMsg(character.Id)); server.Step();
        var entered = client.Drain(); var enter = Assert.Single(entered.OfType<EnterWorldMsg>());
        Assert.Equal(optedIn, enter.ForecastEpoch is not null);
        var session = Assert.Single(server.Sessions.Sessions); var sim = session.Zone!.Sim; var runtime = sim.Service<WeaveRuntime>();
        var book = sim.World.Get<WeaveBook>(session.Entity)!; var at = sim.World.Get<Transform>(session.Entity)!.Position;
        var slot = book.Add(new WeaveSpec { Preset = "cleave" });
        Assert.True(runtime.TryBegin(sim, session.Entity, slot, Entity.None, at + new Vector3(2, 0, 0), Vector2.UnitX, out var reason), reason);
        var id = book.Casting!.CastId; server.Step();
        var first = client.Drain().OfType<CastForecastMsg>().ToArray();
        if (!optedIn) Assert.Empty(first);
        else
        {
            var state = Assert.Single(first.Last().Casts, c => c.CastId == id);
            Assert.Equal(enter.ForecastEpoch, first.Last().Epoch);
            Assert.Equal("primary_candidate", state.Coverage); Assert.NotNull(state.Region);
            Assert.Equal(state.StartedAt, state.NextEmissionStartedAt);
            var plan = runtime.DescribeCast(sim, session.Entity)!.Delivery.PrimaryRegion!.Value;
            Assert.Equal(plan.Size.Radius, state.Region.Size.Radius);
            Assert.Equal(plan.Size.Inner * plan.Size.Radius, state.Region.Size.Inner);
            Assert.Equal(MathF.Sqrt(plan.Shape.CenterAllowanceSquared), state.Region.CenterAllowance);
            Assert.Equal(plan.Anchor.X, state.Region.X); Assert.Equal(0, state.Region.Heading);
            Assert.Equal(plan.BodyContact ? "body" : "center", state.Region.TargetTest);
            Assert.Equal(plan.BodyContact ? plan.Bottom : (float?)null, state.Region.Bottom);
            Assert.Equal(plan.BodyContact ? plan.Top : (float?)null, state.Region.Top);
            // Exact unchanged data uses its original revision; a reset repairs a fresh observer.
            var builder = new CastForecastBuilder(sim);
            var view = new SnapshotView(); view.Known.UnionWith(session.View.Known);
            var baseline = builder.Build(view, session.Entity, .05);
            var compact = builder.Build(view, session.Entity, .05);
            Assert.DoesNotContain(compact.Casts, c => c.CastId == id);
            Assert.Contains(compact.Retained!, r => r.CastId == id && r.Revision == baseline.Sequence);
            view.Reset(); view.Known.UnionWith(session.View.Known);
            Assert.Contains(builder.Build(view, session.Entity, .05).Casts, c => c.CastId == id);
            var neighbor = sim.Service<PatternFactory>().Spawn(sim, "hob", at + new Vector3(1, 0, 0));
            sim.World.Remove<Behavior>(neighbor);
            var neighborSlot = sim.World.Get<WeaveBook>(neighbor)!.Add(new WeaveSpec { Preset = "cleave" });
            Assert.True(runtime.TryBegin(sim, neighbor, neighborSlot, Entity.None, at + new Vector3(3, 0, 0), Vector2.UnitX, out reason), reason);
            var boundedView = new SnapshotView(); boundedView.Known.UnionWith(new[] { session.Entity.Key, neighbor.Key });
            var bounded = new CastForecastBuilder(sim, new() { MaxCasts = 1 });
            var nearSelf = bounded.Build(boundedView, session.Entity, .05);
            Assert.Equal(session.Entity.Key, Assert.Single(nearSelf.Casts).Source); Assert.Equal(1, nearSelf.Omitted);
            var nearNeighbor = bounded.Build(boundedView, neighbor, .05);
            Assert.Equal(neighbor.Key, Assert.Single(nearNeighbor.Casts).Source); Assert.Equal(1, nearNeighbor.Omitted);
            runtime.Interrupt(sim, neighbor);
            sim.World.Get<Stats>(session.Entity)!.Attributes.Add(new Modifier { Attribute = "area.radius", Value = 2 });
            session.View.Reset(); server.Step();
            var repaired = Assert.Single(client.Drain().OfType<CastForecastMsg>()).Casts.Single(c => c.CastId == id);
            Assert.True(repaired.Region!.Size.Radius > state.Region.Size.Radius);
            Assert.Equal(state.StartedAt, repaired.StartedAt);
        }
        runtime.Interrupt(sim, session.Entity); server.Step();
        var ended = client.Drain();
        if (optedIn) Assert.DoesNotContain(Assert.Single(ended.OfType<CastForecastMsg>()).Casts, c => c.CastId == id);
        // A stored target outside interest cannot reveal its position through an area anchor or expression.
        var hidden = sim.Service<PatternFactory>().Spawn(sim, "hob", at + new Vector3(500, 0, 0)); sim.World.Remove<Behavior>(hidden);
        var other = book.Add(new WeaveSpec { Preset = "cleave" });
        Assert.True(runtime.TryBegin(sim, session.Entity, other, Entity.None, at + new Vector3(2, 0, 0), Vector2.UnitX, out reason), reason);
        book.Casting!.Target = hidden; server.Step();
        var redacted = client.Drain().OfType<CastForecastMsg>().ToArray();
        if (optedIn)
        {
            var c = Assert.Single(redacted).Casts.Single(c => c.Source == session.Entity.Key);
            Assert.Null(c.Region); Assert.Equal("hidden_target", c.UnavailableReason); Assert.Equal(at.X, c.X);
        }
        else Assert.Empty(redacted);
        session.Zone.Remove(session); Assert.False(sim.EmitCastPresentation);
        await server.ShutdownAsync();
    }
}
