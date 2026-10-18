namespace HollowWake.Core.Weaves.Probes;

/// <summary>
/// The quantities the mechanism probes read through both doors, each an effect type and the parameter its node
/// writes, with the weave attribute the handler falls back to when the node leaves the parameter out. The attribute
/// is the one <see cref="AxisBindings"/> declares, the registry the composition lint reads as well
/// (<c>CompositionLint.FallbackAttribute</c>), so a probe and the lint cannot disagree about a door, and a test holds
/// every entry to the lint's answer. <see cref="WeaveProbe.Holds(System.Text.Json.Nodes.JsonObject, string, Func{double, bool})"/>
/// opens a quantity's attribute door only through this list, so a probe of a new quantity lists it here first.
/// </summary>
public static class ProbeBindings
{
    private static readonly (string Type, string Parameter)[] Quantities =
    {
        ("projectile", "forks"),
        ("projectile", "chains"),
        ("projectile", "rehit"),
        ("projectile", "bounce"),
        ("projectile", "homing"),
        ("projectile", "guide"),
        ("projectile", "erratic"),
        ("projectile", "spiral"),
        ("projectile", "orbit"),
        ("projectile", "wave"),
        ("projectile", "accel"),
        ("projectile", "recurve"),
        ("projectile", "converge"),
        ("deploy", "count"),
        ("spawn", "count"),
        ("cascade", "count"),
    };

    /// <summary>Every quantity the probes read through both doors.</summary>
    public static IReadOnlyList<(string Type, string Parameter)> All => Quantities;

    /// <summary>The weave attribute a listed quantity falls back to; null where its handler reads none (a spawn's count).</summary>
    public static string? AttributeOf(string type, string parameter)
    {
        if (Array.IndexOf(Quantities, (type, parameter)) < 0)
        {
            throw new InvalidOperationException($"no probe reads '{type}.{parameter}' through both doors; list it in ProbeBindings first.");
        }
        return AxisBindings.FallbackOf(type, parameter)?.Attribute;
    }
}
