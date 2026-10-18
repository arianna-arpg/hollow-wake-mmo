using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using HollowWake.Core.Attributes;
using HollowWake.Core.Combat;
using HollowWake.Core.Combat.Stages;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Maths;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.StatusEffects;
using HollowWake.Core.Tags;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>The damage pipeline as registered stages: order and skips from rules, victim tags from rules, the hit scope.</summary>
public class HitStageTests
{
    // A seeded duel recorded before the pipeline became stages: every hit's tick, total, crit and evade, and
    // every status applied. The default stage order must reproduce it exactly.
    private static readonly string[] RecordedDuel =
    {
        "36 hit 2.090823260075437 False False", "38 hit 4.739443420457901 False False", "56 hit 1.9731031930877518 False False",
        "63 hit 5.4421430946797935 False False", "78 hit 3.0804489547475398 False False", "88 hit 3.3804250333546557 False False",
        "97 hit 2.91318678840672 False False", "115 hit 7.772796045734013 True False", "117 hit 2.946730901004785 False False",
        "137 hit 3.0065409395435627 False False", "140 hit 5.2545448882043235 False False", "157 hit 2.430210688385009 False False",
        "164 hit 3.2654794794481345 False False", "178 hit 3.478955793321854 False False", "191 hit 8.245092966268576 True False",
        "191 status withered", "197 hit 3.6122714717230373 False False", "216 hit 4.839981448748942 False False",
        "217 hit 2.1079918556976605 False False", "237 hit 3.0328791370478556 False False", "241 hit 5.087222818606842 False False",
        "257 hit 0 False True", "266 hit 3.525209283170595 False False", "277 hit 3.0350114316041243 False False",
        "291 hit 4.741048639277133 False False", "297 hit 3.5705506499020183 False False", "316 hit 0 False True",
        "317 hit 1.977092579580482 False False", "337 hit 1.8474038126081185 False False", "341 hit 0 False True",
        "358 hit 2.8800015132791885 False False", "366 hit 3.7309664028488774 False False", "378 hit 2.28107562304755 False False",
        "391 hit 5.096704690205085 False False", "398 hit 3.50179717662931 False False", "416 hit 5.084563812441066 False False",
        "418 hit 2.4222004570072153 False False", "438 hit 0 True True", "441 hit 3.911429815769479 False False",
        "458 hit 2.367453889425592 False False", "466 hit 3.8059990878321974 False False", "478 hit 0 False True",
        "491 hit 3.5383422248642034 False False", "498 hit 4.054866816105618 False False",
    };

    private static List<string> RunDuel(ContentRegistry registry)
    {
        var sim = ZoneBuilder.CreateArena(registry, Rng.Hash64("duel:remnant_shade:barrow_wight"));
        var log = new List<string>();
        sim.Events.Subscribe<DamageDealt>(d => log.Add($"{sim.Clock.Tick} hit {d.Total.ToString("R", CultureInfo.InvariantCulture)} {d.Crit} {d.Evaded}"));
        sim.Events.Subscribe<StatusApplied>(s => log.Add($"{sim.Clock.Tick} status {s.Status}"));
        var factory = sim.Service<PatternFactory>();
        factory.Spawn(sim, "remnant_shade", new Vector3(10, 10, 0), new SpawnOptions { Faction = "red" });
        factory.Spawn(sim, "barrow_wight", new Vector3(14, 10, 0), new SpawnOptions { Faction = "blue" });
        sim.RunSeconds(25);
        return log;
    }

    /// <summary>The core content with the combat rule group edited, and the duel factions.</summary>
    private static ContentRegistry WithCombatRules(Action<JsonObject> edit)
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
    public void DefaultStageOrderReproducesARecordedSeededDuel()
    {
        Assert.Equal(RecordedDuel, RunDuel(CoreContent.Registry));
        // Without the rule the pipeline falls back to the same ladder.
        Assert.Equal(RecordedDuel, RunDuel(WithCombatRules(v => v.Remove("stages"))));
        Assert.Equal(HitStages.DefaultOrder, HitPlan.For(CoreContent.Registry.Rules).StageNames);
    }

    /// <summary>An arena with an attacker and a victim standing still at a distance, and a way to hit the victim.</summary>
    private sealed class Bench
    {
        public required Simulation Sim;
        public required Entity Attacker;
        public required Entity Victim;
        public readonly List<DamageDealt> Hits = new();

        public static Bench Create(ContentRegistry registry, float distance = 3, string victim = "hob")
        {
            var sim = ZoneBuilder.CreateArena(registry, 11);
            var factory = sim.Service<PatternFactory>();
            var a = factory.Spawn(sim, "loosed_brigand", new Vector3(10, 10, 0), new SpawnOptions { Faction = "red" });
            var v = factory.Spawn(sim, victim, new Vector3(10 + distance, 10, 0), new SpawnOptions { Faction = "blue" });
            sim.World.Remove<Behavior>(a);
            sim.World.Remove<Behavior>(v);
            var bench = new Bench { Sim = sim, Attacker = a, Victim = v };
            sim.Events.Subscribe<DamageDealt>(d => bench.Hits.Add(d));
            return bench;
        }

        public Stats AttackerStats => Sim.World.Get<Stats>(Attacker)!;
        public Stats VictimStats => Sim.World.Get<Stats>(Victim)!;

        /// <summary>A damage effect of the attacker's on the victim, through the effect registry like any graph.</summary>
        public DamageDealt Hit(Entity target, string tags = "", double amount = 10, bool canCrit = false)
        {
            var spec = JsonSerializer.Deserialize<TypedSpec>($"{{\"type\": \"damage\", \"amount\": {amount.ToString(CultureInfo.InvariantCulture)}, \"threads\": [\"stone\"], \"can_crit\": {(canCrit ? "true" : "false")}}}", ContentJson.Options)!;
            var effects = Sim.Service<EffectRegistry>();
            var effect = effects.Compile(spec, new EffectCompileContext { Content = Sim.Content, Registry = effects, Owner = "test" });
            var ctx = new EffectContext { Sim = Sim, Source = Attacker, Target = target, Rng = new Rng(5), Tags = TagSet.Parse(tags), Point = Sim.World.Get<Transform>(target)!.Position };
            var before = Hits.Count;
            effect.Execute(ctx);
            Assert.Equal(before + 1, Hits.Count);
            return Hits[^1];
        }
    }

    [Fact]
    public void VictimTagsComeFromRuleExpressions()
    {
        var registry = WithCombatRules(v => ((JsonObject)v["victim_tags"]!)["far"] = "distance > 5");
        var near = Bench.Create(registry, distance: 2);
        Assert.False(near.Hit(near.Victim).Tags.Has("vs.far"));
        var far = Bench.Create(registry, distance: 8);
        var tags = far.Hit(far.Victim).Tags;
        Assert.True(tags.Has("vs.far"));
        Assert.True(tags.Has("vs.full_life"));
        Assert.True(tags.Has("vs.hob"));

        // The default rules keep today's victim tags: low and full life read the target in the hit scope.
        var standard = Bench.Create(CoreContent.Registry);
        standard.VictimStats.SetResource("life", standard.VictimStats.ResourceMax("life") * 0.2);
        var low = standard.Hit(standard.Victim).Tags;
        Assert.True(low.Has("vs.low_life"));
        Assert.False(low.Has("vs.full_life"));
        Assert.False(low.Has("vs.far"));
    }

    [Fact]
    public void TheHitScopeReadsTheTargetsStacksWithoutTouchingTheCache()
    {
        var bench = Bench.Create(CoreContent.Registry);
        var factory = bench.Sim.Service<PatternFactory>();
        var bleeding = factory.Spawn(bench.Sim, "hob", new Vector3(12, 12, 0), new SpawnOptions { Faction = "blue" });
        bench.Sim.World.Remove<Behavior>(bleeding);
        bench.Sim.Service<StatusService>().Apply(bench.Sim, bleeding, "bleeding", bench.Attacker, new StatusApplication { Stacks = 3, Magnitude = 0.001, Duration = 60 });
        Assert.Equal(3, bench.Sim.World.Get<Stats>(bleeding)!.Gauge("status.bleeding.stacks"));

        var attacker = bench.AttackerStats;
        var cachedBefore = attacker.Get("damage", TagSet.Of("hit"));
        attacker.Attributes.Add(new Modifier { Attribute = "damage", Form = ModifierForm.More, Value = 0.1, Gauge = "target.status.bleeding.stacks", Source = "test" });

        // The attacker's damage fold reads the struck body's stacks: three are thirty percent more, none nothing,
        // and folding against one target leaves nothing behind for the next.
        double Raw(Entity target) => DamagePipeline.Build(Context(bench, target), 10, new[] { "stone" }, TagSet.Empty, false).Amounts["stone"];
        var plain = Raw(bench.Victim);
        var stacked = Raw(bleeding);
        var plainAgain = Raw(bench.Victim);
        Assert.Equal(plain * 1.3, stacked, 9);
        Assert.Equal(plain, plainAgain);
        // Outside a hit the modifier has no target to read and stays inert: the cached fold is as before.
        Assert.Equal(cachedBefore, attacker.Get("damage", TagSet.Of("hit")));
        // The whole pipeline lands such a hit like any other.
        Assert.True(bench.Hit(bleeding).Total > 0);
    }

    private static EffectContext Context(Bench bench, Entity target) => new()
    {
        Sim = bench.Sim,
        Source = bench.Attacker,
        Target = target,
        Rng = new Rng(9),
    };

    [Fact]
    public void StageSkipsComeFromRulesByHitTag()
    {
        var registry = WithCombatRules(v => v["stage_skips"] = new JsonObject
        {
            ["t_unblockable"] = new JsonArray("block"),
            ["t_steady"] = new JsonArray("crit"),
        });
        var bench = Bench.Create(registry);
        bench.VictimStats.Attributes.Add(new Modifier { Attribute = "block.chance", Form = ModifierForm.Flat, Value = 1, Source = "test" });
        Assert.True(bench.Hit(bench.Victim).Blocked);
        Assert.False(bench.Hit(bench.Victim, "t_unblockable").Blocked);

        bench.AttackerStats.Attributes.Add(new Modifier { Attribute = "crit.chance", Form = ModifierForm.Flat, Value = 1, Source = "test" });
        Assert.True(bench.Hit(bench.Victim, "t_unblockable", canCrit: true).Crit);
        Assert.False(bench.Hit(bench.Victim, "t_unblockable t_steady", canCrit: true).Crit);
    }

    [Fact]
    public void RegisteredStagesRunWhereTheRulesPlaceThem()
    {
        HitStages.Register(new DoublingStage());
        var registry = WithCombatRules(v => v["stages"] = new JsonArray("immunity", "evade", "block", "mitigate", "taken", "t_double", "pools", "cull"));
        var doubled = Bench.Create(registry);
        var standard = Bench.Create(CoreContent.Registry);
        Assert.Equal(standard.Hit(standard.Victim).Total * 2, doubled.Hit(doubled.Victim).Total, 9);
        Assert.Empty(HitPlan.For(registry.Rules).Problems);
        var unknown = WithCombatRules(v => v["stages"] = new JsonArray("immunity", "no_such_stage"));
        Assert.Contains(HitPlan.For(unknown.Rules).Problems, p => p.Contains("no_such_stage"));
    }

    [Fact]
    public void AResourceEffectTakesNoLifeAroundTheLadder()
    {
        // Life falls only through the stages: a resource effect a formula turns against a body's life changes
        // nothing and says so once per weave, while it still moves other resources and still restores life.
        var arena = DeliveryArena.Create();
        var log = new ListSimLog();
        arena.Sim.Log = log;
        var boar = arena.Body(12, 10);
        var stats = arena.Sim.World.Get<Stats>(boar)!;
        var at = arena.Sim.World.Get<Transform>(boar)!.Position;
        var blast = arena.Weave("blast");
        var drain = arena.Effect("{\"type\": \"resource\", \"resource\": \"life\", \"amount\": \"-min(5, target.life)\"}");
        stats.SetResource("life", stats.ResourceMax("life") - 20);
        var life = stats.Resource("life");
        drain.Execute(arena.Context(at, target: boar, weave: blast));
        drain.Execute(arena.Context(at, target: boar, weave: blast));
        Assert.Equal(life, stats.Resource("life"));
        Assert.Empty(arena.Of<DamageDealt>());
        var refusal = Assert.Single(log.Lines);
        Assert.Contains($"'{blast.Key}'", refusal);
        Assert.Contains("damage", refusal);
        // Another weave is told once more; a caster turning it on itself loses nothing either.
        var bolt = arena.Weave("bolt");
        drain.Execute(arena.Context(at, target: boar, weave: bolt));
        Assert.Equal(2, log.Lines.Count);
        Assert.Contains($"'{bolt.Key}'", log.Lines[1]);
        var caster = arena.Sim.World.Get<Stats>(arena.Caster)!;
        var casterLife = caster.Resource("life");
        arena.Effect("{\"type\": \"resource\", \"resource\": \"life\", \"amount\": \"-min(5, source.life)\", \"self\": true}").Execute(arena.Context(at, target: boar, weave: blast));
        Assert.Equal(casterLife, caster.Resource("life"));
        arena.Effect("{\"type\": \"resource\", \"resource\": \"life\", \"amount\": 5}").Execute(arena.Context(at, target: boar, weave: blast));
        Assert.Equal(life + 5, stats.Resource("life"), 6);
        caster.Attributes.Add(new Modifier { Attribute = "mana.max", Form = ModifierForm.Flat, Value = 100, Source = "test" });
        caster.SetResource("mana", 50);
        arena.Effect("{\"type\": \"resource\", \"resource\": \"mana\", \"amount\": -5, \"self\": true}").Execute(arena.Context(at, target: boar, weave: blast));
        Assert.Equal(45, caster.Resource("mana"), 6);
    }

    [Theory]
    [InlineData("resource")]
    [InlineData("heal")]
    public void ValidationRefusesALiteralLifeDecreaseOnADirectWriter(string type)
    {
        var dir = Path.Combine(Path.GetTempPath(), "hollowwake-drain-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "resources.yaml"), "kind: resources\nentries:\n  - { id: t_vitality, max: life.max, lethal: true }\n");
            File.WriteAllText(Path.Combine(dir, "weaves.yaml"), string.Join("\n",
                "kind: weaves",
                "entries:",
                "  - { id: t_drain_number, name: Drain, form: slam_quick, effects: [ { type: resource, resource: life, amount: -5 } ] }",
                "  - { id: t_drain_text, name: Drain, form: slam_quick, effects: [ { type: area, radius: 2, effects: [ { type: resource, resource: life, amount: \"-5\" } ] } ] }",
                "  - { id: t_drain_formula, name: Drain, form: slam_quick, effects: [ { type: resource, resource: life, amount: \"-min(5, target.life)\" } ] }",
                "  - { id: t_spend_mana, name: Spend, form: slam_quick, effects: [ { type: resource, resource: mana, amount: -5 } ] }",
                "  - { id: t_give_life, name: Give, form: slam_quick, effects: [ { type: resource, resource: life, amount: 5 } ] }",
                "  - { id: t_drain_custom, name: Drain, form: slam_quick, effects: [ { type: resource, resource: t_vitality, amount: -5 } ] }",
                "  - { id: t_drain_default, name: Drain, form: slam_quick, effects: [ { type: resource, amount: -5 } ] }",
                "").Replace("type: resource", $"type: {type}", StringComparison.Ordinal));
            ContentRegistry.Load(new[] { CoreContent.FindContentDir(), dir }, out var report);
            var errors = report.Errors.Select(e => $"{e.Id}: {e.Message}").ToList();
            Assert.Contains(errors, e => e.StartsWith("t_drain_number") && e.Contains("'damage'"));
            Assert.Contains(errors, e => e.StartsWith("t_drain_text") && e.Contains("'damage'"));
            Assert.Contains(errors, e => e.StartsWith("t_drain_custom") && e.Contains("'damage'"));
            Assert.Equal(type == "heal", errors.Any(e => e.StartsWith("t_drain_default") && e.Contains("'damage'")));
            // A formula is refused when it runs, not at load; other resources may fall, and life may rise.
            Assert.DoesNotContain(errors, e => e.StartsWith("t_drain_formula") || e.StartsWith("t_spend_mana") || e.StartsWith("t_give_life"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>A test stage after damage taken: doubles what was received.</summary>
    private sealed class DoublingStage : IHitStage
    {
        public string Name => "t_double";

        public HitFlow Run(HitScope hit)
        {
            foreach (var thread in hit.Result.Received.Keys.ToList())
            {
                hit.Result.Received[thread] *= 2;
            }
            hit.Total *= 2;
            hit.Result.Total = hit.Total;
            return HitFlow.Continue;
        }
    }
}
