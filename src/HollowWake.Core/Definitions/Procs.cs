using System.Text.Json.Serialization;
using HollowWake.Core.Expressions;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Definitions;

/// <summary>
/// A proc: a chance, granted by any modifier source through an attribute, to run an effect graph when a trigger
/// kind fires for its owner. <see cref="Definition.Tags"/> are the tags the triggering event must carry (a
/// melee hit, a spell crit). The proc service subscribes through the trigger hub, rolls from per-owner Rng
/// streams, and keeps internal cooldowns, per-minute rates and depth limits (rules <c>procs.*</c>).
/// </summary>
public sealed record ProcDef : Definition
{
    /// <summary>The trigger kind: hit, crit, kill, hurt, block, evade, status_applied, cast, heal ...</summary>
    public required string On { get; init; }

    /// <summary>
    /// Tags the triggering event must not carry. Damage over time by default, so a burning tick is not a hit
    /// for an on-hit proc; <c>excludes: []</c> lets ticks through.
    /// </summary>
    public TagSet Excludes { get; init; } = TagSet.Of("dot");

    /// <summary>An expression on the owner that must hold; it also reads the trigger's <c>amount</c>, <c>crit</c> and <c>roll</c>.</summary>
    public Expr? When { get; init; }

    /// <summary>An expression on the other body of the event (the struck, the striker, the slain) that must hold.</summary>
    public Expr? Victim { get; init; }

    /// <summary>The attribute holding the chance; <c>proc.{id}</c> when left out.</summary>
    public string? Chance { get; init; }

    /// <summary>Seconds after firing before the proc can fire again for the same owner.</summary>
    public double Icd { get; init; }

    /// <summary>Procs per minute: the chance per event grows with the time since the owner's last check.</summary>
    public double Ppm { get; init; }

    /// <summary>Chance kept per level of effect depth; rule <c>procs.depth_falloff</c> when left out.</summary>
    public double? Falloff { get; init; }

    /// <summary>Who the payload targets: <c>other</c> (the event's other body, the default) or <c>owner</c>.</summary>
    public string Target { get; init; } = "other";

    /// <summary>The payload's hits never crit (they carry the <c>no_crit</c> tag that rule <c>combat.stage_skips</c> reads).</summary>
    [JsonPropertyName("no_crit")]
    public bool NoCrit { get; init; }

    public List<TypedSpec> Effects { get; init; } = new();
}
