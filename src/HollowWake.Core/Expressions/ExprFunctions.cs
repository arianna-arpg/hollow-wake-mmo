using System.Collections.Concurrent;

namespace HollowWake.Core.Expressions;

/// <summary>
/// Registry of numeric functions available to expressions. Mods and systems can add their own with
/// <see cref="Register"/>; built-ins cover the arithmetic content authors reach for most.
/// </summary>
public static class ExprFunctions
{
    /// <summary>
    /// A function body. The arguments arrive already evaluated, in order, as a span over a buffer the
    /// evaluator reuses, so a body reads them during the call and keeps nothing.
    /// </summary>
    public delegate double Body(ReadOnlySpan<double> args);

    public sealed record Function(string Name, int MinArgs, int MaxArgs, Body Impl, bool PreviewSafe = false);

    private static readonly ConcurrentDictionary<string, Function> Registry = new(StringComparer.Ordinal);

    static ExprFunctions()
    {
        RegisterPure("min", 1, 64, Min);
        RegisterPure("max", 1, 64, Max);
        RegisterPure("clamp", 3, 3, static a => Math.Clamp(a[0], Math.Min(a[1], a[2]), Math.Max(a[1], a[2])));
        RegisterPure("floor", 1, 1, static a => Math.Floor(a[0]));
        RegisterPure("ceil", 1, 1, static a => Math.Ceiling(a[0]));
        RegisterPure("round", 1, 2, static a => a.Length == 1 ? Math.Round(a[0], MidpointRounding.AwayFromZero) : Math.Round(a[0], (int)a[1], MidpointRounding.AwayFromZero));
        RegisterPure("trunc", 1, 1, static a => Math.Truncate(a[0]));
        RegisterPure("abs", 1, 1, static a => Math.Abs(a[0]));
        RegisterPure("sign", 1, 1, static a => Math.Sign(a[0]));
        RegisterPure("sqrt", 1, 1, static a => a[0] <= 0 ? 0 : Math.Sqrt(a[0]));
        RegisterPure("pow", 2, 2, static a => Math.Pow(a[0], a[1]));
        RegisterPure("log", 1, 2, static a => a[0] <= 0 ? 0 : (a.Length == 1 ? Math.Log(a[0]) : Math.Log(a[0], a[1])));
        RegisterPure("exp", 1, 1, static a => Math.Exp(a[0]));
        RegisterPure("lerp", 3, 3, static a => a[0] + (a[1] - a[0]) * a[2]);
        RegisterPure("inverse_lerp", 3, 3, static a => Math.Abs(a[1] - a[0]) < 1e-12 ? 0 : (a[2] - a[0]) / (a[1] - a[0]));
        RegisterPure("step", 2, 2, static a => a[1] >= a[0] ? 1 : 0);
        RegisterPure("smoothstep", 3, 3, static a =>
        {
            var t = Math.Clamp((a[2] - a[0]) / Math.Max(1e-12, a[1] - a[0]), 0, 1);
            return t * t * (3 - 2 * t);
        });
        RegisterPure("pct", 1, 1, static a => a[0] / 100.0);
        RegisterPure("avg", 1, 64, static a => Sum(a) / a.Length);
        RegisterPure("sum", 1, 64, Sum);
        // Diminishing returns curve common in ARPG mitigation: x / (x + k).
        RegisterPure("diminish", 2, 2, static a => a[0] + a[1] <= 0 ? 0 : a[0] / (a[0] + a[1]));
    }

    /// <summary>
    /// Preview safety is an explicit promise: a deterministic function of its arguments only, with no
    /// external reads, writes or rolls. Existing registrations remain executable without that promise.
    /// </summary>
    public static void Register(string name, int minArgs, int maxArgs, Body impl, bool previewSafe = false)
    {
        Registry[name] = new Function(name, minArgs, maxArgs, impl, previewSafe);
    }

    private static void RegisterPure(string name, int minArgs, int maxArgs, Body impl) => Register(name, minArgs, maxArgs, impl, previewSafe: true);

    public static bool TryGet(string name, out Function? fn) => Registry.TryGetValue(name, out fn);

    public static IEnumerable<string> Names => Registry.Keys.OrderBy(k => k, StringComparer.Ordinal);

    // min, max, sum and avg keep the exact results of the LINQ operators they replace: NaN anywhere makes
    // min NaN, max ignores NaN unless every value is one, ties keep the earlier value (so signed zeros
    // come out as written), and sums add left to right from zero.

    private static double Min(ReadOnlySpan<double> a)
    {
        var value = a[0];
        if (double.IsNaN(value))
        {
            return value;
        }
        for (var i = 1; i < a.Length; i++)
        {
            var current = a[i];
            if (current < value)
            {
                value = current;
            }
            else if (double.IsNaN(current))
            {
                return current;
            }
        }
        return value;
    }

    private static double Max(ReadOnlySpan<double> a)
    {
        var i = 0;
        var value = a[0];
        while (double.IsNaN(value))
        {
            if (++i >= a.Length)
            {
                return value;
            }
            value = a[i];
        }
        for (i++; i < a.Length; i++)
        {
            var current = a[i];
            if (current > value)
            {
                value = current;
            }
        }
        return value;
    }

    private static double Sum(ReadOnlySpan<double> a)
    {
        var sum = 0.0;
        foreach (var v in a)
        {
            sum += v;
        }
        return sum;
    }
}
