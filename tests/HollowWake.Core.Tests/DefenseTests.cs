using System.Numerics;
using System.Text.Json.Nodes;
using HollowWake.Core.Attributes;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Sim;
using HollowWake.Core.Systems;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>The defender's ladder and the attacker's build: skips, added damage, conversion, leech, pools and poise.</summary>
public class DefenseTests
{
    /// <summary>The core content with the combat rule group edited.</summary>
    internal static ContentRegistry WithCombatRules(Action<JsonObject> edit)
    {
        var registry = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out var report);
        Assert.False(report.HasErrors, report.ToString());
        var values = (JsonObject)registry.Require<RulesDef>("combat").Values!.DeepClone();
        edit(values);
        registry.With(new RulesDef { Id = "combat", Values = values });
        registry.With(new FactionDef { Id = "red", Relations = { ["blue"] = Stance.Hostile } });
        registry.With(new FactionDef { Id = "blue", Relations = { ["red"] = Stance.Hostile } });
        return registry;
    }

    [Fact]
    public void DamageOverTimeSkipsEvasionBlockCritAndArmor()
    {
        var bench = CombatBench.Create();
        bench.Modify(bench.Victim, "block.chance", 1);
        bench.Modify(bench.Victim, "armor", 400);

        // A hit is blocked whole and a Stone hit is armored.
        Assert.True(bench.Hit(20).Blocked);
        bench.Modify(bench.Victim, "block.chance", -1);
        var armored = bench.Hit(20);
        Assert.False(armored.Blocked);
        Assert.True(armored.Total < 20 * 0.5, $"armor should cut a Stone hit, got {armored.Total}");

        // A tick of damage over time is neither blocked nor armored: it lands whole.
        bench.Modify(bench.Victim, "block.chance", 1);
        var tick = bench.Hit(20, tags: "dot");
        Assert.False(tick.Blocked);
        Assert.Equal(20, tick.Total, 9);

        // Resistances still apply to damage over time.
        bench.Modify(bench.Victim, "resist.ember", 0.5);
        Assert.Equal(10, bench.Hit(20, thread: "ember", tags: "dot").Total, 9);
    }

    [Fact]
    public void BleedingTicksIgnoreArmorAndBlock()
    {
        var bench = CombatBench.Create();
        bench.Modify(bench.Victim, "block.chance", 1);
        bench.Modify(bench.Victim, "armor", 400);
        bench.Apply(bench.Victim, "bleeding", magnitude: 4, duration: 3);
        bench.Run(1.2);
        var ticks = bench.Events.OfType<DamageDealt>().Where(d => d.Target == bench.Victim).ToList();
        Assert.NotEmpty(ticks);
        Assert.All(ticks, d => Assert.False(d.Blocked));
        // Four per second ticking every half second: two a tick, untouched by armor.
        Assert.All(ticks, d => Assert.Equal(2, d.Total, 9));
    }

    [Fact]
    public void AddedDamageReachesThreadsTheHitLacked()
    {
        var bench = CombatBench.Create();
        bench.Modify(bench.Attacker, "added.ember", 5);
        var hit = bench.Hit(10, thread: "stone");
        Assert.Equal(10, hit.ByThread["stone"], 9);
        Assert.Equal(5, hit.ByThread["ember"], 9);
        Assert.Equal(15, hit.Total, 9);

        // Added damage is scaled by its effectiveness, and folded like the rest of the hit.
        bench.Modify(bench.Attacker, "added.effectiveness", -0.5);
        bench.Modify(bench.Attacker, "damage", 1, ModifierForm.Increased, "ember");
        Assert.Equal(5, bench.Hit(10).ByThread["ember"], 9);

        // Damage over time takes no added damage.
        Assert.False(bench.Hit(10, tags: "dot").ByThread.ContainsKey("ember"));
    }

    [Fact]
    public void PerThreadAmountsMixBases()
    {
        var bench = CombatBench.Create();
        bench.Run("{\"type\": \"damage\", \"amounts\": {\"ember\": 6, \"tide\": 4}, \"can_crit\": false}");
        var hit = bench.Events.OfType<DamageDealt>().Last();
        Assert.Equal(6, hit.ByThread["ember"], 9);
        Assert.Equal(4, hit.ByThread["tide"], 9);
        Assert.False(hit.ByThread.ContainsKey("stone"));
        Assert.True(hit.Tags.Has("ember") && hit.Tags.Has("tide"));

        // An amount split over threads and per-Thread amounts add up.
        bench.Run("{\"type\": \"damage\", \"amount\": 10, \"threads\": [\"stone\"], \"amounts\": {\"ember\": 3}, \"can_crit\": false}");
        hit = bench.Events.OfType<DamageDealt>().Last();
        Assert.Equal(10, hit.ByThread["stone"], 9);
        Assert.Equal(3, hit.ByThread["ember"], 9);
    }

    [Fact]
    public void DefenderSideConversionMitigatesAsTheNewThread()
    {
        var bench = CombatBench.Create();
        bench.Modify(bench.Victim, "taken_as.stone.ember", 0.5);
        bench.Modify(bench.Victim, "resist.ember", 0.5);
        var hit = bench.Hit(20, thread: "stone");
        // Half arrives as Stone (no armor on a hob), half as Ember and is halved by the resistance.
        Assert.Equal(10, hit.ByThread["stone"], 9);
        Assert.Equal(5, hit.ByThread["ember"], 9);
        Assert.Equal(15, hit.Total, 9);
    }

    [Fact]
    public void LeechReturnsAShareOfWhatLanded()
    {
        var bench = CombatBench.Create();
        var attacker = bench.AttackerStats;
        attacker.SetResource("life", attacker.ResourceMax("life") - 30);
        bench.Modify(bench.Attacker, "leech.life", 0.2);
        bench.Modify(bench.Attacker, "on_hit.life", 1);
        var before = attacker.Resource("life");
        bench.Hit(20);
        Assert.Equal(before + 20 * 0.2 + 1, attacker.Resource("life"), 9);
        var paid = bench.Events.OfType<LeechPaid>().ToList();
        Assert.Contains(paid, p => p.Kind == "leech" && Math.Abs(p.Amount - 4) < 1e-9);
        Assert.Contains(paid, p => p.Kind == "hit" && Math.Abs(p.Amount - 1) < 1e-9);

        // The hit's own leech joins the attacker's, and a full pool takes nothing.
        bench.Run("{\"type\": \"damage\", \"amount\": 10, \"threads\": [\"stone\"], \"can_crit\": false, \"leech\": 0.5}");
        Assert.Equal(before + 5 + 10 * 0.7 + 1, attacker.Resource("life"), 9);
        attacker.SetResource("life", attacker.ResourceMax("life"));
        var count = bench.Events.OfType<LeechPaid>().Count();
        bench.Hit(10);
        Assert.Equal(count, bench.Events.OfType<LeechPaid>().Count());
    }

    [Fact]
    public void KillsPayOnKillGains()
    {
        var bench = CombatBench.Create();
        var attacker = bench.AttackerStats;
        attacker.SetResource("mana", 0);
        bench.Modify(bench.Attacker, "on_kill.mana", 7);
        bench.Hit(10_000);
        Assert.Equal(7, attacker.Resource("mana"), 9);
        Assert.Contains(bench.Events.OfType<LeechPaid>(), p => p.Kind == "kill" && p.Resource == "mana");
    }

    [Fact]
    public void CritAvoidanceLowersTheChanceAndDamageOverTimeCanCritByItsOwnChance()
    {
        var bench = CombatBench.Create();
        bench.Modify(bench.Attacker, "crit.chance", 1);
        Assert.True(bench.Hit(10, canCrit: true).Crit);
        bench.Modify(bench.Victim, "crit.avoid", 1);
        Assert.False(bench.Hit(10, canCrit: true).Crit);

        // A tick never crits by crit.chance; crit.dot gives it a chance of its own (still lowered by avoidance).
        var other = CombatBench.Create();
        other.Modify(other.Attacker, "crit.chance", 1);
        Assert.False(other.Hit(10, tags: "dot", canCrit: true).Crit);
        other.Modify(other.Attacker, "crit.dot", 1);
        Assert.True(other.Hit(10, tags: "dot", canCrit: false).Crit);
    }

    [Fact]
    public void GuardValueBlocksStopUpToAValueAndStopAilments()
    {
        var registry = WithCombatRules(v => v["block"] = "guard");
        var bench = CombatBench.Create(registry);
        bench.Modify(bench.Victim, "block.chance", 1);
        bench.Modify(bench.Victim, "block.value", 8);
        // A hob is level 1: the rule stops 10 + 2 + 8 = 20 at most.
        var light = bench.Hit(15);
        Assert.True(light.Blocked);
        Assert.Equal(0, light.Total, 9);
        var heavy = bench.Hit(50);
        Assert.Equal(30, heavy.Total, 9);
        // Block power caps the stoppable fraction: half of a 30 hit is 15.
        bench.Modify(bench.Victim, "block.power", -0.5);
        Assert.Equal(15, bench.Hit(30).Total, 9);

        // What seeps through a block leaves no ailment while rule block_stops_ailments holds.
        bench.Modify(bench.Attacker, "status.bleeding.chance", 1);
        bench.Hit(30);
        Assert.False(bench.Sim.World.Get<Statuses>(bench.Victim)?.Has("bleeding") ?? false);
    }

    [Fact]
    public void ArmorPenetrationReadsInTheArmorRule()
    {
        var bench = CombatBench.Create();
        bench.Modify(bench.Victim, "armor", 100);
        var armored = bench.Hit(20).Total;
        bench.Modify(bench.Attacker, "pen.armor", 1);
        Assert.Equal(20, bench.Hit(20).Total, 9);
        Assert.True(armored < 20);
    }

    [Fact]
    public void EntropyEvasionLandsHitsOnSchedule()
    {
        var registry = WithCombatRules(v => v["evasion"] = "entropy");
        var bench = CombatBench.Create(registry);
        bench.Modify(bench.Victim, "evasion", 100_000);
        // With the hit chance at its floor, an evadable hit lands once in twenty: never twice in a row, never
        // more than twenty misses apart.
        var landed = new List<int>();
        for (var i = 0; i < 60; i++)
        {
            if (!bench.Hit(10, tags: "attack").Evaded)
            {
                landed.Add(i);
            }
        }
        Assert.InRange(landed.Count, 2, 4);
        for (var i = 1; i < landed.Count; i++)
        {
            Assert.Equal(20, landed[i] - landed[i - 1]);
        }
    }

    [Fact]
    public void CannotKillLeavesALastPointOfLife()
    {
        var bench = CombatBench.Create();
        bench.Run("{\"type\": \"damage\", \"amount\": 100000, \"threads\": [\"stone\"], \"can_crit\": false, \"cannot_kill\": true}");
        Assert.Equal(1, bench.VictimStats.Resource("life"), 9);
        Assert.False(bench.Sim.World.Has<Dead>(bench.Victim));
    }

    [Fact]
    public void AHitCapClampsWhatOneHitLands()
    {
        var bench = CombatBench.Create();
        bench.Modify(bench.Victim, "hit.cap", 0.1);
        var max = bench.VictimStats.ResourceMax("life");
        var hit = bench.Hit(1000);
        Assert.True(hit.Clamped);
        Assert.Equal(max * 0.1, hit.Total, 6);
    }

    [Fact]
    public void StaggerDefersPartOfAHitAndPaysItOutWithoutMitigatingAgain()
    {
        var bench = CombatBench.Create();
        bench.Modify(bench.Victim, "stagger.fraction", 0.5);
        bench.Modify(bench.Victim, "resist.ember", 0.5);
        var hit = bench.Hit(40, thread: "ember");
        // Half of the 20 that landed after resistance is deferred.
        Assert.Equal(10, hit.Total, 9);
        Assert.Contains(bench.Events.OfType<HitStaggered>(), s => Math.Abs(s.Amount - 10) < 1e-9);
        bench.Run(4);
        // The rest arrives over the stagger's time in even parts, not halved by resistance again: twenty in all.
        var landed = bench.Events.OfType<DamageDealt>().Where(d => d.Target == bench.Victim).ToList();
        Assert.Equal(20, landed.Sum(d => d.Total), 6);
        Assert.All(landed.Skip(1), d => Assert.True(d.Tags.Has("deferred")));
        Assert.All(landed.Skip(1), d => Assert.Equal(2, d.Total, 6));
    }

    [Fact]
    public void CheatingDeathLeavesOneLifeOnceUntilItsStatusEnds()
    {
        var bench = CombatBench.Create();
        bench.Modify(bench.Victim, "cheat_death.chance", 1);
        bench.Hit(100_000);
        Assert.Equal(1, bench.VictimStats.Resource("life"), 9);
        Assert.Single(bench.Events.OfType<DeathCheated>());
        Assert.True(bench.Sim.World.Get<Statuses>(bench.Victim)!.Has("death_cheated"));
        bench.Hit(100_000);
        Assert.True(bench.Sim.World.Has<Dead>(bench.Victim));
    }

    [Fact]
    public void BehindIsAVictimTagFromTheVictimsFacing()
    {
        var bench = CombatBench.Create();
        var victim = bench.Sim.World.Get<Transform>(bench.Victim)!;
        // The attacker stands west of the victim. Facing west, the victim sees it; facing east, it is behind.
        victim.Yaw = MathF.PI;
        Assert.False(bench.Hit(10).Tags.Has("vs.behind"));
        victim.Yaw = 0;
        Assert.True(bench.Hit(10).Tags.Has("vs.behind"));
    }

    [Fact]
    public void AShieldAbsorbsWaitsOutItsDelayThenRecharges()
    {
        var bench = CombatBench.Create();
        var victim = bench.VictimStats;
        victim.Attributes.SetBase("shield.max", 40);
        victim.Attributes.SetBase("shield.recharge_delay", 1);
        victim.Attributes.SetBase("shield.recharge_rate", 0.5);
        victim.FillResources();
        var life = victim.Resource("life");
        Assert.Equal(40, victim.Resource("shield"), 9);

        // The shield takes the hit before life, breaks, and its delay begins.
        bench.Hit(50);
        Assert.Equal(0, victim.Resource("shield"), 9);
        Assert.Equal(life - 10, victim.Resource("life"), 9);
        Assert.Contains(bench.Events.OfType<PoolBroken>(), b => b.Resource == "shield");
        bench.Sim.Tick();
        Assert.True(victim.HasFlag("pool.shield.broken"));
        Assert.True(victim.Recent.ContainsKey("pool.shield.broken"));

        // Nothing comes back during the delay; after it the shield recharges at half its maximum a second.
        bench.Run(0.8);
        Assert.Equal(0, victim.Resource("shield"), 9);
        bench.Run(0.6);
        Assert.True(victim.Resource("shield") > 0);
        Assert.True(victim.HasFlag("pool.shield.recharging"));
        Assert.Contains(bench.Events.OfType<PoolRecharging>(), r => r.Resource == "shield");
        bench.Run(2.5);
        Assert.Equal(40, victim.Resource("shield"), 6);
        Assert.False(victim.HasFlag("pool.shield.recharging"));
        Assert.Contains(bench.Events.OfType<PoolRestored>(), r => r.Resource == "shield");
    }

    [Fact]
    public void PoiseBreaksIntoAStatusAndReArms()
    {
        var bench = CombatBench.Create();
        var victim = bench.VictimStats;
        victim.Attributes.SetBase("poise.max", 30);
        victim.Attributes.SetBase("life.max", 1000);
        victim.FillResources();
        var life = victim.Resource("life");

        // Poise is a break bar: it takes the hit's damage without absorbing any of it.
        bench.Hit(20);
        Assert.Equal(10, victim.Resource("poise"), 9);
        Assert.Equal(life - 20, victim.Resource("life"), 9);
        bench.Hit(15);
        Assert.Equal(0, victim.Resource("poise"), 9);
        var statuses = bench.Sim.World.Get<Statuses>(bench.Victim)!;
        Assert.True(statuses.Has("poise_broken"));
        Assert.Contains(bench.Events.OfType<PoolBroken>(), b => b.Resource == "poise" && b.By == bench.Attacker);
        bench.Sim.Tick();
        Assert.True(victim.HasFlag("pool.poise.broken"));

        // While broken it takes no poise damage and does not refill; then it re-arms quickly and cannot break again
        // until it is whole.
        bench.Hit(5);
        Assert.Equal(0, victim.Resource("poise"), 9);
        bench.Run(2.1);
        Assert.False(statuses.Has("poise_broken"));
        Assert.True(statuses.Has("poise_rearming"));
        Assert.True(victim.HasFlag("pool.poise.rearming"));
        bench.Hit(25);
        Assert.True(statuses.Has("poise_rearming"));
        bench.Run(4);
        Assert.Equal(30, victim.Resource("poise"), 6);
        Assert.False(statuses.Has("poise_rearming"));
        Assert.False(victim.HasFlag("pool.poise.rearming"));

        // Damage over time breaks no poise.
        bench.Hit(20, tags: "dot");
        Assert.Equal(30, victim.Resource("poise"), 6);
    }

    [Fact]
    public void PoolsExistOnlyWhereABodyHasAMaximum()
    {
        var bench = CombatBench.Create();
        var victim = bench.VictimStats;
        Assert.False(victim.Resources.ContainsKey("shield"));
        Assert.False(victim.Resources.ContainsKey("poise"));
        Assert.False(victim.Resources.ContainsKey("ward"));
        // Granting a maximum later makes the pool recharge into being, a fifth of it each second.
        victim.Attributes.Add(new Modifier { Attribute = "shield.max", Form = ModifierForm.Flat, Value = 10, Source = "test" });
        bench.Run(2.5);
        Assert.InRange(victim.Resource("shield"), 4.5, 5.5);
        bench.Run(3);
        Assert.Equal(10, victim.Resource("shield"), 6);
    }

    [Fact]
    public void ReservationsLowerTheUsableMaximumAndMirrorIntoAGauge()
    {
        var bench = CombatBench.Create();
        var stats = bench.VictimStats;
        var max = stats.ResourceMax("mana");
        stats.Attributes.Add(new Modifier { Attribute = "mana.reserved", Form = ModifierForm.Flat, Value = 10, Source = "contract" });
        Assert.Equal(max - 10, stats.ResourceMax("mana"), 9);
        bench.Sim.Tick();
        Assert.Equal(10, stats.Gauge("reserved.mana"), 9);
        Assert.True(stats.Resource("mana") <= max - 10 + 1e-9);
        stats.Attributes.RemoveSource("contract");
        bench.Sim.Tick();
        Assert.Equal(max, stats.ResourceMax("mana"), 9);
        Assert.Equal(0, stats.Gauge("reserved.mana"), 9);
    }

    [Fact]
    public void InsightFillsWithMomentumAndTurnsBlowsAsideFirst()
    {
        var bench = CombatBench.Create();
        // The attacker builds insight by landing blows; it starts empty.
        bench.Modify(bench.Attacker, "insight.max", 30);
        bench.Modify(bench.Attacker, "on_hit.insight", 5);
        var attacker = bench.AttackerStats;
        bench.Sim.Tick();
        Assert.Equal(0, attacker.Resource("insight"), 9);
        bench.Hit(10);
        bench.Hit(10);
        Assert.Equal(10, attacker.Resource("insight"), 9);
        // Struck, it takes the blow before any other pool.
        var life = attacker.Resource("life");
        bench.Run("""{"type": "damage", "amount": 8, "threads": ["stone"], "can_crit": false}""", source: bench.Victim, target: bench.Attacker);
        Assert.Equal(2, attacker.Resource("insight"), 9);
        Assert.Equal(life, attacker.Resource("life"), 9);
    }

    [Fact]
    public void WardCanBeBoughtWithSpentCharges()
    {
        double WardAfterCasting(double fury)
        {
            var bench = CombatBench.Create();
            var sim = bench.Sim;
            bench.Modify(bench.Attacker, "ward.max", 200);
            sim.Service<ChargeService>().Gain(sim, bench.Attacker, "fury", fury);
            var book = sim.World.Get<HollowWake.Core.Components.WeaveBook>(bench.Attacker) ?? sim.World.Add(bench.Attacker, new HollowWake.Core.Components.WeaveBook());
            var slot = book.Add(new WeaveSpec { Form = "ward", Threads = { "lumen" }, Glyphs = { "furywoven" } });
            var at = sim.World.Get<Transform>(bench.Attacker)!.Position;
            Assert.True(sim.Service<HollowWake.Core.Weaves.WeaveRuntime>().TryBegin(sim, bench.Attacker, slot, bench.Attacker, at, Vector2.Zero, out var reason), reason);
            Assert.Equal(0, sim.Service<ChargeService>().Count(sim, bench.Attacker, "fury"));
            return bench.AttackerStats.Resource("ward");
        }
        // The furywoven glyph spends all fury and wards eight for each charge spent, on top of the form's own ward.
        Assert.Equal(24, WardAfterCasting(3) - WardAfterCasting(0), 6);
    }

    [Fact]
    public void PointsOfACoreAttributeGrantTheirPerPointLines()
    {
        var bench = CombatBench.Create();
        var stats = bench.VictimStats;
        // A level one hob with seven vigor: 40 + 10 from the formula, 28 from vigor's per-point line.
        Assert.Equal(78, stats.ResourceMax("life"), 9);
        var vigor = stats.Attributes.Explain("life.max", stats).Applied.Single(m => m.Source == "per_point:vigor");
        Assert.Equal(4, vigor.Value, 9);
        bench.Modify(bench.Victim, "vigor", 5);
        Assert.Equal(98, stats.ResourceMax("life"), 9);
        // Will grants spell power per point through the same fold as every other modifier.
        Assert.Equal(1 + 0.015 + 0.025 * stats.Get("will"), stats.Get("spell.power"), 9);
    }

    [Fact]
    public void TradesGainOneAttributeAsAnotherWhileForgoingSome()
    {
        var bench = CombatBench.Create();
        var stats = bench.VictimStats;
        var life = stats.ResourceMax("life");
        Assert.Equal(0, stats.ResourceMax("shield"), 9);
        // The dials are ordinary attributes: half of life woven into shield, a fifth of life given up for it.
        bench.Modify(bench.Victim, "trade.life_to_shield", 0.5);
        bench.Modify(bench.Victim, "trade.life_to_shield.forgo", 0.2);
        // Both maxima round to whole points, as declared.
        var kept = Math.Round(life * 0.8, MidpointRounding.AwayFromZero);
        Assert.Equal(kept, stats.ResourceMax("life"), 9);
        Assert.Equal(Math.Round(kept * 0.5, MidpointRounding.AwayFromZero), stats.ResourceMax("shield"), 9);
        Assert.Contains(stats.Attributes.ModifiersFor("shield.max"), m => m.Source == "trade:life.max:shield.max");
    }

    [Fact]
    public void TheDefaultLadderListsTheNewStagesInRuleOrder()
    {
        var plan = HitPlan.For(CoreContent.Registry.Rules);
        Assert.Empty(plan.Problems);
        Assert.Equal(new[] { "immunity", "guard", "evade", "block", "taken_as", "mitigate", "taken", "cap", "stagger", "pools", "cheat_death", "cull", "leech" }, plan.StageNames);
        Assert.Equal(new[] { "insight", "poise", "ward", "shield", "life" }, plan.Pools);
        Assert.True(plan.PoolRuleOf("poise")!.Break);
        Assert.Equal("shield_delay", plan.PoolRuleOf("shield")!.OnHurt);
    }

    [Fact]
    public void ARolledAmountDrawsAsRandWouldAndTheHitCarriesItsRoll()
    {
        var rolled = CombatBench.Create();
        var ctx = rolled.Run("{\"type\": \"damage\", \"amount\": \"lerp(10, 20, roll)\", \"threads\": [\"stone\"], \"can_crit\": false}");
        var random = CombatBench.Create();
        var plain = random.Run("{\"type\": \"damage\", \"amount\": \"lerp(10, 20, rand())\", \"threads\": [\"stone\"], \"can_crit\": false}");
        // One draw at the same point: writing roll for rand() changes no outcome, and the roll is that draw.
        Assert.Equal(random.HitsOn(random.Victim).Single().Total, rolled.HitsOn(rolled.Victim).Single().Total, 9);
        Assert.Equal(new HollowWake.Core.Maths.Rng(5).NextDouble(), ctx.Vars["roll"], 12);
        // An amount that never reads the roll draws none.
        Assert.False(plain.Vars.ContainsKey("roll"));

        var hit = rolled.Context(rolled.Attacker, rolled.Victim);
        var packet = DamagePipeline.Build(hit, 10, new[] { "stone" }, HollowWake.Core.Tags.TagSet.Empty, false);
        packet.Roll = 0.75;
        Assert.Equal(0.75, DamagePipeline.Apply(hit, packet).Roll);
    }

    [Fact]
    public void AHighRollCanDoMoreAfterTheHit()
    {
        var bench = CombatBench.Create();
        var roll = new HollowWake.Core.Maths.Rng(5).NextDouble().ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        bench.Run("{\"type\": \"sequence\", \"effects\": [" +
            "{\"type\": \"damage\", \"amount\": \"lerp(10, 20, roll)\", \"threads\": [\"stone\"], \"can_crit\": false}," +
            "{\"type\": \"condition\", \"if\": \"abs(roll - " + roll + ") < 0.000000001\", " +
            "\"then\": [{\"type\": \"apply_status\", \"status\": \"vulnerable\", \"duration\": 3}], " +
            "\"else\": [{\"type\": \"apply_status\", \"status\": \"weakened\", \"duration\": 3}]}]}");
        var statuses = bench.Sim.World.Get<Statuses>(bench.Victim)!;
        Assert.True(statuses.Has("vulnerable"));
        Assert.False(statuses.Has("weakened"));
    }

    [Fact]
    public void LowAndFullShieldReadTheirLinesWhereABodyHoldsAShield()
    {
        var bench = CombatBench.Create();
        var stats = bench.VictimStats;
        // Without a shield neither condition holds.
        Assert.False(stats.HasFlag("low_shield"));
        Assert.False(stats.HasFlag("full_shield"));
        stats.Attributes.SetBase("shield.max", 40);
        stats.SetResource("shield", 40);
        Assert.True(stats.HasFlag("full_shield"));
        Assert.False(stats.HasFlag("low_shield"));
        stats.SetResource("shield", 10);
        Assert.True(stats.HasFlag("low_shield"));
        // The line is an attribute like any other: lowered to 0.15, a quarter of the shield is no longer low.
        bench.Modify(bench.Victim, "low_shield.line", -0.2);
        Assert.False(stats.HasFlag("low_shield"));
        // A resource whose lines nothing declares has no such conditions.
        stats.Attributes.SetBase("poise.max", 30);
        stats.SetResource("poise", 0);
        Assert.False(stats.HasFlag("low_poise"));
    }

    [Fact]
    public void ARaisedGuardLetsDamageOverTimeThrough()
    {
        var arena = CastingArena.Create();
        arena.Begin(arena.Slot("t_guard"));
        Assert.True(arena.Stats.HasFlag("guarding"));
        // Past the parry window, the foe in front burns the guarded caster.
        arena.RunSeconds(0.3);
        var life = arena.Stats.Resource("life");
        arena.Sim.Service<StatusEffects.StatusService>().Apply(arena.Sim, arena.Caster, "burning", arena.Enemy, new StatusEffects.StatusApplication { Magnitude = 8, Duration = 2 });
        arena.RunSeconds(1.1);
        var guard = arena.Sim.World.Get<Guard>(arena.Caster)!;
        var ticks = arena.Of<DamageDealt>().Where(d => d.Target == arena.Caster && d.Tags.Has("dot")).ToList();
        Assert.NotEmpty(ticks);
        Assert.All(ticks, d => Assert.False(d.Blocked));
        Assert.Equal(0, guard.Absorbed, 9);
        Assert.True(arena.Stats.Resource("life") < life);
        // A blow from the same foe is caught.
        arena.Hit(arena.Enemy, arena.Caster, 10);
        Assert.Equal(10, guard.Absorbed, 9);
    }
}
