using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using HollowWake.Core.Definitions;
using HollowWake.Protocol;
using Xunit;

namespace HollowWake.Server.Tests;

/// <summary>
/// Every message on the wire, with every optional field filled: each serializes, reads back to the same
/// JSON, and puts on the wire only keys the web client's TypeScript interfaces declare, at every depth. A
/// message type without a sample here fails, so a new message cannot reach the wire untested.
/// </summary>
public partial class InterfaceProtocolTests
{
    private static ItemInfo Card(long uid) => new(uid, "short_sword", "Short Sword", "magic", 1, 4, "main_hand", new List<string> { "+3 damage" }, new Dictionary<string, double> { ["weapon.damage.min"] = 2 })
    {
        X = 1,
        Y = 2,
        W = 1,
        H = 3,
        Class = "weapon.sword",
        Icon = "sword",
        Quality = 0.5,
        Flavor = "Old steel.",
        Locked = true,
        Sockets = new List<SocketInfo> { new(0) { Stone = new ItemInfo(uid + 1, "vestige", "Vestige", "common", 1, 1, null, new List<string>(), new Dictionary<string, double>()) }, new(1) },
        Requirements = new List<RequirementInfo> { new("Might 14", true), new("Level 3", false) },
        Glyph = new ItemGlyphInfo("forking", "Forking", 2) { Rows = new List<string> { "+1 fork" } },
        Form = new ItemFormInfo("ember_bolt", "Ember Bolt", 3, 2) { Weave = "ember_bolt" },
        Fits = new List<StoneFitInfo> { new(0, "fits"), new(1, "misfit") { Reason = "needs projectile" } },
    };

    private static BoardInfo Board() => new(10, 4, new List<ItemInfo> { Card(9) })
    {
        Mask = new List<string> { "XXXXXXXXXX", "XXXXXXXXX." },
        Rungs = new List<RungInfo> { new(0, true, new List<string> { "XX" }) { Requirement = "Level 5" } },
    };

    /// <summary>One sample of every message, every optional field set.</summary>
    public static IEnumerable<Message> Samples()
    {
        yield return new CastCueMsg(9, 7, "release", 1, 2, "ember_bolt", "Ember Bolt", "cast",
            new List<string> { "ember", "spell" }, 12, 11, 1, 1, 2, 3, 4, 5, 6, true, "confirmed");
        yield return new CastForecastMsg("zone-epoch", 10, 12, 12.5, new()
        {
            new() { CastId = 7, Source = 1, Name = "Cleave", Mode = "charge", StartedAt = 11,
                NextEmissionStartedAt = 12, NextEmissionAt = 13, ReadyAt = 12.5, MaxHoldEndsAt = 15, EmissionOrdinal = 2,
                DirectionPolicy = "aim_on_release", DirectionCommitted = false, X = 1, Y = 2, Z = 3,
                Coverage = "primary_candidate", UnavailableReason = "sample reason",
                Region = new() { Node = "root", Shape = "cone", Size = new() { Radius = 3, Angle = 1.2f, Width = 1, Inner = .5f, Curvature = 1 },
                    X = 1, Y = 2, Z = 3, Heading = .5f, Anchor = "source", HeightTolerance = 4, CenterAllowance = .1f,
                    TargetTest = "center", RequiresLineOfSight = true, HasRequirement = true, HasTargetLimit = true, ViewerEligibility = "candidate" } },
        }, 2) { Retained = new() { new(6, 9) } };
        var item = Card(9);
        var quest = new QuestInfo("q", "Quest", "active", new List<string> { "Slay" }, new List<int> { 1 }, new List<int> { 3 }, "quartermaster", 2);
        var state = new EntityState { Key = 5L << 32 | 3, Name = "Hob", Pattern = "hob", Look = "hob", Faction = "the_loosed", Rarity = "magic", Interactable = "talk", Casting = "Ember", VX = 1, Static = true, Resources = { ["life"] = 3 }, ResourceMax = { ["life"] = 9 }, Flags = { "monster" }, Statuses = { "burning" }, Shape = "cone", ShapeSize = new ShapeSizeInfo { Radius = 3, Angle = 1.2f, Width = 1, Inner = 0.5f, Curvature = 1 }, Heading = 0.5f, Spin = 0.25f, Until = 12.5, Appearance = new List<string> { "chest=armor.chest light", "main_hand=weapon.sword melee blade one_hand" } };
        var delta = new EntityDelta { Key = 9, Name = "Hob", Pattern = "", Look = "", X = 1, Y = 2, Z = 3, Yaw = 1, Radius = 1, Height = 2, Faction = "", Rarity = "", Level = 2, Resources = new Dictionary<string, float> { ["life"] = 2 }, ResourceMax = new Dictionary<string, float> { ["life"] = 9 }, Flags = new List<string> { "monster" }, Statuses = new List<string> { "burning" }, Interactable = "", Dead = true, Casting = "", CastProgress = 0.5f, Grounded = false, VX = 0, VY = 0, VZ = 0, Shape = "ring", ShapeSize = new ShapeSizeInfo { Radius = 2, Inner = 1 }, Heading = 1, Spin = 2, Appearance = new List<string>() };
        var slotInfo = new WeaveSlotInfo(0, "ember", "Ember", "ember", 0.5, new Dictionary<string, double> { ["mana"] = 4 }, "enemy", 12);

        // client -> server
        yield return new HelloMsg("test", ProtocolInfo.Version) { Features = new List<string> { ProtocolInfo.EntityDeltaFeature, ProtocolInfo.InterfaceFeature } };
        yield return new LoginMsg("tester", "token");
        yield return new CreateCharacterMsg("Wren", "kindler");
        yield return new SelectCharacterMsg("c1");
        yield return new LeaveWorldMsg();
        yield return new MoveDirMsg(1, 0);
        yield return new MoveToMsg(1, 2, 3);
        yield return new StopMsg();
        yield return new JumpMsg();
        yield return new UseWeaveMsg(1, 7, 1, 2, 3, 0.5f, 0.5f);
        yield return new CancelCastMsg();
        yield return new InteractMsg(4);
        yield return new PickUpMsg(4);
        yield return new EquipMsg(9, "main_hand");
        yield return new UnequipMsg("main_hand");
        yield return new DropItemMsg(9);
        yield return new AllocatePassiveMsg("hf1") { Option = "iron" };
        yield return new SetWeaveMsg(0, new WeaveSpec { Form = "bolt", Threads = new List<string> { "ember" }, Glyphs = new List<GlyphRef> { "forking@2" }, Level = 2 });
        yield return new AcceptQuestMsg("first_threads", 4);
        yield return new TurnInQuestMsg("first_threads", 4);
        yield return new CraftStartMsg("reweave_blade", 4);
        yield return new CraftSubmitMsg("reweave_blade", 4, new List<double> { 0.5 }, new List<int> { 1 }, new Dictionary<string, double> { ["heat"] = 0.5 });
        yield return new BindFountMsg(4);
        yield return new EnactDecreeMsg("h1", "bloom");
        yield return new BuildStructureMsg("h1", "wall", 1, 2, 3);
        yield return new ChatMsg("zone", "hello");
        yield return new RespawnMsg();
        yield return new PingMsg(42);
        yield return new RequestChunkMsg(1, 2);
        yield return new ReleaseWeaveMsg(1);
        yield return new PressWeaveMsg(1);
        yield return new AimMsg(1, 2, 3, 0, 1);
        yield return new UseMetaMsg(1);
        yield return new SwapWeaveSlotsMsg(0, 3);
        yield return new CommandMinionsMsg("assault", 7, 1, 2, 3) { Group = "remnants" };
        yield return new MoveItemMsg(9, "equipment", 0, 0) { Slot = "main_hand" };
        yield return new SocketStoneMsg(10, 1, 0) { Host = 9 };
        yield return new UnsocketStoneMsg(1, 0) { X = 3, Y = 1, Host = 9 };
        yield return new LearnFormMsg(11, 2);
        yield return new UnlearnFormMsg(2) { X = 0, Y = 0 };
        yield return new RefundPassiveMsg("hf1");
        yield return new BindGraftMsg("wd3", 1);
        yield return new BuyTreeNodeMsg(4, "swelling");
        yield return new ChooseDialogueMsg("quartermaster", "work") { Node = "greet" };
        yield return new CloseDialogueMsg("quartermaster");
        yield return new ClosePaneMsg("vendor", 4);
        yield return new BuyMsg(4, "floss", 2);
        yield return new SellMsg(4, 9) { Count = 1 };
        yield return new BuyBackMsg(4, 9);
        yield return new BuyPageMsg("personal_stash");
        yield return new StationServiceMsg(4, "level_item") { Item = 10, Slot = 1, Node = "hf1" };

        // server -> client
        yield return new WelcomeMsg("1", ProtocolInfo.Version, "dev", "motd") { Features = new List<string> { ProtocolInfo.InterfaceFeature } };
        yield return new LoginResultMsg(true, "tester", "none");
        yield return new CharacterListMsg(new List<CharacterSummary> { new("c1", "Wren", "kindler", 2, "0_0") }, new List<string> { "kindler" });
        yield return new ErrorMsg("code", "text");
        yield return new EnterWorldMsg("c1", 3, "0_0", 1, 2, 3, new WorldInfo(ulong.MaxValue, 32, 8, 8, new List<OverlayInfo> { new("o", true, new List<int[]> { new[] { 0, 0 } }, new List<int[]>(), new double[] { 1, 2, 3 }, new Dictionary<string, double> { ["bloom"] = 1 }, 1) }, "abc"), 0, 20) { ForecastEpoch = "zone-epoch", DayLength = 1800, TimeOfDay = 0.3 };
        yield return new ZoneChangedMsg("0_0", "Ninewells", 1, true);
        yield return new EntitySpawnMsg(state);
        yield return new HitBodiesMsg(new() { new("test", 0, new() { new("ellipsoid", .1f, .2f, .5f, .3f, .5f) }) });
        yield return new EntityUpdateMsg(new List<EntityState> { state with { Static = false } }, 12.5);
        yield return new EntityDeltaMsg(new List<EntityDelta> { delta }, 3.5);
        yield return new EntityDespawnMsg(9);
        yield return new SelfStateMsg(1, 2, 3, 4, 5, new List<string> { "hf1" }, new Dictionary<string, double> { ["might"] = 10 }, new Dictionary<string, double> { ["life"] = 5 }, new Dictionary<string, double> { ["life"] = 9 },
            new List<WeaveSlotInfo> { slotInfo }, new List<ItemInfo> { item }, 40, new Dictionary<string, ItemInfo> { ["main_hand"] = item }, new Dictionary<string, double> { ["shards"] = 3 },
            new List<QuestInfo> { quest }, new Dictionary<string, double> { ["weftmarch_compact"] = 10 }, new List<string> { "h1" })
        {
            TimeOfDay = 0.25,
            Reserved = new Dictionary<string, double> { ["mana"] = 12 },
            Pools = new List<PoolInfo> { new("ward", "recharging") { Since = 1.5, Until = 4 } },
            Statuses = new List<StatusInfo> { new("burning", 2, 1.5, 4) },
            Cast = new CastInfo(1, "overcharge", 0.5, 1, 3) { Name = "Bolt", Brim = 0.8, Held = 1.2, Duration = 2, Pool = 0.4 },
            Charges = new List<ChargeInfo> { new("fury", 2, 3) },
            Stamps = new Dictionary<string, double> { ["hurt"] = 11.5 },
        };
        yield return new CombatEventMsg("damage", 1, 2, 3, true, new List<string> { "ember" }, "burning");
        yield return new ChatBroadcastMsg("Wren", "zone", "hello");
        yield return new NoticeMsg("text", "info");
        yield return new QuestOffersMsg(4, new List<QuestInfo> { quest }, new List<QuestInfo> { quest });
        yield return new CraftChallengeMsg("reweave_blade", "timing", "{}", 4);
        yield return new CraftResultMsg("reweave_blade", true, 1, 0.9, "ok", item);
        yield return new ChunkDataMsg(0, 0, 1, new float[] { 1 }, new byte[] { 1 }, new List<string> { "ground grass" }, new ushort[] { 0 }, new List<LayerInfo> { new(0, 2, true, "bridge") }, 1, 0.5f);
        yield return new IntentRejectedMsg("MoveItem", "no_room");
        yield return new PongMsg(42, 3.5);
        yield return new DisconnectMsg("bye");
        yield return new InventoryStateMsg(Board(), new List<EquipSlotInfo> { new("main_hand", "Main Hand", 0), new("off_hand", "Off Hand", 1) { OccupiedBy = "main_hand" } },
            new Dictionary<string, ItemInfo> { ["main_hand"] = item }, new List<CurrencyInfo> { new("shards", "Shards", 25) },
            new List<ContainerInfo>
            {
                new("reliquary", "Reliquary", "reliquary", Board(), true) { Owner = "account", Active = true },
                new("personal_stash:2", "Stash", "stash", Board(), false) { Reason = "Walk to a stash chest.", Owner = "character", Of = "personal_stash", Page = 2, Locked = true, Price = new Dictionary<string, double> { ["shards"] = 100 } },
            });
        yield return new WeaveSlotsMsg(new List<WeaveSeatInfo>
        {
            new(0, "bolt[ember]+forking@2", "bolt", "Ember Bolt", 2, 2, new List<SeatGlyphInfo> { new(0, "forking", "Forking", 2) { Dormant = "needs projectile", Item = 10, Riding = true } }, 1.5, 4,
                new List<PreviewLineInfo> { new("Damage", "12 to 18 Ember") })
            {
                Icon = "bolt",
                Dormant = "requires Will 20",
                Item = 11,
                Threads = new List<string> { "ember" },
                CastMode = "cast",
                Targeting = "direction",
                Costs = new Dictionary<string, double> { ["mana"] = 6 },
                Uses = 1,
                UsesMax = 3,
                Stacks = 2,
                Preset = "ember_bolt",
                Converted = new FaceInfo("Reload") { Icon = "reload", Weave = "quiver_restring", Form = "restring" },
                Meta = new FaceInfo("Detonate") { Weave = "ember_blast", Form = "blast" },
                Active = true,
                Tree = new List<string> { "swelling", "swelling", "quick_pour" },
                DormantNodes = new List<DormantNodeInfo> { new("broad_stream", "excluded by quick_pour") },
                TreeSpent = 3,
                TreePoints = 3,
            },
            new(1, null, null, "", 0, 0, new List<SeatGlyphInfo>(), 0, 0, new List<PreviewLineInfo>()),
        });
        yield return new PassiveStateMsg(new List<string> { "start_holdfast", "hf1" }, new Dictionary<string, List<string>> { ["hf3"] = new() { "iron" } }, new Dictionary<string, int> { ["passive"] = 2, ["devotion"] = 1 },
            new List<string> { "star", "devotion" }, true) { RefundCost = "40 Shards", Font = 4, Grafts = new List<GraftInfo> { new("wd3", "anchor", "Anchor", 1) { Slot = 1 }, new("hf3:iron", "ring", "Ring", 2) } };
        yield return new PageStateMsg(new List<PageInfo> { new("passives", false, 2), new("personal_stash", true, 0) { Reason = "Walk to a stash chest.", Container = "personal_stash:1" } });
        yield return new DialogueStateMsg(4, "quartermaster", "Quartermaster", "Hobs again.", new List<DialogueChoiceInfo> { new("work", "Any work?", true), new("trade", "Show me your stock.", false) { Reason = "Not yet." } }, false)
        {
            Node = "greet",
            Reach = 4,
        };
        yield return new VendorStateMsg(4, "quartermaster_stock", "Quartermaster", new List<VendorStockInfo> { new("floss", item, new Dictionary<string, double> { ["shards"] = 5 }) { Count = 3, Enabled = false, Reason = "Not enough shards." } },
            new List<VendorBuyBackInfo> { new(item, new Dictionary<string, double> { ["shards"] = 2 }) }, new List<VendorQuoteInfo> { new(9, new Dictionary<string, double> { ["shards"] = 1 }) }, false) { Reach = 4 };
        yield return new StationStateMsg(4, "Smith's Anvil", new List<RecipeInfo>
        {
            new("reweave_blade", "Reweave a Blade", new List<RecipeInputInfo> { new("Iron Ore", 1, 3) }, item, false) { Reason = "Missing Iron Ore.", Cost = new Dictionary<string, double> { ["shards"] = 5 } },
        }, false) { Tags = new List<string> { "smithing" }, Reach = 2.5, Services = new List<StationServiceInfo> { new("level_item", "Raise a level") { Item = 10, Slot = 1, Node = "hf1", Price = new Dictionary<string, double> { ["shards"] = 40 }, Enabled = false, Reason = "Not enough Shards." } } };
    }

    [Fact]
    public void EveryMessageTypeHasASample()
    {
        var sampled = Samples().Select(m => m.GetType()).ToHashSet();
        var all = typeof(Message).GetCustomAttributes<JsonDerivedTypeAttribute>().Select(a => a.DerivedType).ToList();
        var missing = all.Where(t => !sampled.Contains(t)).Select(t => t.Name).ToList();
        Assert.True(missing.Count == 0, "messages with no sample: " + string.Join(", ", missing));
    }

    [Fact]
    public void EveryMessageReadsBackToTheSameJson()
    {
        foreach (var message in Samples())
        {
            var json = MessageSerializer.ToJson(message);
            var back = MessageSerializer.Decode(MessageSerializer.Encode(message));
            Assert.NotNull(back);
            Assert.Equal(message.GetType(), back!.GetType());
            Assert.Equal(json, MessageSerializer.ToJson(back));
        }
    }

    [GeneratedRegex(@"export interface (\w+) \{\n(.*?)\n\}", RegexOptions.Singleline)]
    private static partial Regex InterfacePattern();

    [GeneratedRegex(@"^  (\w+)\??:", RegexOptions.Multiline)]
    private static partial Regex FieldPattern();

    [Fact]
    public void EveryKeyOnTheWireIsDeclaredAtEveryDepth()
    {
        var interfaces = InterfacePattern().Matches(TypeScriptContract.Generate())
            .ToDictionary(m => m.Groups[1].Value, m => FieldPattern().Matches(m.Groups[2].Value).Select(f => f.Groups[1].Value).ToHashSet());
        foreach (var message in Samples())
        {
            var node = JsonNode.Parse(MessageSerializer.ToJson(message))!.AsObject();
            Check(interfaces, message.GetType(), node, message.GetType().Name);
        }
    }

    /// <summary>The interface's states and intents named in docs/design/ui.md exist on the wire.</summary>
    [Fact]
    public void TheInterfaceContractIsOnTheWire()
    {
        var tags = typeof(Message).GetCustomAttributes<JsonDerivedTypeAttribute>().Select(a => (string)a.TypeDiscriminator!).ToHashSet();
        foreach (var tag in new[]
        {
            "self_state", "weave_slots", "inventory_state", "passive_state", "page_state", "dialogue_state", "vendor_state", "station_state",
            "move_item", "socket_stone", "unsocket_stone", "learn_form", "set_weave", "swap_weave_slots", "allocate_passive", "refund_passive",
            "choose_dialogue", "close_dialogue", "buy", "sell", "buy_back", "release_weave", "press_weave", "aim", "use_meta", "command_minions",
        })
        {
            Assert.Contains(tag, tags);
        }
    }

    private static readonly JsonNamingPolicy Naming = JsonNamingPolicy.SnakeCaseLower;

    private static void Check(Dictionary<string, HashSet<string>> interfaces, Type type, JsonObject node, string path)
    {
        Assert.True(interfaces.TryGetValue(type.Name, out var fields), $"no TypeScript interface {type.Name} at {path}");
        foreach (var (key, _) in node)
        {
            Assert.True(key == "t" || fields!.Contains(key), $"{path} ({type.Name}) has '{key}' on the wire, which the TypeScript interface does not declare");
        }
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var name = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? Naming.ConvertName(property.Name);
            if (node[name] is not { } value)
            {
                continue;
            }
            CheckValue(interfaces, property.PropertyType, value, path + "." + name);
        }
    }

    private static void CheckValue(Dictionary<string, HashSet<string>> interfaces, Type type, JsonNode value, string path)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (value is JsonObject obj && IsRecord(type))
        {
            Check(interfaces, type, obj, path);
            return;
        }
        if (!type.IsGenericType)
        {
            return;
        }
        var args = type.GetGenericArguments();
        var definition = type.GetGenericTypeDefinition();
        if (definition == typeof(List<>) && value is JsonArray array)
        {
            var i = 0;
            foreach (var element in array)
            {
                if (element is not null)
                {
                    CheckValue(interfaces, args[0], element, $"{path}[{i}]");
                }
                i++;
            }
        }
        else if (definition == typeof(Dictionary<,>) && value is JsonObject map)
        {
            foreach (var (key, element) in map)
            {
                if (element is not null)
                {
                    CheckValue(interfaces, args[1], element, $"{path}.{key}");
                }
            }
        }
    }

    private static bool IsRecord(Type type) => type.IsClass && type.Namespace == typeof(Message).Namespace;
}
