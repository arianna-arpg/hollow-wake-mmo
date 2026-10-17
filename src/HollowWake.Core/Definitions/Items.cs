using System.Text.Json.Serialization;
using HollowWake.Core.Attributes;
using HollowWake.Core.Expressions;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Definitions;

/// <summary>
/// A modifier line that may roll within a range. Implicits, affix lines and unique lines are ModLines.
/// Rolls are kept as fractions 0..1 on the item so a rebalance of ranges retunes existing items.
/// </summary>
public record ModLine : Modifier
{
    /// <summary>[min, max] window for the tier-one roll. Absent means the fixed <see cref="Modifier.Value"/>.</summary>
    public double[]? Range { get; init; }
    /// <summary>Fractional growth of the window per base tier above one.</summary>
    [JsonPropertyName("tier_scale")]
    public double? TierScale { get; init; }
    /// <summary>The line modifies the item's own local attributes (weapon damage, armor) rather than the wearer.</summary>
    public bool Local { get; init; }
    /// <summary>Display override; {v} is replaced with the rolled value.</summary>
    public string? Text { get; init; }
}

/// <summary>A base item family. Tier resolves from item level at mint; the family itself is tierless.</summary>
public sealed record ItemBaseDef : Definition
{
    /// <summary>Hierarchical class such as weapon.sword or armor.chest. Implied as a tag.</summary>
    public required string Class { get; init; }
    public string? Slot { get; init; }
    /// <summary>Additional slots this item occupies when equipped (a two-handed weapon occupies off_hand).</summary>
    public TagSet Occupies { get; init; } = TagSet.Empty;
    public int Width { get; init; } = 1;
    public int Height { get; init; } = 1;
    /// <summary>Local attribute ranges: weapon.damage.min, weapon.damage.max, attack.time, armor, evasion.</summary>
    public Dictionary<string, double[]> Local { get; init; } = new();
    /// <summary>Defense mix weights priced by the slot budget rule, for armor families.</summary>
    public Dictionary<string, double> Defense { get; init; } = new();
    public List<ModLine> Implicits { get; init; } = new();
    public Dictionary<string, double> Requirements { get; init; } = new();
    [JsonPropertyName("drop_weight")]
    public double DropWeight { get; init; } = 1;
    [JsonPropertyName("min_level")]
    public int MinLevel { get; init; } = 1;
    /// <summary>Affix theme multipliers: {caster: 2.5} biases caster-themed affixes toward this base.</summary>
    public Dictionary<string, double> Affinity { get; init; } = new();
    [JsonPropertyName("affix_cap")]
    public Dictionary<string, int> AffixCap { get; init; } = new();
    public int Stack { get; init; } = 1;
    public double Value { get; init; }
    public string? Icon { get; init; }
    /// <summary>Names by tier; falls back to the global tier prefixes.</summary>
    [JsonPropertyName("names_by_tier")]
    public List<string> NamesByTier { get; init; } = new();
}

public enum AffixKind
{
    Prefix,
    Suffix,
}

public sealed record AffixTier
{
    public int Level { get; init; } = 1;
    public List<double[]> Ranges { get; init; } = new();
    public double Weight { get; init; } = 1;
    [JsonPropertyName("magic_only")]
    public bool MagicOnly { get; init; }
}

/// <summary>An affix family: its lines, its tiers (best first), and what it rolls on.</summary>
public sealed record AffixDef : Definition
{
    public AffixKind Kind { get; init; } = AffixKind.Prefix;
    /// <summary>One item never carries two affixes of the same group.</summary>
    public string Group { get; init; } = "";
    /// <summary>Name particles indexed by tier, best first.</summary>
    public List<string> Names { get; init; } = new();
    public List<ModLine> Lines { get; init; } = new();
    public List<AffixTier> Tiers { get; init; } = new();
    public double Weight { get; init; } = 1;
    /// <summary>Rolls only on bases carrying at least one of these tags. Empty means any base.</summary>
    public TagSet Requires { get; init; } = TagSet.Empty;
    public TagSet Excludes { get; init; } = TagSet.Empty;
    public TagSet Themes { get; init; } = TagSet.Empty;
}

/// <summary>A unique item pinned to a base family with its own lines.</summary>
public sealed record UniqueDef : Definition
{
    public required string Base { get; init; }
    public List<ModLine> Lines { get; init; } = new();
    public string Flavor { get; init; } = "";
    public double Weight { get; init; } = 1;
    [JsonPropertyName("min_level")]
    public int MinLevel { get; init; } = 1;
}

public sealed record CountWeight
{
    public int Count { get; init; }
    public double Weight { get; init; } = 1;
}

public sealed record ItemRarityDef : Definition
{
    public double Weight { get; init; } = 1;
    public int Prefixes { get; init; }
    public int Suffixes { get; init; }
    /// <summary>Distribution of total affix count; empty means always the maximum.</summary>
    [JsonPropertyName("count_weights")]
    public List<CountWeight> CountWeights { get; init; } = new();
    public int Rank { get; init; }
    public string Color { get; init; } = "#ffffff";
    /// <summary>Base roll window width (commons roll widest).</summary>
    [JsonPropertyName("base_roll_window")]
    public double BaseRollWindow { get; init; } = 0.05;
}

public sealed record LootEntry
{
    public double Weight { get; init; } = 1;
    /// <summary>item, unique, table, currency or nothing.</summary>
    public string Kind { get; init; } = "item";
    public string? Item { get; init; }
    /// <summary>Class filter for random bases: weapon, armor.chest, weapon.sword.</summary>
    public string? Class { get; init; }
    public string? Rarity { get; init; }
    [JsonPropertyName("rarity_weights")]
    public Dictionary<string, double> RarityWeights { get; init; } = new();
    [JsonPropertyName("level_bonus")]
    public int LevelBonus { get; init; }
    public string? Unique { get; init; }
    public string? Table { get; init; }
    public string? Currency { get; init; }
    public double[]? Amount { get; init; }
    public Expr? Condition { get; init; }
}

public sealed record LootRoll
{
    public Expr? Count { get; init; }
    public List<LootEntry> Entries { get; init; } = new();
}

public sealed record LootTableDef : Definition
{
    public List<LootRoll> Rolls { get; init; } = new();
}

public sealed record CurrencyDef : Definition
{
    public string Color { get; init; } = "#ffffff";
    public int Order { get; init; }
    [JsonPropertyName("max_stack")]
    public int MaxStack { get; init; } = int.MaxValue;
}
