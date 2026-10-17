using System.Globalization;
using System.Numerics;
using System.Text.Json;
using HollowWake.Core.Attributes;
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
using HollowWake.Core.Weaves;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>
/// Two bodies standing still in an arena, an effect compiler and a log of every event: the bench the status,
/// defense, proc and charge tests share.
/// </summary>
internal sealed class CombatBench
{
    public required Simulation Sim;
    public required Entity Attacker;
    public required Entity Victim;
    public readonly List<GameEvent> Events = new();

    public static CombatBench Create(ContentRegistry? registry = null, string attacker = "hob", string victim = "hob", float distance = 3, ulong seed = 11)
    {
        var sim = ZoneBuilder.CreateArena(registry ?? CoreContent.Registry, seed);
        var factory = sim.Service<PatternFactory>();
        var a = factory.Spawn(sim, attacker, new Vector3(10, 10, 0), new SpawnOptions { Faction = "red" });
        var v = factory.Spawn(sim, victim, new Vector3(10 + distance, 10, 0), new SpawnOptions { Faction = "blue" });
        sim.World.Remove<Behavior>(a);
        sim.World.Remove<Behavior>(v);
        var bench = new CombatBench { Sim = sim, Attacker = a, Victim = v };
        sim.Events.SubscribeAll(bench.Events.Add);
        return bench;
    }

    public Stats AttackerStats => Sim.World.Get<Stats>(Attacker)!;
    public Stats VictimStats => Sim.World.Get<Stats>(Victim)!;
    public StatusService Statuses => Sim.Service<StatusService>();

    public static TypedSpec Spec(string json) => JsonSerializer.Deserialize<TypedSpec>(json, ContentJson.Options)!;

    public CompiledEffect Effect(string json)
    {
        var effects = Sim.Service<EffectRegistry>();
        return effects.Compile(Spec(json), new EffectCompileContext { Content = Sim.Content, Registry = effects, Owner = "test", Compiler = Sim.Service<WeaveCompiler>() });
    }

    public EffectContext Context(Entity source, Entity target, string tags = "", int depth = 0, ulong rng = 5) => new()
    {
        Sim = Sim,
        Source = source,
        Target = target,
        Tags = TagSet.Parse(tags),
        Depth = depth,
        Rng = new Rng(rng),
        Point = Sim.World.Get<Transform>(target)?.Position ?? default,
    };

    /// <summary>Runs an effect from the attacker at the victim (or the given bodies).</summary>
    public EffectContext Run(string json, Entity? source = null, Entity? target = null, string tags = "", int depth = 0)
    {
        var ctx = Context(source ?? Attacker, target ?? Victim, tags, depth);
        Effect(json).Execute(ctx);
        return ctx;
    }

    /// <summary>A damage effect of the attacker on the victim; returns the hit it published.</summary>
    public DamageDealt Hit(double amount = 10, string thread = "stone", string tags = "", bool canCrit = false, Entity? source = null, Entity? target = null)
    {
        var before = Events.OfType<DamageDealt>().Count();
        var json = $"{{\"type\": \"damage\", \"amount\": {amount.ToString(CultureInfo.InvariantCulture)}, \"threads\": [\"{thread}\"], \"can_crit\": {(canCrit ? "true" : "false")}}}";
        Run(json, source, target, tags);
        var hits = Events.OfType<DamageDealt>().ToList();
        Assert.Equal(before + 1, hits.Count);
        return hits[^1];
    }

    public StatusInstance? Apply(Entity target, string status, double magnitude = 1, double? duration = null, int stacks = 1, Entity? source = null) =>
        Statuses.Apply(Sim, target, status, source ?? Attacker, new StatusApplication { Magnitude = magnitude, Duration = duration, Stacks = stacks });

    public void Modify(Entity who, string attribute, double value, ModifierForm form = ModifierForm.Flat, string scope = "") =>
        Sim.World.Get<Stats>(who)!.Attributes.Add(new Modifier { Attribute = attribute, Form = form, Value = value, Scope = TagSet.Parse(scope), Source = "test" });

    public void Run(double seconds) => Sim.RunSeconds(seconds);

    /// <summary>Another body standing still at a point, on a side.</summary>
    public Entity Spawn(string pattern, Vector3 at, string faction)
    {
        var e = Sim.Service<PatternFactory>().Spawn(Sim, pattern, at, new SpawnOptions { Faction = faction });
        Sim.World.Remove<Behavior>(e);
        return e;
    }

    public StatusInstance? Instance(Entity bearer, string status) => Sim.World.Get<Statuses>(bearer)?.Active.FirstOrDefault(s => s.Id == status);

    public List<DamageDealt> HitsOn(Entity target) => Events.OfType<DamageDealt>().Where(d => d.Target == target).ToList();
}

/// <summary>Statuses: forbidden tags, temporary modifiers, and the status mechanics of the parity rows.</summary>
public class StatusTests
{
    [Fact]
    public void ForbiddenTagsStopCasts()
    {
        var bench = CombatBench.Create(attacker: "loosed_kindler", victim: "hob", distance: 6);
        var sim = bench.Sim;
        var book = sim.World.Get<WeaveBook>(bench.Attacker)!;
        var bolt = book.Add(new WeaveSpec { Form = "bolt", Threads = { "ember" } });
        var strike = book.Add(new WeaveSpec { Form = "strike", Threads = { "stone" } });
        var runtime = sim.Service<WeaveRuntime>();
        var victimAt = sim.World.Get<Transform>(bench.Victim)!.Position;

        bench.Apply(bench.Attacker, "silenced", duration: 3);
        Assert.True(bench.AttackerStats.HasFlag("forbid:spell"));
        Assert.False(runtime.TryBegin(sim, bench.Attacker, bolt, bench.Victim, victimAt, Vector2.Zero, out var reason));
        Assert.Equal("forbidden:spell", reason);
        // Silence forbids spells only: an attack is refused for range, not for silence.
        runtime.TryBegin(sim, bench.Attacker, strike, bench.Victim, victimAt, Vector2.Zero, out reason);
        Assert.NotEqual("forbidden:attack", reason);
        Assert.DoesNotContain("forbidden", reason);

        bench.Statuses.Remove(sim, bench.Attacker, "silenced", TagSet.Empty);
        Assert.False(bench.AttackerStats.HasFlag("forbid:spell"));
        Assert.True(runtime.TryBegin(sim, bench.Attacker, bolt, bench.Victim, victimAt, Vector2.Zero, out reason), reason);
    }

    [Fact]
    public void ForbidFlagsEndWithTheStatus()
    {
        var bench = CombatBench.Create();
        bench.Apply(bench.Victim, "rooted", duration: 0.5);
        Assert.True(bench.VictimStats.HasFlag("forbid:movement"));
        bench.Run(1);
        Assert.False(bench.VictimStats.HasFlag("forbid:movement"));
        Assert.False(bench.VictimStats.HasFlag("rooted"));
    }

    [Fact]
    public void RefreshedTemporaryModifiersLastTheirFullTime()
    {
        var bench = CombatBench.Create();
        var stats = bench.VictimStats;
        var before = stats.Get("move.speed");
        const string modify = "{\"type\": \"modify\", \"attribute\": \"move.speed\", \"form\": \"flat\", \"value\": 1, \"duration\": 2, \"key\": \"t_haste\"}";
        bench.Run(modify);
        Assert.Equal(before + 1, stats.Get("move.speed"), 9);
        bench.Run(1.5);
        // Refreshed with half a second left on the first timer: the first timer must not end it.
        bench.Run(modify);
        bench.Run(1.0);
        Assert.Equal(before + 1, stats.Get("move.speed"), 9);
        bench.Run(1.2);
        Assert.Equal(before, stats.Get("move.speed"), 9);
    }

    [Fact]
    public void BanksGrowUpToTheirCapDealThemselvesOutAndRupture()
    {
        var bench = CombatBench.Create();
        // Each scalding application banks magnitude times duration; the bank is capped at four of the strongest.
        var scalding = bench.Apply(bench.Victim, "scalding", magnitude: 2, duration: 4)!;
        Assert.Equal(8, scalding.Value("bank"), 9);
        for (var i = 0; i < 5; i++)
        {
            bench.Apply(bench.Victim, "scalding", magnitude: 2, duration: 4);
        }
        Assert.Equal(32, scalding.Value("bank"), 9);
        Assert.Equal(8, scalding.BankPeak, 9);

        // A rupture spends half of it at once, as the status's Thread, tagged rupture and dot.
        var ctx = bench.Run("""{"type": "rupture", "status": "scalding", "fraction": 0.5}""");
        var burst = bench.Events.OfType<StatusRuptured>().Single();
        Assert.Equal(16, burst.Amount, 9);
        Assert.Equal(16, ctx.Vars["ruptured"], 9);
        var hit = bench.HitsOn(bench.Victim).Single();
        Assert.True(hit.Tags.Has("rupture") && hit.Tags.Has("dot") && hit.ByThread.ContainsKey("ember"));
        Assert.Equal(16, scalding.Value("bank"), 9);

        // The rest is dealt out over the time left.
        bench.Run(4.2);
        var ticks = bench.HitsOn(bench.Victim).Skip(1).Sum(d => d.Total);
        Assert.Equal(16, ticks, 6);
        Assert.Null(bench.Instance(bench.Victim, "scalding"));
    }

    [Fact]
    public void PopsReadWhatWasLeftAndThePoppersPower()
    {
        var bench = CombatBench.Create();
        bench.Apply(bench.Victim, "hemorrhage", magnitude: 4, duration: 5);
        // Applied again at once: twenty left to deal, half of it bursts.
        bench.Apply(bench.Victim, "hemorrhage", magnitude: 4, duration: 5);
        var pop = bench.HitsOn(bench.Victim).Single(d => d.Tags.Has("pop"));
        Assert.Equal(10, pop.Total, 6);
        // The applier's pop power adds to the fraction.
        bench.Modify(bench.Attacker, "status.hemorrhage.pop", 0.5);
        bench.Apply(bench.Victim, "hemorrhage", magnitude: 4, duration: 5);
        Assert.Equal(20, bench.HitsOn(bench.Victim).Last(d => d.Tags.Has("pop")).Total, 6);
    }

    [Fact]
    public void ConsumableStacksAreSpentByTheBearersMatchingHits()
    {
        var bench = CombatBench.Create();
        bench.Apply(bench.Attacker, "honed", stacks: 2, duration: 8, source: bench.Attacker);
        var honed = bench.Instance(bench.Attacker, "honed")!;
        Assert.Equal(2, honed.Stacks);
        // A melee hit lands harder and spends a stack; a hit that is not melee spends none.
        Assert.Equal(15, bench.Hit(10, tags: "melee").Total, 9);
        Assert.Equal(1, honed.Stacks);
        Assert.Equal(10, bench.Hit(10).Total, 9);
        Assert.Equal(1, honed.Stacks);
        bench.Hit(10, tags: "melee");
        Assert.True(honed.Removed);
        Assert.Null(bench.Instance(bench.Attacker, "honed"));
        Assert.Equal(2, bench.Events.OfType<StatusConsumed>().Count(c => c.Status == "honed"));
    }

    [Fact]
    public void NextHitRidersRunAgainstTheStruckTargetOnce()
    {
        var bench = CombatBench.Create();
        bench.Apply(bench.Attacker, "searing_edge", duration: 6, source: bench.Attacker);
        // Damage over time is not the bearer's next hit.
        bench.Hit(5, tags: "dot");
        Assert.NotNull(bench.Instance(bench.Attacker, "searing_edge"));
        Assert.Null(bench.Instance(bench.Victim, "burning"));
        bench.Hit(5);
        Assert.NotNull(bench.Instance(bench.Victim, "burning"));
        Assert.Null(bench.Instance(bench.Attacker, "searing_edge"));
        // The rider's own effects run one deeper than the hit that carried them.
        Assert.Equal(1, bench.Events.OfType<StatusApplied>().Last(a => a.Status == "burning").Depth);
    }

    [Fact]
    public void IndependentStackClocksLetEachStackFallOffAlone()
    {
        var bench = CombatBench.Create();
        bench.Apply(bench.Victim, "poison", magnitude: 0.1, duration: 3);
        bench.Run(1.5);
        bench.Apply(bench.Victim, "poison", magnitude: 0.1, duration: 3);
        var poison = bench.Instance(bench.Victim, "poison")!;
        Assert.Equal(2, poison.Stacks);
        bench.Run(1.6);
        Assert.Equal(1, poison.Stacks);
        Assert.False(poison.Removed);
        bench.Run(1.6);
        Assert.True(poison.Removed);
    }

    [Fact]
    public void EveryStatusRollsItsOwnChanceOnEveryHit()
    {
        var bench = CombatBench.Create();
        bench.Modify(bench.Attacker, "status.weakened.chance", 1);
        bench.Hit(10);
        Assert.NotNull(bench.Instance(bench.Victim, "weakened"));

        // A Thread's own ailment adds the chance to its roll; other Threads roll it on its own.
        bench.Modify(bench.Attacker, "status.bleeding.chance", 1);
        bench.Hit(10, thread: "ember");
        Assert.NotNull(bench.Instance(bench.Victim, "bleeding"));
        Assert.Single(bench.Events.OfType<StatusApplied>(), a => a.Status == "bleeding");

        // Damage over time leaves no statuses.
        var other = CombatBench.Create();
        other.Modify(other.Attacker, "status.weakened.chance", 1);
        other.Hit(10, tags: "dot");
        Assert.Null(other.Instance(other.Victim, "weakened"));
    }

    [Fact]
    public void ActiveStatusesAreVictimTags()
    {
        var bench = CombatBench.Create();
        bench.Modify(bench.Attacker, "damage", 1, ModifierForm.More, "vs.burning");
        Assert.Equal(10, bench.Hit(10).Total, 9);
        bench.Apply(bench.Victim, "burning", magnitude: 0.01, duration: 5);
        var hit = bench.Hit(10);
        Assert.True(hit.Tags.Has("vs.burning"));
        Assert.Equal(20, hit.Total, 9);
    }

    [Fact]
    public void FixedDurationsIgnoreEffectDuration()
    {
        var bench = CombatBench.Create();
        bench.Modify(bench.Attacker, "effect.duration", 1);
        bench.Run("""{"type": "apply_status", "status": "weakened", "duration": 2}""");
        Assert.Equal(4, bench.Instance(bench.Victim, "weakened")!.Remaining, 9);
        bench.Run("""{"type": "apply_status", "status": "vulnerable", "fixed_duration": 2}""");
        Assert.Equal(2, bench.Instance(bench.Victim, "vulnerable")!.Remaining, 9);
    }

    [Fact]
    public void BuffsReachTheSourceItsMinionsAndAlliesNearby()
    {
        var bench = CombatBench.Create();
        var ally = bench.Spawn("hob", new Vector3(9, 11, 0), "red");
        var farAlly = bench.Spawn("hob", new Vector3(40, 40, 0), "red");
        var minion = bench.Spawn("hob", new Vector3(30, 30, 0), "red");
        bench.Sim.World.Add(minion, new Minion { Owner = bench.Attacker });

        bench.Run("""{"type": "apply_status", "status": "hasted", "to": "allies", "radius": 5}""");
        Assert.NotNull(bench.Instance(bench.Attacker, "hasted"));
        Assert.NotNull(bench.Instance(ally, "hasted"));
        Assert.Null(bench.Instance(farAlly, "hasted"));
        Assert.Null(bench.Instance(bench.Victim, "hasted"));

        bench.Run("""{"type": "apply_status", "status": "fortified", "to": "minions"}""");
        Assert.NotNull(bench.Instance(minion, "fortified"));
        Assert.Null(bench.Instance(bench.Attacker, "fortified"));

        bench.Run("""{"type": "apply_status", "status": "regrowth", "to": "self"}""");
        Assert.NotNull(bench.Instance(bench.Attacker, "regrowth"));
        Assert.Null(bench.Instance(bench.Victim, "regrowth"));
    }

    [Fact]
    public void CleansesTakeHarmfulStatusesNewestFirst()
    {
        var bench = CombatBench.Create();
        bench.Apply(bench.Victim, "weakened", duration: 9);
        bench.Apply(bench.Victim, "hasted", duration: 9);
        bench.Apply(bench.Victim, "vulnerable", duration: 9);
        bench.Run("""{"type": "remove_status", "count": 1}""");
        Assert.Null(bench.Instance(bench.Victim, "vulnerable"));
        Assert.NotNull(bench.Instance(bench.Victim, "weakened"));
        bench.Run("""{"type": "remove_status"}""");
        Assert.Null(bench.Instance(bench.Victim, "weakened"));
        Assert.NotNull(bench.Instance(bench.Victim, "hasted"));
        bench.Run("""{"type": "remove_status", "kind": "beneficial"}""");
        Assert.Null(bench.Instance(bench.Victim, "hasted"));
    }

    [Fact]
    public void BaselinesGiveAMagnitudeWithoutAHit()
    {
        var bench = CombatBench.Create();
        bench.Run("""{"type": "apply_status", "status": "poison"}""");
        // A level one applier: 1 + 0.3.
        Assert.Equal(1.3, bench.Instance(bench.Victim, "poison")!.Magnitude, 9);
    }

    [Fact]
    public void TheFirstApplierKeepsTheCreditWhenTheStatusAsks()
    {
        var bench = CombatBench.Create();
        var second = bench.Spawn("hob", new Vector3(10, 12, 0), "red");
        bench.Apply(bench.Victim, "taunted", duration: 4);
        bench.Apply(bench.Victim, "taunted", duration: 4, source: second);
        Assert.Equal(bench.Attacker, bench.Instance(bench.Victim, "taunted")!.Source);
        bench.Apply(bench.Victim, "vulnerable", duration: 4);
        bench.Apply(bench.Victim, "vulnerable", duration: 4, source: second);
        Assert.Equal(second, bench.Instance(bench.Victim, "vulnerable")!.Source);
    }

    [Fact]
    public void StatusPoolsAbsorbAndStatusesBoundToThemEndWithThem()
    {
        var bench = CombatBench.Create();
        var life = bench.VictimStats.Resource("life");
        bench.Apply(bench.Victim, "warded_shell", magnitude: 30, duration: 6, source: bench.Victim);
        bench.Apply(bench.Victim, "shell_glow", duration: 6, source: bench.Victim);
        bench.Hit(20);
        Assert.Equal(life, bench.VictimStats.Resource("life"), 9);
        Assert.Equal(10, bench.Instance(bench.Victim, "warded_shell")!.Value("pool"), 9);
        bench.Hit(20);
        Assert.Equal(life - 10, bench.VictimStats.Resource("life"), 9);
        Assert.Null(bench.Instance(bench.Victim, "warded_shell"));
        Assert.Null(bench.Instance(bench.Victim, "shell_glow"));
    }

    [Fact]
    public void AnIronWardBanksWhatLandsThenLetsItLoose()
    {
        var bench = CombatBench.Create();
        bench.Apply(bench.Victim, "iron_ward", duration: 2, source: bench.Victim);
        // Less damage taken while half of what lands is banked.
        Assert.Equal(14, bench.Hit(20).Total, 9);
        Assert.Equal(7, bench.Instance(bench.Victim, "iron_ward")!.Value("bank"), 9);
        var attackerLife = bench.AttackerStats.Resource("life");
        bench.Run(2.2);
        var burst = bench.HitsOn(bench.Attacker).Single(d => d.Tags.Has("iron_ward"));
        Assert.Equal(7, burst.Total, 9);
        Assert.True(bench.AttackerStats.Resource("life") < attackerLife);
    }

    [Fact]
    public void RidersStampedByTheApplierTickWithTheStatus()
    {
        var bench = CombatBench.Create();
        bench.Run("""{"type": "apply_status", "status": "burning", "magnitude": 2, "duration": 2, "on_status_tick": [ {"type": "damage", "amount": 1, "threads": ["void"], "tags": ["dot"], "can_crit": false} ]}""");
        Assert.NotNull(bench.Instance(bench.Victim, "burning")!.Riders);
        bench.Run(1.1);
        var hits = bench.HitsOn(bench.Victim);
        var ember = hits.Count(d => d.ByThread.ContainsKey("ember"));
        var riders = hits.Count(d => d.ByThread.ContainsKey("void"));
        Assert.True(ember >= 2);
        Assert.Equal(ember, riders);
    }

    [Fact]
    public void ImpalesDischargeIntoTheBlowsThatLandOnTheBearer()
    {
        var bench = CombatBench.Create();
        bench.Apply(bench.Victim, "impaled", magnitude: 5, duration: 4, stacks: 2);
        var impaled = bench.Instance(bench.Victim, "impaled")!;
        Assert.Equal(20, impaled.Value("bank"), 9);
        const string blow = """{"type": "damage", "amount": 10, "threads": ["stone"], "can_crit": false}""";
        bench.Run(blow);
        // A quarter of the bank discharges into the blow and a stack is spent.
        Assert.Equal(5, bench.Events.OfType<StatusRuptured>().Single().Amount, 9);
        Assert.Equal(1, impaled.Stacks);
        bench.Run(blow);
        Assert.Equal(3.75, bench.Events.OfType<StatusRuptured>().Last().Amount, 9);
        Assert.True(impaled.Removed);
    }

    [Fact]
    public void DoomFallsTheMomentItWouldKill()
    {
        var bench = CombatBench.Create();
        var life = bench.VictimStats.Resource("life");
        bench.Apply(bench.Victim, "doom", magnitude: life / 12, duration: 6);
        Assert.False(bench.Sim.World.Has<Dead>(bench.Victim));
        bench.Apply(bench.Victim, "doom", magnitude: life / 12, duration: 6);
        Assert.True(bench.Sim.World.Has<Dead>(bench.Victim));
        Assert.Single(bench.Events.OfType<StatusRuptured>(), r => r.Status == "doom");
    }

    [Fact]
    public void StatusLeechReturnsLifeFromItsTicks()
    {
        var bench = CombatBench.Create();
        var attacker = bench.AttackerStats;
        attacker.Attributes.Add(new Modifier { Attribute = "life.regen", Form = ModifierForm.More, Value = -1, Source = "test" });
        attacker.SetResource("life", attacker.ResourceMax("life") - 20);
        bench.Modify(bench.Attacker, "status.bleeding.leech", 0.5);
        var before = attacker.Resource("life");
        bench.Apply(bench.Victim, "bleeding", magnitude: 4, duration: 3);
        bench.Run(1.1);
        var ticks = bench.HitsOn(bench.Victim).Sum(d => d.Total);
        Assert.True(ticks > 0);
        Assert.Equal(before + ticks * 0.5, attacker.Resource("life"), 6);
    }

    [Fact]
    public void ConsumeSpreadAndMoveStatusesBetweenBodies()
    {
        var bench = CombatBench.Create();
        bench.Apply(bench.Victim, "bleeding", magnitude: 2, duration: 5, stacks: 3);
        var ctx = bench.Run("""{"type": "consume_status", "status": "bleeding", "count": 2, "as": "spent"}""");
        Assert.Equal(2, ctx.Vars["spent"], 9);
        Assert.Equal(2 * 2 * 5, ctx.Vars["spent_damage"], 6);
        Assert.Equal(1, bench.Instance(bench.Victim, "bleeding")!.Stacks);

        // A neighbour of the victim, on its side, catches its afflictions at half strength with the time left.
        var neighbour = bench.Spawn("hob", new Vector3(14, 11, 0), "blue");
        bench.Apply(bench.Victim, "burning", magnitude: 6, duration: 4);
        ctx = bench.Run("""{"type": "spread_status", "status": "burning", "radius": 4, "strength": 0.5}""");
        Assert.Equal(1, ctx.Vars["spread"], 9);
        var caught = bench.Instance(neighbour, "burning")!;
        Assert.Equal(3, caught.Magnitude, 9);
        Assert.Equal(bench.Instance(bench.Victim, "burning")!.Remaining, caught.Remaining, 6);
        Assert.Equal(bench.Attacker, caught.Source);

        // A transfusion pours the source's affliction onto the target, who now suffers it from the source.
        bench.Apply(bench.Attacker, "weakened", duration: 5, source: bench.Victim);
        ctx = bench.Run("""{"type": "move_status", "status": "weakened", "from": "self", "to": "target"}""");
        Assert.Equal(1, ctx.Vars["moved"], 9);
        Assert.Null(bench.Instance(bench.Attacker, "weakened"));
        Assert.Equal(bench.Attacker, bench.Instance(bench.Victim, "weakened")!.Source);
    }

    [Fact]
    public void TheAppliersStatusStacksRaiseTheCap()
    {
        var bench = CombatBench.Create();
        bench.Apply(bench.Victim, "bleeding", magnitude: 0.01, duration: 5, stacks: 9);
        Assert.Equal(5, bench.Instance(bench.Victim, "bleeding")!.Stacks);
        bench.Modify(bench.Attacker, "status.stacks", 2, scope: "status.bleeding");
        bench.Apply(bench.Victim, "bleeding", magnitude: 0.01, duration: 5, stacks: 9);
        Assert.Equal(7, bench.Instance(bench.Victim, "bleeding")!.Stacks);
    }

    [Fact]
    public void TicksReadHowLongTheStatusHasRun()
    {
        var bench = CombatBench.Create();
        bench.Apply(bench.Victim, "kindling_brand", magnitude: 4, duration: 5);
        bench.Run(5.2);
        var ticks = bench.HitsOn(bench.Victim).Select(d => d.Total).ToList();
        Assert.True(ticks.Count >= 8);
        // The brand burns hotter as it holds: each tick at least the one before.
        for (var i = 1; i < ticks.Count; i++)
        {
            Assert.True(ticks[i] >= ticks[i - 1] - 1e-9);
        }
        Assert.True(ticks[^1] > ticks[0] * 1.5);
    }

    [Fact]
    public void MuddledStacksMakeCastsFizzle()
    {
        var arena = CastingArena.Create();
        var slot = arena.Slot("t_free");
        var statuses = arena.Sim.Service<StatusService>();
        statuses.Apply(arena.Sim, arena.Caster, "muddled", arena.Enemy, new StatusApplication { Stacks = 2, Duration = 3 });
        Assert.Equal(0.5, arena.Stats.Get("cast.fizzle_chance"), 9);
        // Four doses leave no chance of the cast holding.
        statuses.Apply(arena.Sim, arena.Caster, "muddled", arena.Enemy, new StatusApplication { Stacks = 2, Duration = 3 });
        Assert.Equal(1, arena.Stats.Get("cast.fizzle_chance"), 9);
        Assert.False(arena.Begin(slot, out var reason));
        Assert.Equal("fizzled", reason);

        statuses.Remove(arena.Sim, arena.Caster, "muddled", TagSet.Empty);
        Assert.Equal(0, arena.Stats.Get("cast.fizzle_chance"), 9);
        // Once the reeling from the fizzle passes, the cast holds.
        arena.RunSeconds(0.5);
        Assert.True(arena.Begin(slot, out reason), reason);
    }

    [Fact]
    public void SappingHealsTheSapperNotTheSapped()
    {
        var bench = CombatBench.Create();
        // Both bodies are hurt, so a heal shows on whichever it lands.
        bench.AttackerStats.SetResource("life", bench.AttackerStats.ResourceMax("life") * 0.5);
        bench.VictimStats.SetResource("life", bench.VictimStats.ResourceMax("life") * 0.5);
        bench.Apply(bench.Victim, "sapped", magnitude: 4, duration: 3);
        bench.Run(2.2);
        var heals = bench.Events.OfType<Healed>().ToList();
        Assert.NotEmpty(heals);
        Assert.All(heals, h => Assert.Equal(bench.Attacker, h.Target));
        Assert.NotEmpty(bench.HitsOn(bench.Victim));
    }
}
