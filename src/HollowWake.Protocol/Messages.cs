using System.Text.Json.Serialization;
using HollowWake.Core.Definitions;

namespace HollowWake.Protocol;

/// <summary>
/// Every message on the wire. JSON with a type discriminator, length-prefixed by the transport.
/// The format is deliberately readable during development; the serializer is one class and can be
/// swapped for a binary one without touching the message types.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "t")]
// client -> server
[JsonDerivedType(typeof(HelloMsg), "hello")]
[JsonDerivedType(typeof(LoginMsg), "login")]
[JsonDerivedType(typeof(CreateCharacterMsg), "create_character")]
[JsonDerivedType(typeof(SelectCharacterMsg), "select_character")]
[JsonDerivedType(typeof(LeaveWorldMsg), "leave_world")]
[JsonDerivedType(typeof(MoveDirMsg), "move_dir")]
[JsonDerivedType(typeof(MoveToMsg), "move_to")]
[JsonDerivedType(typeof(StopMsg), "stop")]
[JsonDerivedType(typeof(JumpMsg), "jump")]
[JsonDerivedType(typeof(UseWeaveMsg), "use_weave")]
[JsonDerivedType(typeof(CancelCastMsg), "cancel_cast")]
[JsonDerivedType(typeof(InteractMsg), "interact")]
[JsonDerivedType(typeof(PickUpMsg), "pick_up")]
[JsonDerivedType(typeof(EquipMsg), "equip")]
[JsonDerivedType(typeof(UnequipMsg), "unequip")]
[JsonDerivedType(typeof(DropItemMsg), "drop_item")]
[JsonDerivedType(typeof(AllocatePassiveMsg), "allocate_passive")]
[JsonDerivedType(typeof(SetWeaveMsg), "set_weave")]
[JsonDerivedType(typeof(AcceptQuestMsg), "accept_quest")]
[JsonDerivedType(typeof(TurnInQuestMsg), "turn_in_quest")]
[JsonDerivedType(typeof(CraftStartMsg), "craft_start")]
[JsonDerivedType(typeof(CraftSubmitMsg), "craft_submit")]
[JsonDerivedType(typeof(BindFountMsg), "bind_fount")]
[JsonDerivedType(typeof(EnactDecreeMsg), "enact_decree")]
[JsonDerivedType(typeof(BuildStructureMsg), "build_structure")]
[JsonDerivedType(typeof(ChatMsg), "chat")]
[JsonDerivedType(typeof(RespawnMsg), "respawn")]
[JsonDerivedType(typeof(PingMsg), "ping")]
[JsonDerivedType(typeof(RequestChunkMsg), "request_chunk")]
// server -> client
[JsonDerivedType(typeof(WelcomeMsg), "welcome")]
[JsonDerivedType(typeof(LoginResultMsg), "login_result")]
[JsonDerivedType(typeof(CharacterListMsg), "character_list")]
[JsonDerivedType(typeof(ErrorMsg), "error")]
[JsonDerivedType(typeof(EnterWorldMsg), "enter_world")]
[JsonDerivedType(typeof(ZoneChangedMsg), "zone_changed")]
[JsonDerivedType(typeof(EntitySpawnMsg), "entity_spawn")]
[JsonDerivedType(typeof(EntityUpdateMsg), "entity_update")]
[JsonDerivedType(typeof(EntityDespawnMsg), "entity_despawn")]
[JsonDerivedType(typeof(SelfStateMsg), "self_state")]
[JsonDerivedType(typeof(CombatEventMsg), "combat_event")]
[JsonDerivedType(typeof(ChatBroadcastMsg), "chat_broadcast")]
[JsonDerivedType(typeof(NoticeMsg), "notice")]
[JsonDerivedType(typeof(QuestOffersMsg), "quest_offers")]
[JsonDerivedType(typeof(CraftChallengeMsg), "craft_challenge")]
[JsonDerivedType(typeof(CraftResultMsg), "craft_result")]
[JsonDerivedType(typeof(ChunkDataMsg), "chunk_data")]
[JsonDerivedType(typeof(IntentRejectedMsg), "intent_rejected")]
[JsonDerivedType(typeof(PongMsg), "pong")]
[JsonDerivedType(typeof(DisconnectMsg), "disconnect")]
public abstract record Message;

// ---------------------------------------------------------------- client -> server

public sealed record HelloMsg(string ClientVersion, int Protocol) : Message;
public sealed record LoginMsg(string Account, string Token) : Message;
public sealed record CreateCharacterMsg(string Name, string Calling) : Message;
public sealed record SelectCharacterMsg(string CharacterId) : Message;
public sealed record LeaveWorldMsg : Message;
public sealed record MoveDirMsg(float X, float Y) : Message;
public sealed record MoveToMsg(float X, float Y, float Z) : Message;
public sealed record StopMsg : Message;
public sealed record JumpMsg : Message;
public sealed record UseWeaveMsg(int Slot, long Target, float X, float Y, float Z, float DirX, float DirY) : Message;
public sealed record CancelCastMsg : Message;
public sealed record InteractMsg(long Target) : Message;
public sealed record PickUpMsg(long Drop) : Message;
public sealed record EquipMsg(long ItemUid, string? Slot) : Message;
public sealed record UnequipMsg(string Slot) : Message;
public sealed record DropItemMsg(long ItemUid) : Message;
public sealed record AllocatePassiveMsg(string Node) : Message;
public sealed record SetWeaveMsg(int Slot, WeaveSpec Spec) : Message;
public sealed record AcceptQuestMsg(string Quest, long Giver) : Message;
public sealed record TurnInQuestMsg(string Quest, long Giver) : Message;
public sealed record CraftStartMsg(string Recipe, long Station) : Message;
public sealed record CraftSubmitMsg(string Recipe, long Station, List<double> Times, List<int> Choices, Dictionary<string, double> Values) : Message;
public sealed record BindFountMsg(long Fount) : Message;
public sealed record EnactDecreeMsg(string Holding, string Decree) : Message;
public sealed record BuildStructureMsg(string Holding, string Structure, float X, float Y, float Z) : Message;
public sealed record ChatMsg(string Channel, string Text) : Message;
public sealed record RespawnMsg : Message;
public sealed record PingMsg(long Nonce) : Message;
public sealed record RequestChunkMsg(int X, int Y) : Message;

// ---------------------------------------------------------------- server -> client

/// <summary>What a client needs to generate the same world locally: seed, layout and active overlays.</summary>
public sealed record WorldInfo(ulong Seed, int ChunkSize, int ZoneChunks, int RegionZones, List<OverlayInfo> Overlays, string ContentHash);

public sealed record OverlayInfo(string Id, bool Global, List<int[]> Regions, List<int[]> Zones, double[]? Circle, Dictionary<string, double> Params, int Priority);

public sealed record WelcomeMsg(string ServerVersion, int Protocol, string ServerName, string Motd) : Message;
public sealed record LoginResultMsg(bool Ok, string? AccountId, string? Error) : Message;
public sealed record CharacterSummary(string Id, string Name, string Calling, int Level, string Zone);
public sealed record CharacterListMsg(List<CharacterSummary> Characters, List<string> Callings) : Message;
public sealed record ErrorMsg(string Code, string Text) : Message;
public sealed record EnterWorldMsg(string CharacterId, long EntityKey, string Zone, float X, float Y, float Z, WorldInfo World, double ServerTime, int TickRate) : Message;
public sealed record ZoneChangedMsg(string Zone, string ZoneName, int Level, bool Safe) : Message;

public sealed record EntityState
{
    public long Key { get; init; }
    public string Name { get; init; } = "";
    public string? Pattern { get; init; }
    public string? Look { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
    public float Yaw { get; init; }
    public float Radius { get; init; }
    public float Height { get; init; }
    public string? Faction { get; init; }
    public string? Rarity { get; init; }
    public bool IsPlayer { get; init; }
    public int Level { get; init; }
    public Dictionary<string, float> Resources { get; init; } = new();
    public Dictionary<string, float> ResourceMax { get; init; } = new();
    public List<string> Flags { get; init; } = new();
    public List<string> Statuses { get; init; } = new();
    public string? Interactable { get; init; }
    public bool Dead { get; init; }
    public string? Casting { get; init; }
    public float CastProgress { get; init; }
    public bool Grounded { get; init; } = true;
    public float VX { get; init; }
    public float VY { get; init; }
    public float VZ { get; init; }
}

public sealed record EntitySpawnMsg(EntityState State) : Message;
public sealed record EntityUpdateMsg(List<EntityState> States, double ServerTime) : Message;
public sealed record EntityDespawnMsg(long Key) : Message;

public sealed record WeaveSlotInfo(int Slot, string Key, string Name, string? Look, double ReadyIn, Dictionary<string, double> Costs, string Targeting, double Range);
public sealed record ItemInfo(long Uid, string Base, string Name, string Rarity, int Count, int Level, string? Slot, List<string> Lines, Dictionary<string, double> Local);
public sealed record QuestInfo(string Id, string Name, string State, List<string> Objectives, List<int> Counts, List<int> Required, string? Giver, int Level);

public sealed record SelfStateMsg
(
    long Key,
    int Level,
    double Xp,
    double XpToNext,
    int PassivePoints,
    List<string> Passives,
    Dictionary<string, double> Attributes,
    Dictionary<string, double> Resources,
    Dictionary<string, double> ResourceMax,
    List<WeaveSlotInfo> Weaves,
    List<ItemInfo> Bag,
    int BagCapacity,
    Dictionary<string, ItemInfo> Equipment,
    Dictionary<string, double> Currency,
    List<QuestInfo> Quests,
    Dictionary<string, double> Reputation,
    List<string> Holdings
) : Message;

public sealed record CombatEventMsg(string Kind, long Source, long Target, double Amount, bool Crit, List<string> Tags, string? Status) : Message;
public sealed record ChatBroadcastMsg(string From, string Channel, string Text) : Message;
public sealed record NoticeMsg(string Text, string Level) : Message;
public sealed record QuestOffersMsg(long Giver, List<QuestInfo> Offers, List<QuestInfo> TurnIns) : Message;
public sealed record CraftChallengeMsg(string Recipe, string Type, string Data, double TimeLimit) : Message;
public sealed record CraftResultMsg(string Recipe, bool Success, double Score, double Quality, string Reason, ItemInfo? Item) : Message;

/// <summary>
/// A chunk's terrain. Sent for modified chunks on entering a zone and for any chunk on request: the desktop
/// client generates unmodified chunks from the world seed, the web client asks for every chunk it shows.
/// WaterLevel is the chunk's water surface height, absent when the chunk has none.
/// </summary>
public sealed record ChunkDataMsg(int X, int Y, int Size, float[] Height, byte[] Flags, List<string> Tags, ushort[] SurfaceTag, List<LayerInfo> Layers, int GenVersion, float? WaterLevel = null) : Message;
public sealed record LayerInfo(int Index, float Height, bool Walkable, string Tags);

public sealed record IntentRejectedMsg(string Intent, string Reason) : Message;
public sealed record PongMsg(long Nonce, double ServerTime) : Message;
public sealed record DisconnectMsg(string Reason) : Message;

public static class ProtocolInfo
{
    public const int Version = 1;
}
