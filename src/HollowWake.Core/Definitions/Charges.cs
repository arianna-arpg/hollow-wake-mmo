using HollowWake.Core.Expressions;

namespace HollowWake.Core.Definitions;

/// <summary>
/// A named charge: banked by effects and hits, spent by casts, decaying or draining over time. Counts live in
/// the <c>Charges</c> component behind the charge service and read as gauges named <c>charge.{id}</c>; the most
/// a body holds is its attribute <c>charge.{id}.max</c> (a family member, or declared one by one where a kind
/// wants its own base). The definition says how charges fade.
/// </summary>
public sealed record ChargeDef : Definition
{
    /// <summary>Seconds charges last after the last gain, read on the holder; none means they never decay.</summary>
    public Expr? Duration { get; init; }

    /// <summary>Charges lost per second once the duration has run out; zero loses them all at once.</summary>
    public double Decay { get; init; }

    public string Color { get; init; } = "#ffffff";
    public string? Icon { get; init; }
}
