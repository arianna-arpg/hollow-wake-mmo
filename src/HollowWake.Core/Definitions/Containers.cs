using System.Text.Json.Serialization;
using HollowWake.Core.Attributes;
using HollowWake.Core.Expressions;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Definitions;

// Containers and boards beyond the bag: the account reliquary, whose seated items are active, and stashes of
// paged boards. Items keep their cells in them as they do in the bag.

/// <summary>
/// A container board (row I11): what it accepts, who owns it (a character or the account), whether the items
/// seated in it are active on the hero, and its board as unlock rungs whose cells add up as the hero meets them.
/// </summary>
public sealed record ContainerDef : Definition
{
    /// <summary>What kind of container it is (reliquary ...), which pages and clients group by.</summary>
    public string Kind { get; init; } = "container";

    /// <summary>Item tags it takes; empty takes anything.</summary>
    public TagSet Accepts { get; init; } = TagSet.Empty;

    /// <summary><c>character</c> or <c>account</c>.</summary>
    public string Owner { get; init; } = "character";

    /// <summary>Items seated here apply their lines to the hero, under source <c>container:{id}</c>.</summary>
    public bool Active { get; init; }

    /// <summary>The rungs of the board. The board is the union of the cells of every rung the hero has unlocked.</summary>
    public List<ContainerRungDef> Board { get; init; } = new();

    /// <summary>
    /// Modifiers the container puts on the hero while it holds anything; the gauge <c>gauge.container.{id}.cells</c> reads
    /// the number of cells its items cover (an experience price per occupied cell, say).
    /// </summary>
    public List<Modifier> Modifiers { get; init; } = new();

    /// <summary>The interactable kind the hero must stand at to use it; null reaches it anywhere.</summary>
    public string? At { get; init; }

    /// <summary>Moving items in or out needs the calm a loadout change needs (rule <c>casting.swap_requires</c>).</summary>
    public bool Calm { get; init; }
}

/// <summary>One unlock rung of a container board: when it unlocks, the cells it adds (rows of X and .), and what it asks as text.</summary>
public sealed record ContainerRungDef
{
    /// <summary>When the rung is unlocked, over the hero; null means always.</summary>
    public Expr? Unlock { get; init; }

    public List<string> Cells { get; init; } = new();

    /// <summary>What unlocking the rung asks, as the player reads it.</summary>
    public string Requirement { get; init; } = "";
}

/// <summary>
/// A stash (row I10): pages of identical boards, the first few free and the rest bought one at a time, reached at a
/// chest (an interactable kind), owned by a character or the account.
/// </summary>
public sealed record StashDef : Definition
{
    public int Width { get; init; } = 12;
    public int Height { get; init; } = 8;

    /// <summary>Pages in all.</summary>
    public int Pages { get; init; } = 4;

    /// <summary>Pages the hero has without buying them.</summary>
    [JsonPropertyName("free_pages")]
    public int FreePages { get; init; } = 1;

    /// <summary>The price of the next page per currency, expressions of the hero and <c>page</c> (the page's number).</summary>
    [JsonPropertyName("page_cost")]
    public Dictionary<string, Expr> PageCost { get; init; } = new(StringComparer.Ordinal);

    public TagSet Accepts { get; init; } = TagSet.Empty;

    /// <summary><c>character</c> or <c>account</c>.</summary>
    public string Owner { get; init; } = "character";

    /// <summary>The interactable kind of the chest that opens it.</summary>
    public string At { get; init; } = "stash";
}
