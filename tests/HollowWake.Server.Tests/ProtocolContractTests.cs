using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using HollowWake.Protocol;
using Xunit;

namespace HollowWake.Server.Tests;

/// <summary>The web client's TypeScript message module must say exactly what the C# messages put on the wire.</summary>
public partial class ProtocolContractTests
{
    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10 && dir is not null; i++)
        {
            if (File.Exists(Path.Combine(dir, "HollowWake.sln")))
            {
                return dir;
            }
            dir = Path.GetDirectoryName(dir);
        }
        throw new DirectoryNotFoundException("repository root (HollowWake.sln)");
    }

    [Fact]
    public void WebClientModuleMatchesTheMessages()
    {
        var path = Path.Combine(RepoRoot(), TypeScriptContract.ModulePath);
        Assert.True(File.Exists(path), $"{TypeScriptContract.ModulePath} is missing; run: {TypeScriptContract.RegenerateCommand}");
        var committed = File.ReadAllText(path).Replace("\r\n", "\n");
        Assert.True(committed == TypeScriptContract.Generate(), $"{TypeScriptContract.ModulePath} is stale; run: {TypeScriptContract.RegenerateCommand}");
    }

    [GeneratedRegex(@"export interface (\w+) \{\n(.*?)\n\}", RegexOptions.Singleline)]
    private static partial Regex InterfacePattern();

    [GeneratedRegex(@"^  (\w+)\??:", RegexOptions.Multiline)]
    private static partial Regex FieldPattern();

    /// <summary>Serializes real messages and checks every key the server writes is declared by the TypeScript interface.</summary>
    [Fact]
    public void EveryKeyOnTheWireIsDeclared()
    {
        var interfaces = InterfacePattern().Matches(TypeScriptContract.Generate())
            .ToDictionary(m => m.Groups[1].Value, m => FieldPattern().Matches(m.Groups[2].Value).Select(f => f.Groups[1].Value).ToHashSet());
        var state = new EntityState { Key = 5L << 32 | 3, Name = "Hob", Pattern = "hob", Look = "hob", Faction = "the_loosed", Rarity = "magic", Interactable = "talk", Casting = "Ember", VX = 1, Resources = { ["life"] = 3 }, ResourceMax = { ["life"] = 9 }, Flags = { "monster" }, Statuses = { "burning" } };
        var item = new ItemInfo(9, "short_blade", "Short Blade", "rare", 1, 4, "main_hand", new List<string> { "+3 damage" }, new Dictionary<string, double> { ["damage.min"] = 2 });
        var quest = new QuestInfo("q", "Quest", "active", new List<string> { "Slay" }, new List<int> { 1 }, new List<int> { 3 }, "quartermaster", 2);
        var delta = new EntityDelta { Key = 9, X = 1, Look = "", Dead = true, Resources = new Dictionary<string, float> { ["life"] = 2 }, Statuses = new List<string> { "burning" }, VX = 0 };
        var samples = new (Message Message, string Interface)[]
        {
            (new EntitySpawnMsg(state), nameof(EntitySpawnMsg)),
            (new EntitySpawnMsg(state with { Static = true }), nameof(EntitySpawnMsg)),
            (new EntityDeltaMsg(new List<EntityDelta> { delta }, 3.5), nameof(EntityDeltaMsg)),
            (new EntityUpdateMsg(new List<EntityState> { state }, 12.5), nameof(EntityUpdateMsg)),
            (new UseWeaveMsg(1, 7, 1, 2, 3, 0.5f, 0.5f), nameof(UseWeaveMsg)),
            (new ChunkDataMsg(0, 0, 1, new float[] { 1 }, new byte[] { 1 }, new List<string> { "ground grass" }, new ushort[] { 0 }, new List<LayerInfo> { new(0, 2, true, "bridge") }, 1, 0.5f), nameof(ChunkDataMsg)),
            (new SelfStateMsg(1, 2, 3, 4, 5, new List<string>(), new Dictionary<string, double>(), new Dictionary<string, double>(), new Dictionary<string, double>(),
                new List<WeaveSlotInfo> { new(0, "ember", "Ember", null, 0.5, new Dictionary<string, double> { ["mana"] = 4 }, "enemy", 12) }, new List<ItemInfo> { item }, 20,
                new Dictionary<string, ItemInfo> { ["main_hand"] = item }, new Dictionary<string, double>(), new List<QuestInfo> { quest }, new Dictionary<string, double>(), new List<string>()), nameof(SelfStateMsg)),
            (new QuestOffersMsg(4, new List<QuestInfo> { quest }, new List<QuestInfo>()), nameof(QuestOffersMsg)),
            (new EnterWorldMsg("c1", 3, "0_0", 1, 2, 3, new WorldInfo(ulong.MaxValue, 32, 8, 8, new List<OverlayInfo> { new("o", true, new List<int[]> { new[] { 0, 0 } }, new List<int[]>(), new double[] { 1, 2, 3 }, new Dictionary<string, double>(), 1) }, "abc"), 0, 20), nameof(EnterWorldMsg)),
            (new CombatEventMsg("damage", 1, 2, 3, true, new List<string> { "fire" }, "burning"), nameof(CombatEventMsg)),
        };
        foreach (var (message, name) in samples)
        {
            var node = JsonNode.Parse(MessageSerializer.ToJson(message))!.AsObject();
            AssertDeclared(interfaces, name, node);
        }
    }

    private static void AssertDeclared(Dictionary<string, HashSet<string>> interfaces, string name, JsonObject node)
    {
        Assert.True(interfaces.TryGetValue(name, out var fields), $"no interface {name}");
        foreach (var (key, value) in node)
        {
            Assert.True(key == "t" || fields!.Contains(key), $"{name} on the wire has '{key}', which the TypeScript interface does not declare");
        }
        // Nested records are checked through the interfaces they are declared as.
        var nested = new Dictionary<string, string> { ["state"] = "EntityState", ["states"] = "EntityState", ["deltas"] = "EntityDelta", ["world"] = "WorldInfo", ["weaves"] = "WeaveSlotInfo", ["bag"] = "ItemInfo", ["offers"] = "QuestInfo", ["quests"] = "QuestInfo", ["layers"] = "LayerInfo", ["overlays"] = "OverlayInfo" };
        foreach (var (key, record) in nested)
        {
            switch (node[key])
            {
                case JsonObject obj:
                    AssertDeclared(interfaces, record, obj);
                    break;
                case JsonArray array:
                    foreach (var element in array.OfType<JsonObject>())
                    {
                        AssertDeclared(interfaces, record, element);
                    }
                    break;
            }
        }
    }
}
