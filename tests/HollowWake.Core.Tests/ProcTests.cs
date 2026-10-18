using System.Numerics;
using System.Text.Json;
using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>Procs: chances from attributes, internal cooldowns, per-minute rates, depth limits and falloff, determinism.</summary>
public class ProcTests
{
    /// <summary>The core content with extra procs.</summary>
    private static ContentRegistry WithProcs(params string[] procs)
    {
        var registry = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out var report);
        Assert.False(report.HasErrors, report.ToString());
        foreach (var json in procs)
        {
            registry.With(JsonSerializer.Deserialize<ProcDef>(json, ContentJson.Options)!);
        }
        registry.With(new FactionDef { Id = "red", Relations = { ["blue"] = Stance.Hostile } });
        registry.With(new FactionDef { Id = "blue", Relations = { ["red"] = Stance.Hostile } });
        return registry;
    }

    /// <summary>Grants a proc's chance and lets the status system notice it, as it would on the next tick.</summary>
    private static void Grant(CombatBench bench, Entity who, string proc, double chance = 1)
    {
        bench.Modify(who, proc, chance);
        bench.Sim.Tick();
    }

    [Fact]
    public void AProcFiresFromItsChanceAndRunsItsEffectsOneDeeper()
    {
        var bench = CombatBench.Create();
        bench.Modify(bench.Attacker, "crit.chance", 1);
        Grant(bench, bench.Attacker, "proc.brutal_strike");
        // A melee crit strikes again for half of what it dealt (the crit multiplier is 1.5).
        bench.Run("""{"type": "damage", "amount": 10, "threads": ["stone"]}""", tags: "melee");
        var hits = bench.HitsOn(bench.Victim);
        Assert.Equal(2, hits.Count);
        var blow = hits.Single(d => d.Depth == 0);
        var again = hits.Single(d => d.Depth == 1);
        Assert.Equal(15, blow.Total, 9);
        Assert.True(again.Tags.Has("proc.brutal_strike"));
        // The payload is a hit of its own: half of the fifteen, and it may crit like any hit.
        Assert.Equal(7.5 * 1.5, again.Total, 9);
        var fired = bench.Events.OfType<ProcFired>().Single();
        Assert.Equal(bench.Victim, fired.Target);
        Assert.Equal(1, fired.Depth);
    }

    [Fact]
    public void OnlyBodiesCarryingAChanceAreSubscribed()
    {
        var bench = CombatBench.Create();
        var hub = bench.Sim.Service<TriggerHub>();
        bench.Sim.Tick();
        Assert.False(hub.HasSubscribers(TriggerKinds.Crit));
        Grant(bench, bench.Attacker, "proc.brutal_strike");
        Assert.True(hub.HasSubscribers(TriggerKinds.Crit));
        bench.AttackerStats.Attributes.RemoveSource("test");
        bench.Sim.Tick();
        Assert.False(hub.HasSubscribers(TriggerKinds.Crit));
    }

    [Fact]
    public void InternalCooldownsHoldBetweenFirings()
    {
        var bench = CombatBench.Create();
        bench.Modify(bench.Attacker, "crit.chance", 1);
        Grant(bench, bench.Attacker, "proc.brutal_strike");
        const string blow = """{"type": "damage", "amount": 10, "threads": ["stone"]}""";
        bench.Run(blow, tags: "melee");
        bench.Run(blow, tags: "melee");
        Assert.Single(bench.Events.OfType<ProcFired>());
        bench.Run(0.6);
        bench.Run(blow, tags: "melee");
        Assert.Equal(2, bench.Events.OfType<ProcFired>().Count());
    }

    [Fact]
    public void DepthRefusesChainsAndFallsOffBelowTheLimit()
    {
        // Echoing blow keeps its whole chance at every depth, so each blow echoes three times and the fourth is refused.
        var bench = CombatBench.Create();
        Grant(bench, bench.Attacker, "proc.echoing_blow");
        bench.Run("""{"type": "damage", "amount": 10, "threads": ["stone"], "can_crit": false}""");
        Assert.Equal(new[] { 1, 2, 3 }, bench.Events.OfType<ProcFired>().Select(p => p.Depth).ToArray());
        var refused = bench.Events.OfType<ProcRefused>().Single();
        Assert.Equal(3, refused.Depth);

        // With the rule's falloff a chain thins out: over many blows, fewer echoes reach each deeper level.
        var registry = WithProcs("""{"id": "t_echo", "on": "hit", "effects": [ {"type": "damage", "amount": 1, "threads": ["stone"], "can_crit": false} ]}""");
        var falling = CombatBench.Create(registry);
        falling.VictimStats.Attributes.SetBase("life.max", 1_000_000);
        falling.VictimStats.FillResources();
        Grant(falling, falling.Attacker, "proc.t_echo");
        for (var i = 0; i < 200; i++)
        {
            falling.Run("""{"type": "damage", "amount": 1, "threads": ["stone"], "can_crit": false}""");
        }
        var byDepth = falling.Events.OfType<ProcFired>().GroupBy(p => p.Depth).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(200, byDepth[1]);
        Assert.InRange(byDepth[2], 70, 130);
        Assert.InRange(byDepth[3], 10, 45);
        Assert.False(byDepth.ContainsKey(4));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ADepthPreservingAreaSelectorKeepsTheDirectBlowsEchoBudget(bool selector)
    {
        var bench = CombatBench.Create();
        Grant(bench, bench.Attacker, "proc.echoing_blow");
        const string blow = """{"type":"damage","amount":10,"threads":["stone"],"can_crit":false}""";
        bench.Run(selector ? "{\"type\":\"area\",\"center\":\"focus\",\"radius\":2,\"max_targets\":1,\"payload_depth\":\"same\",\"effects\":[" + blow + "]}" : blow);
        Assert.Equal(new[] { 1, 2, 3 }, bench.Events.OfType<ProcFired>().Select(p => p.Depth).ToArray());
        Assert.Equal(3, Assert.Single(bench.Events.OfType<ProcRefused>()).Depth);
    }

    [Fact]
    public void ProcsPerMinuteGrowWithTheTimeSinceTheLastCheck()
    {
        var registry = WithProcs("""{"id": "t_rate", "on": "hit", "ppm": 30, "effects": [ {"type": "log", "text": "rate"} ]}""");
        int Fires(double gap)
        {
            var bench = CombatBench.Create(registry);
            bench.VictimStats.Attributes.SetBase("life.max", 1_000_000);
            bench.VictimStats.FillResources();
            Grant(bench, bench.Attacker, "proc.t_rate");
            for (var i = 0; i < 60; i++)
            {
                bench.Run("""{"type": "damage", "amount": 1, "threads": ["stone"], "can_crit": false}""");
                bench.Run(gap);
            }
            return bench.Events.OfType<ProcFired>().Count();
        }
        // Thirty a minute: hits two seconds apart always proc (the first, with no time behind it, has an even
        // chance), hits a fifth of a second apart rarely.
        Assert.InRange(Fires(2), 59, 60);
        Assert.InRange(Fires(0.2), 1, 15);
        // And the same seed gives the same count.
        Assert.Equal(Fires(0.2), Fires(0.2));
    }

    [Fact]
    public void DamageOverTimeIsNotAHitForAProcUnlessItSaysSo()
    {
        var bench = CombatBench.Create();
        Grant(bench, bench.Attacker, "proc.echoing_blow");
        bench.Hit(10, tags: "dot");
        Assert.Empty(bench.Events.OfType<ProcFired>());

        var registry = WithProcs("""{"id": "t_dot", "on": "hit", "excludes": [], "effects": [ {"type": "log", "text": "tick"} ]}""");
        var ticking = CombatBench.Create(registry);
        Grant(ticking, ticking.Attacker, "proc.t_dot");
        ticking.Hit(10, tags: "dot");
        Assert.Single(ticking.Events.OfType<ProcFired>());
    }

    [Fact]
    public void AHighRollCanProc()
    {
        var registry = WithProcs("""{"id": "t_high", "on": "hit", "when": "roll >= 0.5", "effects": [ {"type": "log", "text": "high"} ]}""");
        var bench = CombatBench.Create(registry);
        bench.VictimStats.Attributes.SetBase("life.max", 1_000_000);
        bench.VictimStats.FillResources();
        Grant(bench, bench.Attacker, "proc.t_high");
        var rolled = bench.Effect("""{"type": "damage", "amount": "lerp(1, 2, roll)", "threads": ["stone"], "can_crit": false}""");
        for (var i = 0; i < 40; i++)
        {
            rolled.Execute(bench.Context(bench.Attacker, bench.Victim, rng: (ulong)i + 1));
        }
        var hits = bench.HitsOn(bench.Victim);
        Assert.Equal(40, hits.Count);
        Assert.All(hits, h => Assert.InRange(h.Roll!.Value, 0, 1));
        var high = hits.Count(h => h.Roll >= 0.5);
        Assert.InRange(high, 1, 39);
        Assert.Equal(high, bench.Events.OfType<ProcFired>().Count());
        // A hit that rolled no position reads a roll of zero.
        Assert.Null(bench.Hit(5).Roll);
        Assert.Equal(high, bench.Events.OfType<ProcFired>().Count());
    }

    [Fact]
    public void RetaliationAnswersTheStriker()
    {
        var bench = CombatBench.Create();
        Grant(bench, bench.Victim, "proc.retaliation");
        bench.Run("""{"type": "damage", "amount": 5, "threads": ["stone"], "can_crit": false}""", tags: "melee");
        var answer = bench.HitsOn(bench.Attacker).Single();
        Assert.Equal(bench.Victim, answer.Source);
        Assert.True(answer.Tags.Has("proc.retaliation"));
    }

    [Fact]
    public void AStatusCanGrantAProcWhileItLasts()
    {
        var bench = CombatBench.Create();
        bench.Modify(bench.Attacker, "crit.chance", 1);
        bench.Apply(bench.Attacker, "battle_trance", duration: 1, source: bench.Attacker);
        bench.Sim.Tick();
        bench.Run("""{"type": "damage", "amount": 10, "threads": ["stone"]}""", tags: "melee");
        Assert.Single(bench.Events.OfType<ProcFired>());
        bench.Run(1.2);
        bench.Run(1);
        bench.Run("""{"type": "damage", "amount": 10, "threads": ["stone"]}""", tags: "melee");
        Assert.Single(bench.Events.OfType<ProcFired>());
    }

    [Fact]
    public void ADuelWithProcsReplaysExactly()
    {
        List<string> Duel()
        {
            var sim = ZoneBuilder.CreateArena(CoreContent.Registry, 31);
            var factory = sim.Service<PatternFactory>();
            factory.Spawn(sim, "proving_reaver", new Vector3(10, 10, 0), new SpawnOptions { Faction = "red" });
            factory.Spawn(sim, "proving_bulwark", new Vector3(13, 10, 0), new SpawnOptions { Faction = "blue" });
            var log = new List<string>();
            sim.Events.Subscribe<CombatNote>(n => log.Add($"{sim.Clock.Tick} {n.Describe(e => e.Key.ToString())}"));
            sim.Events.Subscribe<DamageDealt>(d => log.Add($"{sim.Clock.Tick} {d.Total:R}"));
            sim.RunSeconds(15);
            return log;
        }
        var first = Duel();
        Assert.Equal(first, Duel());
        Assert.Contains(first, l => l.Contains("proc echoing_blow"));
        Assert.Contains(first, l => l.Contains("refused at depth 3"));
        Assert.Contains(first, l => l.Contains("leeched"));
        Assert.Contains(first, l => l.Contains("ruptured bleeding"));
        Assert.Contains(first, l => l.Contains("'s shield recharging"));
        Assert.Contains(first, l => l.Contains("'s poise"));
    }
}
