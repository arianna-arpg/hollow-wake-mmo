using System.Text.Json.Serialization;
using HollowWake.Core.Expressions;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Definitions;

// Conversations and trade: dialogues the server runs line by line, and the vendors a line can open.

/// <summary>
/// A conversation: lines (nodes) with a speaker, choices gated by expressions over the hero and the world, and
/// effects when a line is shown or a choice taken (story flags, quests offered, accepted and turned in, rewards,
/// a vendor or a station opened in the same book). It opens when the hero talks to an entity it is <see cref="For"/>.
/// </summary>
public sealed record DialogueDef : Definition
{
    /// <summary>The patterns (or interactable refs) of the entities this conversation opens for when talked to.</summary>
    public List<string> For { get; init; } = new();

    /// <summary>Among conversations for the same entity, the highest priority whose gate holds opens.</summary>
    public int Priority { get; init; }

    /// <summary>Whether the conversation opens for this hero; null means always.</summary>
    public Expr? When { get; init; }

    /// <summary>The first line.</summary>
    public string Start { get; init; } = "start";

    /// <summary>Who speaks, when not the entity's own name.</summary>
    public string? Speaker { get; init; }

    /// <summary>How far the hero may stand from the speaker; null takes rule <c>dialogue.reach</c>.</summary>
    public double? Reach { get; init; }

    public Dictionary<string, DialogueNodeDef> Nodes { get; init; } = new(StringComparer.Ordinal);
}

/// <summary>One line of a conversation: who says it, what it says, what happens when it is shown, and the choices after it.</summary>
public sealed record DialogueNodeDef
{
    /// <summary>The line. <c>{hero}</c> becomes the hero's name and <c>{speaker}</c> the speaker's.</summary>
    public string Line { get; init; } = "";

    public string? Speaker { get; init; }

    /// <summary>Run when the line is shown.</summary>
    public List<TypedSpec> Effects { get; init; } = new();

    /// <summary>The choices; a line with none (or none open) offers only to leave.</summary>
    public List<DialogueChoiceDef> Choices { get; init; } = new();
}

/// <summary>A choice after a line: its text, when it is offered and when it can be taken, what it does, where it leads.</summary>
public sealed record DialogueChoiceDef
{
    public required string Id { get; init; }
    public string Text { get; init; } = "";

    /// <summary>The choice is offered only while this holds; null means always.</summary>
    public Expr? When { get; init; }

    /// <summary>The choice is offered but cannot be taken while this is false; <see cref="Reason"/> says why.</summary>
    public Expr? Enabled { get; init; }

    public string Reason { get; init; } = "";

    /// <summary>Run when the choice is taken, before the conversation moves on.</summary>
    public List<TypedSpec> Effects { get; init; } = new();

    /// <summary>The line the conversation moves to; with neither this nor <see cref="End"/> it stays on the same line.</summary>
    public string? Goto { get; init; }

    /// <summary>Taking the choice ends the conversation.</summary>
    public bool End { get; init; }
}

/// <summary>
/// A vendor: stock minted from content for each hero who opens it, prices from rule formulas (or the stock entry's
/// own), what it buys back from a hero's bag and at what price, and a buy-back list per hero.
/// </summary>
public sealed record VendorDef : Definition
{
    public List<VendorStockDef> Stock { get; init; } = new();

    /// <summary>Item tags the vendor buys from a hero's bag; empty buys anything.</summary>
    public TagSet Buys { get; init; } = TagSet.Empty;

    /// <summary>The currency prices are in.</summary>
    public string Currency { get; init; } = "shards";

    /// <summary>The price of one unit of stock; null takes rule <c>inventory.vendor.price</c>.</summary>
    public Expr? Price { get; init; }

    /// <summary>What the vendor pays for an item; null takes rule <c>inventory.vendor.sell_price</c>.</summary>
    [JsonPropertyName("sell_price")]
    public Expr? SellPrice { get; init; }

    /// <summary>How many sold items the vendor keeps for each hero to buy back.</summary>
    [JsonPropertyName("buy_back")]
    public int BuyBack { get; init; } = 8;

    /// <summary>The vendor trades with the hero only while this holds.</summary>
    public Expr? When { get; init; }

    /// <summary>How far the hero may stand from the vendor; null takes rule <c>dialogue.reach</c>.</summary>
    public double? Reach { get; init; }
}

/// <summary>One line of a vendor's stock.</summary>
public sealed record VendorStockDef
{
    public required string Id { get; init; }
    /// <summary>The base item sold.</summary>
    public required string Item { get; init; }
    public string? Rarity { get; init; }
    /// <summary>The item level, an expression of the hero (<c>level</c>); null takes the hero's level.</summary>
    public Expr? Level { get; init; }
    /// <summary>Units in one purchase (a stack).</summary>
    public int Count { get; init; } = 1;
    /// <summary>Purchases the vendor has before it runs out; null for endless.</summary>
    public int? Stock { get; init; }
    /// <summary>The price per purchase, per currency; empty takes the vendor's formula in its currency.</summary>
    public Dictionary<string, Expr> Prices { get; init; } = new(StringComparer.Ordinal);
    /// <summary>For a glyph stone or codex: what it holds.</summary>
    public string? Glyph { get; init; }
    public string? Form { get; init; }
    public string? Weave { get; init; }
    /// <summary>Offered only while this holds over the hero.</summary>
    public Expr? When { get; init; }
    /// <summary>Offered but not for sale while this is false; <see cref="Reason"/> says why.</summary>
    public Expr? Enabled { get; init; }
    public string Reason { get; init; } = "";
}
