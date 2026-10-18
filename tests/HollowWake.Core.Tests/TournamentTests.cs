using System.Globalization;
using System.Numerics;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Evaluation;
using HollowWake.Core.Sim;
using HollowWake.Tools;
using Xunit;
using Xunit.Abstractions;

namespace HollowWake.Core.Tests;

/// <summary>
/// The evaluation harness (docs/design/evaluation.md): seeded duels across pairings measured from the
/// simulation's events, repeating exactly, with bands read from content that name what they break, pairings
/// listed in content, trace hashes a baseline holds a later run to, and the command that drives them.
/// </summary>
public class TournamentTests
{
    private readonly ITestOutputHelper _output;

    public TournamentTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string Scratch(string name)
    {
        var dir = Path.Combine(Path.GetTempPath(), "hollowwake-" + name + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>The repository's root: the folder that holds content/core.</summary>
    private static string RepositoryRoot() => Path.GetDirectoryName(Path.GetDirectoryName(CoreContent.FindContentDir()))!;

    /// <summary>Runs the tournament command over <paramref name="args"/>; returns its exit code and the lines it wrote.</summary>
    private static (int Code, string[] Lines) Command(ContentRegistry content, params string[] args)
    {
        var written = new StringWriter();
        var code = TournamentCommand.Run(content, args, written);
        return (code, written.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToArray());
    }

    [Theory]
    [InlineData("unknown option --check-hash", "--standard", "--check-hash", "baseline.json")]
    [InlineData("unknown option --seed", "--seed", "3")]
    [InlineData("--check-hashes needs a value", "--standard", "--check-hashes")]
    [InlineData("--check-hashes needs a value", "--check-hashes", "--standard")]
    [InlineData("--out needs a value", "--hashes", "--out")]
    [InlineData("--out needs a value", "--out", "--hashes")]
    [InlineData("--seeds needs a value", "--pairs", "wolf:hob", "--seeds")]
    [InlineData("unexpected word 'wolf:hob'", "--standard", "wolf:hob")]
    [InlineData("--seeds is given twice", "--seeds", "3", "--seeds", "4")]
    [InlineData("--seeds takes a whole number", "--pairs", "wolf:hob", "--seeds", "0")]
    [InlineData("--check-hashes: no baseline at", "--check-hashes", "no-such-baseline.json")]
    public void TheCommandRefusesABadOptionWithOneLineBeforeAnyFight(string refusal, params string[] args)
    {
        var (code, lines) = Command(CoreContent.Registry, args);
        Assert.Equal(1, code);
        // One line, and no table: nothing was fought.
        var line = Assert.Single(lines);
        Assert.StartsWith(refusal, line, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCommittedBaselineHoldsOnItsPlatform()
    {
        var path = Path.Combine(RepositoryRoot(), "experiments", "combat-grammar", "baselines", "standard-duels.json");
        var baseline = TraceBaseline.FromJson(File.ReadAllText(path));
        var standard = Pairing.Listed(CoreContent.Registry, Pairing.StandardDuels);
        var run = Tournament.Run(CoreContent.Registry, standard, baseline.Seeds, baseline.Seconds, checkBands: false);
        if (baseline.Platform == TraceBaseline.CurrentPlatform)
        {
            Assert.Empty(baseline.Differences(run));
            return;
        }
        // On another platform a hash need only repeat: how it differs from the recorded one is a finding to record.
        var again = Tournament.Run(CoreContent.Registry, standard, baseline.Seeds, baseline.Seconds, checkBands: false);
        Assert.Equal(run.Pairs.Select(p => p.Hash), again.Pairs.Select(p => p.Hash));
        _output.WriteLine($"the baseline was recorded on {baseline.Platform ?? "an unnamed platform"}, this run is on {TraceBaseline.CurrentPlatform}:");
        foreach (var difference in baseline.Differences(run))
        {
            _output.WriteLine("    " + difference);
        }
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
    public void AFightThatReachesTheCapMovesItsHashWhenItsBodiesEndItDifferently()
    {
        var dir = Scratch("evaluation-regen");
        try
        {
            // Three times the base life regeneration: no hit, cast or status need change, only the life the bodies keep.
            File.WriteAllText(Path.Combine(dir, "attributes.yaml"), """
kind: attributes
entries:
  - { id: life.regen, name: Life Regeneration, category: resource, base: 1.5, decimals: 2 }
""".Replace("\r\n", "\n"));
            var registry = ContentRegistry.Load(new[] { CoreContent.FindContentDir(), dir }, out var report);
            Assert.Empty(report.Errors);
            var pairing = new Pairing("proving_bulwark", "proving_bulwark");
            var core = Tournament.Duel(CoreContent.Registry, pairing, index: 0, seconds: 20);
            var regenerating = Tournament.Duel(registry, pairing, index: 0, seconds: 20);
            Assert.Equal("draw", core.Outcome);
            Assert.Equal("draw", regenerating.Outcome);
            Assert.NotEqual(core.SideA.LifeLeft + core.SideB.LifeLeft, regenerating.SideA.LifeLeft + regenerating.SideB.LifeLeft);
            Assert.NotEqual(core.Hash, regenerating.Hash);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ADoubleKnockoutIsADraw()
    {
        var dir = Scratch("evaluation-knockout");
        try
        {
            // A blow that kills its target and, in the same effect run, its striker.
            File.WriteAllText(Path.Combine(dir, "weaves.yaml"), """
kind: weaves
entries:
  - id: t_mutual_ruin
    name: Mutual Ruin
    form: strike
    threads: []
    effects:
      - { type: damage, amount: 100000, can_crit: false }
      - type: area
        shape: circle
        radius: 1
        center: source
        filter: self
        effects: [{ type: damage, amount: 100000, can_crit: false }]
""".Replace("\r\n", "\n"));
            File.WriteAllText(Path.Combine(dir, "patterns.yaml"), """
kind: patterns
entries:
  - id: t_martyr
    name: Martyr
    extends: monster
    tags: [humanoid]
    level: 5
    components:
      stats: { attributes: { might: 10, vigor: 10, move.speed: 4.8, weapon.damage.min: 4, weapon.damage.max: 6, weapon.attack.time: 1.0 } }
      weaves: { list: [t_mutual_ruin] }
      behavior: { id: aggressive_melee }
""".Replace("\r\n", "\n"));
            var registry = ContentRegistry.Load(new[] { CoreContent.FindContentDir(), dir }, out var report);
            Assert.Empty(report.Errors);
            var run = Tournament.Run(registry, new[] { new Pairing("t_martyr", "hob") }, seeds: 4, seconds: 20, checkBands: false);
            var knockouts = run.Fights.Where(f => f.SideA.LifeLeft == 0 && f.SideB.LifeLeft == 0).ToList();
            Assert.NotEmpty(knockouts);
            Assert.All(knockouts, f =>
            {
                Assert.Equal("draw", f.Outcome);
                Assert.True(f.Seconds < 20, "a double knockout ends before the cap");
            });
            // A draw counts toward the draw rate and never toward the time to kill.
            var pair = Assert.Single(run.Pairs);
            Assert.Equal((double)run.Fights.Count(f => f.Outcome == "draw") / run.Fights.Count, pair.DrawRate, 3);
            if (run.Fights.All(f => f.Outcome == "draw"))
            {
                Assert.Equal(0, pair.TtkMedian);
            }
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void NeitherSideIsStructurallyFirst()
    {
        // The side spawned first is created first, and so thinks and acts first on every tick: it alternates with
        // the fight's index.
        Assert.Equal(new[] { false, true, false, true }, Enumerable.Range(0, 4).Select(Tournament.BFirst));
        foreach (var bFirst in new[] { false, true })
        {
            var sim = ZoneBuilder.CreateArena(CoreContent.Registry, 1);
            var (a, b) = Tournament.Spawn(sim, new Pairing("wolf", "hob"), bFirst);
            Assert.Equal(bFirst, b.Index < a.Index);
            var ta = sim.World.Get<Transform>(a)!;
            var tb = sim.World.Get<Transform>(b)!;
            // Mirrored about the arena's origin, a to the west, and each turned toward the other.
            Assert.Equal(-ta.Position.X, tb.Position.X, 3);
            Assert.Equal(ta.Position.Y, tb.Position.Y, 3);
            Assert.True(ta.Position.X < tb.Position.X);
            Assert.Equal(1, Vector2.Dot(ta.Forward, Vector2.UnitX), 3);
            Assert.Equal(-1, Vector2.Dot(tb.Forward, Vector2.UnitX), 3);
        }
    }

    [Fact]
    public void ATournamentSeedTextNamesItsFight()
    {
        var pairing = new Pairing("wolf", "hob");
        Assert.Equal("tournament:wolf:hob:7", Tournament.SeedText(pairing, 7));
        Assert.True(Tournament.TryReadSeed(Tournament.SeedText(pairing, 7), out var read, out var index));
        Assert.Equal(pairing, read);
        Assert.Equal(7, index);
        foreach (var other in new[] { "fight", "tournament:wolf:hob", "tournament:wolf:hob:x", "tournament:wolf:hob:-1", "tourney:wolf:hob:1", "tournament::hob:1" })
        {
            Assert.False(Tournament.TryReadSeed(other, out _, out _), other);
        }
    }

    [Fact]
    public void AFightNamesTheSeedTextThatReplaysIt()
    {
        var pairing = new Pairing("proving_reaver", "proving_bulwark");
        var run = Tournament.Run(CoreContent.Registry, new[] { pairing }, seeds: 4, seconds: 60, checkBands: false);
        var dt = ZoneBuilder.CreateArena(CoreContent.Registry).Clock.Dt;
        foreach (var fight in run.Fights)
        {
            Assert.Equal(Tournament.SeedText(pairing, fight.Index), fight.Seed);
            // Seconds is the end tick's time to the millisecond, free of the noise a product of ticks carries.
            Assert.Equal(Math.Round(fight.EndTick * dt, 3), fight.Seconds);
            Assert.Equal(fight.Seconds, Math.Round(fight.Seconds, 3));

            // What hollowwake fight does with the seed text: the arena it seeds and the staging it reads back give
            // the same fight, up to the tick it ended on.
            Assert.True(Tournament.TryReadSeed(fight.Seed, out var read, out var index));
            var sim = ZoneBuilder.CreateArena(CoreContent.Registry, Maths.Rng.Hash64(fight.Seed));
            var (a, b) = Tournament.Spawn(sim, read, Tournament.BFirst(index));
            long? died = null;
            sim.Events.Subscribe<EntityDied>(d =>
            {
                if (d.Entity == a || d.Entity == b)
                {
                    died ??= sim.Clock.Tick;
                }
            });
            while (died is null && sim.Clock.Time < 60)
            {
                sim.Tick();
            }
            Assert.Equal(fight.EndTick, died ?? sim.Clock.Tick);
        }
        // Both orders of spawning were replayed.
        Assert.Contains(run.Fights, f => Tournament.BFirst(f.Index));
        Assert.Contains(run.Fights, f => !Tournament.BFirst(f.Index));
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
        Assert.Contains("one_two", pair.SeatsA);
        Assert.Contains("attack", pair.MechanismsA);
        Assert.True(pair.CoverageA > 0 && pair.CoverageB > 0);
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
        Assert.True(report.BandsChecked);
        Assert.Equal(bands, report.Bands);
        Assert.False(report.Passed);

        // A run that does not check the bands judged nothing: it neither passed nor failed, and says so.
        var unjudged = Tournament.Run(CoreContent.Registry, new[] { new Pairing("hob", "proving_bulwark") }, seeds: 1, seconds: 8, checkBands: false);
        Assert.False(unjudged.BandsChecked);
        Assert.Null(unjudged.Bands);
        Assert.Null(unjudged.Passed);
        Assert.Empty(Assert.Single(unjudged.Pairs).Breaches);
        var json = System.Text.Json.Nodes.JsonNode.Parse(unjudged.ToJson())!;
        Assert.False(json["BandsChecked"]!.GetValue<bool>());
        Assert.Null(json["Passed"]);
        Assert.Null(json["Bands"]);
    }

    [Fact]
    public void AMisspelledOrMissingBandIsRefused()
    {
        var dir = Scratch("evaluation-bands");
        try
        {
            // A patch that misspells a band adds a rule the harness never reads.
            File.WriteAllText(Path.Combine(dir, "rules.yaml"), """
kind: rules
entries:
  - id: evaluation
    patch: true
    values:
      win_rate_cieling: 0.5
      draw_ceiling: 1.5
""".Replace("\r\n", "\n"));
            ContentRegistry.Load(new[] { CoreContent.FindContentDir(), dir }, out var patched);
            var errors = patched.Errors.Where(e => e.Id == "evaluation").Select(e => e.Message).ToList();
            Assert.Contains(errors, m => m.Contains("'win_rate_cieling'") && m.Contains("not an evaluation rule"));
            Assert.Contains(errors, m => m.Contains("draw_ceiling") && m.Contains("from 0 to 1"));
            Assert.Equal(2, errors.Count);

            // A layer that replaces the group with the misspelling leaves the real band unwritten, and the harness
            // has no number of its own to judge by.
            File.WriteAllText(Path.Combine(dir, "rules.yaml"), """
kind: rules
entries:
  - id: evaluation
    values:
      proving_pairs: ["wolf:hob"]
      standard_duels: ["wolf:hob"]
      ttk_min: 5
      ttk_max: 45
      win_rate_cieling: 0.5
      draw_ceiling: 0.2
      coverage_floor: 0.25
""".Replace("\r\n", "\n"));
            var replaced = ContentRegistry.Load(new[] { CoreContent.FindContentDir(), dir }, out var report);
            errors = report.Errors.Where(e => e.Id == "evaluation").Select(e => e.Message).ToList();
            Assert.Contains(errors, m => m.Contains("'win_rate_cieling'"));
            Assert.Contains(errors, m => m.StartsWith("win_rate_ceiling is missing", StringComparison.Ordinal));
            var thrown = Assert.Throws<InvalidDataException>(() => Bands.Of(replaced));
            Assert.Contains("evaluation.win_rate_ceiling", thrown.Message);
            Assert.Throws<InvalidDataException>(() => Tournament.Run(replaced, new[] { new Pairing("wolf", "hob") }, seeds: 1, seconds: 2, checkBands: true));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void APairingIsWrittenAColonB()
    {
        Assert.Equal(new Pairing("wolf", "hob"), Pairing.Parse(" wolf : hob "));
        Assert.Equal("wolf:hob", Pairing.Parse("wolf:hob").ToString());
        foreach (var bad in new[] { "wolf", "", " ", ":hob", " :hob", "wolf:", "wolf: ", " : " })
        {
            Assert.Throws<ArgumentException>(() => Pairing.Parse(bad));
        }
    }

    [Fact]
    public void AnUnknownPatternIsRefusedBeforeAnyFight()
    {
        Assert.Null(Tournament.Refusal(CoreContent.Registry, new Pairing("wolf", "hob")));
        var refusal = Tournament.Refusal(CoreContent.Registry, new Pairing("wolf", "no_such_pattern"));
        Assert.Contains("wolf:no_such_pattern", refusal);
        Assert.Contains("'no_such_pattern'", refusal);
        // The known pairing listed first is not fought: the run refuses before its first fight.
        var thrown = Assert.Throws<ArgumentException>(() => Tournament.Run(CoreContent.Registry, new[] { new Pairing("wolf", "hob"), new Pairing("wolf", "no_such_pattern") }, seeds: 1, seconds: 60, checkBands: false));
        Assert.Contains("'no_such_pattern'", thrown.Message);
    }

    [Fact]
    public void ADuelAddsTheTwoFactionsOnlyWhereTheRegistryLacksThem()
    {
        var red = CoreContent.Registry.Get<FactionDef>("red");
        Tournament.Duel(CoreContent.Registry, new Pairing("wolf", "hob"), index: 0, seconds: 2);
        Assert.Same(red, CoreContent.Registry.Get<FactionDef>("red"));

        var fresh = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out _);
        Assert.False(fresh.Has<FactionDef>("red") || fresh.Has<FactionDef>("blue"));
        Tournament.Duel(fresh, new Pairing("wolf", "hob"), index: 0, seconds: 2);
        var added = fresh.Get<FactionDef>("red");
        Assert.NotNull(added);
        Assert.True(fresh.Has<FactionDef>("blue"));
        Tournament.Duel(fresh, new Pairing("wolf", "hob"), index: 1, seconds: 2);
        Assert.Same(added, fresh.Get<FactionDef>("red"));
    }

    [Fact]
    public void TheReportReadsTheSameInEveryCulture()
    {
        var pairing = new Pairing("hob", "proving_bulwark");
        var invariant = Tournament.Run(CoreContent.Registry, new[] { pairing }, seeds: 1, seconds: 8, checkBands: true);
        var before = CultureInfo.CurrentCulture;
        TournamentReport german;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            german = Tournament.Run(CoreContent.Registry, new[] { pairing }, seeds: 1, seconds: 8, checkBands: true);
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
        // The seed, the trace, the breaches and the table are written in the invariant culture inside the library.
        Assert.Equal(invariant.Fights[0].Seed, german.Fights[0].Seed);
        Assert.Equal(invariant.Hash, german.Hash);
        Assert.Equal(invariant.Pairs[0].Breaches, german.Pairs[0].Breaches);
        Assert.Equal(invariant.Table(), german.Table());
        Assert.Equal(invariant.HashLines(), german.HashLines());
        Assert.Contains(german.Pairs[0].Breaches, b => b.Contains("0.2", StringComparison.Ordinal) || b.Contains("0.8", StringComparison.Ordinal));
        Assert.Contains(german.Table(), line => line.Contains("1.00", StringComparison.Ordinal));
    }

    [Fact]
    public void ASideIsCreditedWithWhatItsMinionsDoAndItsKitStaysItsOwn()
    {
        var dir = Scratch("evaluation-summoner");
        try
        {
            // A summoner whose body has no weave that deals damage: everything it deals, its minions dealt.
            File.WriteAllText(Path.Combine(dir, "patterns.yaml"), """
kind: patterns
entries:
  - id: t_shade_caller
    name: Shade Caller
    extends: monster
    tags: [humanoid, caster]
    level: 5
    components:
      stats: { attributes: { might: 6, grace: 9, will: 15, vigor: 8, move.speed: 4.8 } }
      weaves: { list: [call_remnant] }
      behavior: { id: aggressive_ranged }
""".Replace("\r\n", "\n"));
            var registry = ContentRegistry.Load(new[] { CoreContent.FindContentDir(), dir }, out var report);
            Assert.Empty(report.Errors);
            var fight = Tournament.Duel(registry, new Pairing("t_shade_caller", "hob"), index: 0, seconds: 30);
            var summoner = fight.SideA;
            Assert.True(summoner.DamageDealt > 0 && summoner.Hits > 0, "the minions' damage was not credited to their owner");
            // Every hit is a shade's single strike and the summoning lands none, so the casts outnumber the hits only
            // when the shades' casts are credited too.
            Assert.True(summoner.Casts > summoner.Hits, "the minions' casts were not credited to their owner");
            // Those casts count, but only the summoning is its kit, and its presses are the only uses of it.
            Assert.Equal(new[] { "call_remnant" }, summoner.Seats);
            Assert.Equal(1, summoner.Kit);
            Assert.Equal(1, summoner.Coverage);
            Assert.True(summoner.Casts > summoner.Uses, "the minions' presses were counted as uses of the summoner's seat");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void AUseCountsOncePerPressAgainstTheSeatThatOwnsIt()
    {
        // A combo chain whose third press casts another weave, its face, against a channel whose one press beats again
        // and again.
        var run = Tournament.Run(CoreContent.Registry, new[] { new Pairing("proving_pitfighter", "tide_channeler") }, seeds: 4, seconds: 30, checkBands: false);
        Assert.All(run.Fights, f =>
        {
            var pitfighter = f.SideA;
            Assert.Equal(3, pitfighter.Kit);
            Assert.All(pitfighter.Seats, seat => Assert.Contains(seat, new[] { "one_two", "haymaker", "frenzy" }));
            Assert.Equal(Math.Round(pitfighter.Seats.Count / 3.0, 3), pitfighter.Coverage);
            Assert.InRange(pitfighter.Spread, 1, pitfighter.Seats.Count);
            var channeler = f.SideB;
            Assert.Equal(1, channeler.Kit);
            Assert.Equal(1, channeler.Coverage);
            Assert.Equal(1, channeler.Spread);
            Assert.True(channeler.Casts > channeler.Uses, "a channel's beats were counted as uses");
        });
        // The chain's face was pressed, and counted against the chain's seat, never as a seat of its own.
        Assert.Contains(run.Fights, f => f.SideA.Faces.Contains("cross_jab"));
        Assert.All(run.Fights, f => Assert.DoesNotContain("cross_jab", f.SideA.Seats));
        Assert.All(run.Fights.Where(f => f.SideA.Faces.Contains("cross_jab")), f => Assert.Contains("one_two", f.SideA.Seats));
    }

    [Fact]
    public void AFightCarriesWhatTheSimulationLogged()
    {
        var dir = Scratch("evaluation-broken");
        try
        {
            File.WriteAllText(Path.Combine(dir, "patterns.yaml"), """
kind: patterns
entries:
  - id: t_broken
    name: Broken
    extends: monster
    tags: [humanoid]
    level: 5
    components:
      stats: { attributes: { might: 10, vigor: 10, move.speed: 4.8 } }
      weaves: { list: [no_such_weave] }
      behavior: { id: no_such_behavior }
  - id: t_garbled
    name: Garbled
    extends: monster
    tags: [humanoid]
    level: 5
    components:
      stats: { attributes: { might: 10, vigor: 10, move.speed: 4.8 } }
      weaves: { list: [5] }
      behavior: { id: aggressive_melee }
""".Replace("\r\n", "\n"));
            var registry = ContentRegistry.Load(new[] { CoreContent.FindContentDir(), dir }, out _);

            // An unknown weave: the seat is there and never used, and the fight says why instead of reading as an empty kit.
            var broken = Tournament.Run(registry, new[] { new Pairing("t_broken", "hob") }, seeds: 1, seconds: 5, checkBands: false);
            var fight = Assert.Single(broken.Fights);
            Assert.Equal(1, fight.SideA.Kit);
            Assert.Equal(0, fight.SideA.Coverage);
            Assert.Contains(fight.Warnings, w => w.Contains("no_such_weave") && w.Contains("failed to compile"));
            Assert.Contains(fight.Warnings, w => w.Contains("no_such_behavior"));
            Assert.Empty(fight.Errors);
            Assert.Null(broken.FirstError());
            Assert.Contains(broken.Table(), line => line.StartsWith("    WARNING in 1 of 1 fights: ", StringComparison.Ordinal) && line.Contains("no_such_weave"));

            // A weave list the binder cannot read is an error, and the report names the first.
            var garbled = Tournament.Run(registry, new[] { new Pairing("t_garbled", "hob") }, seeds: 2, seconds: 5, checkBands: false);
            Assert.All(garbled.Fights, f => Assert.Contains(f.Errors, e => e.Contains("'t_garbled'") && e.Contains("'weaves'")));
            Assert.StartsWith("t_garbled:hob fight 0: pattern 't_garbled' component 'weaves' failed to bind", garbled.FirstError());
            Assert.Contains(garbled.Table(), line => line.StartsWith("    ERROR in 2 of 2 fights: ", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void AStatusWhoseApplierIsGoneIsItsBearersOwnNotDamageItDealt()
    {
        var dir = Scratch("evaluation-wisp");
        try
        {
            // A summoner of imps that burn what they strike; past its cap an old imp gives way to a new one, and the
            // burns it laid tick on, sourced from the body that bears them.
            File.WriteAllText(Path.Combine(dir, "patterns.yaml"), """
kind: patterns
entries:
  - id: t_wisp_caller
    name: Wisp Caller
    extends: monster
    tags: [humanoid, caster]
    level: 5
    components:
      stats: { attributes: { might: 6, grace: 9, will: 15, vigor: 8, move.speed: 4.8 } }
      weaves: { list: [cinderwisp] }
      behavior: { id: aggressive_ranged }
""".Replace("\r\n", "\n"));
            var registry = ContentRegistry.Load(new[] { CoreContent.FindContentDir(), dir }, out var report);
            Assert.Empty(report.Errors);
            var fight = Tournament.Duel(registry, new Pairing("t_wisp_caller", "proving_bulwark"), index: 0, seconds: 60);
            var bulwark = fight.SideB;
            // The burns that ticked on after their imp was gone are the bulwark's own, booked apart.
            Assert.True(bulwark.SelfDamage > 0, "no self-sourced damage was booked");
            // Its bite lands at most one hit per cast and it deals damage no other way, so its hits cannot outnumber its
            // casts; booking those ticks as its work did exactly that.
            Assert.InRange(bulwark.Hits, 1, bulwark.Casts);
            // What it lays on itself, its shield's delay, is its own too, never a status it applied.
            Assert.Contains("shield_delay", bulwark.SelfStatuses);
            Assert.DoesNotContain("shield_delay", bulwark.StatusesApplied);
            // The summoner is credited with its imps' work on the bulwark, and booked nothing as its own.
            Assert.True(fight.SideA.DamageDealt > 0 && fight.SideA.Hits > 0);
            Assert.Equal(0, fight.SideA.SelfDamage);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
