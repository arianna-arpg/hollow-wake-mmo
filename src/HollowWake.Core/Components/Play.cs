using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;

namespace HollowWake.Core.Components;

// Quests and the player. Weaves and casting live in Weaves.cs, items in Inventory.cs, AI state in Ai.cs
// and minions in Minions.cs.

public enum QuestState
{
    Offered,
    Active,
    Complete,
    TurnedIn,
    Failed,
}

public sealed class QuestProgress
{
    public required string QuestId;
    public QuestState State = QuestState.Active;
    public Dictionary<string, int> Counts = new(StringComparer.Ordinal);
    public double StartedAt;
    public QuestDef? Generated;
}

public sealed class QuestLog : IComponent
{
    public Dictionary<string, QuestProgress> Quests { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Completed { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Discovered { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, double> Reputation { get; } = new(StringComparer.Ordinal);

    /// <summary>Story flags conversations and rewards set (<c>has("flag.met_quartermaster")</c> in gates).</summary>
    public HashSet<string> Flags { get; } = new(StringComparer.Ordinal);
}

/// <summary>
/// The world panes a hero has open: a conversation, a vendor, a crafting station. Each remembers the entity that
/// opened it and how far the hero may stand from it; walking out of reach closes it.
/// </summary>
public sealed class Panes : IComponent
{
    public ConversationPane? Conversation;
    public VendorPane? Vendor;
    public StationPane? Station;

    /// <summary>Items the hero sold, per vendor entity, newest last, to buy back.</summary>
    public Dictionary<long, List<SoldItem>> BuyBack { get; } = new();
}

public sealed class ConversationPane
{
    public required string Dialogue;
    public required Ecs.Entity Source;
    public required string Node;
    public double Reach;
}

public sealed class VendorPane
{
    public required string Vendor;
    public required Ecs.Entity Source;
    public double Reach;
    /// <summary>The stock as minted for this hero when the pane opened: what is bought is what was shown.</summary>
    public List<VendorOffer> Stock { get; } = new();
}

/// <summary>A stock entry as offered: the item (copied on purchase), its price per currency, and whether it can be bought.</summary>
public sealed class VendorOffer
{
    public required string Id;
    public required Items.ItemInstance Item;
    public Dictionary<string, double> Price = new(StringComparer.Ordinal);
    public bool Enabled = true;
    public string? Reason;
}

/// <summary>An item sold to a vendor and what was paid for it.</summary>
public sealed class SoldItem
{
    public required Items.ItemInstance Item;
    public Dictionary<string, double> Paid = new(StringComparer.Ordinal);
}

public sealed class StationPane
{
    public required Ecs.Entity Source;
    public HollowWake.Core.Tags.TagSet Tags = HollowWake.Core.Tags.TagSet.Empty;
    public double Reach;
}

/// <summary>Purchases left per stock entry of the vendors an entity offers (an entity's stock runs out for everyone).</summary>
public sealed class VendorStock : IComponent
{
    public Dictionary<string, int> Remaining { get; } = new(StringComparer.Ordinal);
}

/// <summary>A player-controlled entity and its pending intents.</summary>
public sealed class Player : IComponent
{
    public required string AccountId;
    public required string CharacterId;
    public Queue<Intents.IIntent> Intents { get; } = new();
    public double LastInputAt;
    public double RespawnAt = double.NegativeInfinity;
}
