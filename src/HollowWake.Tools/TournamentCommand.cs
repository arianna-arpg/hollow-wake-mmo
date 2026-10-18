using HollowWake.Core.Content;
using HollowWake.Core.Evaluation;

namespace HollowWake.Tools;

/// <summary>
/// <c>hollowwake tournament</c>: seeded duels across pairings, as a table and, with <c>--out</c>, as JSON.
/// <c>--pairs a:b,c:d</c> names the pairings (the proving pairs by default), <c>--seeds</c> how many fights each,
/// <c>--seconds</c> the cap, <c>--bands</c> checks the evaluation bands and fails the command on a breach.
/// </summary>
public static class TournamentCommand
{
    private const string ProvingPairs = "proving_reaver:proving_bulwark,proving_hexer:proving_plaguer,proving_pitfighter:proving_mower,proving_fletcher:proving_evoker";

    public static int Run(ContentRegistry content, Func<string, string, string> opt, Func<string, bool> flag)
    {
        var pairings = opt("pairs", ProvingPairs).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(Pairing.Parse).ToList();
        var seeds = int.Parse(opt("seeds", "10"));
        var seconds = double.Parse(opt("seconds", "60"));
        var bands = flag("bands");
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
        var outPath = opt("out", "");
        if (outPath.Length > 0)
        {
            File.WriteAllText(outPath, report.ToJson());
            Console.WriteLine($"wrote {outPath}");
        }
        if (bands)
        {
            Console.WriteLine(report.Passed ? "bands: pass" : "bands: FAIL");
            return report.Passed ? 0 : 1;
        }
        return 0;
    }
}
