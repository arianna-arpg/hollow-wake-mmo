using System.Numerics;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Maths;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>
/// The rest of the prototype's status roster (content/core/combat/statuses-roster.yaml) and its charges, as the
/// port ledger describes them: debuffs that build into worse ones, statuses that lash or zap around the bearer,
/// riders that burst when they end or when the bearer is struck, the ailment magnitude rules, and the proving
/// duel of the hexer against the plaguer that tells the same story twice.
/// </summary>
public class PortedStatusTests
{
    [Theory]
    [InlineData("faintness", 6, "swoon")]
    [InlineData("queasy", 5, "retching")]
    [InlineData("petrifying", 6, "petrified")]
    [InlineData("harrowing", 5, "horrified")]
    [InlineData("disoriented", 5, "bewildered")]
    [InlineData("sunscorched", 8, "heatstroke")]
    public void StackingDebuffsBuildIntoTheirWorseState(string status, int stacks, string into)
    {
        var bench = CombatBench.Create();
        for (var i = 0; i < stacks - 1; i++)
        {
            bench.Apply(bench.Victim, status, duration: 6);
        }
        Assert.NotNull(bench.Instance(bench.Victim, status));
        Assert.Null(bench.Instance(bench.Victim, into));
        bench.Apply(bench.Victim, status, duration: 6);
        Assert.Null(bench.Instance(bench.Victim, status));
        Assert.NotNull(bench.Instance(bench.Victim, into));
    }

    [Fact]
    public void MaddenedLashesAtWhoeverStandsNearestTheBearer()
    {
        var bench = CombatBench.Create(distance: 8);
        // A third body right beside the victim: the madness strikes it, not the far-off applier.
        var bystander = bench.Spawn("hob", new Vector3(19, 10, 0), "blue");
        bench.Apply(bench.Victim, "maddened", duration: 4);
        bench.Run(2);
        Assert.NotEmpty(bench.HitsOn(bystander));
        Assert.Empty(bench.HitsOn(bench.Attacker));
        Assert.Empty(bench.HitsOn(bench.Victim));
    }

    [Fact]
    public void FulgurBrandZapsTheBearersNeighboursNotTheBearer()
    {
        var bench = CombatBench.Create(distance: 8);
        var neighbour = bench.Spawn("hob", new Vector3(19.5f, 10, 0), "blue");
        bench.Apply(bench.Victim, "fulgur_brand", duration: 4);
        bench.Run(1.5);
        var zaps = bench.HitsOn(neighbour);
        Assert.NotEmpty(zaps);
        Assert.All(zaps, z => Assert.True(z.ByThread.ContainsKey("storm") && z.Tags.Has("dot")));
        Assert.Empty(bench.HitsOn(bench.Victim));
    }

    [Fact]
    public void ALivingBombBurstsOnTheBearersAlliesWhenItEnds()
    {
        var bench = CombatBench.Create(distance: 8);
        var ally = bench.Spawn("hob", new Vector3(19, 10, 0), "blue");
        bench.Apply(bench.Victim, "living_bomb", duration: 1);
        bench.Run(0.8);
        Assert.Empty(bench.HitsOn(ally));
        bench.Run(0.5);
        var burst = Assert.Single(bench.HitsOn(ally));
        Assert.True(burst.ByThread.ContainsKey("ember"));
        Assert.Single(bench.HitsOn(bench.Victim));
    }

    [Fact]
    public void APowderChargeBurstsWhenAnEmberBlowLandsAndIsSpent()
    {
        var bench = CombatBench.Create();
        bench.Apply(bench.Victim, "powder_charge", duration: 2);
        // A Stone blow leaves the powder alone.
        bench.Hit(5, "stone");
        Assert.NotNull(bench.Instance(bench.Victim, "powder_charge"));
        Assert.Single(bench.HitsOn(bench.Victim));
        // An Ember blow sets it off: the burst lands on the bearer beside the blow and the charge is spent.
        bench.Run("""{"type": "damage", "amount": 5, "threads": ["ember"], "can_crit": false}""");
        Assert.Equal(3, bench.HitsOn(bench.Victim).Count);
        Assert.Contains(bench.HitsOn(bench.Victim), h => h.Tags.Has("burst"));
        Assert.True(bench.Instance(bench.Victim, "powder_charge")?.Removed ?? true);
    }

    [Fact]
    public void TheMagnitudeRulesSizeAPortedAilmentFromTheHit()
    {
        var bench = CombatBench.Create();
        bench.Modify(bench.Attacker, "status.decay.chance", 1);
        bench.Hit(20, "umbra");
        var decay = bench.Instance(bench.Victim, "decay")!;
        Assert.Equal(20 * 0.35, decay.Magnitude, 6);
        bench.Modify(bench.Attacker, "status.hemorrhage.chance", 1);
        bench.Hit(10, "stone");
        Assert.Equal(10 * 0.45, bench.Instance(bench.Victim, "hemorrhage")!.Magnitude, 6);
    }

    [Fact]
    public void ContagionPassesToTheFallensAllies()
    {
        var bench = CombatBench.Create(distance: 3);
        var ally = bench.Spawn("hob", new Vector3(14, 10, 0), "blue");
        bench.Apply(bench.Victim, "contagion", magnitude: 2, duration: 7);
        bench.Hit(1000, "stone");
        Assert.True(bench.Sim.World.Has<Dead>(bench.Victim));
        Assert.NotNull(bench.Instance(ally, "contagion"));
    }

    [Fact]
    public void ThePortedChargesHoldTheirCaps()
    {
        var bench = CombatBench.Create();
        Assert.Equal(5, bench.AttackerStats.Get("charge.wisp.max"), 6);
        Assert.Equal(5, bench.AttackerStats.Get("charge.wakeflame.max"), 6);
        Assert.Equal(3, bench.AttackerStats.Get("charge.frenzy.max"), 6);
        bench.Run("""{"type": "charge", "id": "frenzy", "gain": 5, "self": true}""");
        Assert.Equal(3, bench.AttackerStats.Gauge("charge.frenzy"), 6);
    }

    [Fact]
    public void TheHexerAndThePlaguerTellTheSameStoryTwice()
    {
        List<string> Duel()
        {
            var sim = ZoneBuilder.CreateArena(CoreContent.Registry, Rng.Hash64("roster"));
            var factory = sim.Service<PatternFactory>();
            factory.Spawn(sim, "proving_hexer", new Vector3(10, 10, 0), new SpawnOptions { Faction = "red" });
            factory.Spawn(sim, "proving_plaguer", new Vector3(14, 10, 0), new SpawnOptions { Faction = "blue" });
            var log = new List<string>();
            sim.Events.Subscribe<StatusApplied>(s => log.Add($"{sim.Clock.Tick} status {s.Status} x{s.Stacks} on {s.Target.Key}"));
            sim.Events.Subscribe<CombatNote>(n => log.Add($"{sim.Clock.Tick} {n.Describe(e => e.Key.ToString())}"));
            sim.Events.Subscribe<DamageDealt>(d => log.Add($"{sim.Clock.Tick} {d.Total:R}"));
            sim.Events.Subscribe<EntityDied>(d => log.Add($"{sim.Clock.Tick} died {d.Entity.Key}"));
            sim.RunSeconds(40);
            return log;
        }
        var first = Duel();
        Assert.Equal(first, Duel());
        Assert.Contains(first, l => l.Contains("status despair"));
        Assert.Contains(first, l => l.Contains("status wither_agony"));
        Assert.Contains(first, l => l.Contains("status maddened"));
        Assert.Contains(first, l => l.Contains("status contagion"));
        Assert.Contains(first, l => l.Contains("status faintness x3"));
        Assert.Contains(first, l => l.Contains("status powder_charge"));
        Assert.Contains(first, l => l.Contains("spent 1 powder_charge"));
        Assert.Contains(first, l => l.StartsWith("died ", StringComparison.Ordinal) || l.Contains(" died "));
    }
}
