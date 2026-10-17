using System.Text.Json.Serialization;
using HollowWake.Core.Expressions;

namespace HollowWake.Core.Definitions;

// Casting fields of a glyph: the trigger that makes its host weave cast itself on an event.
public sealed partial record GlyphDef
{
    /// <summary>Casts the host weave when an event of the named trigger kind happens to its caster.</summary>
    public TriggerSpec? Trigger { get; init; }
}

/// <summary>
/// Cast on an event: <c>{ on, chance, icd, life_fraction, status, power, max_cast_time }</c>. <c>on</c>
/// names a trigger kind from the trigger registry (<c>hit</c>, <c>crit</c>, <c>kill</c>, <c>hurt</c> ...).
/// </summary>
public sealed record TriggerSpec
{
    public required string On { get; init; }
    /// <summary>Chance per event, scaled by attribute <c>trigger.chance</c>.</summary>
    public Expr? Chance { get; init; }
    /// <summary>Internal cooldown in seconds.</summary>
    public double Icd { get; init; }
    /// <summary>For damage-taken triggers: the fraction of maximum life that must accumulate.</summary>
    [JsonPropertyName("life_fraction")]
    public double? LifeFraction { get; init; }
    /// <summary>For status triggers: the status that must be applied.</summary>
    public string? Status { get; init; }
    /// <summary>Power banked from the event, readable by the triggered cast.</summary>
    public Expr? Power { get; init; }
    /// <summary>Weaves with a longer base cast time cannot be triggered unless a permit lifts the limit.</summary>
    [JsonPropertyName("max_cast_time")]
    public double? MaxCastTime { get; init; }
}
