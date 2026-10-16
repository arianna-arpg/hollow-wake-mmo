using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Skein.Core.Attributes;
using Skein.Core.Expressions;
using Skein.Core.Tags;

namespace Skein.Core.Definitions;

public interface IDefinition
{
    string Id { get; }
}

/// <summary>Common shape of every content definition.</summary>
public abstract record Definition : IDefinition
{
    public required string Id { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public TagSet Tags { get; init; } = TagSet.Empty;

    /// <summary>Free-form metadata for tools, UI and mods. Never read by the simulation.</summary>
    public JsonObject? Meta { get; init; }

    [JsonIgnore]
    public string DisplayName => string.IsNullOrEmpty(Name) ? Humanize(Id) : Name;

    public static string Humanize(string id)
    {
        var last = id.Contains('.') ? id[(id.LastIndexOf('.') + 1)..] : id;
        var words = last.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words.Select(w => w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w[1..]));
    }
}

/// <summary>
/// A polymorphic spec: <c>{ type: damage, amount: 10 }</c>. Effects, AI actions, generator passes,
/// crafting procedures and objective types all use it, with a registry per family deciding what the
/// type names mean. New types come from code (mods included); content composes them.
/// </summary>
public sealed record TypedSpec
{
    public required string Type { get; init; }
    public JsonObject Params { get; init; } = new();

    public T Bind<T>() => Params.Deserialize<T>(Content.ContentJson.Options) ?? throw new InvalidOperationException($"Cannot bind params of '{Type}' to {typeof(T).Name}.");

    public override string ToString() => Params.Count == 0 ? Type : $"{Type} {Params.ToJsonString()}";
}

/// <summary>
/// What a character actually casts: a preset weave, or a composition of a form, threads and glyphs.
/// Presets exist for onboarding and monsters; compositions are how players build.
/// </summary>
public sealed record WeaveSpec
{
    public string? Preset { get; init; }
    public string? Form { get; init; }
    public List<string> Threads { get; init; } = new();
    public List<string> Glyphs { get; init; } = new();
    public int Level { get; init; } = 1;
    public string? Name { get; init; }

    public override string ToString() => Preset ?? $"{Form}[{string.Join(",", Threads)}]+{string.Join(",", Glyphs)}";
}

public sealed record ItemGrant
{
    public required string Item { get; init; }
    public int Count { get; init; } = 1;
    public string? Rarity { get; init; }
    public int? Level { get; init; }
    public string? Unique { get; init; }
}

/// <summary>A numeric attribute every owner can carry. Formulas make derived attributes; channels can be undeclared.</summary>
public sealed record AttributeDef : Definition
{
    public string Category { get; init; } = "general";
    public double Base { get; init; }
    public Expr? Formula { get; init; }
    public double? Min { get; init; }
    public double? Max { get; init; }
    public int? Decimals { get; init; }
    public bool Percent { get; init; }
    public bool Hidden { get; init; }
}

/// <summary>A current/maximum pair with regeneration, such as life, mana, stamina or ward.</summary>
public sealed record ResourceDef : Definition
{
    public required string Max { get; init; }
    public string? Regen { get; init; }
    [JsonPropertyName("start_fraction")]
    public double StartFraction { get; init; } = 1.0;
    public bool Lethal { get; init; }
    public string Color { get; init; } = "#ffffff";
    public int Order { get; init; }
}

/// <summary>One of the nine Threads: the element and damage type vocabulary of the world.</summary>
public sealed record ThreadDef : Definition
{
    public string Color { get; init; } = "#ffffff";
    public string? Opposes { get; init; }
    /// <summary>Attribute holding the target's resistance to this thread.</summary>
    public string? Resist { get; init; }
    /// <summary>Attribute holding the attacker's penetration of this thread's resistance.</summary>
    public string? Penetration { get; init; }
    /// <summary>Which mitigation rule applies: resist, armor or none.</summary>
    public string Mitigation { get; init; } = "resist";
    /// <summary>Default status applied on hit (chance comes from the attacker's ailment attributes).</summary>
    public string? Status { get; init; }
    [JsonPropertyName("status_chance")]
    public string? StatusChance { get; init; }
}

/// <summary>Documents a known tag. Optional: tags are open, but declaring them powers tools and validation hints.</summary>
public sealed record TagDef : Definition
{
    public string Category { get; init; } = "general";
}

/// <summary>Named rule groups. Values are arbitrary nested data read by code through <see cref="Content.Rules"/>.</summary>
public sealed record RulesDef : Definition
{
    public JsonObject? Values { get; init; }
}

/// <summary>An equipment slot.</summary>
public sealed record SlotDef : Definition
{
    /// <summary>Item classes or tags that may occupy the slot.</summary>
    public TagSet Accepts { get; init; } = TagSet.Empty;
    public int Order { get; init; }
}

/// <summary>Stances between factions.</summary>
public enum Stance
{
    Hostile,
    Neutral,
    Friendly,
    Allied,
}

public sealed record FactionDef : Definition
{
    public Dictionary<string, Stance> Relations { get; init; } = new();
    public Stance Default { get; init; } = Stance.Neutral;
    public string Color { get; init; } = "#cccccc";
    /// <summary>Whether players can earn reputation and whether the faction claims territory.</summary>
    public bool Playable { get; init; }
    public bool Territorial { get; init; }
}
