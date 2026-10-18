using static HollowWake.Core.Weaves.AxisUnits;

namespace HollowWake.Core.Weaves.Axes;

/// <summary>
/// The mechanisms family: what the compiled weave states it can do, its declared mechanisms and every probe
/// that holds on it, each on the dimension <see cref="AxisBindings"/> gives the probe. A mechanism with no
/// dimension declared (a plain word a form declares, such as <c>spell</c>) places nothing.
/// </summary>
internal static class MechanismAxes
{
    public static void Read(AxisReading r)
    {
        var weave = r.Weave;
        foreach (var mechanism in weave.Mechanisms)
        {
            if (!AxisBindings.TryMechanism(mechanism, out var dimension))
            {
                continue;
            }
            var declared = weave.DeclaredMechanisms.HasExact(mechanism);
            r.Add(dimension, "mechanism", null, mechanism, Word, declared ? DeclaredBy(r, mechanism) : AxisSource.Probe, declared ? "weave.mechanisms" : "weave.probes");
        }
    }

    /// <summary>Who declares a mechanism: the form, else the preset, else a glyph.</summary>
    private static AxisSource DeclaredBy(AxisReading r, string mechanism) =>
        r.Weave.Form.Mechanisms.HasExact(mechanism) ? AxisSource.Form
        : r.Preset?.Mechanisms.HasExact(mechanism) == true ? AxisSource.Preset
        : AxisSource.Glyph;
}
