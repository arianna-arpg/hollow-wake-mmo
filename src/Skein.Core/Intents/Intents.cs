using System.Numerics;
using Skein.Core.Ecs;

namespace Skein.Core.Intents;

/// <summary>
/// What a player wants to do. Intents arrive from the network, are queued on the player entity, and
/// are consumed by the intent system during the input phase. They are requests, never results: the
/// simulation validates every one.
/// </summary>
public interface IIntent
{
}

public sealed record MoveToIntent(Vector3 Point) : IIntent;

public sealed record MoveDirIntent(Vector2 Direction) : IIntent;

public sealed record StopIntent : IIntent;

public sealed record JumpIntent : IIntent;

public sealed record UseWeaveIntent(int Slot, Entity Target, Vector3 Point, Vector2 Direction) : IIntent;

public sealed record CancelCastIntent : IIntent;

public sealed record InteractIntent(Entity Target) : IIntent;

public sealed record PickUpIntent(Entity Drop) : IIntent;

public sealed record EquipIntent(long ItemUid, string? Slot) : IIntent;

public sealed record UnequipIntent(string Slot) : IIntent;

public sealed record DropItemIntent(long ItemUid) : IIntent;

public sealed record AllocatePassiveIntent(string NodeId) : IIntent;

public sealed record SetWeaveIntent(int Slot, Definitions.WeaveSpec Spec) : IIntent;

public sealed record AcceptQuestIntent(string QuestId, Entity Giver) : IIntent;

public sealed record TurnInQuestIntent(string QuestId, Entity Giver) : IIntent;

public sealed record CraftStartIntent(string RecipeId, Entity Station) : IIntent;

public sealed record CraftSubmitIntent(string RecipeId, Entity Station, Crafting.ProcedureInput Input) : IIntent;

public sealed record BindFountIntent(Entity Fount) : IIntent;

public sealed record EnactDecreeIntent(string HoldingId, string DecreeId) : IIntent;

public sealed record BuildStructureIntent(string HoldingId, string StructureId, Vector3 Point) : IIntent;

public sealed record RespawnIntent : IIntent;
