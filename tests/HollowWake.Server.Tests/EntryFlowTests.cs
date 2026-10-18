using HollowWake.Core.Characters;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Tags;
using HollowWake.Protocol;
using HollowWake.Server.Config;
using HollowWake.Server.Net;
using HollowWake.Server.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HollowWake.Server.Tests;

public class EntryFlowTests
{
    private static string ContentDir()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "content/core"))) d = d.Parent;
        return Path.Combine(d!.FullName, "content/core");
    }
    private static List<Message> Pump(GameServer s, LoopbackClient c) { s.Step(); return c.Drain(); }
    private static (GameServer, LoopbackClient, MemoryStores) Server(string? patch = null)
    {
        var store = new MemoryStores(); var transport = new LoopbackTransport();
        var cfg = new ServerConfig { ContentLayers = patch is null ? [ContentDir()] : [ContentDir(), patch], WorldEvents = false, AutosaveSeconds = 1e9 };
        var server = new GameServer(cfg, NullLoggerFactory.Instance, transport, store, store, store);
        server.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        return (server, transport.Connect(), store);
    }
    private static CharacterListMsg Login(GameServer s, LoopbackClient c, bool feature = true)
    {
        c.Send(new HelloMsg("entry-test", ProtocolInfo.Version) { Features = feature ? ["entry_flow"] : null });
        c.Send(new LoginMsg("entrytest", "")); var replies = Pump(s, c);
        var welcome = Assert.Single(replies.OfType<WelcomeMsg>());
        Assert.Equal(feature, welcome.Features?.Contains(ProtocolInfo.EntryFlowFeature) == true);
        Assert.DoesNotContain(replies, m => m is EnterWorldMsg or EntitySpawnMsg);
        return Assert.Single(replies.OfType<CharacterListMsg>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SelectionIsTransientAndCreationCorrelatesOnlyForOptedInClients(bool feature)
    {
        var (s, c, store) = Server(); var list = Login(s, c, feature);
        Assert.Empty(list.Characters); Assert.Empty(store.Load("entrytest")!.EntryFacts);
        var content = ContentRegistry.Load([ContentDir()], out _, strict: true);
        Assert.Equal(content.Each<CallingDef>().Select(d => d.Id), list.Callings);
        if (feature) { Assert.NotNull(list.Entry); Assert.All(list.Entry.Callings, v => Assert.True(v.Available)); }
        else Assert.Null(list.Entry);
        c.Send(new CreateCharacterMsg("Wren", "kindler") { RequestId = "request-one" });
        var replies = Pump(s, c); list = Assert.Single(replies.OfType<CharacterListMsg>());
        var created = Assert.Single(list.Characters);
        Assert.Equal(feature ? created.Id : null, list.CreatedCharacterId); Assert.Equal(feature ? "request-one" : null, list.RequestId);
        Assert.DoesNotContain(replies, m => m is EnterWorldMsg);
        c.Send(new SelectCharacterMsg(created.Id)); Assert.Single(Pump(s, c).OfType<EnterWorldMsg>());
        c.Send(new CreateCharacterMsg("Ash", "kindler"));
        replies = Pump(s, c);
        Assert.Contains(replies, m => m is ErrorMsg { Code: "in_world" });
        Assert.DoesNotContain(replies, m => m is CharacterListMsg);
        c.Send(new LeaveWorldMsg()); var returned = Assert.Single(Pump(s, c).OfType<CharacterListMsg>());
        Assert.Single(returned.Characters); Assert.Null(returned.CreatedCharacterId); Assert.Empty(store.Load("entrytest")!.EntryFacts);
        c.Disconnect(); s.Step();
    }

    [Fact]
    public void AvailabilityAndCapacityAreRecheckedOnTheServerIncludingLegacyClients()
    {
        var patch = Path.Combine(Path.GetTempPath(), "hw-entry-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(patch);
        try
        {
            File.WriteAllText(Path.Combine(patch, "gate.json"), """{"kind":"callings","entries":[{"id":"kindler","patch":true,"account_requirements":["calling.ember"]}]}""");
            File.WriteAllText(Path.Combine(patch, "flow.json"), """{"kind":"entry_flows","entries":[{"id":"waking","patch":true,"character_limit":1}]}""");
            var (s, c, store) = Server(patch); var list = Login(s, c);
            Assert.DoesNotContain("kindler", list.Callings); Assert.False(list.Entry!.Callings.Single(x => x.Id == "kindler").Available);
            c.Send(new CreateCharacterMsg("Wren", "kindler")); Assert.Contains(Pump(s, c), m => m is ErrorMsg { Code: "calling_unavailable" });
            Assert.Empty(store.Load("entrytest")!.Characters);
            store.Load("entrytest")!.EntryFacts.Add("calling.ember.mastered"); // A server-owned grant; hierarchical tags apply.
            c.Send(new CreateCharacterMsg("Wren", "kindler")); list = Assert.Single(Pump(s, c).OfType<CharacterListMsg>());
            var id = Assert.Single(list.Characters).Id; Assert.False(list.Entry!.CanCreate);
            c.Send(new CreateCharacterMsg("Ash", "holdfast")); Assert.Contains(Pump(s, c), m => m is ErrorMsg { Code: "too_many" });
            store.Load("entrytest")!.EntryFacts.Clear();
            c.Send(new SelectCharacterMsg(id)); Assert.Single(Pump(s, c).OfType<EnterWorldMsg>()); // Later rule changes do not erase an existing life.
            c.Send(new LeaveWorldMsg()); Pump(s, c);
            Login(s, c, feature: false);
            c.Send(new CreateCharacterMsg("Ash", "kindler")); Assert.Contains(Pump(s, c), m => m is ErrorMsg { Code: "calling_unavailable" });
            c.Disconnect(); s.Step();
        }
        finally { Directory.Delete(patch, true); }
    }

    [Fact]
    public void ARegisteredPriorStageBlocksBypassAndCompletionAtItsThresholdResumesMu()
    {
        const string stageKind = "entry_test_stage"; EntryStageKinds.Register(stageKind, false);
        var patch = Path.Combine(Path.GetTempPath(), "hw-entry-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(patch);
        try
        {
            File.WriteAllText(Path.Combine(patch, "flow.json"), """{"kind":"entry_flows","entries":[{"id":"waking","patch":true,"stages":[{"id":"prologue","kind":"entry_test_stage","presentation":"mu","completed_by":["journey.prologue"]},{"id":"mu","kind":"vessels","presentation":"mu"}]}]}""");
            var (s, c, store) = Server(patch); var list = Login(s, c);
            Assert.Equal("prologue", list.Entry!.Stage); Assert.False(list.Entry.CanCreate);
            c.Send(new CreateCharacterMsg("Wren", "kindler")); Assert.Contains(Pump(s, c), m => m is ErrorMsg { Code: "entry_required" });
            c.Send(new SelectCharacterMsg("anything")); Assert.Contains(Pump(s, c), m => m is ErrorMsg { Code: "entry_required" });
            Assert.Equal("prologue", Login(s, c).Entry!.Stage); // Abort never stamps completion.
            store.Load("entrytest")!.EntryFacts.Add("journey.prologue.faction.ember");
            Assert.Equal("prologue", Login(s, c).Entry!.Stage); // A constituent step is not the final completion fact.
            store.Load("entrytest")!.EntryFacts.Add("journey.prologue");
            Assert.Equal("mu", Login(s, c).Entry!.Stage);
            c.Send(new CreateCharacterMsg("Wren", "kindler")); Assert.Single(Assert.Single(Pump(s, c).OfType<CharacterListMsg>()).Characters);
            c.Disconnect(); s.Step();
        }
        finally { Directory.Delete(patch, true); }
    }

    [Theory]
    [InlineData("remove")]
    [InlineData("disable")]
    [InlineData("duplicate")]
    public void MissingOrAmbiguousDefaultFlowFailsContentValidationBeforeLogin(string change)
    {
        var patch = Path.Combine(Path.GetTempPath(), "hw-entry-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(patch);
        try
        {
            var entry = change switch {
                "remove" => """{"id":"waking","remove":true}""",
                "disable" => """{"id":"waking","patch":true,"default":false}""",
                _ => """{"id":"another","default":true,"character_limit":4,"stages":[{"id":"mu","kind":"vessels","presentation":"mu"}]}""",
            };
            File.WriteAllText(Path.Combine(patch, "flow.json"), "{\"kind\":\"entry_flows\",\"entries\":[" + entry + "]}");
            ContentRegistry.Load([ContentDir(), patch], out var report);
            Assert.Contains("exactly one entry flow must be default", report.ToString());
            Assert.Throws<ContentLoadException>(() => ContentRegistry.Load([ContentDir(), patch], out _, strict: true));
        }
        finally { Directory.Delete(patch, true); }
    }

    [Fact]
    public void MissingCharacterSheetsStillConsumeTheAccountCapacity()
    {
        var (s, c, store) = Server(); Login(s, c);
        var content = ContentRegistry.Load([ContentDir()], out _, strict: true);
        var limit = content.Each<EntryFlowDef>().Single(f => f.Default).CharacterLimit;
        store.Load("entrytest")!.Characters.AddRange(Enumerable.Range(0, limit).Select(i => "missing-" + i));
        var list = Login(s, c);
        Assert.Empty(list.Characters); Assert.False(list.Entry!.CanCreate);
        c.Send(new CreateCharacterMsg("Wren", "kindler"));
        Assert.Contains(Pump(s, c), m => m is ErrorMsg { Code: "too_many" });
        c.Disconnect(); s.Step();
    }

    [Fact]
    public void OldAccountsLoadWithEmptyFactsAndServerCompletionPersists()
    {
        var root = Path.Combine(Path.GetTempPath(), "hw-entry-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonFileStores(root); store.Save(new Account { Id = "old", Name = "Old" });
            var file = Path.Combine(root, "accounts", "old.json");
            var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(file))!.AsObject(); json.Remove("entry_facts"); File.WriteAllText(file, json.ToJsonString());
            var account = store.Load("old")!; Assert.Empty(account.EntryFacts);
            account.EntryFacts.Add("journey.prologue"); store.Save(account);
            Assert.Contains("journey.prologue", new JsonFileStores(root).Load("old")!.EntryFacts);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
