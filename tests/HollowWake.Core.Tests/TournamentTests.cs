using HollowWake.Core.Content;
using HollowWake.Core.Evaluation;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>
/// The evaluation harness (docs/design/evaluation.md): seeded duels across pairings measured from the
/// simulation's events, repeating exactly, with bands read from content that name what they break, pairings
/// listed in content, and trace hashes a baseline holds a later run to.
/// </summary>
public class TournamentTests
{
    private static string Scratch(string name)
    {
        var dir = Path.Combine(Path.GetTempPath(), "hollowwake-" + name + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void TheSamePairingAndSeedGiveTheSameTraceHash()
    {
        var pairing = new Pairing("wolf", "hob");
        var first = Tournament.Duel(CoreContent.Registry, pairing, index: 0, seconds: 20);
        var again = Tournament.Duel(CoreContent.Registry, pairing, index: 0, seconds: 20);
        Assert.Matches("^[0-9a-f]{16}$", first.Hash);
        Assert.Equal(first.Hash, again.Hash);

        // A tournament's fight is the same duel, and its pairing and run hashes fold the fights' in order.
        var report = Tournament.Run(CoreContent.Registry, new[] { pairing }, seeds: 1, seconds: 20, checkBands: false);
        Assert.Equal(first.Hash, Assert.Single(report.Fights).Hash);
        Assert.Matches("^[0-9a-f]{16}$", Assert.Single(report.Pairs).Hash);
        Assert.Matches("^[0-9a-f]{16}$", report.Hash);
    }

    [Fact]
    public void TwoSeedsGiveDifferentTraceHashes()
    {
        var pairing = new Pairing("wolf", "hob");
        var first = Tournament.Duel(CoreContent.Registry, pairing, index: 0, seconds: 20);
        var second = Tournament.Duel(CoreContent.Registry, pairing, index: 1, seconds: 20);
        Assert.NotEqual(first.Seed, second.Seed);
        Assert.NotEqual(first.Hash, second.Hash);
    }

    [Fact]
    public void TheProvingPairsComeFromTheRule()
    {
        Assert.Contains(new Pairing("proving_reaver", "proving_bulwark"), Pairing.Listed(CoreContent.Registry, Pairing.ProvingPairs));
        Assert.Contains(new Pairing("wolf", "hob"), Pairing.Listed(CoreContent.Registry, Pairing.StandardDuels));
        var dir = Scratch("evaluation");
        try
        {
            File.WriteAllText(Path.Combine(dir, "rules.yaml"), """
kind: rules
entries:
  - id: evaluation
    patch: true
    values:
      proving_pairs: ["wolf:boar", "hob:loosed_kindler"]
""".Replace("\r\n", "\n"));
            var registry = ContentRegistry.Load(new[] { CoreContent.FindContentDir(), dir }, out var report);
            Assert.Empty(report.Errors);
            Assert.Equal(new[] { new Pairing("wolf", "boar"), new Pairing("hob", "loosed_kindler") }, Pairing.Listed(registry, Pairing.ProvingPairs));
            // The layer replaced one list and left the rest of the group as core wrote it.
            Assert.Equal(Pairing.Listed(CoreContent.Registry, Pairing.StandardDuels), Pairing.Listed(registry, Pairing.StandardDuels));
            Assert.Equal(Bands.Of(CoreContent.Registry), Bands.Of(registry));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void AListedPairingNamesTwoPatterns()
    {
        var dir = Scratch("evaluation-unknown");
        try
        {
            File.WriteAllText(Path.Combine(dir, "rules.yaml"), """
kind: rules
entries:
  - id: evaluation
    patch: true
    values:
      standard_duels: ["wolf:no_such_pattern", "wolf"]
""".Replace("\r\n", "\n"));
            ContentRegistry.Load(new[] { CoreContent.FindContentDir(), dir }, out var report);
            var errors = report.Errors.Where(e => e.Id == "evaluation").Select(e => e.Message).ToList();
            Assert.Contains(errors, m => m.Contains("standard_duels") && m.Contains("'no_such_pattern'"));
            Assert.Contains(errors, m => m.Contains("standard_duels") && m.Contains("a:b"));
            Assert.Equal(2, errors.Count);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ABaselineNamesEachPairingWhoseHashChanged()
    {
        var report = Tournament.Run(CoreContent.Registry, new[] { new Pairing("wolf", "hob") }, seeds: 1, seconds: 10, checkBands: false);
        var baseline = TraceBaseline.FromJson(TraceBaseline.Of(report).ToJson());
        Assert.Equal(report.Pairs[0].Hash, baseline.Hashes["wolf:hob"]);
        Assert.Empty(baseline.Differences(report));

        var stale = baseline with { Hashes = new Dictionary<string, string> { ["wolf:hob"] = "0000000000000000", ["boar:hob"] = "0000000000000000" } };
        var differences = stale.Differences(report);
        Assert.Equal(2, differences.Count);
        Assert.Contains(differences, d => d.StartsWith("wolf:hob changed from 0000000000000000", StringComparison.Ordinal));
        Assert.Contains(differences, d => d.StartsWith("boar:hob is in the baseline", StringComparison.Ordinal));
        Assert.NotEmpty((baseline with { Seeds = 2 }).Differences(report));
    }

    [Fact]
    public void ASeededTournamentRepeatsExactlyAndMeasuresWhatEachSideUsed()
    {
        var pairings = new[] { new Pairing("proving_pitfighter", "proving_mower") };
        var report = Tournament.Run(CoreContent.Registry, pairings, seeds: 2, seconds: 20, checkBands: false);
        var again = Tournament.Run(CoreContent.Registry, pairings, seeds: 2, seconds: 20, checkBands: false);
        Assert.Equal(report.ToJson(), again.ToJson());

        var pair = Assert.Single(report.Pairs);
        Assert.Equal(2, pair.Fights);
        Assert.Equal(2, report.Fights.Count);
        Assert.NotEqual(report.Fights[0].Seed, report.Fights[1].Seed);
        Assert.All(report.Fights, f =>
        {
            Assert.True(f.SideA.Casts > 0 && f.SideB.Casts > 0, "a side cast nothing");
            Assert.True(f.SideA.DamageDealt > 0 && f.SideB.DamageDealt > 0, "a side dealt nothing");
            Assert.Contains(f.Outcome, new[] { "a", "b", "draw" });
            Assert.InRange(f.Seconds, 0, 20);
        });
        Assert.Contains("one_two", pair.WeavesA);
        Assert.Contains("attack", pair.MechanismsA);
        Assert.True(pair.VarietyA > 0 && pair.VarietyB > 0);
        Assert.Empty(pair.Breaches);
    }

    [Fact]
    public void BandsComeFromContentAndNameTheirBreaches()
    {
        var bands = Bands.Of(CoreContent.Registry);
        Assert.True(bands.TtkMax > bands.TtkMin);
        Assert.InRange(bands.WinRateCeiling, 0, 1);
        // One fight cannot sit inside every band: a win is a rate of one, a draw a draw rate of one.
        var report = Tournament.Run(CoreContent.Registry, new[] { new Pairing("hob", "proving_bulwark") }, seeds: 1, seconds: 8, checkBands: true);
        var pair = Assert.Single(report.Pairs);
        Assert.NotEmpty(pair.Breaches);
        Assert.False(report.Passed);
    }

    [Fact]
    public void APairingIsWrittenAColonB()
    {
        Assert.Equal(new Pairing("wolf", "hob"), Pairing.Parse(" wolf : hob "));
        Assert.Throws<ArgumentException>(() => Pairing.Parse("wolf"));
    }
}
