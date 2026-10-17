using HollowWake.Core.Ecs;

namespace HollowWake.Core.Components;

/// <summary>
/// Named charges an entity has banked (fury, frenzy, focus ...). Change them through the charge service,
/// which keeps each count mirrored as the gauge <c>charge.{id}</c> that modifiers and formulas read, and
/// which decays and drains them over time.
/// </summary>
public sealed class Charges : IComponent
{
    public Dictionary<string, double> Counts { get; } = new(StringComparer.Ordinal);

    /// <summary>Each held charge's clock: when it was last gained and any drain burning it down.</summary>
    public Dictionary<string, ChargeClock> Clocks { get; } = new(StringComparer.Ordinal);

    public double Count(string id) => Counts.TryGetValue(id, out var v) ? v : 0;

    /// <summary>The clock of a charge, made on first use.</summary>
    public ChargeClock ClockOf(string id)
    {
        if (!Clocks.TryGetValue(id, out var clock))
        {
            clock = new ChargeClock();
            Clocks[id] = clock;
        }
        return clock;
    }
}

/// <summary>The time state of one held charge.</summary>
public sealed class ChargeClock
{
    /// <summary>When charges of this kind were last gained.</summary>
    public double LastGain = double.NegativeInfinity;

    /// <summary>Charges burned per second while draining; gains are refused while it is above zero.</summary>
    public double Drain;

    /// <summary>A fraction of a charge lost to decay or drain and not yet taken off the count.</summary>
    public double Owed;
}
