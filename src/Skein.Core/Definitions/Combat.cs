using System.Text.Json.Serialization;
using Skein.Core.Attributes;
using Skein.Core.Expressions;
using Skein.Core.Tags;

namespace Skein.Core.Definitions;

public enum StackPolicy
{
    /// <summary>Re-application refreshes the timer; the stronger magnitude is kept.</summary>
    Refresh,
    /// <summary>Only a stronger application takes hold, replacing timer and magnitude.</summary>
    Strongest,
    /// <summary>Applications add stacks up to the cap; each refreshes the shared timer.</summary>
    Stack,
    /// <summary>Each source keeps its own independent instance.</summary>
    Independent,
}

public sealed record StatusTick
{
    public double Interval { get; init; } = 1.0;
    public List<TypedSpec> Effects { get; init; } = new();
}

/// <summary>A status: ailment, buff, debuff, state. Modifiers, flags and periodic effects while worn.</summary>
public sealed record StatusDef : Definition
{
    public Expr? Duration { get; init; }
    [JsonPropertyName("max_stacks")]
    public int MaxStacks { get; init; } = 1;
    [JsonPropertyName("stack_policy")]
    public StackPolicy StackPolicy { get; init; } = StackPolicy.Refresh;
    public bool Beneficial { get; init; }
    public bool Dispellable { get; init; } = true;
    public List<Modifier> Modifiers { get; init; } = new();
    /// <summary>Scale <see cref="Modifiers"/> by the current stack count.</summary>
    [JsonPropertyName("modifiers_per_stack")]
    public bool ModifiersPerStack { get; init; }
    /// <summary>Flags the bearer gains while afflicted: stunned, rooted, silenced, flying, hidden.</summary>
    public TagSet Flags { get; init; } = TagSet.Empty;
    /// <summary>Weave tags the bearer cannot use while afflicted (silence forbids spell, root forbids movement).</summary>
    [JsonPropertyName("forbids_tags")]
    public TagSet ForbidsTags { get; init; } = TagSet.Empty;
    public StatusTick? Tick { get; init; }
    [JsonPropertyName("on_apply")]
    public List<TypedSpec> OnApply { get; init; } = new();
    [JsonPropertyName("on_expire")]
    public List<TypedSpec> OnExpire { get; init; } = new();
    /// <summary>Reaching max stacks consumes them into this status (chilled into frozen).</summary>
    [JsonPropertyName("buildup_into")]
    public string? BuildupInto { get; init; }
    [JsonPropertyName("propagate_on_death")]
    public bool PropagateOnDeath { get; init; }
    /// <summary>Non-damaging statuses scale their modifiers by the applier's potency when true.</summary>
    [JsonPropertyName("scales_with_potency")]
    public bool ScalesWithPotency { get; init; } = true;
    public string Color { get; init; } = "#ffffff";
    public string? Icon { get; init; }
}

public enum Targeting
{
    Self,
    Enemy,
    Ally,
    Ground,
    Direction,
    Any,
}

public enum CastMode
{
    Instant,
    Cast,
    Channel,
}

/// <summary>Hints monsters use to pick weaves without bespoke scripting.</summary>
public sealed record AiHint
{
    public string Role { get; init; } = "damage";
    [JsonPropertyName("preferred_range")]
    public double PreferredRange { get; init; } = 2;
    public double Weight { get; init; } = 1;
    public Expr? When { get; init; }
}

/// <summary>
/// A form is the delivery of a weave: strike, projectile, nova, beam, field, ward, summon, dash.
/// It carries the targeting rules, timing, costs, local attribute bases and an effect graph that
/// references the chosen threads with placeholders.
/// </summary>
public sealed record FormDef : Definition
{
    public Targeting Targeting { get; init; } = Targeting.Enemy;
    [JsonPropertyName("cast_mode")]
    public CastMode CastMode { get; init; } = CastMode.Cast;
    public Expr? Range { get; init; }
    [JsonPropertyName("cast_time")]
    public Expr? CastTime { get; init; }
    public Expr? Cooldown { get; init; }
    [JsonPropertyName("channel_interval")]
    public Expr? ChannelInterval { get; init; }
    public Dictionary<string, Expr> Costs { get; init; } = new();
    /// <summary>Weave-local attribute bases: projectile.count, area.radius, duration, damage.effectiveness.</summary>
    public Dictionary<string, double> Attributes { get; init; } = new();
    /// <summary>What the weave mechanically has (projectile, area, duration, cooldown, channel...). Glyphs gate on these.</summary>
    public TagSet Mechanisms { get; init; } = TagSet.Empty;
    public List<TypedSpec> Effects { get; init; } = new();
    [JsonPropertyName("min_threads")]
    public int MinThreads { get; init; } = 1;
    [JsonPropertyName("max_threads")]
    public int MaxThreads { get; init; } = 2;
    [JsonPropertyName("max_glyphs")]
    public int MaxGlyphs { get; init; } = 4;
    public AiHint? Ai { get; init; }
    public string? Animation { get; init; }
}

/// <summary>A glyph alters a weave: local modifiers, extra tags, extra effects, gated by tags and mechanisms.</summary>
public sealed record GlyphDef : Definition
{
    /// <summary>The weave must carry at least one of these tags.</summary>
    public TagSet Requires { get; init; } = TagSet.Empty;
    public TagSet Excludes { get; init; } = TagSet.Empty;
    /// <summary>The weave must have every one of these mechanisms, from any source.</summary>
    [JsonPropertyName("requires_mechanisms")]
    public TagSet RequiresMechanisms { get; init; } = TagSet.Empty;
    public List<Modifier> Modifiers { get; init; } = new();
    /// <summary>Extra per-level modifiers, scaled by (level - 1).</summary>
    [JsonPropertyName("per_level")]
    public List<Modifier> PerLevel { get; init; } = new();
    [JsonPropertyName("adds_tags")]
    public TagSet AddsTags { get; init; } = TagSet.Empty;
    [JsonPropertyName("adds_mechanisms")]
    public TagSet AddsMechanisms { get; init; } = TagSet.Empty;
    [JsonPropertyName("adds_effects")]
    public List<TypedSpec> AddsEffects { get; init; } = new();
    public int Level { get; init; } = 1;
    public double Weight { get; init; } = 1;
    public string Color { get; init; } = "#ffffff";
}

/// <summary>A preset weave: a named composition, with optional overrides. Monsters and starters use these.</summary>
public sealed record WeaveDef : Definition
{
    public required string Form { get; init; }
    public List<string> Threads { get; init; } = new();
    public List<string> Glyphs { get; init; } = new();
    public Dictionary<string, double> Attributes { get; init; } = new();
    public List<Modifier> Modifiers { get; init; } = new();
    public List<TypedSpec> Effects { get; init; } = new();
    public AiHint? Ai { get; init; }
    public string Color { get; init; } = "#ffffff";
    public string? Icon { get; init; }
}

/// <summary>Monster rarity tiers: normal, magic, rare, champion, crowned. Rolled at spawn, data all the way.</summary>
public sealed record MonsterRarityDef : Definition
{
    public double Weight { get; init; } = 1;
    [JsonPropertyName("affix_count")]
    public int AffixCount { get; init; }
    public List<Modifier> Modifiers { get; init; } = new();
    [JsonPropertyName("size_scale")]
    public double SizeScale { get; init; } = 1;
    [JsonPropertyName("xp_multiplier")]
    public double XpMultiplier { get; init; } = 1;
    [JsonPropertyName("bonus_loot_rolls")]
    public int BonusLootRolls { get; init; }
    public int Rank { get; init; }
    public string Color { get; init; } = "#ffffff";
    /// <summary>Monster affix pool to draw from.</summary>
    [JsonPropertyName("affix_pool")]
    public TagSet AffixPool { get; init; } = TagSet.Empty;
}

/// <summary>An affix for monsters: a named bundle of modifiers, optionally a weave.</summary>
public sealed record MonsterAffixDef : Definition
{
    public double Weight { get; init; } = 1;
    public List<Modifier> Modifiers { get; init; } = new();
    public List<WeaveSpec> Weaves { get; init; } = new();
    public TagSet Pool { get; init; } = TagSet.Empty;
}
