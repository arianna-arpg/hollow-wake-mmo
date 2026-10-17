using System.Collections.Concurrent;

namespace HollowWake.Core.Expressions;

/// <summary>
/// Named shaping curves over a parameter: channel ramps, charge-ups and size envelopes name one instead of
/// carrying a formula. The parameter is a fraction from zero to one for envelopes, or seconds held for
/// ramps; curves meant for fractions clamp it. Mods register more with <see cref="Register"/>.
/// </summary>
public static class Curves
{
    public delegate double Curve(double t);

    private static readonly ConcurrentDictionary<string, Curve> Registry = new(StringComparer.Ordinal);

    static Curves()
    {
        Register("linear", static t => t);
        Register("quadratic", static t => t * t);
        Register("quad_in", static t => t * t);
        Register("quad_out", static t =>
        {
            var u = Math.Clamp(t, 0, 1);
            return 1 - (1 - u) * (1 - u);
        });
        Register("cubic", static t => t * t * t);
        Register("exponential", static t => Math.Exp(t) - 1);
        Register("sqrt", static t => t <= 0 ? 0 : Math.Sqrt(t));
        // Rises and falls once over the fraction: zero at both ends, one at the middle.
        Register("breath", static t => Math.Sin(Math.PI * Math.Clamp(t, 0, 1)));
        Register("smooth", static t =>
        {
            var u = Math.Clamp(t, 0, 1);
            return u * u * (3 - 2 * u);
        });
    }

    public static void Register(string name, Curve curve) => Registry[name] = curve;

    public static bool TryGet(string name, out Curve curve) => Registry.TryGetValue(name, out curve!);

    public static bool Has(string name) => Registry.ContainsKey(name);

    /// <summary>The curve's value at <paramref name="t"/>; an unknown name is linear.</summary>
    public static double Evaluate(string name, double t) => Registry.TryGetValue(name, out var curve) ? curve(t) : t;

    public static IEnumerable<string> Names => Registry.Keys.OrderBy(k => k, StringComparer.Ordinal);
}
