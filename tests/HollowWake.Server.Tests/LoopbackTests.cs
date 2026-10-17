using Microsoft.Extensions.Logging.Abstractions;
using HollowWake.Core.Characters;
using HollowWake.Protocol;
using HollowWake.Server;
using HollowWake.Server.Config;
using HollowWake.Server.Net;
using HollowWake.Server.Persistence;
using Xunit;

namespace HollowWake.Server.Tests;

/// <summary>Stores that live in memory so tests leave nothing on disk.</summary>
internal sealed class MemoryStores : IAccountStore, ICharacterStore, IWorldStore
{
    private readonly Dictionary<string, Account> _accounts = new();
    private readonly Dictionary<string, CharacterSheet> _characters = new();
    private WorldSave? _world;
    private int _ids;

    public Account? Load(string id) => _accounts.GetValueOrDefault(id);
    public void Save(Account account) => _accounts[account.Id] = account;
    CharacterSheet? ICharacterStore.Load(string id) => _characters.GetValueOrDefault(id);
    public void Save(CharacterSheet sheet) => _characters[sheet.Id] = sheet;
    public string NewId() => "test" + (++_ids);
    public WorldSave? Load() => _world;
    public void Save(WorldSave save) => _world = save;
    public Core.World.ChunkData? LoadChunk(Core.World.ChunkCoord coord) => null;
    public void SaveChunk(Core.World.ChunkData chunk) { }
}

public class LoopbackTests
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

    private static (GameServer Server, LoopbackTransport Transport) Create()
    {
        var config = new ServerConfig { ContentLayers = new List<string> { ContentDir() }, WorldEvents = false, AutosaveSeconds = 1e9, SnapshotRate = 20 };
        var transport = new LoopbackTransport();
        var stores = new MemoryStores();
        var server = new GameServer(config, NullLoggerFactory.Instance, transport, stores, stores, stores);
        server.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        return (server, transport);
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
    public void LoginCreateEnterMoveAndSee()
    {
        var (server, transport) = Create();
        var client = transport.Connect();
        client.Send(new HelloMsg("test", ProtocolInfo.Version));
        client.Send(new LoginMsg("tester", ""));
        var replies = Pump(server, client);
        Assert.Contains(replies, m => m is WelcomeMsg);
        var login = Assert.Single(replies.OfType<LoginResultMsg>());
        Assert.True(login.Ok, login.Error);
        var list = Assert.Single(replies.OfType<CharacterListMsg>());
        Assert.Empty(list.Characters);
        Assert.Contains("kindler", list.Callings);

        client.Send(new CreateCharacterMsg("Wren", "kindler"));
        replies = Pump(server, client);
        list = Assert.Single(replies.OfType<CharacterListMsg>());
        var character = Assert.Single(list.Characters);
        Assert.Equal("Wren", character.Name);

        client.Send(new SelectCharacterMsg(character.Id));
        replies = Pump(server, client, 3);
        var enter = Assert.Single(replies.OfType<EnterWorldMsg>());
        Assert.NotEqual(0UL, enter.World.Seed);
        Assert.Contains(replies, m => m is ZoneChangedMsg);
        Assert.Contains(replies, m => m is EntitySpawnMsg s && s.State.Key == enter.EntityKey && s.State.IsPlayer);
        Assert.Contains(replies, m => m is SelfStateMsg self && self.Weaves.Count > 0 && self.Bag.Count + self.Equipment.Count > 0);

        var startX = enter.X;
        client.Send(new MoveDirMsg(1, 0));
        replies = Pump(server, client, 40);
        var updates = replies.OfType<EntityUpdateMsg>().SelectMany(u => u.States).Where(s => s.Key == enter.EntityKey).ToList();
        Assert.NotEmpty(updates);
        Assert.True(updates.Last().X > startX + 1, $"did not move: {updates.Last().X} vs {startX}");

        client.Send(new JumpMsg());
        replies = Pump(server, client, 10);
        Assert.Contains(replies.OfType<EntityUpdateMsg>().SelectMany(u => u.States), s => s.Key == enter.EntityKey && (s.Z > 0.05f || !s.Grounded));

        client.Send(new ChatMsg("zone", "hello weft"));
        replies = Pump(server, client, 2);
        Assert.Contains(replies, m => m is ChatBroadcastMsg c && c.Text == "hello weft" && c.From == "Wren");

        client.Send(new PingMsg(42));
        replies = Pump(server, client);
        Assert.Contains(replies, m => m is PongMsg p && p.Nonce == 42);

        client.Disconnect();
        server.Step();
        Assert.Empty(server.Sessions.Sessions);
    }

    [Fact]
    public void RejectsBadProtocolAndUnauthenticatedMessages()
    {
        var (server, transport) = Create();
        var client = transport.Connect();
        client.Send(new MoveDirMsg(1, 0));
        var replies = Pump(server, client);
        Assert.Contains(replies, m => m is ErrorMsg e && e.Code == "not_logged_in");
        client.Send(new HelloMsg("old", 0));
        replies = Pump(server, client);
        Assert.Contains(replies, m => m is DisconnectMsg);
        Assert.False(client.IsOpen);
    }

    [Fact]
    public void FramingRoundTrips()
    {
        var reader = new MessageSerializer.FrameReader();
        var a = MessageSerializer.Frame(MessageSerializer.Encode(new PingMsg(1)));
        var b = MessageSerializer.Frame(MessageSerializer.Encode(new ChatMsg("zone", "hi")));
        var all = a.Concat(b).ToArray();
        reader.Append(all.AsSpan(0, 5));
        Assert.False(reader.TryRead(out _));
        reader.Append(all.AsSpan(5));
        Assert.True(reader.TryRead(out var first));
        Assert.True(reader.TryRead(out var second));
        Assert.False(reader.TryRead(out _));
        Assert.IsType<PingMsg>(MessageSerializer.Decode(first));
        Assert.Equal("hi", Assert.IsType<ChatMsg>(MessageSerializer.Decode(second)).Text);
    }
}
