using Microsoft.Extensions.Logging.Abstractions;
using HollowWake.Core.Characters;
using HollowWake.Core.Components;
using HollowWake.Protocol;
using HollowWake.Server;
using HollowWake.Server.Config;
using HollowWake.Server.Net;
using HollowWake.Server.Persistence;
using HollowWake.Server.Sessions;
using HollowWake.Server.World;
using Xunit;
using Xunit.Abstractions;

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
    private readonly ITestOutputHelper _output;

    public LoopbackTests(ITestOutputHelper output)
    {
        _output = output;
    }

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

    // ------------------------------------------------------------ interest and deltas

    /// <summary>Logs in a new account, creates a character and enters the world; returns the welcome and what arrived on entering.</summary>
    private static (LoopbackClient Client, EnterWorldMsg Enter, Session Session, List<Message> Replies, WelcomeMsg Welcome) Enter(GameServer server, LoopbackTransport transport, string account, string name, List<string>? features = null)
    {
        var client = transport.Connect();
        client.Send(new HelloMsg("test", ProtocolInfo.Version) { Features = features });
        client.Send(new LoginMsg(account, ""));
        var welcome = Assert.Single(Pump(server, client).OfType<WelcomeMsg>());
        client.Send(new CreateCharacterMsg(name, "kindler"));
        var list = Assert.Single(Pump(server, client).OfType<CharacterListMsg>());
        client.Send(new SelectCharacterMsg(Assert.Single(list.Characters).Id));
        var replies = Pump(server, client, 3);
        var enter = Assert.Single(replies.OfType<EnterWorldMsg>());
        var session = server.Sessions.Sessions.Single(s => s.Character?.Name == name);
        return (client, enter, session, replies, welcome);
    }

    private static void Step(GameServer server, int ticks, params (LoopbackClient Client, List<Message> Inbox)[] clients)
    {
        for (var i = 0; i < ticks; i++)
        {
            server.Step();
            foreach (var (client, inbox) in clients)
            {
                inbox.AddRange(client.Drain());
            }
        }
    }

    [Fact]
    public void StaticEntitiesSpawnOnceAndNeverUpdate()
    {
        var (server, transport) = Create();
        var (client, enter, session, replies, welcome) = Enter(server, transport, "flora", "Fern");
        Assert.Null(welcome.Features);
        Assert.False(session.View.SendDeltas);
        var zone = session.Zone!;
        var spawns = replies.OfType<EntitySpawnMsg>().Select(s => s.State).ToList();
        Assert.NotEmpty(spawns);
        foreach (var s in spawns)
        {
            var e = zone.Sim.World.Resolve(s.Key);
            if (!e.IsNone)
            {
                Assert.Equal(Snapshots.IsStatic(zone.Sim, e), s.Static);
            }
        }
        var flora = spawns.Where(s => zone.Sim.World.Has<Prop>(zone.Sim.World.Resolve(s.Key))).ToList();
        Assert.NotEmpty(flora);
        Assert.All(flora, s => Assert.True(s.Static, $"{s.Name} ({s.Pattern}) arrived without the static flag"));
        Assert.Contains(spawns, s => s.Key == enter.EntityKey && !s.Static && s.IsPlayer);
        var statics = spawns.Where(s => s.Static).Select(s => s.Key).ToHashSet();

        // Walk out and back so interest changes and plenty of updates flow; no static entity may appear in one.
        var inbox = new List<Message>();
        client.Send(new MoveDirMsg(1, 0));
        Step(server, 40, (client, inbox));
        client.Send(new MoveDirMsg(-1, 0));
        Step(server, 40, (client, inbox));
        client.Send(new StopMsg());
        Step(server, 10, (client, inbox));
        var updated = inbox.OfType<EntityUpdateMsg>().SelectMany(u => u.States).ToList();
        Assert.Contains(updated, s => s.Key == enter.EntityKey);
        var allStatics = statics.Concat(inbox.OfType<EntitySpawnMsg>().Where(s => s.State.Static).Select(s => s.State.Key)).ToHashSet();
        Assert.True(allStatics.Count > statics.Count, "walking brought no new static entities into interest");
        Assert.DoesNotContain(updated, s => s.Static || allStatics.Contains(s.Key));
        // A static entity is spawned again only after it left interest (a despawn in between) or changed.
        var despawned = inbox.OfType<EntityDespawnMsg>().Select(d => d.Key).ToHashSet();
        var respawned = inbox.OfType<EntitySpawnMsg>().Where(s => statics.Contains(s.State.Key)).ToList();
        Assert.All(respawned, s => Assert.Contains(s.State.Key, despawned));
        _output.WriteLine($"{spawns.Count} spawns on entering ({statics.Count} static, {flora.Count} flora or props); {updated.Count} updated states while walking; {session.SnapshotsBuilt} snapshots, {session.SnapshotBytesPerSnapshot:0} snapshot bytes per snapshot");
    }

    [Fact]
    public void StandingPlayerGetsNoUpdatesForItselfWhileAMovingOneKeepsGettingThem()
    {
        var (server, transport) = Create();
        var (still, stillEnter, stillSession, _, _) = Enter(server, transport, "still", "Wren");
        var (mover, moverEnter, moverSession, _, _) = Enter(server, transport, "mover", "Ash");
        var stillInbox = new List<Message>();
        var moverInbox = new List<Message>();

        // The mover walks off; the standing player settles, and any refresh owed after settling goes out.
        mover.Send(new MoveDirMsg(1, 0));
        Step(server, 20, (still, stillInbox), (mover, moverInbox));
        mover.Send(new MoveDirMsg(0, 1));
        Step(server, 40, (still, stillInbox), (mover, moverInbox));
        stillInbox.Clear();
        moverInbox.Clear();
        var stillBytes = stillSession.SnapshotBytes;
        var stillSnapshots = stillSession.SnapshotsBuilt;

        // The window: the mover keeps walking (back the way it came, which is clear), the other stands still.
        mover.Send(new MoveDirMsg(0, -1));
        Step(server, 30, (still, stillInbox), (mover, moverInbox));
        var snapshots = stillSession.SnapshotsBuilt - stillSnapshots;
        var stillSelf = stillInbox.OfType<EntityUpdateMsg>().SelectMany(u => u.States).Count(s => s.Key == stillEnter.EntityKey);
        var stillSawMover = stillInbox.OfType<EntityUpdateMsg>().SelectMany(u => u.States).Count(s => s.Key == moverEnter.EntityKey);
        var moverSelf = moverInbox.OfType<EntityUpdateMsg>().SelectMany(u => u.States).Where(s => s.Key == moverEnter.EntityKey).ToList();
        var windowBytes = (stillSession.SnapshotBytes - stillBytes) / (double)Math.Max(1, snapshots);
        _output.WriteLine($"window of {snapshots} snapshots: standing player got {stillSelf} updates for itself and {stillSawMover} for the mover; the mover got {moverSelf.Count} for itself");
        _output.WriteLine($"standing player: {windowBytes:0} snapshot bytes per snapshot in the window, {stillSession.SnapshotBytesPerSnapshot:0} overall; mover: {moverSession.SnapshotBytesPerSnapshot:0} overall; self states {stillSession.View.SelfStates} sent, {stillSession.View.SelfStatesSkipped} skipped as unchanged");

        Assert.True(snapshots >= 25, $"only {snapshots} snapshots in the window");
        Assert.True(stillSelf <= 1, $"a standing player got {stillSelf} updates for itself");
        Assert.True(moverSelf.Count >= snapshots / 2, $"a moving player got only {moverSelf.Count} updates for itself in {snapshots} snapshots");
        Assert.True(moverSelf.Last().Y < moverSelf.First().Y, "the mover did not walk south in the window");
        Assert.True(stillSawMover >= snapshots / 2, $"the standing player saw the mover only {stillSawMover} times");
        Assert.True(stillSession.View.SelfStatesSkipped > 0, "the standing player's unchanged self state was never skipped");
        Assert.True(windowBytes < 25 * 1024, $"{windowBytes:0} snapshot bytes per snapshot is over the budget");
    }

    [Fact]
    public void SnapshotSettingsBindFromServerYaml()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hollowwake-snapshots-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, "snapshot_rate: 10\nsnapshots:\n  refresh_seconds: 3\n  sweep_seconds: 12\n  self_seconds: 0.25\n  self_max_seconds: 4\n  resource_step: 0.05\n  cast_steps: 4\n  interest_height: 32\n");
        try
        {
            var bound = ServerConfig.Load(path).Snapshots;
            Assert.Equal(new SnapshotSettings { RefreshSeconds = 3, SweepSeconds = 12, SelfSeconds = 0.25, SelfMaxSeconds = 4, ResourceStep = 0.05, CastSteps = 4, InterestHeight = 32 }, bound);
        }
        finally
        {
            File.Delete(path);
        }
        // The committed server settings load, and a settings section that is absent means the defaults.
        var committed = ServerConfig.Load(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(ContentDir()))!, "config", "server.yaml"));
        Assert.NotNull(committed.Snapshots);
        Assert.Equal(SnapshotSettings.Default, new ServerConfig().Snapshots);
        Assert.Equal(new SnapshotSettings { RefreshSeconds = 0.05, SweepSeconds = 0.05, SelfSeconds = 0.05, SelfMaxSeconds = 0.05, ResourceStep = 0, CastSteps = 1, InterestHeight = 0 },
            new SnapshotSettings { RefreshSeconds = 0, SweepSeconds = -1, SelfSeconds = 0, SelfMaxSeconds = 0, ResourceStep = -1, CastSteps = 0, InterestHeight = -5 }.Normalized());
    }

    [Fact]
    public void HelloFeaturesAreOptInAndAbsentWhenEmpty()
    {
        // A hello without features is byte-for-byte the plain protocol, empty or not.
        Assert.Equal("{\"t\":\"hello\",\"client_version\":\"test\",\"protocol\":1}", MessageSerializer.ToJson(new HelloMsg("test", ProtocolInfo.Version)));
        Assert.Equal("{\"t\":\"hello\",\"client_version\":\"test\",\"protocol\":1}", MessageSerializer.ToJson(new HelloMsg("test", ProtocolInfo.Version) { Features = new List<string>() }));
        var asked = MessageSerializer.ToJson(new HelloMsg("test", ProtocolInfo.Version) { Features = new List<string> { ProtocolInfo.EntityDeltaFeature } });
        Assert.Contains("\"features\":[\"entity_delta\"]", asked);
        var back = Assert.IsType<HelloMsg>(MessageSerializer.Decode(System.Text.Encoding.UTF8.GetBytes(asked)));
        Assert.Equal(new List<string> { ProtocolInfo.EntityDeltaFeature }, back.Features);
        Assert.Null(Assert.IsType<HelloMsg>(MessageSerializer.Decode(System.Text.Encoding.UTF8.GetBytes("{\"t\":\"hello\",\"client_version\":\"old\",\"protocol\":1}"))).Features);

        var (server, transport) = Create();
        var plain = transport.Connect();
        plain.Send(new HelloMsg("old", ProtocolInfo.Version));
        var welcome = Assert.Single(Pump(server, plain).OfType<WelcomeMsg>());
        Assert.Null(welcome.Features);
        Assert.DoesNotContain("features", MessageSerializer.ToJson(welcome));
        var unknown = transport.Connect();
        unknown.Send(new HelloMsg("future", ProtocolInfo.Version) { Features = new List<string> { "teleport" } });
        Assert.Null(Assert.Single(Pump(server, unknown).OfType<WelcomeMsg>()).Features);
        var asking = transport.Connect();
        asking.Send(new HelloMsg("new", ProtocolInfo.Version) { Features = new List<string> { "teleport", ProtocolInfo.EntityDeltaFeature } });
        Assert.Equal(new List<string> { ProtocolInfo.EntityDeltaFeature }, Assert.Single(Pump(server, asking).OfType<WelcomeMsg>()).Features);
        Assert.Equal(new[] { false, false, true }, server.Sessions.Sessions.OrderBy(s => s.Id).Select(s => s.View.SendDeltas).ToArray());
    }

    [Fact]
    public void DeltasCarryOnlyWhatChanged()
    {
        var (server, transport) = Create();
        var (client, enter, session, replies, welcome) = Enter(server, transport, "delta", "Sorrel", new List<string> { ProtocolInfo.EntityDeltaFeature });
        Assert.Equal(new List<string> { ProtocolInfo.EntityDeltaFeature }, welcome.Features);
        Assert.True(session.View.SendDeltas);
        var mirror = new Dictionary<long, EntityState>();
        var inbox = new List<Message>();
        client.Send(new MoveDirMsg(1, 0));
        Step(server, 30, (client, inbox));
        client.Send(new StopMsg());
        Step(server, 50, (client, inbox));

        var deltas = inbox.OfType<EntityDeltaMsg>().SelectMany(d => d.Deltas).ToList();
        var mine = deltas.Where(d => d.Key == enter.EntityKey).ToList();
        Assert.NotEmpty(mine);
        Assert.All(mine, d => Assert.Null(d.Name));
        Assert.Contains(mine, d => d.X is not null);

        // Applying spawns, full updates and deltas in order reproduces the state the server would send now.
        foreach (var m in replies.Concat(inbox))
        {
            switch (m)
            {
                case EntitySpawnMsg s:
                    mirror[s.State.Key] = s.State;
                    break;
                case EntityUpdateMsg u:
                    foreach (var s in u.States)
                    {
                        mirror[s.Key] = s;
                    }
                    break;
                case EntityDeltaMsg d:
                    foreach (var delta in d.Deltas)
                    {
                        if (mirror.TryGetValue(delta.Key, out var before))
                        {
                            mirror[delta.Key] = delta.ApplyTo(before);
                        }
                    }
                    break;
                case EntityDespawnMsg gone:
                    mirror.Remove(gone.Key);
                    break;
            }
        }
        var sim = session.Zone!.Sim;
        var now = Snapshots.Capture(sim, session.Entity);
        var seen = mirror[enter.EntityKey];
        Assert.Equal(now.X, seen.X);
        Assert.Equal(now.Y, seen.Y);
        Assert.Equal(now.VX, seen.VX);
        Assert.Equal(now.Flags.OrderBy(f => f).ToList(), seen.Flags.OrderBy(f => f).ToList());
        Assert.Equal(session.Known.OrderBy(k => k).ToList(), mirror.Keys.OrderBy(k => k).ToList());
        var deltaBytes = deltas.Count == 0 ? 0 : inbox.OfType<EntityDeltaMsg>().Sum(d => MessageSerializer.Encode(d).Length) / (double)deltas.Count;
        var fullBytes = inbox.OfType<EntityUpdateMsg>().SelectMany(u => u.States).Select(s => MessageSerializer.Encode(new EntityUpdateMsg(new List<EntityState> { s }, 0)).Length).DefaultIfEmpty(0).Average();
        _output.WriteLine($"{deltas.Count} deltas ({mine.Count} for itself), about {deltaBytes:0} bytes each against {fullBytes:0} for a full state; {session.SnapshotBytesPerSnapshot:0} snapshot bytes per snapshot");
    }

    [Fact]
    public void DeltaOfIdenticalStatesIsEmptyAndApplyingADeltaRoundTrips()
    {
        var a = new EntityState { Key = 7, Name = "Hob", X = 1, Y = 2, Casting = "Strike", CastProgress = 0.3f, Flags = { "moving" }, Resources = { ["life"] = 10, ["mana"] = 5 }, ResourceMax = { ["life"] = 20, ["mana"] = 5 } };
        Assert.NotNull(Snapshots.Delta(a, a with { }, out var empty));
        Assert.True(empty);
        var b = a with { X = 1.5f, Casting = null, CastProgress = 0, Flags = new List<string> { "stationary" }, Resources = new Dictionary<string, float> { ["life"] = 7, ["mana"] = 5 }, Dead = true };
        var delta = Snapshots.Delta(a, b, out empty)!;
        Assert.False(empty);
        Assert.Null(delta.Name);
        Assert.Null(delta.Y);
        Assert.Equal(1.5f, delta.X);
        Assert.Equal("", delta.Casting);
        Assert.Equal(new Dictionary<string, float> { ["life"] = 7 }, delta.Resources);
        Assert.Null(delta.ResourceMax);
        Assert.True(delta.Dead);
        var applied = delta.ApplyTo(a);
        Assert.Equal(MessageSerializer.ToJson(new EntitySpawnMsg(b)), MessageSerializer.ToJson(new EntitySpawnMsg(applied)));
        Assert.Same(a.ResourceMax, applied.ResourceMax);
        // A changed resource set cannot be expressed as a delta: the full state must go.
        Assert.Null(Snapshots.Delta(a, a with { Resources = new Dictionary<string, float> { ["life"] = 10 } }, out _));
        // Round trip through the wire format keeps absent fields absent.
        var wire = MessageSerializer.ToJson(new EntityDeltaMsg(new List<EntityDelta> { delta }, 1));
        Assert.DoesNotContain("\"name\"", wire);
        var back = Assert.IsType<EntityDeltaMsg>(MessageSerializer.Decode(MessageSerializer.Encode(new EntityDeltaMsg(new List<EntityDelta> { delta }, 1))));
        Assert.Equal(delta.X, back.Deltas[0].X);
        Assert.Null(back.Deltas[0].Y);
    }

    // ------------------------------------------------------------ the interface

    [Fact]
    public void InterfaceStatesGoToClientsThatAskAndToNoOthers()
    {
        var (server, transport) = Create();
        var (client, enter, session, replies, welcome) = Enter(server, transport, "panes", "Linden", new List<string> { ProtocolInfo.InterfaceFeature });
        Assert.Equal(new List<string> { ProtocolInfo.InterfaceFeature }, welcome.Features);
        Assert.True(session.View.SendInterface);
        var inventory = Assert.Single(replies.OfType<InventoryStateMsg>());
        Assert.True(inventory.Bag.Width > 0 && inventory.Bag.Height > 0);
        Assert.NotEmpty(inventory.Slots);
        Assert.Contains(inventory.Slots, s => s.Id == "main_hand");
        Assert.NotEmpty(inventory.Equipment);
        Assert.All(inventory.Bag.Items, i => Assert.True(i.X is not null && i.Y is not null && i.W >= 1 && i.H >= 1, $"{i.Name} has no place on the board"));
        var slots = Assert.Single(replies.OfType<WeaveSlotsMsg>());
        Assert.True(slots.Seats.Count >= 4);
        Assert.Equal(Enumerable.Range(0, slots.Seats.Count), slots.Seats.Select(s => s.Slot));
        Assert.Contains(slots.Seats, s => s.Key is not null && s.Name.Length > 0 && s.Preview.Count > 0);
        var passives = Assert.Single(replies.OfType<PassiveStateMsg>());
        Assert.Contains("start_kindler", passives.Allocated);
        Assert.True(passives.Points.ContainsKey("passive"));
        var pages = Assert.Single(replies.OfType<PageStateMsg>());
        Assert.Contains(pages.Pages, p => p.Id == "passives");

        // Nothing changed, so nothing is sent again; a move of an item would change the inventory state.
        var inbox = new List<Message>();
        Step(server, 30, (client, inbox));
        Assert.DoesNotContain(inbox, m => m is InventoryStateMsg or WeaveSlotsMsg or PassiveStateMsg or PageStateMsg);

        // A client that did not ask gets none of it, and its self states stay as they were.
        var (_, _, plainSession, plainReplies, plainWelcome) = Enter(server, transport, "plainpanes", "Rowan");
        Assert.Null(plainWelcome.Features);
        Assert.False(plainSession.View.SendInterface);
        Assert.DoesNotContain(plainReplies, m => m is InventoryStateMsg or WeaveSlotsMsg or PassiveStateMsg or PageStateMsg);
        var plainSelf = plainReplies.OfType<SelfStateMsg>().Last();
        Assert.Null(plainSelf.Pools);
        Assert.Null(plainSelf.Stamps);
        Assert.DoesNotContain("\"pools\"", MessageSerializer.ToJson(plainSelf));
        _output.WriteLine($"interface states on entering: inventory {MessageSerializer.Encode(inventory).Length} bytes, slots {MessageSerializer.Encode(slots).Length}, passives {MessageSerializer.Encode(passives).Length}, pages {MessageSerializer.Encode(pages).Length}");
    }

    [Fact]
    public void MinionCommandsBecomeEventsForTheMinionSystem()
    {
        var (server, transport) = Create();
        var (client, _, session, _, _) = Enter(server, transport, "commander", "Briar");
        var sim = session.Zone!.Sim;
        var heard = new List<Core.Sim.MinionCommandRequested>();
        sim.Events.Subscribe<Core.Sim.MinionCommandRequested>(heard.Add);
        client.Send(new CommandMinionsMsg("assault", 0, 3, 4, 0) { Group = "remnants" });
        client.Send(new CommandMinionsMsg("", 0, 0, 0, 0));
        var replies = Pump(server, client, 2);
        var command = Assert.Single(heard);
        Assert.Equal(session.Entity, command.Owner);
        Assert.Equal("assault", command.Command);
        Assert.Equal("remnants", command.Group);
        Assert.True(command.Target.IsNone);
        Assert.Contains(replies, m => m is IntentRejectedMsg r && r.Reason == "bad_command");
    }

    /// <summary>Steps until a message of type T matching the predicate arrives (or the ticks run out), keeping everything received.</summary>
    private static T? Await<T>(GameServer server, LoopbackClient client, List<Message> inbox, Func<T, bool>? match = null, int ticks = 30) where T : Message
    {
        for (var i = 0; i < ticks; i++)
        {
            server.Step();
            var got = client.Drain();
            inbox.AddRange(got);
            if (got.OfType<T>().LastOrDefault(m => match is null || match(m)) is { } found)
            {
                return found;
            }
        }
        return null;
    }

    [Fact]
    public void InterfaceFlowsMoveItemsSocketStonesAndAllocateAndRefundChoices()
    {
        var (server, transport) = Create();
        var (client, _, session, replies, _) = Enter(server, transport, "flows", "Hazel", new List<string> { ProtocolInfo.InterfaceFeature });
        var sim = session.Zone!.Sim;
        var hero = session.Entity;
        var inbox = new List<Message>(replies);

        // Move an item to another cell of the bag.
        var bag = replies.OfType<InventoryStateMsg>().Last().Bag;
        var floss = bag.Items.First(i => i.Base == "thread_floss");
        client.Send(new MoveItemMsg(floss.Uid, "bag", 11, 4));
        var moved = Await<InventoryStateMsg>(server, client, inbox, s => s.Bag.Items.Any(i => i.Uid == floss.Uid && i.X == 11 && i.Y == 4));
        Assert.NotNull(moved);

        // Socket a glyph stone beside the codex of the first seat; the weave slot state shows it with its preview.
        var stone = sim.Service<Core.Items.ItemGenerator>().Roll("glyph_stone", 1, "common", new Core.Maths.Rng(5), new Core.Items.ItemGenerator.PayloadRequest { Glyph = "needle" });
        Assert.True(sim.World.Get<Inventory>(hero)!.TryAdd(stone));
        client.Send(new SocketStoneMsg(stone.Uid, 0, 0));
        var slots = Await<WeaveSlotsMsg>(server, client, inbox, s => s.Seats[0].Glyphs.Any(g => g.Item == stone.Uid));
        Assert.NotNull(slots);
        var seat = slots!.Seats[0];
        Assert.Equal("needle", seat.Glyphs.Single().Id);
        Assert.NotNull(seat.Item);
        Assert.Contains(seat.Preview, p => p.Label == "Damage");
        Assert.Contains(seat.Preview, p => p.Label == "Cast time");

        // Allocate a choice node with an option, then refund it at a font.
        sim.World.Get<Stats>(hero)!.Level = 5;
        client.Send(new AllocatePassiveMsg("inner_a") { Option = "might" });
        var passives = Await<PassiveStateMsg>(server, client, inbox, p => p.Picks.ContainsKey("inner_a"));
        Assert.NotNull(passives);
        Assert.Equal(new List<string> { "might" }, passives!.Picks["inner_a"]);
        Assert.False(passives.Refund);

        var at = sim.World.Get<Transform>(hero)!.Position;
        sim.Service<Core.Patterns.PatternFactory>().Spawn(sim, "passive_font", at + new System.Numerics.Vector3(1.5f, 0, 0));
        sim.World.Get<Inventory>(hero)!.AddCurrency("shards", 200);
        var atFont = Await<PassiveStateMsg>(server, client, inbox, p => p.Refund);
        Assert.NotNull(atFont);
        Assert.NotNull(atFont!.RefundCost);
        client.Send(new RefundPassiveMsg("inner_a"));
        var refunded = Await<PassiveStateMsg>(server, client, inbox, p => !p.Allocated.Contains("inner_a"));
        Assert.NotNull(refunded);
        Assert.False(refunded!.Picks.ContainsKey("inner_a"));
        Assert.DoesNotContain(inbox, m => m is IntentRejectedMsg);
    }

    [Fact]
    public void AConversationOpensTheVendorAndAPurchaseLandsInTheBag()
    {
        var (server, transport) = Create();
        var (client, _, session, replies, _) = Enter(server, transport, "trader", "Teasel", new List<string> { ProtocolInfo.InterfaceFeature });
        var sim = session.Zone!.Sim;
        var hero = session.Entity;
        var inbox = new List<Message>(replies);
        var at = sim.World.Get<Transform>(hero)!.Position;
        var quartermaster = sim.Service<Core.Patterns.PatternFactory>().Spawn(sim, "quartermaster", at + new System.Numerics.Vector3(1.5f, 0, 0));
        sim.World.Get<Inventory>(hero)!.AddCurrency("shards", 500);

        client.Send(new InteractMsg(quartermaster.Key));
        var greet = Await<DialogueStateMsg>(server, client, inbox);
        Assert.NotNull(greet);
        Assert.Equal(quartermaster.Key, greet!.Source);
        Assert.Equal("greet", greet.Node);
        Assert.Contains(greet.Choices, c => c.Id == "trade" && c.Enabled);
        Assert.Contains(inbox, m => m is QuestOffersMsg);

        client.Send(new ChooseDialogueMsg("quartermaster", "trade") { Node = "greet" });
        var vendor = Await<VendorStateMsg>(server, client, inbox);
        Assert.NotNull(vendor);
        Assert.Equal(quartermaster.Key, vendor!.Source);
        Assert.False(vendor.Closed);
        var floss = vendor.Stock.Single(s => s.Id == "floss");
        Assert.NotEqual(false, floss.Enabled);
        Assert.Contains(inbox, m => m is DialogueStateMsg d && d.Node == "trading");

        var before = sim.World.Get<Inventory>(hero)!.CountOf(i => i.Base == "thread_floss");
        client.Send(new BuyMsg(quartermaster.Key, "floss", 1));
        var after = Await<VendorStateMsg>(server, client, inbox, v => v != vendor);
        Assert.NotNull(after);
        Assert.Equal(before + floss.Item.Count, sim.World.Get<Inventory>(hero)!.CountOf(i => i.Base == "thread_floss"));
        Assert.Equal(500 - floss.Price["shards"], sim.World.Get<Inventory>(hero)!.CurrencyOf("shards"));
        Assert.DoesNotContain(inbox, m => m is IntentRejectedMsg);

        // Selling a bag item pays what the vendor quoted for it.
        var quote = after!.SellPrices.First();
        var shards = sim.World.Get<Inventory>(hero)!.CurrencyOf("shards");
        client.Send(new SellMsg(quartermaster.Key, quote.Item));
        Await<VendorStateMsg>(server, client, inbox, v => v.BuyBack.Any(b => b.Item.Uid == quote.Item));
        Assert.Equal(shards + quote.Price["shards"], sim.World.Get<Inventory>(hero)!.CurrencyOf("shards"));

        // Closing the conversation ends it on the server too.
        client.Send(new CloseDialogueMsg("quartermaster"));
        Assert.NotNull(Await<DialogueStateMsg>(server, client, inbox, d => d.Ended));
    }

    [Fact]
    public void UseBanksMetasAndConvertedFacesReachTheWeaveSlots()
    {
        var (server, transport) = Create();
        var (client, _, session, replies, _) = Enter(server, transport, "faces", "Linnet", new List<string> { ProtocolInfo.InterfaceFeature });
        var sim = session.Zone!.Sim;
        var hero = session.Entity;
        var inbox = new List<Message>(replies);
        var book = sim.World.Get<Core.Components.WeaveBook>(hero)!;
        var quiver = book.Add(new Core.Definitions.WeaveSpec { Form = "quiver" });
        book.Add(new Core.Definitions.WeaveSpec { Form = "bolt", Threads = { "ember" }, Glyphs = { "twin_rite" } });

        var slots = Await<WeaveSlotsMsg>(server, client, inbox, s => s.Seats.Any(x => x.UsesMax == 4) && s.Seats.Any(x => x.Meta is not null));
        Assert.NotNull(slots);
        var bank = slots!.Seats.Single(x => x.UsesMax == 4);
        Assert.Equal(4, bank.Uses);
        Assert.Null(bank.Converted);
        Assert.Equal("Detonate", slots.Seats.Single(x => x.Meta is not null).Meta!.Label);

        // An empty bank converts the face to its restring.
        quiver.State[Core.Components.SlotState.Uses] = 0;
        quiver.State[Core.Components.SlotState.UsesAt] = sim.Clock.Time;
        var spent = Await<WeaveSlotsMsg>(server, client, inbox, s => s.Seats.Any(x => x.UsesMax == 4 && x.Converted is not null));
        Assert.NotNull(spent);
        Assert.Equal("Restring", spent!.Seats.Single(x => x.UsesMax == 4).Converted!.Label);
    }

    [Fact]
    public void ATreeIsBoughtOverTheWireShownOnItsSeatAndResetAtAFont()
    {
        var (server, transport) = Create();
        var (client, _, session, replies, _) = Enter(server, transport, "tree", "Wrenna", new List<string> { ProtocolInfo.InterfaceFeature });
        var sim = session.Zone!.Sim;
        var hero = session.Entity;
        var inbox = new List<Message>(replies);
        var inv = sim.World.Get<Inventory>(hero)!;
        var codex = sim.Service<Core.Items.ItemGenerator>().Roll("weave_codex", 6, "common", new Core.Maths.Rng(81), new Core.Items.ItemGenerator.PayloadRequest { Form = "torrent", Level = 4 });
        Assert.True(inv.TryAdd(codex));
        client.Send(new LearnFormMsg(codex.Uid, 4));
        var learned = Await<WeaveSlotsMsg>(server, client, inbox, s => s.Seats.Count > 4 && s.Seats[4].Form == "torrent");
        Assert.NotNull(learned);
        Assert.Equal(0, learned!.Seats[4].TreeSpent);
        Assert.Equal(3, learned.Seats[4].TreePoints);

        client.Send(new BuyTreeNodeMsg(4, "swelling"));
        client.Send(new BuyTreeNodeMsg(4, "quick_pour"));
        var bought = Await<WeaveSlotsMsg>(server, client, inbox, s => s.Seats[4].TreeSpent == 2);
        Assert.NotNull(bought);
        Assert.Equal(new List<string> { "swelling", "quick_pour" }, bought!.Seats[4].Tree);
        Assert.Null(bought.Seats[4].DormantNodes);

        // A node the compiler would not count is refused with its reason, and the seat is unchanged.
        client.Send(new BuyTreeNodeMsg(4, "broad_stream"));
        var refused = Await<IntentRejectedMsg>(server, client, inbox);
        Assert.NotNull(refused);
        Assert.Equal(new[] { "swelling", "quick_pour" }, sim.World.Get<WeaveBook>(hero)!.Slots[4].Spec.Tree);

        // A font quotes the reset for the seat and unweaves it.
        var at = sim.World.Get<Transform>(hero)!.Position;
        var font = sim.Service<Core.Patterns.PatternFactory>().Spawn(sim, "passive_font", at + new System.Numerics.Vector3(1.5f, 0, 0));
        inv.AddCurrency("shards", 100);
        client.Send(new InteractMsg(font.Key));
        var pane = Await<StationStateMsg>(server, client, inbox, s => s.Services?.Any(v => v.Id == "reset_tree") == true);
        Assert.NotNull(pane);
        Assert.Equal(4, pane!.Services!.Single(v => v.Id == "reset_tree").Slot);
        client.Send(new StationServiceMsg(font.Key, "reset_tree") { Slot = 4 });
        var reset = Await<WeaveSlotsMsg>(server, client, inbox, s => s.Seats[4].TreeSpent == 0);
        Assert.NotNull(reset);
        Assert.Null(reset!.Seats[4].Tree);
    }

    [Fact]
    public void ARaisedGuardShowsItsPoolOnTheCastBar()
    {
        var (server, transport) = Create();
        var (client, _, session, replies, _) = Enter(server, transport, "guard", "Brannock", new List<string> { ProtocolInfo.InterfaceFeature });
        var sim = session.Zone!.Sim;
        var hero = session.Entity;
        var inbox = new List<Message>(replies);
        var slot = sim.World.Get<Core.Components.WeaveBook>(hero)!.Add(new Core.Definitions.WeaveSpec { Preset = "stone_bulwark" });
        var at = sim.World.Get<Transform>(hero)!.Position;
        Assert.True(sim.Service<Core.Weaves.WeaveRuntime>().TryBegin(sim, hero, slot, Core.Ecs.Entity.None, at, System.Numerics.Vector2.UnitX, out var reason), reason);

        var self = Await<SelfStateMsg>(server, client, inbox, s => s.Cast is { Mode: "guard" });
        Assert.NotNull(self);
        Assert.Equal(1, self!.Cast!.Pool);
    }

    [Fact]
    public void ContainersStashPagesAndStationServicesTravelOverTheWire()
    {
        var (server, transport) = Create();
        var (client, _, session, replies, _) = Enter(server, transport, "keeper", "Sorrel", new List<string> { ProtocolInfo.InterfaceFeature });
        var sim = session.Zone!.Sim;
        var hero = session.Entity;
        var inbox = new List<Message>(replies);
        var inv = sim.World.Get<Inventory>(hero)!;
        inv.AddCurrency("shards", 2000);

        // The reliquary arrives with its rungs; a relic moved into it shows there.
        var relic = sim.Service<Core.Items.ItemGenerator>().Roll("relic_shard", 5, "magic", new Core.Maths.Rng(71));
        Assert.True(inv.TryAdd(relic));
        client.Send(new MoveItemMsg(relic.Uid, "reliquary", 0, 0));
        var seated = Await<InventoryStateMsg>(server, client, inbox, s => s.Containers.Any(c => c.Id == "reliquary" && c.Board.Items.Any(i => i.Uid == relic.Uid)));
        Assert.NotNull(seated);
        var reliquary = seated!.Containers.Single(c => c.Id == "reliquary");
        Assert.True(reliquary.Active);
        Assert.Equal("account", reliquary.Owner);
        Assert.Contains(reliquary.Board.Rungs!, r => !r.Unlocked && r.Requirement is not null);

        // Stash pages are out of reach until the hero stands at a chest; then a page can be bought.
        var stash = seated.Containers.Single(c => c.Id == "personal_stash:1");
        Assert.False(stash.Reachable);
        Assert.NotNull(stash.Reason);
        Assert.True(seated.Containers.Single(c => c.Id == "personal_stash:2").Locked);
        var at = sim.World.Get<Transform>(hero)!.Position;
        sim.Service<Core.Patterns.PatternFactory>().Spawn(sim, "stash_chest", at + new System.Numerics.Vector3(1.5f, 0, 0));
        Assert.NotNull(Await<InventoryStateMsg>(server, client, inbox, s => s.Containers.Single(c => c.Id == "personal_stash:1").Reachable));
        client.Send(new BuyPageMsg("personal_stash"));
        var bought = Await<InventoryStateMsg>(server, client, inbox, s => s.Containers.Single(c => c.Id == "personal_stash:2").Locked is null);
        Assert.NotNull(bought);

        // A font quotes its services in the station pane and raises a stone for shards.
        var font = sim.Service<Core.Patterns.PatternFactory>().Spawn(sim, "passive_font", at + new System.Numerics.Vector3(0, 1.5f, 0));
        var stone = sim.Service<Core.Items.ItemGenerator>().Roll("glyph_stone", 1, "common", new Core.Maths.Rng(72), new Core.Items.ItemGenerator.PayloadRequest { Glyph = "needle" });
        Assert.True(inv.TryAdd(stone));
        client.Send(new InteractMsg(font.Key));
        var pane = Await<StationStateMsg>(server, client, inbox, s => s.Services is not null);
        Assert.NotNull(pane);
        var raise = pane!.Services!.Single(s => s.Id == "raise_stone" && s.Item == stone.Uid);
        Assert.True(raise.Enabled);
        Assert.True(raise.Price!["shards"] > 0);
        var level = stone.Payload!.Level;
        client.Send(new StationServiceMsg(font.Key, "raise_stone") { Item = stone.Uid });
        Assert.NotNull(Await<StationStateMsg>(server, client, inbox, s => s != pane));
        Assert.Equal(level + 1, stone.Payload.Level);
        Assert.DoesNotContain(inbox, m => m is IntentRejectedMsg);
    }
}
