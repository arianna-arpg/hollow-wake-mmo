using HollowWake.Core.Ecs;

namespace HollowWake.Core.Intents;

public sealed record PickUpIntent(Entity Drop) : IIntent;

public sealed record EquipIntent(long ItemUid, string? Slot) : IIntent;

public sealed record UnequipIntent(string Slot) : IIntent;

public sealed record DropItemIntent(long ItemUid) : IIntent;

public sealed record CraftStartIntent(string RecipeId, Entity Station) : IIntent;

public sealed record CraftSubmitIntent(string RecipeId, Entity Station, Crafting.ProcedureInput Input) : IIntent;
