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
/// fails the command on a change. An option the command does not know, one that needs a value and has none, one given
/// twice, a word that is no option's value, a bad number, an unknown pattern, a missing baseline or output folder each
/// stop the command with one line before any fight. The table names every warning and error the simulation logged, and
/// an error fails the command, naming the first.
/// </summary>
public static class TournamentCommand
{
    /// <summary>The options that take a value. <c>--content</c> is the tool's own, read before the command runs.</summary>
    private static readonly string[] Valued = { "pairs", "seeds", "seconds", "out", "check-hashes", "content" };

    /// <summary>The options that stand alone.</summary>
    private static readonly string[] Flags = { "standard", "bands", "hashes" };

    private const string Usage = "the tournament takes --pairs a:b,c:d, --standard, --seeds n, --seconds s, --bands, --hashes, --out file, --check-hashes file and --content dirs";

    /// <summary>Runs the command over <paramref name="args"/>, the words after its name, writing to <paramref name="output"/>; returns the exit code.</summary>
    public static int Run(ContentRegistry content, IReadOnlyList<string> args, TextWriter output)
    {
        if (!TryRead(args, out var options, out var flags, out var wrong))
        {
            return Refuse(output, wrong);
        }
        var checkPath = options.GetValueOrDefault("check-hashes", "");
        TraceBaseline? baseline = null;
        if (checkPath.Length > 0)
        {
            if (!File.Exists(checkPath))
            {
                return Refuse(output, $"--check-hashes: no baseline at {checkPath}");
            }
            try
            {
                baseline = TraceBaseline.FromJson(File.ReadAllText(checkPath));
            }
            catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException or UnauthorizedAccessException)
            {
                return Refuse(output, $"--check-hashes: {checkPath} is not a readable trace baseline ({ex.Message.ReplaceLineEndings(" ")})");
            }
        }
        if (!TryPairings(content, options, flags, out var pairings, out var problem))
        {
            return Refuse(output, problem);
        }
        var seedsText = options.GetValueOrDefault("seeds") ?? baseline?.Seeds.ToString(CultureInfo.InvariantCulture) ?? "10";
        if (!int.TryParse(seedsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seeds) || seeds < 1)
        {
            return Refuse(output, $"--seeds takes a whole number of at least 1, not '{seedsText}'");
        }
        var secondsText = options.GetValueOrDefault("seconds") ?? baseline?.Seconds.ToString(CultureInfo.InvariantCulture) ?? "60";
        if (!double.TryParse(secondsText, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) || !double.IsFinite(seconds) || seconds <= 0)
        {
            return Refuse(output, $"--seconds takes a number of seconds above 0, not '{secondsText}'");
        }
        var outPath = options.GetValueOrDefault("out", "");
        if (outPath.Length > 0 && Path.GetDirectoryName(Path.GetFullPath(outPath)) is { } folder && !Directory.Exists(folder))
        {
            return Refuse(output, $"--out: the folder {folder} does not exist");
        }
        var bands = flags.Contains("bands");
        var hashes = flags.Contains("hashes");
        if (bands)
        {
            try
            {
                Bands.Of(content);
            }
            catch (InvalidDataException ex)
            {
                return Refuse(output, $"--bands: {ex.Message}");
            }
        }
        var report = Tournament.Run(content, pairings, seeds, seconds, bands);
        foreach (var line in report.Table())
        {
            output.WriteLine(line);
        }
        if (hashes)
        {
            foreach (var line in report.HashLines())
            {
                output.WriteLine(line);
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
                return Refuse(output, $"--out: could not write {outPath} ({ex.Message.ReplaceLineEndings(" ")})");
            }
            output.WriteLine($"wrote {outPath}");
        }
        var failed = false;
        if (baseline is not null)
        {
            var differences = baseline.Differences(report);
            if (baseline.Platform != TraceBaseline.CurrentPlatform)
            {
                output.WriteLine($"hashes: the baseline was recorded on {baseline.Platform ?? "an unnamed platform"} and this run is on {TraceBaseline.CurrentPlatform}; a difference here is a finding to record, not a reason to overwrite it");
            }
            output.WriteLine(differences.Count == 0 ? $"hashes: identical to {checkPath}" : $"hashes: CHANGED against {checkPath}");
            foreach (var difference in differences)
            {
                output.WriteLine($"    {difference}");
            }
            failed |= differences.Count > 0;
        }
        if (bands)
        {
            output.WriteLine(report.Passed == true ? "bands: pass" : "bands: FAIL");
            failed |= report.Passed != true;
        }
        // A fight on content the simulation could not run is no measurement: an error it logged fails the command.
        if (report.FirstError() is { } error)
        {
            output.WriteLine($"errors: the simulation logged an error, the first in {error}");
            failed = true;
        }
        return failed ? 1 : 0;
    }

    /// <summary>
    /// Reads the words after the command's name. Every option must be one the command knows; one that takes a value
    /// must have it next, a word that is not itself an option; none may come twice; and every other word must be an
    /// option's value. A problem names the first word that breaks this, and what the command takes.
    /// </summary>
    private static bool TryRead(IReadOnlyList<string> args, out Dictionary<string, string> options, out HashSet<string> flags, out string problem)
    {
        options = new Dictionary<string, string>(StringComparer.Ordinal);
        flags = new HashSet<string>(StringComparer.Ordinal);
        problem = "";
        for (var i = 0; i < args.Count; i++)
        {
            var word = args[i];
            if (!word.StartsWith("--", StringComparison.Ordinal))
            {
                problem = $"unexpected word '{word}'; {Usage}";
                return false;
            }
            var name = word[2..];
            if (options.ContainsKey(name) || flags.Contains(name))
            {
                problem = $"{word} is given twice";
                return false;
            }
            if (Flags.Contains(name, StringComparer.Ordinal))
            {
                flags.Add(name);
                continue;
            }
            if (!Valued.Contains(name, StringComparer.Ordinal))
            {
                problem = $"unknown option {word}; {Usage}";
                return false;
            }
            if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                problem = $"{word} needs a value after it; {Usage}";
                return false;
            }
            options[name] = args[++i];
        }
        return true;
    }

    /// <summary>
    /// The pairings to fight: <c>--pairs</c> when given, else the rule <c>--standard</c> or the default names. An empty
    /// entry, one not written a:b, an empty list or a pattern no definition carries is a problem naming it.
    /// </summary>
    private static bool TryPairings(ContentRegistry content, Dictionary<string, string> options, HashSet<string> flags, out IReadOnlyList<Pairing> pairings, out string problem)
    {
        var parsed = new List<Pairing>();
        pairings = parsed;
        problem = "";
        string origin;
        if (options.TryGetValue("pairs", out var listed))
        {
            origin = "--pairs";
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
            origin = flags.Contains("standard") ? Pairing.StandardDuels : Pairing.ProvingPairs;
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

    private static int Refuse(TextWriter output, string message)
    {
        output.WriteLine(message);
        return 1;
    }
}
