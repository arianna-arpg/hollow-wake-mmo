using HollowWake.Core.Content;
using HollowWake.Core.Definitions;

namespace HollowWake.Core.Evaluation;

/// <summary>
/// Rules group <c>evaluation</c>: every rule the harness reads. The pairings it fights, how a duel is staged, how many
/// fights a run takes and its cap unless the command names others, and the bands. The harness has no number or name
/// of its own for any of them: a rule that is missing or of the wrong kind throws, naming it, and validation
/// (<c>Content/Kinds/EvaluationKinds.cs</c>) refuses such content, and any rule in the group the harness does not
/// read, before a run.
/// </summary>
public static class EvaluationRules
{
    /// <summary>The rules group.</summary>
    public const string Group = "evaluation";

    /// <summary>The proving pairs, matched pairings written a:b that a tournament fights unless told otherwise.</summary>
    public const string ProvingPairs = "proving_pairs";

    /// <summary>The standard duels, written a:b: the pairings whose seeded traces are the determinism baseline.</summary>
    public const string StandardDuels = "standard_duels";

    /// <summary>How far apart a duel's two bodies spawn, in metres.</summary>
    public const string SpawnDistance = "spawn_distance";

    /// <summary>The two factions a duel's sides fight under, a's then b's.</summary>
    public const string Factions = "factions";

    /// <summary>The fights per pairing a run takes unless the command names a number.</summary>
    public const string Seeds = "seeds";

    /// <summary>The cap in seconds a run's fights take unless the command names one.</summary>
    public const string Seconds = "seconds";

    /// <summary>The bands: the least and the most the median time to kill may be, in seconds.</summary>
    public const string TtkMin = "ttk_min";
    public const string TtkMax = "ttk_max";

    /// <summary>The bands: the share of fights either side may win, the share that may end in a draw, and the coverage each side must reach.</summary>
    public const string WinRateCeiling = "win_rate_ceiling";
    public const string DrawCeiling = "draw_ceiling";
    public const string CoverageFloor = "coverage_floor";

    /// <summary>The bands, in the order <see cref="Evaluation.Bands"/> holds them.</summary>
    public static readonly IReadOnlyList<string> Bands = new[] { TtkMin, TtkMax, WinRateCeiling, DrawCeiling, CoverageFloor };

    /// <summary>The bands that are shares, from 0 to 1.</summary>
    public static readonly IReadOnlyList<string> Shares = new[] { WinRateCeiling, DrawCeiling, CoverageFloor };

    /// <summary>Every rule of the group: the only names it may hold.</summary>
    public static readonly IReadOnlyList<string> All = new[] { ProvingPairs, StandardDuels, SpawnDistance, Factions, Seeds, Seconds }.Concat(Bands).ToArray();

    /// <summary>A rule's path, <c>evaluation.{name}</c>.</summary>
    public static string Path(string name) => $"{Group}.{name}";

    /// <summary>A number of the group; throws naming the rule when it is missing or not a number.</summary>
    public static double Number(ContentRegistry content, string name)
    {
        var value = content.Rules.Number(Path(name), double.NaN);
        return double.IsNaN(value) ? throw new InvalidDataException($"rule {Path(name)} is missing or not a number") : value;
    }

    /// <summary>A whole number of the group, at least one; throws naming the rule otherwise.</summary>
    public static int Count(ContentRegistry content, string name)
    {
        var value = Number(content, name);
        return value >= 1 && value <= int.MaxValue && value == Math.Floor(value)
            ? (int)value
            : throw new InvalidDataException(FormattableString.Invariant($"rule {Path(name)} is a whole number of at least 1, not {value}"));
    }

    /// <summary>The two factions a duel's sides fight under, a's then b's; throws naming the rule unless it lists two different ids.</summary>
    public static (string A, string B) FactionsOf(ContentRegistry content)
    {
        var ids = content.Rules.Strings(Path(Factions));
        if (ids.Count != 2 || ids[0].Length == 0 || ids[1].Length == 0 || ids[0] == ids[1])
            throw new InvalidDataException($"rule {Path(Factions)} lists the two factions a duel's sides fight under, a's then b's, not [{string.Join(", ", ids)}]");
        // Missing factions will be added hostile by the arena. For existing definitions, honor the same
        // explicit, reciprocal, then default lookup as Relations.StanceBetween without mutating content.
        var a = content.Get<FactionDef>(ids[0]) ?? new FactionDef { Id = ids[0], Relations = { [ids[1]] = Stance.Hostile } };
        var b = content.Get<FactionDef>(ids[1]) ?? new FactionDef { Id = ids[1], Relations = { [ids[0]] = Stance.Hostile } };
        static Stance Toward(FactionDef from, FactionDef to) => from.Relations.TryGetValue(to.Id, out var direct)
            ? direct : to.Relations.TryGetValue(from.Id, out var reciprocal) ? reciprocal : from.Default;
        if (Toward(a, b) != Stance.Hostile || Toward(b, a) != Stance.Hostile)
            throw new InvalidDataException($"rule {Path(Factions)} requires mutually hostile factions; {a.Id} and {b.Id} are not hostile in both directions.");
        return (ids[0], ids[1]);
    }
}
