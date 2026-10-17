using HollowWake.Core.Ecs;

namespace HollowWake.Core.Components;

/// <summary>
/// Named charges an entity has banked (fury, frenzy, focus ...). Change them through the charge service,
/// which keeps each count mirrored as the gauge <c>charge.{id}</c> that modifiers and formulas read.
/// </summary>
public sealed class Charges : IComponent
{
    public Dictionary<string, double> Counts { get; } = new(StringComparer.Ordinal);

    public double Count(string id) => Counts.TryGetValue(id, out var v) ? v : 0;
}
