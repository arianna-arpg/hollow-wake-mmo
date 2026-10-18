using System.Globalization;
using System.Text.Json;
using HollowWake.Core.Content;
using HollowWake.Core.Evaluation;

namespace HollowWake.Tools;

/// <summary>
/// <c>hollowwake tournament</c>: seeded duels across pairings, as a table and, with <c>--out</c>, as JSON. The
/// pairings are the proving pairs rules group <c>evaluation</c> lists, its standard duels with <c>--standard</c>, or
/// <c>--pairs a:b,c:d</c>; <c>--seeds</c> is how many fights each, <c>--seconds</c> the cap, and <c>--bands</c> checks
/// the evaluation bands and fails the command on a breach. <c>--hashes</c> prints each pairing's trace hash and, with
/// <c>--out</c>, writes them as a baseline instead of the report; <c>--check-hashes FILE</c> fights against such a
/// baseline (with its seeds and cap unless given), prints <c>identical</c> or names each pairing that changed, and
/// fails the command on a change. Bad options, an unknown pattern, a missing baseline or output folder each stop the
/// command with one line before any fight. The table names every warning and error the simulation logged, and an
/// error fails the command, naming the first.
/// </summary>
public static class TournamentCommand
{
    public static int Run(ContentRegistry content, Func<string, string, string> opt, Func<string, bool> flag)
    {
        var checkPath = opt("check-hashes", "");
        TraceBaseline? baseline = null;
        if (checkPath.Length > 0)
        {
            if (!File.Exists(checkPath))
            {
                return Refuse($"--check-hashes: no baseline at {checkPath}");
            }
            try
            {
                baseline = TraceBaseline.FromJson(File.ReadAllText(checkPath));
            }
            catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException or UnauthorizedAccessException)
            {
                return Refuse($"--check-hashes: {checkPath} is not a readable trace baseline ({ex.Message.ReplaceLineEndings(" ")})");
            }
        }
        if (!TryPairings(content, opt, flag, out var pairings, out var problem))
        {
            return Refuse(problem);
        }
        var seedsText = opt("seeds", baseline?.Seeds.ToString(CultureInfo.InvariantCulture) ?? "10");
        if (!int.TryParse(seedsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seeds) || seeds < 1)
        {
            return Refuse($"--seeds takes a whole number of at least 1, not '{seedsText}'");
        }
        var secondsText = opt("seconds", baseline?.Seconds.ToString(CultureInfo.InvariantCulture) ?? "60");
        if (!double.TryParse(secondsText, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) || !double.IsFinite(seconds) || seconds <= 0)
        {
            return Refuse($"--seconds takes a number of seconds above 0, not '{secondsText}'");
        }
        var outPath = opt("out", "");
        if (outPath.Length > 0 && Path.GetDirectoryName(Path.GetFullPath(outPath)) is { } folder && !Directory.Exists(folder))
        {
            return Refuse($"--out: the folder {folder} does not exist");
        }
        var bands = flag("bands");
        var hashes = flag("hashes");
        if (bands)
        {
            try
            {
                Bands.Of(content);
            }
            catch (InvalidDataException ex)
            {
                return Refuse($"--bands: {ex.Message}");
            }
        }
        var report = Tournament.Run(content, pairings, seeds, seconds, bands);
        foreach (var line in report.Table())
        {
            Console.WriteLine(line);
        }
        if (hashes)
        {
            foreach (var line in report.HashLines())
            {
                Console.WriteLine(line);
            }
        }
        if (outPath.Length > 0)
        {
            try
            {
                File.WriteAllText(outPath, hashes ? TraceBaseline.Of(report).ToJson() : report.ToJson());
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return Refuse($"--out: could not write {outPath} ({ex.Message.ReplaceLineEndings(" ")})");
            }
            Console.WriteLine($"wrote {outPath}");
        }
        var failed = false;
        if (baseline is not null)
        {
            var differences = baseline.Differences(report);
            Console.WriteLine(differences.Count == 0 ? $"hashes: identical to {checkPath}" : $"hashes: CHANGED against {checkPath}");
            foreach (var difference in differences)
            {
                Console.WriteLine($"    {difference}");
            }
            failed |= differences.Count > 0;
        }
        if (bands)
        {
            Console.WriteLine(report.Passed == true ? "bands: pass" : "bands: FAIL");
            failed |= report.Passed != true;
        }
        // A fight on content the simulation could not run is no measurement: an error it logged fails the command.
        if (report.FirstError() is { } error)
        {
            Console.WriteLine($"errors: the simulation logged an error, the first in {error}");
            failed = true;
        }
        return failed ? 1 : 0;
    }

    /// <summary>
    /// The pairings to fight: <c>--pairs</c> when given, else the rule <c>--standard</c> or the default names. An empty
    /// entry, one not written a:b, an empty list or a pattern no definition carries is a problem naming it.
    /// </summary>
    private static bool TryPairings(ContentRegistry content, Func<string, string, string> opt, Func<string, bool> flag, out IReadOnlyList<Pairing> pairings, out string problem)
    {
        var parsed = new List<Pairing>();
        pairings = parsed;
        problem = "";
        string origin;
        if (flag("pairs"))
        {
            origin = "--pairs";
            var listed = opt("pairs", "");
            if (listed.Trim().Length == 0)
            {
                problem = "--pairs names no pairing; write --pairs a:b,c:d";
                return false;
            }
            foreach (var entry in listed.Split(','))
            {
                if (entry.Trim().Length == 0)
                {
                    problem = $"--pairs has an empty entry in '{listed}'";
                    return false;
                }
                try
                {
                    parsed.Add(Pairing.Parse(entry));
                }
                catch (ArgumentException ex)
                {
                    problem = $"--pairs: {ex.Message}";
                    return false;
                }
            }
        }
        else
        {
            origin = flag("standard") ? Pairing.StandardDuels : Pairing.ProvingPairs;
            try
            {
                parsed.AddRange(Pairing.Listed(content, origin));
            }
            catch (ArgumentException ex)
            {
                problem = $"rule {origin}: {ex.Message}";
                return false;
            }
            if (parsed.Count == 0)
            {
                problem = $"rule {origin} lists no pairings";
                return false;
            }
        }
        foreach (var pairing in parsed)
        {
            if (Tournament.Refusal(content, pairing) is { } why)
            {
                problem = $"{origin}: {why}";
                return false;
            }
        }
        return true;
    }

    private static int Refuse(string message)
    {
        Console.WriteLine(message);
        return 1;
    }
}
