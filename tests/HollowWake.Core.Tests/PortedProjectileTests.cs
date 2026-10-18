using System.Numerics;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Maths;
using HollowWake.Core.Minions;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>
/// The projectiles and bows family of the port (content/core/combat/weaves-projectiles.yaml on the missile and
/// shot shells of forms-shells.yaml): every preset fits and can be loosed, the munitions run dry and reload
/// through their faces, blades are caught on their return and hurled back for each one caught, an orb sheds
/// bolts as it drifts, lightning chains, a corpse missile spends its corpse, and the proving duel of the
/// fletcher against the evoker tells the same story twice.
/// </summary>
public class PortedProjectileTests
{
    private static readonly string[] Shells = { "missile", "missile_quick", "missile_corpse", "shot", "shot_quick" };

    private static WeaveCompiler Compiler() => new(CoreContent.Registry, HollowWake.Core.Effects.EffectRegistry.CreateDefault());

    private static Vector2 Facing(CombatBench bench, Entity from, Vector3 to)
    {
        var at = bench.Sim.World.Get<Transform>(from)!.Position;
        return Vector2.Normalize(new Vector2(to.X - at.X, to.Y - at.Y));
    }

    private static WeaveSlot Slot(CombatBench bench, string preset) => bench.Sim.World.Get<WeaveBook>(bench.Attacker)!.Add(new WeaveSpec { Preset = preset });

    private static bool Begin(CombatBench bench, WeaveSlot slot, Entity target, Vector3 at, out string reason) =>
        bench.Sim.Service<WeaveRuntime>().TryBegin(bench.Sim, bench.Attacker, slot, target, at, Facing(bench, bench.Attacker, at), out reason);

    private static Entity Corpse(CombatBench bench, Vector3 at)
    {
        var body = bench.Spawn("hob", at, "blue");
        bench.Hit(1000, "stone", target: body);
        Assert.True(Corpses.Is(bench.Sim.World, body));
        return body;
    }

    private static int Flights(CombatBench bench) => bench.Events.OfType<EntitySpawned>().Count(e => e.Pattern == "projectile");

    [Fact]
    public void EveryProjectilePresetFitsAndCanBeLoosed()
    {
        var presets = CoreContent.Registry.All<WeaveDef>().Where(w => Shells.Contains(w.Value.Form)).Select(w => w.Key).OrderBy(id => id, StringComparer.Ordinal).ToList();
        Assert.True(presets.Count >= 100, $"only {presets.Count} projectile presets");
        var compiler = Compiler();
        var bench = CombatBench.Create(attacker: "proving_evoker", victim: "proving_bulwark", distance: 6);
        var victimAt = bench.Sim.World.Get<Transform>(bench.Victim)!.Position;
        var failed = new List<string>();
        foreach (var id in presets)
        {
            var weave = compiler.Compile(new WeaveSpec { Preset = id });
            if (!weave.Fits)
            {
                failed.Add($"{id}: does not fit");
                continue;
            }
            var stats = bench.AttackerStats;
            stats.SetResource("mana", stats.ResourceMax("mana"));
            stats.SetResource("stamina", stats.ResourceMax("stamina"));
            var slot = Slot(bench, id);
            var at = victimAt;
            if (weave.Form.Id == "missile_corpse")
            {
                at = bench.Sim.World.Get<Transform>(Corpse(bench, new Vector3(14, 12, 0)))!.Position;
            }
            var flights = Flights(bench);
            if (!Begin(bench, slot, bench.Victim, at, out var reason))
            {
                failed.Add($"{id}: {reason}");
            }
            bench.Run(1.2);
            if (Flights(bench) == flights)
            {
                failed.Add($"{id}: loosed nothing");
            }
        }
        Assert.Empty(failed);
    }

    [Theory]
    [InlineData("gyreblade", "returning")]
    [InlineData("heartchaser", "homing")]
    [InlineData("chain_lightning", "chain")]
    [InlineData("scattergun", "convert")]
    [InlineData("bolt_repeater", "uses")]
    [InlineData("gloriole", "trajectory")]
    public void TheCompilerSeesWhatEachPresetDoes(string preset, string mechanism)
    {
        var weave = Compiler().Compile(new WeaveSpec { Preset = preset });
        Assert.True(weave.Fits);
        Assert.True(weave.Mechanisms.Has(mechanism), $"{preset} lacks {mechanism}: {weave.Mechanisms}");
    }

    [Fact]
    public void EmptyProjectileShellsAreNotRandomCodexDrops()
    {
        // Random codices select every positive-weight form; a bare shell has nothing to teach.
        var shells = new[] { "missile", "missile_quick", "missile_corpse", "shot", "shot_quick", "burst", "reload", "reload_stream" };
        Assert.All(shells, id =>
        {
            var form = CoreContent.Registry.Get<FormDef>(id)!;
            Assert.Empty(form.Effects);
            Assert.True(form.DropWeight == 0, $"{form.Id} is eligible for an empty codex drop");
        });
        Assert.True(Compiler().Compile(new WeaveSpec { Preset = "firebolt" }).Fits);
    }

    [Fact]
    public void MunitionsRunDryAndReloadThroughTheirFace()
    {
        var bench = CombatBench.Create(attacker: "proving_fletcher", victim: "proving_bulwark", distance: 5);
        var runtime = bench.Sim.Service<WeaveRuntime>();
        var at = bench.Sim.World.Get<Transform>(bench.Victim)!.Position;

        // The scattergun holds three shells and never finds more on its own; empty, its face is the reload.
        var gun = Slot(bench, "scattergun");
        Assert.Equal(3, runtime.Uses(bench.Sim, bench.Attacker, gun, out var max));
        Assert.Equal(3, max);
        for (var i = 0; i < 3; i++)
        {
            Assert.True(Begin(bench, gun, bench.Victim, at, out var reason), reason);
            bench.Run(1);
        }
        Assert.Equal(0, runtime.Uses(bench.Sim, bench.Attacker, gun, out _));
        bench.Run(20);
        Assert.Equal(0, runtime.Uses(bench.Sim, bench.Attacker, gun, out _));
        Assert.Equal("reload_shells", runtime.FaceOf(bench.Sim, bench.Attacker, gun)!.Key);
        // Pressing the empty gun rams shells: a held reload racking one a beat.
        Assert.True(Begin(bench, gun, bench.Victim, at, out var why), why);
        bench.Run(2.5);
        Assert.True(runtime.Uses(bench.Sim, bench.Attacker, gun, out _) >= 2, "the reload racked nothing");
        Assert.Contains(bench.Events.OfType<WeaveCast>(), c => c.Weave == "reload_shells");
        Assert.Equal(gun.Key, runtime.FaceOf(bench.Sim, bench.Attacker, gun)!.Key);

        // The arquebus holds one ball; its reload is a bar.
        var arquebus = Slot(bench, "arquebus");
        Assert.True(Begin(bench, arquebus, bench.Victim, at, out var r1), r1);
        bench.Run(1);
        Assert.Equal(0, runtime.Uses(bench.Sim, bench.Attacker, arquebus, out _));
        Assert.Equal("reload_powder", runtime.FaceOf(bench.Sim, bench.Attacker, arquebus)!.Key);
        Assert.True(Begin(bench, arquebus, bench.Victim, at, out var r2), r2);
        bench.Run(2.5);
        Assert.Equal(1, runtime.Uses(bench.Sim, bench.Attacker, arquebus, out _));

        // The repeater's drum refills itself once emptied.
        var repeater = Slot(bench, "bolt_repeater");
        for (var i = 0; i < 8; i++)
        {
            Assert.True(Begin(bench, repeater, bench.Victim, at, out var r3), r3);
            bench.Run(0.5);
        }
        Assert.Equal(0, runtime.Uses(bench.Sim, bench.Attacker, repeater, out _));
        bench.Run(1);
        Assert.Equal(0, runtime.Uses(bench.Sim, bench.Attacker, repeater, out _));
        bench.Run(3);
        Assert.Equal(8, runtime.Uses(bench.Sim, bench.Attacker, repeater, out _));
    }

    [Fact]
    public void AGyrebladeIsCaughtOnItsReturnAndHurledBackForEachOneCaught()
    {
        var bench = CombatBench.Create(attacker: "proving_fletcher", victim: "proving_bulwark", distance: 4);
        var at = bench.Sim.World.Get<Transform>(bench.Victim)!.Position;
        var blade = Slot(bench, "gyreblade");
        for (var i = 0; i < 3; i++)
        {
            Assert.True(Begin(bench, blade, bench.Victim, at, out var reason), reason);
            bench.Run(2.5);
        }
        Assert.Equal(3, bench.Events.OfType<ProjectileReturned>().Count());
        Assert.Equal(3, bench.AttackerStats.Gauge("charge.gyre"), 6);
        Assert.NotEmpty(bench.HitsOn(bench.Victim));

        // The hurl spends every caught blade and throws one more for each.
        var before = Flights(bench);
        var hurl = Slot(bench, "gyre_hurl");
        Assert.True(Begin(bench, hurl, bench.Victim, at, out var why), why);
        bench.Run(0.5);
        Assert.Equal(4, Flights(bench) - before);
        Assert.Equal(0, bench.AttackerStats.Gauge("charge.gyre"), 6);
    }

    [Fact]
    public void AFrozenOrbShedsFrostboltsAsItDrifts()
    {
        var bench = CombatBench.Create(attacker: "proving_evoker", victim: "proving_bulwark", distance: 6);
        var at = bench.Sim.World.Get<Transform>(bench.Victim)!.Position;
        var orb = Slot(bench, "frozen_orb");
        Assert.True(Begin(bench, orb, bench.Victim, at, out var reason), reason);
        bench.Run(3);
        Assert.True(Flights(bench) >= 8, $"the orb shed {Flights(bench) - 1} bolts");
        Assert.Contains(bench.HitsOn(bench.Victim), h => h.ByThread.ContainsKey("tide"));
    }

    [Fact]
    public void ChainLightningLeapsToTheNextBody()
    {
        var bench = CombatBench.Create(attacker: "proving_evoker", victim: "proving_bulwark", distance: 5);
        var bystander = bench.Spawn("hob", new Vector3(17, 12, 0), "blue");
        var at = bench.Sim.World.Get<Transform>(bench.Victim)!.Position;
        var bolt = Slot(bench, "chain_lightning");
        Assert.True(Begin(bench, bolt, bench.Victim, at, out var reason), reason);
        bench.Run(2);
        Assert.NotEmpty(bench.HitsOn(bench.Victim));
        Assert.NotEmpty(bench.HitsOn(bystander));
    }

    [Fact]
    public void ACorpseMissileNeedsACorpseAndSpendsIt()
    {
        var bench = CombatBench.Create(attacker: "proving_evoker", victim: "proving_bulwark", distance: 6);
        var cinders = Slot(bench, "volatile_cinders");
        var point = new Vector3(14, 12, 0);
        Assert.False(Begin(bench, cinders, Entity.None, point, out _));
        var corpse = Corpse(bench, point);
        var before = Flights(bench);
        Assert.True(Begin(bench, cinders, Entity.None, point, out var reason), reason);
        bench.Run(1.5);
        Assert.True(Flights(bench) > before, "no cinders rose");
        Assert.False(bench.Sim.World.IsAlive(corpse) && Corpses.Is(bench.Sim.World, corpse));
    }

    [Fact]
    public void APullOnAFlightReelsTheCatchToTheCaster()
    {
        var bench = CombatBench.Create(attacker: "proving_evoker", victim: "proving_bulwark", distance: 7);
        var at = bench.Sim.World.Get<Transform>(bench.Victim)!.Position;
        var lash = Slot(bench, "vine_lash");
        Assert.True(Begin(bench, lash, bench.Victim, at, out var reason), reason);
        bench.Run(2.5);
        var now = bench.Sim.World.Get<Transform>(bench.Victim)!.Position;
        Assert.True(now.X < at.X - 3, $"the catch stayed at {now}");
        Assert.NotNull(bench.Instance(bench.Victim, "rooted"));
    }

    [Fact]
    public void ACinderwispsImpIsTheCastersOwn()
    {
        var bench = CombatBench.Create(attacker: "proving_evoker", victim: "proving_bulwark", distance: 6);
        var at = bench.Sim.World.Get<Transform>(bench.Victim)!.Position;
        var wisp = Slot(bench, "cinderwisp");
        Assert.True(Begin(bench, wisp, bench.Victim, at, out var reason), reason);
        bench.Run(2.5);
        var imp = Assert.Single(bench.Events.OfType<EntitySpawned>().Where(e => e.Pattern == "cinder_imp")).Entity;
        Assert.Equal(bench.Attacker, bench.Sim.World.Get<Minion>(imp)!.Owner);
    }

    [Fact]
    public void AFireballsBurstSparesTheBodyItStruckAndCatchesItsNeighbour()
    {
        var bench = CombatBench.Create(attacker: "proving_evoker", victim: "proving_bulwark", distance: 6);
        var neighbour = bench.Spawn("hob", new Vector3(17.5f, 10, 0), "blue");
        var at = bench.Sim.World.Get<Transform>(bench.Victim)!.Position;
        var ball = Slot(bench, "fireball");
        Assert.True(Begin(bench, ball, bench.Victim, at, out var reason), reason);
        bench.Run(1.6);
        Assert.Single(bench.HitsOn(bench.Victim).Where(h => !h.Tags.Has("dot")));
        Assert.NotEmpty(bench.HitsOn(neighbour));
    }

    [Fact]
    public void AnIceSpearsShardsSpareTheBodyItStruckAndSprayBehindIt()
    {
        var bench = CombatBench.Create(attacker: "proving_evoker", victim: "proving_bulwark", distance: 6);
        var behind = bench.Spawn("hob", new Vector3(18.5f, 10.3f, 0), "blue");
        var at = bench.Sim.World.Get<Transform>(bench.Victim)!.Position;
        var spear = Slot(bench, "ice_spear");
        Assert.True(Begin(bench, spear, bench.Victim, at, out var reason), reason);
        bench.Run(1.6);
        Assert.Single(bench.HitsOn(bench.Victim).Where(h => !h.Tags.Has("dot")));
        Assert.NotEmpty(bench.HitsOn(behind));
    }

    [Fact]
    public void TheFletcherAndTheEvokerTellTheSameStoryTwice()
    {
        List<string> Duel()
        {
            var sim = ZoneBuilder.CreateArena(CoreContent.Registry, Rng.Hash64("projectiles"));
            var factory = sim.Service<PatternFactory>();
            factory.Spawn(sim, "proving_fletcher", new Vector3(10, 10, 0), new SpawnOptions { Faction = "red" });
            factory.Spawn(sim, "proving_evoker", new Vector3(14, 10, 0), new SpawnOptions { Faction = "blue" });
            var log = new List<string>();
            sim.Events.Subscribe<WeaveCast>(w => log.Add($"{sim.Clock.Tick} cast {w.Weave}"));
            sim.Events.Subscribe<StatusApplied>(s => log.Add($"{sim.Clock.Tick} status {s.Status} x{s.Stacks} on {s.Target.Key}"));
            sim.Events.Subscribe<CombatNote>(n => log.Add($"{sim.Clock.Tick} {n.Describe(e => e.Key.ToString())}"));
            sim.Events.Subscribe<DamageDealt>(d => log.Add($"{sim.Clock.Tick} {d.Total:R}"));
            sim.Events.Subscribe<ProjectileReturned>(r => log.Add($"{sim.Clock.Tick} returned to {r.Owner.Key}"));
            sim.Events.Subscribe<EntityDied>(d => log.Add($"{sim.Clock.Tick} died {d.Entity.Key}"));
            sim.RunSeconds(60);
            return log;
        }
        var first = Duel();
        Assert.Equal(first, Duel());
        Assert.Contains(first, l => l.Contains("cast gyreblade"));
        Assert.Contains(first, l => l.Contains("returned to"));
        Assert.Contains(first, l => l.Contains("gained 1 gyre"));
        Assert.Contains(first, l => l.Contains("cast chain_lightning"));
        Assert.Contains(first, l => l.Contains("status shocked"));
        Assert.Contains(first, l => l.Contains("cast frozen_orb"));
        Assert.Contains(first, l => l.Contains("status chilled"));
        Assert.Contains(first, l => l.Contains("status frozen"));
        Assert.Contains(first, l => l.Contains("spent 2 gyre"));
        Assert.Contains(first, l => l.Contains("cast gyre_hurl"));
        Assert.Contains(first, l => l.Contains("cast web_shot"));
        Assert.Contains(first, l => l.Contains(" died "));
    }
}
