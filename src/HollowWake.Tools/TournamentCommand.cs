using System.Globalization;
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
/// fails the command on a change.
/// </summary>
public static class TournamentCommand
{
    public static int Run(ContentRegistry content, Func<string, string, string> opt, Func<string, bool> flag)
    {
        var checkPath = opt("check-hashes", "");
        var baseline = checkPath.Length > 0 ? TraceBaseline.FromJson(File.ReadAllText(checkPath)) : null;
        var listed = opt("pairs", "");
        var rule = flag("standard") ? Pairing.StandardDuels : Pairing.ProvingPairs;
        var pairings = listed.Length > 0
            ? listed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(Pairing.Parse).ToList()
            : Pairing.Listed(content, rule);
        if (pairings.Count == 0)
        {
            Console.WriteLine($"no pairings to fight: rule {rule} lists none");
            return 1;
        }
        var seeds = int.Parse(opt("seeds", baseline?.Seeds.ToString(CultureInfo.InvariantCulture) ?? "10"), CultureInfo.InvariantCulture);
        var seconds = double.Parse(opt("seconds", baseline?.Seconds.ToString(CultureInfo.InvariantCulture) ?? "60"), CultureInfo.InvariantCulture);
        var bands = flag("bands");
        var hashes = flag("hashes");
        var report = Tournament.Run(content, pairings, seeds, seconds, bands);
        Console.WriteLine($"{"pairing",-44} {"fights",6} {"a wins",7} {"b wins",7} {"draws",6} {"ttk med",8} {"low",6} {"high",6} {"kit a",6} {"kit b",6} {"spr a",6} {"spr b",6}");
        foreach (var p in report.Pairs)
        {
            Console.WriteLine($"{p.A + " v " + p.B,-44} {p.Fights,6} {p.WinRateA,7:0.00} {p.WinRateB,7:0.00} {p.DrawRate,6:0.00} {p.TtkMedian,8:0.#} {p.TtkLow,6:0.#} {p.TtkHigh,6:0.#} {p.VarietyA,6:0.00} {p.VarietyB,6:0.00} {p.SpreadA,6:0.00} {p.SpreadB,6:0.00}");
            Console.WriteLine($"    {p.A}: {string.Join(" ", p.WeavesA)} | {string.Join(" ", p.MechanismsA)}");
            Console.WriteLine($"    {p.B}: {string.Join(" ", p.WeavesB)} | {string.Join(" ", p.MechanismsB)}");
            foreach (var breach in p.Breaches)
            {
                Console.WriteLine($"    BREACH {breach}");
            }
        }
        if (hashes)
        {
            Console.WriteLine($"trace hashes over {report.Seeds} seeds of {report.Seconds:0.#} s");
            foreach (var p in report.Pairs)
            {
                Console.WriteLine($"    {p.A + ":" + p.B,-44} {p.Hash}");
            }
            Console.WriteLine($"    {"run",-44} {report.Hash}");
        }
        var outPath = opt("out", "");
        if (outPath.Length > 0)
        {
            File.WriteAllText(outPath, hashes ? TraceBaseline.Of(report).ToJson() : report.ToJson());
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
            Console.WriteLine(report.Passed ? "bands: pass" : "bands: FAIL");
            failed |= !report.Passed;
        }
        return failed ? 1 : 0;
    }
}
