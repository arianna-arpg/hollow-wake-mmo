using System.Collections.Concurrent;

namespace HollowWake.Core.Expressions;

/// <summary>
/// Named shaping curves over a parameter: channel ramps, charge-ups and size envelopes name one instead of
/// carrying a formula, and expressions read one with <c>curve("name", t)</c>. The parameter is a fraction
/// from zero to one for envelopes, or seconds held for ramps; curves meant for fractions clamp it. Mods
/// register more with <see cref="Register"/>, before content that names them is parsed.
/// </summary>
public static class Curves
{
    public delegate double Curve(double t);

    private static readonly ConcurrentDictionary<string, (Curve Impl, bool PreviewSafe)> Registry = new(StringComparer.Ordinal);

    static Curves()
    {
        RegisterPure("linear", static t => t);
        RegisterPure("quadratic", static t => t * t);
        RegisterPure("quad_in", static t => t * t);
        RegisterPure("quad_out", static t =>
        {
            var u = Math.Clamp(t, 0, 1);
            return 1 - (1 - u) * (1 - u);
        });
        RegisterPure("cubic", static t => t * t * t);
        RegisterPure("exponential", static t => Math.Exp(t) - 1);
        RegisterPure("sqrt", static t => t <= 0 ? 0 : Math.Sqrt(t));
        // Rises and falls once over the fraction: zero at both ends, one at the middle.
        RegisterPure("breath", static t => Math.Sin(Math.PI * Math.Clamp(t, 0, 1)));
        RegisterPure("smooth", static t =>
        {
            var u = Math.Clamp(t, 0, 1);
            return u * u * (3 - 2 * u);
        });
    }

    /// <summary>Custom curves must explicitly promise pure deterministic evaluation to participate in previews.</summary>
    public static void Register(string name, Curve curve, bool previewSafe = false) => Registry[name] = (curve, previewSafe);

    public static bool TryGet(string name, out Curve curve)
    {
        var found = Registry.TryGetValue(name, out var entry);
        curve = entry.Impl!;
        return found;
    }

    public static bool IsPreviewSafe(string name) => Registry.TryGetValue(name, out var entry) && entry.PreviewSafe;

    public static bool Has(string name) => Registry.ContainsKey(name);

    /// <summary>The curve's value at <paramref name="t"/>; an unknown name is linear.</summary>
    public static double Evaluate(string name, double t) => Registry.TryGetValue(name, out var curve) ? curve.Impl(t) : t;

    private static void RegisterPure(string name, Curve curve) => Register(name, curve, previewSafe: true);

    public static IEnumerable<string> Names => Registry.Keys.OrderBy(k => k, StringComparer.Ordinal);
}
