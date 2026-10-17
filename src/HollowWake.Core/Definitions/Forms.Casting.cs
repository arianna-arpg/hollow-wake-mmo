using System.Text.Json.Serialization;
using HollowWake.Core.Attributes;
using HollowWake.Core.Expressions;

namespace HollowWake.Core.Definitions;

/// <summary>
/// How a weave is committed to. The compiler adds a cast-scope tag from it so modifiers can scope by
/// commitment: <c>cast.instant</c>, <c>cast.timed</c> (a cast with a bar), <c>cast.channel</c> or
/// <c>cast.held</c> (charge-up and other held modes).
/// </summary>
public enum CastMode
{
    Instant,
    Cast,
    Channel,
    /// <summary>Hold to charge, release to cast; the release scales by how full the charge was (<see cref="FormDef.Charge"/>).</summary>
    Charge,
}

// Casting fields of a form: moving while casting, the cooldown clock, channel and charge-up blocks, and
// the trigger that casts the weave on an event. Parsed and validated here; the weave runtime reads them
// as the cast modes land.
public sealed partial record FormDef
{
    /// <summary>The fraction of movement speed kept while casting; joins attribute <c>cast.mobility</c>. Null takes rule <c>casting.cast_move</c>.</summary>
    [JsonPropertyName("cast_move")]
    public Expr? CastMove { get; init; }

    /// <summary>When the cooldown clock starts: <c>press</c> or <c>complete</c>. Null takes rule <c>combat.cooldown_at</c>.</summary>
    [JsonPropertyName("cooldown_at")]
    public string? CooldownAt { get; init; }

    public ChannelSpec? Channel { get; init; }

    /// <summary>The charge-up of <see cref="CastMode.Charge"/>.</summary>
    public ChargeUpSpec? Charge { get; init; }

    /// <summary>Casts this weave when an event of the named trigger kind happens to its caster.</summary>
    public TriggerSpec? Trigger { get; init; }
}

// Preset overrides of the casting fields.
public sealed partial record WeaveDef
{
    /// <summary>Overrides the form's <see cref="FormDef.CooldownAt"/>.</summary>
    [JsonPropertyName("cooldown_at")]
    public string? CooldownAt { get; init; }
}

/// <summary>A channel: pulses while held, with windup, jitter, movement, turning, a hold limit, ramps, a release and a persisting field.</summary>
public sealed record ChannelSpec
{
    /// <summary>Seconds between pulses, divided by the speed attribute like cast time.</summary>
    public Expr? Interval { get; init; }
    /// <summary>Seconds before the first pulse.</summary>
    public Expr? Windup { get; init; }
    /// <summary>Random spread of each interval in seconds, drawn from the slot's random stream.</summary>
    public Expr? Jitter { get; init; }
    /// <summary>The fraction of movement speed kept while channeling.</summary>
    public Expr? Move { get; init; }
    /// <summary>Radians per second the channeler may turn.</summary>
    [JsonPropertyName("turn_rate")]
    public Expr? TurnRate { get; init; }
    /// <summary>Follows the live aim while held.</summary>
    [JsonPropertyName("track_aim")]
    public bool TrackAim { get; init; }
    /// <summary>Seconds the channel may be held at most.</summary>
    [JsonPropertyName("max_hold")]
    public Expr? MaxHold { get; init; }
    /// <summary>Starts the cooldown when the channel ends rather than when it begins.</summary>
    [JsonPropertyName("cooldown_on_end")]
    public bool CooldownOnEnd { get; init; }
    /// <summary>Numbers that grow with held time, as cast-local modifiers built each pulse.</summary>
    public List<RampSpec> Ramps { get; init; } = new();
    /// <summary>A payload when the hold ends.</summary>
    public ReleaseSpec? Release { get; init; }
    /// <summary>A field that keeps pulsing after the release.</summary>
    public PersistSpec? Persist { get; init; }
}

/// <summary>One number ramping with held seconds: <c>{ attribute, form, per, max or min, curve, on }</c>.</summary>
public sealed record RampSpec
{
    public required string Attribute { get; init; }
    public ModifierForm Form { get; init; } = ModifierForm.Increased;
    /// <summary>Value per held second, shaped by <see cref="Curve"/>.</summary>
    public double Per { get; init; }
    public double? Max { get; init; }
    public double? Min { get; init; }
    /// <summary>A named curve from the curve registry: <c>linear</c>, <c>quadratic</c>, <c>exponential</c> ...</summary>
    public string Curve { get; init; } = "linear";
    /// <summary><c>weave</c> joins the weave's fold; <c>caster</c> sits on the caster while the channel lasts.</summary>
    public string On { get; init; } = "weave";
}

/// <summary>What ending a hold fires.</summary>
public sealed record ReleaseSpec
{
    /// <summary>Effects run on release; empty runs the form's own graph.</summary>
    public List<TypedSpec> Effects { get; init; } = new();
    /// <summary>Seconds that must be held for the release to fire.</summary>
    [JsonPropertyName("min_hold")]
    public Expr? MinHold { get; init; }
    /// <summary>Fires only on a full hold.</summary>
    [JsonPropertyName("require_full")]
    public bool RequireFull { get; init; }
    /// <summary>Whether the channel pulses while held; false gathers silently.</summary>
    public bool Pulses { get; init; } = true;
}

/// <summary>A field left behind by a released channel.</summary>
public sealed record PersistSpec
{
    [JsonPropertyName("per_held_second")]
    public Expr? PerHeldSecond { get; init; }
    [JsonPropertyName("max_duration")]
    public Expr? MaxDuration { get; init; }
    public Expr? Fade { get; init; }
}

/// <summary>Hold to charge: a maximum charge time and the numbers the fill scales.</summary>
public sealed record ChargeUpSpec
{
    [JsonPropertyName("max_time")]
    public Expr? MaxTime { get; init; }
    public List<ChargeScale> Scales { get; init; } = new();
}

/// <summary>One number a charge-up scales, from its value at an empty charge to its value at a full one.</summary>
public sealed record ChargeScale
{
    public required string Attribute { get; init; }
    public ModifierForm Form { get; init; } = ModifierForm.More;
    [JsonPropertyName("at_zero")]
    public double AtZero { get; init; }
    [JsonPropertyName("at_full")]
    public double AtFull { get; init; }
}
