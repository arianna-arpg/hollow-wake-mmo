using System.Collections.Concurrent;

namespace Skein.Core.Expressions;

/// <summary>
/// Registry of numeric functions available to expressions. Mods and systems can add their own with
/// <see cref="Register"/>; built-ins cover the arithmetic content authors reach for most.
/// </summary>
public static class ExprFunctions
{
    public sealed record Function(string Name, int MinArgs, int MaxArgs, Func<double[], double> Impl);

    private static readonly ConcurrentDictionary<string, Function> Registry = new(StringComparer.Ordinal);

    static ExprFunctions()
    {
        Register("min", 1, 64, a => a.Min());
        Register("max", 1, 64, a => a.Max());
        Register("clamp", 3, 3, a => Math.Clamp(a[0], Math.Min(a[1], a[2]), Math.Max(a[1], a[2])));
        Register("floor", 1, 1, a => Math.Floor(a[0]));
        Register("ceil", 1, 1, a => Math.Ceiling(a[0]));
        Register("round", 1, 2, a => a.Length == 1 ? Math.Round(a[0], MidpointRounding.AwayFromZero) : Math.Round(a[0], (int)a[1], MidpointRounding.AwayFromZero));
        Register("trunc", 1, 1, a => Math.Truncate(a[0]));
        Register("abs", 1, 1, a => Math.Abs(a[0]));
        Register("sign", 1, 1, a => Math.Sign(a[0]));
        Register("sqrt", 1, 1, a => a[0] <= 0 ? 0 : Math.Sqrt(a[0]));
        Register("pow", 2, 2, a => Math.Pow(a[0], a[1]));
        Register("log", 1, 2, a => a[0] <= 0 ? 0 : (a.Length == 1 ? Math.Log(a[0]) : Math.Log(a[0], a[1])));
        Register("exp", 1, 1, a => Math.Exp(a[0]));
        Register("lerp", 3, 3, a => a[0] + (a[1] - a[0]) * a[2]);
        Register("inverse_lerp", 3, 3, a => Math.Abs(a[1] - a[0]) < 1e-12 ? 0 : (a[2] - a[0]) / (a[1] - a[0]));
        Register("step", 2, 2, a => a[1] >= a[0] ? 1 : 0);
        Register("smoothstep", 3, 3, a =>
        {
            var t = Math.Clamp((a[2] - a[0]) / Math.Max(1e-12, a[1] - a[0]), 0, 1);
            return t * t * (3 - 2 * t);
        });
        Register("pct", 1, 1, a => a[0] / 100.0);
        Register("avg", 1, 64, a => a.Average());
        Register("sum", 1, 64, a => a.Sum());
        // Diminishing returns curve common in ARPG mitigation: x / (x + k).
        Register("diminish", 2, 2, a => a[0] + a[1] <= 0 ? 0 : a[0] / (a[0] + a[1]));
    }

    public static void Register(string name, int minArgs, int maxArgs, Func<double[], double> impl)
    {
        Registry[name] = new Function(name, minArgs, maxArgs, impl);
    }

    public static bool TryGet(string name, out Function? fn) => Registry.TryGetValue(name, out fn);

    public static IEnumerable<string> Names => Registry.Keys.OrderBy(k => k, StringComparer.Ordinal);
}
