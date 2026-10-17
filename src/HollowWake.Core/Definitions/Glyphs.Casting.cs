using System.Text.Json.Serialization;
using HollowWake.Core.Attributes;
using HollowWake.Core.Expressions;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Definitions;

// Casting fields of a glyph: the trigger that makes its host weave cast itself on an event, and the slot
// behaviors a glyph can levy or grant. Where a weave can hold only one of a kind (a use bank, a charge
// spend, a convert, a cycle, a self stack, a combo chain) the first active glyph in socket order wins
// over later glyphs and over the form; triggers, gates, metas, follow-ups and worn modifiers add up.
public sealed partial record GlyphDef
{
    /// <summary>Casts the host weave when an event of the named trigger kind happens to its caster.</summary>
    public TriggerSpec? Trigger { get; init; }

    /// <summary>Conditions on the caster the host must meet to be used.</summary>
    public List<Expr> Gates { get; init; } = new();

    /// <summary>Charges the host requires and spends when it resolves.</summary>
    public SpendSpec? Spend { get; init; }

    /// <summary>A bank of use charges granted to the host.</summary>
    public UsesSpec? Uses { get; init; }

    /// <summary>While the expression holds, pressing the host's slot casts another weave instead.</summary>
    public ConvertSpec? Convert { get; init; }

    /// <summary>A meta action run after the form's and earlier glyphs' metas.</summary>
    public MetaSpec? Meta { get; init; }

    /// <summary>Modifiers the caster wears while the host sits in a slot.</summary>
    [JsonPropertyName("equip_modifiers")]
    public List<Modifier> EquipModifiers { get; init; } = new();

    /// <summary>Another weave cast a beat after each use of the host.</summary>
    [JsonPropertyName("follow_up")]
    public FollowUpSpec? FollowUp { get; init; }

    public CycleSpec? Cycle { get; init; }

    [JsonPropertyName("self_stack")]
    public SelfStackSpec? SelfStack { get; init; }

    public ComboSpec? Chain { get; init; }
}

/// <summary>
/// Cast on an event: <c>{ on, chance, icd, life_fraction, status, power, max_cast_time, tags }</c>. <c>on</c>
/// names a trigger kind from the trigger registry (<c>hit</c>, <c>crit</c>, <c>kill</c>, <c>hurt</c> ...).
/// The chance is the base of attribute <c>trigger.chance</c> in the weave's scope, capped by rule
/// <c>triggers.chance_cap</c>.
/// </summary>
public sealed record TriggerSpec
{
    public required string On { get; init; }
    /// <summary>Chance per event; the base of attribute <c>trigger.chance</c>. One when absent.</summary>
    public Expr? Chance { get; init; }
    /// <summary>Internal cooldown in seconds, per trigger.</summary>
    public double Icd { get; init; }
    /// <summary>For damage-taken triggers: the fraction of maximum life that must accumulate.</summary>
    [JsonPropertyName("life_fraction")]
    public double? LifeFraction { get; init; }
    /// <summary>For status triggers: the status that must be applied.</summary>
    public string? Status { get; init; }
    /// <summary>Power banked from the event, readable by the triggered cast as <c>trigger.power</c>; <c>amount</c> is the event's amount.</summary>
    public Expr? Power { get; init; }
    /// <summary>Weaves with a longer base cast time cannot be triggered unless a permit lifts the limit. Null takes rule <c>triggers.max_cast_time</c>.</summary>
    [JsonPropertyName("max_cast_time")]
    public double? MaxCastTime { get; init; }
    /// <summary>The event must carry one of these tags (a melee hit: <c>[melee]</c>).</summary>
    public TagSet Tags { get; init; } = TagSet.Empty;
}
