using System.Text.Json.Serialization;
using HollowWake.Core.Attributes;
using HollowWake.Core.Expressions;

namespace HollowWake.Core.Definitions;

// Progression beyond passive nodes: choice groups dealt by nodes, realms with their own adjacency and
// currencies, and refunds. The passive state component keeps a character's picks.

/// <summary>Whether an option of a choice group may be picked again.</summary>
public enum ChoiceUnique
{
    /// <summary>Any option, as often as picks allow.</summary>
    None,
    /// <summary>Once per node.</summary>
    Node,
    /// <summary>Once across the character (masteries).</summary>
    Character,
}

/// <summary>How the nodes of a choice group deal its options.</summary>
public enum ChoiceDeal
{
    /// <summary>Every node of the group deals the options, each with its own picks.</summary>
    Each,
    /// <summary>The first node of the group allocated claims it; the group's other nodes cannot be allocated while it holds (a mutually exclusive cluster).</summary>
    First,
    /// <summary>Only the first node of the group allocated deals; its siblings can be allocated for their links but deal nothing (shortcuts).</summary>
    Sole,
}

/// <summary>
/// A group of options passive nodes deal: a node that names the group lets its owner pick options from it
/// (row H2 of the parity plan). Options are modifiers (and, later, a glyph graft); options can also be generated
/// over an attribute category (selectable attribute nodes, row H3).
/// </summary>
public sealed record PassiveChoiceDef : Definition
{
    public List<PassiveOptionDef> Options { get; init; } = new();

    /// <summary>Options a node of this group lets its owner pick, unless the node says otherwise.</summary>
    public int Pick { get; init; } = 1;

    public ChoiceUnique Unique { get; init; } = ChoiceUnique.Node;

    public ChoiceDeal Deal { get; init; } = ChoiceDeal.Each;

    /// <summary>The most picks from this group across the character; null for no limit beyond the nodes'.</summary>
    public int? Max { get; init; }

    /// <summary>The option given to a node that was allocated before it dealt choices (content added a choice to it).</summary>
    public string? Default { get; init; }

    /// <summary>Options generated over an attribute category: one per attribute, adding <see cref="PassiveOptionsFrom.Value"/> of it.</summary>
    public PassiveOptionsFrom? Generate { get; init; }
}

/// <summary>One option of a choice group.</summary>
public sealed record PassiveOptionDef
{
    public required string Id { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public List<Modifier> Modifiers { get; init; } = new();

    [JsonIgnore]
    public string DisplayName => string.IsNullOrEmpty(Name) ? Definition.Humanize(Id) : Name;
}

/// <summary>Options generated over the attributes of one category, each a single modifier.</summary>
public sealed record PassiveOptionsFrom
{
    public string Category { get; init; } = "core";
    public ModifierForm Form { get; init; } = ModifierForm.Flat;
    public double Value { get; init; } = 1;
}

/// <summary>A node's choice: the group it deals from and, when it differs from the group's, how many picks.</summary>
public sealed record PassiveChoiceRef
{
    public required string Group { get; init; }
    public int? Pick { get; init; }
    /// <summary>The option given to this node when it was allocated before it dealt choices; overrides the group's.</summary>
    public string? Default { get; init; }
}

/// <summary>
/// A realm: a constellation of its own, shown as a tab (row H4). Its nodes are those whose <c>realm</c> names it;
/// its roots are granted free when it opens; its points come from its own currency; it opens when its unlock
/// expression holds over the hero.
/// </summary>
public sealed record PassiveRealmDef : Definition
{
    /// <summary><c>tree</c> (a node needs an allocated neighbor) or <c>free</c> (any node of the realm).</summary>
    public string Adjacency { get; init; } = "tree";

    /// <summary>Nodes granted free when the realm opens; anchors for refunds, never refunded.</summary>
    public List<string> Roots { get; init; } = new();

    /// <summary>The point currency the realm spends.</summary>
    public string Currency { get; init; } = "passive";

    /// <summary>When the realm is open to the hero; null means always.</summary>
    public Expr? Unlock { get; init; }

    /// <summary>Why a sealed realm is sealed, as the player reads it.</summary>
    [JsonPropertyName("sealed_text")]
    public string SealedText { get; init; } = "";

    /// <summary>The calling whose start node this realm hangs from, for vocations.</summary>
    public string? Calling { get; init; }

    public int Order { get; init; }
}
