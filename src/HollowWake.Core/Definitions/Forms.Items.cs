using System.Text.Json.Serialization;

namespace HollowWake.Core.Definitions;

// Item fields of a form. A hero learns a form from a codex, an item that teaches the form at a level; the
// codex's rarity sets how many sockets the weave slot gets. Drop weights and level floors decide which codices
// drop; requirements gate learning.
public sealed partial record FormDef
{
    /// <summary>How often a codex of this form drops when a loot entry asks for any codex. Zero never drops one.</summary>
    [JsonPropertyName("drop_weight")]
    public double DropWeight { get; init; } = 1;

    /// <summary>The lowest item level at which a codex of this form drops.</summary>
    [JsonPropertyName("min_level")]
    public int MinLevel { get; init; } = 1;

    /// <summary>Attribute (or <c>level</c>) requirements to learn a codex of this form.</summary>
    public Dictionary<string, double> Requirements { get; init; } = new();
}
