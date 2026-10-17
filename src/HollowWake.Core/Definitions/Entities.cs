using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using HollowWake.Core.Attributes;
using HollowWake.Core.Expressions;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Definitions;

/// <summary>
/// An entity template: a named bag of components. Monsters, NPCs, flora, props, projectiles, fields,
/// resource nodes and summons are all patterns. A pattern may extend another, merging component data.
/// </summary>
public sealed record PatternDef : Definition
{
    public string? Extends { get; init; }
    public Dictionary<string, JsonObject> Components { get; init; } = new();
    public int Level { get; init; } = 1;
    public string? Faction { get; init; }
    public double Radius { get; init; } = 0.4;
    public double Height { get; init; } = 1.8;
    public string? Look { get; init; }
}

public sealed record BehaviorTransition
{
    public required string To { get; init; }
    public Expr? When { get; init; }
    public int Priority { get; init; }
}

public sealed record BehaviorState
{
    public List<TypedSpec> Enter { get; init; } = new();
    public List<TypedSpec> Tick { get; init; } = new();
    public List<TypedSpec> Exit { get; init; } = new();
    public List<BehaviorTransition> Transitions { get; init; } = new();
}

/// <summary>A data-driven state machine for AI. Transitions are expressions over perception scope values.</summary>
public sealed record BehaviorDef : Definition
{
    public string Initial { get; init; } = "idle";
    [JsonPropertyName("tick_interval")]
    public double TickInterval { get; init; } = 0.25;
    public Dictionary<string, BehaviorState> States { get; init; } = new();
    [JsonPropertyName("aggro_range")]
    public Expr? AggroRange { get; init; }
    [JsonPropertyName("leash_range")]
    public Expr? LeashRange { get; init; }
}

public enum PassiveKind
{
    Start,
    Small,
    Notable,
    Keystone,
    Attribute,
}

/// <summary>A node on a passive constellation. Links are undirected; allocation walks from a start node.</summary>
public sealed record PassiveNodeDef : Definition
{
    public PassiveKind Kind { get; init; } = PassiveKind.Small;
    public double X { get; init; }
    public double Y { get; init; }
    public List<string> Links { get; init; } = new();
    public List<Modifier> Modifiers { get; init; } = new();
    public int Cost { get; init; } = 1;
    public string Realm { get; init; } = "star";
    public string? Calling { get; init; }
}

/// <summary>A starting path: attribute spread, starting weaves, items, passives. Never a cage.</summary>
public sealed record CallingDef : Definition
{
    public TagSet Threads { get; init; } = TagSet.Empty;
    public Dictionary<string, double> Attributes { get; init; } = new();
    public List<WeaveSpec> Weaves { get; init; } = new();
    public List<ItemGrant> Items { get; init; } = new();
    public List<string> Passives { get; init; } = new();
    [JsonPropertyName("start_node")]
    public string? StartNode { get; init; }
    public List<Modifier> Modifiers { get; init; } = new();
    public string Color { get; init; } = "#ffffff";
    public string? Look { get; init; }
}
