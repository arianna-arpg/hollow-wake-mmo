using HollowWake.Core.Content;
using HollowWake.Core.Evaluation;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>
/// The evaluation harness (docs/design/evaluation.md): seeded duels across pairings measured from the
/// simulation's events, repeating exactly, with bands read from content that name what they break.
/// </summary>
public class TournamentTests
{
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
