using Microsoft.Extensions.Logging.Abstractions;
using HollowWake.Core.Components;
using HollowWake.Protocol;
using HollowWake.Server.Config;
using HollowWake.Server.Net;
using Xunit;

namespace HollowWake.Server.Tests;

/// <summary>Minions over the wire: a hero summons, and a command message reaches its minions through the minion system.</summary>
public class MinionLoopbackTests
{
    private static string ContentDir()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, "content", "core");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            dir = Path.GetDirectoryName(dir);
        }
        throw new DirectoryNotFoundException("content/core");
    }

    private static List<Message> Pump(GameServer server, LoopbackClient client, int ticks = 1)
    {
        for (var i = 0; i < ticks; i++)
        {
            server.Step();
        }
        return client.Drain();
    }

    [Fact]
    public void ACommandFromTheWireOrdersTheHerosMinions()
    {
        var config = new ServerConfig { ContentLayers = new List<string> { ContentDir() }, WorldEvents = false, AutosaveSeconds = 1e9, SnapshotRate = 20 };
        var transport = new LoopbackTransport();
        var stores = new MemoryStores();
        var server = new GameServer(config, NullLoggerFactory.Instance, transport, stores, stores, stores);
        server.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        var client = transport.Connect();
        client.Send(new HelloMsg("test", ProtocolInfo.Version));
        client.Send(new LoginMsg("summoner", ""));
        Pump(server, client);
        client.Send(new CreateCharacterMsg("Vesper", "shade"));
        var character = Assert.Single(Pump(server, client).OfType<CharacterListMsg>().Single().Characters);
        client.Send(new SelectCharacterMsg(character.Id));
        var enter = Assert.Single(Pump(server, client, 3).OfType<EnterWorldMsg>());

        // The shade's fourth weave calls a remnant.
        client.Send(new UseWeaveMsg(3, 0, enter.X, enter.Y, enter.Z, 0, 0));
        var replies = Pump(server, client, 40);
        var spawn = replies.OfType<EntitySpawnMsg>().Select(s => s.State).First(s => s.Pattern == "remnant_shade");
        var sim = server.Zones.Zones.Values.Single(z => z.Sessions.Count > 0).Sim;
        var hero = sim.World.Resolve(enter.EntityKey);
        var minion = sim.World.Resolve(spawn.Key);
        Assert.Equal(hero, sim.World.Get<Minion>(minion)!.Owner);

        client.Send(new CommandMinionsMsg("hold", 0, enter.X + 6, enter.Y, enter.Z));
        Pump(server, client, 2);
        var order = sim.World.Get<Behavior>(minion)!.Order;
        Assert.NotNull(order);
        Assert.Equal("hold", order!.Command);
        Assert.Equal(enter.X + 6, order.Point.X, 3);

        // A command the content does not define is ignored by the minion system; the standing order stays.
        client.Send(new CommandMinionsMsg("dance", 0, enter.X - 6, enter.Y, enter.Z));
        Pump(server, client, 2);
        Assert.Equal("hold", sim.World.Get<Behavior>(minion)!.Order?.Command);
    }
}
