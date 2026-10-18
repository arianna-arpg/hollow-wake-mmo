using System.Text.Json.Serialization;
using HollowWake.Core.Attributes;
using HollowWake.Core.Expressions;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Definitions;

public sealed record ObjectiveDef
{
    public required string Id { get; init; }
    /// <summary>kill, collect, reach, interact, craft, bind, discover, deliver, escort...</summary>
    public required string Type { get; init; }
    public string? Target { get; init; }
    public TagSet Tags { get; init; } = TagSet.Empty;
    public int Count { get; init; } = 1;
    public string? Zone { get; init; }
    public string? Poi { get; init; }
    public string Text { get; init; } = "";
    public bool Optional { get; init; }
}

public sealed record RewardDef
{
    public Expr? Xp { get; init; }
    public List<ItemGrant> Items { get; init; } = new();
    public string? Loot { get; init; }
    public Dictionary<string, double> Currency { get; init; } = new();
    public Dictionary<string, double> Reputation { get; init; } = new();
    public List<string> Unlocks { get; init; } = new();
    public List<WeaveSpec> Weaves { get; init; } = new();
    /// <summary>Passive points granted per currency (a realm's currency, say).</summary>
    public Dictionary<string, int> Points { get; init; } = new();
    /// <summary>Story flags set on the hero.</summary>
    public List<string> Flags { get; init; } = new();
}

/// <summary>A hand-authored quest.</summary>
public sealed record QuestDef : Definition
{
    public int Level { get; init; } = 1;
    public string? Giver { get; init; }
    public string? Zone { get; init; }
    public Expr? Prerequisites { get; init; }
    public List<ObjectiveDef> Objectives { get; init; } = new();
    public RewardDef Rewards { get; init; } = new();
    public bool Repeatable { get; init; }
    public Dictionary<string, string> Text { get; init; } = new();
    public string? Faction { get; init; }
}

/// <summary>
/// A quest template instantiated from generated points of interest. Slots name what the template needs
/// ("a ruin near a town", "undead in this zone"); the generator fills them with real places and
/// patterns, producing a concrete quest bound to the world as it was generated.
/// </summary>
public sealed record QuestTemplateDef : Definition
{
    public double Weight { get; init; } = 1;
    /// <summary>Slot name to requirement: {ruin: {poi_tags: [ruin], within: 200}, foe: {pattern_tags: [undead]}}.</summary>
    public Dictionary<string, QuestSlot> Slots { get; init; } = new();
    /// <summary>Objectives referencing slots with $slot placeholders in target, poi and text.</summary>
    public List<ObjectiveDef> Objectives { get; init; } = new();
    public RewardDef Rewards { get; init; } = new();
    [JsonPropertyName("level_offset")]
    public int LevelOffset { get; init; }
    public Dictionary<string, string> Text { get; init; } = new();
    public bool Repeatable { get; init; } = true;
}

public sealed record QuestSlot
{
    [JsonPropertyName("poi_tags")]
    public TagSet PoiTags { get; init; } = TagSet.Empty;
    [JsonPropertyName("pattern_tags")]
    public TagSet PatternTags { get; init; } = TagSet.Empty;
    [JsonPropertyName("item_tags")]
    public TagSet ItemTags { get; init; } = TagSet.Empty;
    /// <summary>Maximum distance in cells from the giver's point of interest.</summary>
    public double Within { get; init; } = 256;
    public bool Hub { get; init; }
    /// <summary>Eligible target level minus this quest's level; omitted means unrestricted.</summary>
    [JsonPropertyName("level_delta")]
    public Window? LevelDelta { get; init; }
    /// <summary>Pattern slots must resolve to an actual nearby generated spawn point.</summary>
    [JsonPropertyName("local_population")]
    public bool LocalPopulation { get; init; }
}

public sealed record ItemRequirement
{
    public string? Item { get; init; }
    public TagSet Tags { get; init; } = TagSet.Empty;
    public int Count { get; init; } = 1;
    public bool Consume { get; init; } = true;
}

/// <summary>A Reweaving recipe: inputs, output, the minigame procedure that decides quality.</summary>
public sealed record RecipeDef : Definition
{
    public TagSet Station { get; init; } = TagSet.Empty;
    public List<ItemRequirement> Inputs { get; init; } = new();
    public required ItemGrant Output { get; init; }
    public TypedSpec? Procedure { get; init; }
    public int Level { get; init; } = 1;
    /// <summary>Maps the procedure score (0..1) to quality (0..1). Defaults to identity.</summary>
    public Expr? Quality { get; init; }
    public Expr? Xp { get; init; }
    public double Time { get; init; } = 2;
    public Dictionary<string, double> Currency { get; init; } = new();
}

/// <summary>A reusable crafting minigame configuration.</summary>
public sealed record ProcedureDef : Definition
{
    public required TypedSpec Spec { get; init; }
}

/// <summary>A decree a holding may enact: policy as modifiers and generation or spawn changes over its territory.</summary>
public sealed record DecreeDef : Definition
{
    public Dictionary<string, double> Cost { get; init; } = new();
    public Dictionary<string, double> Upkeep { get; init; } = new();
    public List<Modifier> Modifiers { get; init; } = new();
    public Dictionary<string, double> Generation { get; init; } = new();
    public TagSet Requires { get; init; } = TagSet.Empty;
    public TagSet Excludes { get; init; } = TagSet.Empty;
}

/// <summary>A structure a holding may raise: a landmark stamp with costs, effects and yields.</summary>
public sealed record StructureDef : Definition
{
    public Dictionary<string, double> Cost { get; init; } = new();
    public string? Landmark { get; init; }
    public List<Modifier> Modifiers { get; init; } = new();
    public Dictionary<string, double> Yield { get; init; } = new();
    public TagSet Requires { get; init; } = TagSet.Empty;
    [JsonPropertyName("build_time")]
    public double BuildTime { get; init; } = 60;
    public TagSet Grants { get; init; } = TagSet.Empty;
    [JsonPropertyName("max_per_holding")]
    public int MaxPerHolding { get; init; } = 1;
}
