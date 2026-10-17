using HollowWake.Core.Ecs;

namespace HollowWake.Core.Intents;

public sealed record PickUpIntent(Entity Drop) : IIntent;

public sealed record EquipIntent(long ItemUid, string? Slot) : IIntent;

public sealed record UnequipIntent(string Slot) : IIntent;

public sealed record DropItemIntent(long ItemUid) : IIntent;

public sealed record CraftStartIntent(string RecipeId, Entity Station) : IIntent;

public sealed record CraftSubmitIntent(string RecipeId, Entity Station, Crafting.ProcedureInput Input) : IIntent;

/// <summary>
/// Moves an item to a container: <c>bag</c> or a container board's id at a cell, <c>equipment</c> with a
/// slot, or <c>world</c> to drop it.
/// </summary>
public sealed record MoveItemIntent(long ItemUid, string Container, int X, int Y) : IIntent
{
    public string? Slot { get; init; }
}

/// <summary>Sockets a stone from the bag into a weave slot's socket, or with <see cref="Host"/> into a gear item's.</summary>
public sealed record SocketStoneIntent(long StoneUid, int Slot, int Socket) : IIntent
{
    public long? Host { get; init; }
}

/// <summary>Takes a stone out of a weave slot's socket (or a gear item's, with <see cref="Host"/>) into the bag, at a cell when one is given.</summary>
public sealed record UnsocketStoneIntent(int Slot, int Socket, int? X, int? Y) : IIntent
{
    public long? Host { get; init; }
}

/// <summary>Buys the next page of a stash.</summary>
public sealed record BuyPageIntent(string Stash) : IIntent;

/// <summary>Asks a station for a service by name, naming the item, weave slot or node it applies to.</summary>
public sealed record StationServiceIntent(Entity Station, string Service, long? Item, int? Slot, string? Node) : IIntent;

/// <summary>Learns a form item from the bag into a weave slot.</summary>
public sealed record LearnFormIntent(long ItemUid, int Slot) : IIntent;

/// <summary>Takes a learned form and its stones out of a weave slot into the bag.</summary>
public sealed record UnlearnFormIntent(int Slot, int? X, int? Y) : IIntent;

/// <summary>Buys units of a stock entry from the vendor the entity offers.</summary>
public sealed record BuyIntent(Entity Vendor, string Stock, int Count) : IIntent;

/// <summary>Sells a bag item to a vendor; a null count sells the whole stack.</summary>
public sealed record SellIntent(Entity Vendor, long ItemUid, int? Count) : IIntent;

/// <summary>Buys back an item sold to the vendor.</summary>
public sealed record BuyBackIntent(Entity Vendor, long ItemUid) : IIntent;
