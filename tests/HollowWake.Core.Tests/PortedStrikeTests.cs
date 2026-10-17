using System.Numerics;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Maths;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>
/// The strikes and melee family of the port (content/core/combat/weaves-strikes.yaml on the shells of
/// forms-shells.yaml): presets that compile and fit, the behaviors they carry through the preset seam, the
/// riders the hand pass authored, and the proving duel of the pitfighter against the mower that tells the same
/// story twice.
/// </summary>
public class PortedStrikeTests
{
    private static WeaveCompiler Compiler() => new(CoreContent.Registry, HollowWake.Core.Effects.EffectRegistry.CreateDefault());

    [Theory]
    [InlineData("one_two", "chain")]
    [InlineData("haymaker", "spend")]
    [InlineData("reckoning", "spend")]
    [InlineData("reprisal", "gates")]
    [InlineData("zealots_cadence", "uses")]
    [InlineData("harvest_stroke", "follow_up")]
    [InlineData("thousand_cuts", "self_stack")]
    [InlineData("zanshin_cut", "cycle")]
    public void PresetsCarryTheirCastingBehaviors(string preset, string behavior)
    {
        var weave = Compiler().Compile(new WeaveSpec { Preset = preset });
        Assert.True(weave.Fits);
        var casting = weave.Casting;
        switch (behavior)
        {
            case "chain": Assert.NotNull(casting.Chain); Assert.Equal(2, casting.Chain!.Steps.Count); break;
            case "spend": Assert.NotNull(casting.Spend); Assert.Equal("fury", casting.Spend!.Charge); Assert.True(casting.Spend.All); break;
            case "gates": Assert.Single(casting.Gates); break;
            case "uses": Assert.NotNull(casting.Uses); Assert.Equal(3, casting.Uses!.Max); break;
            case "follow_up": Assert.Single(casting.FollowUps); Assert.Equal("follow_sweep", casting.FollowUps[0].Weave); break;
            case "self_stack": Assert.NotNull(casting.SelfStack); Assert.Equal(8, casting.SelfStack!.Max); break;
            case "cycle": Assert.NotNull(casting.Cycle); Assert.Equal(3, casting.Cycle!.Count); break;
        }
    }

    [Fact]
    public void ThemeTagsRideThePresetSoGlyphGatesSeeThem()
    {
        var compiler = Compiler();
        var flurry = compiler.Compile(new WeaveSpec { Preset = "lancing_flurry" });
        Assert.True(flurry.Tags.Has("javelin"));
        Assert.True(flurry.Tags.Has("preset.lancing_flurry"));
        Assert.True(flurry.Tags.Has("attack") && flurry.Tags.Has("melee"));
        var tuning = compiler.Compile(new WeaveSpec { Preset = "tuning_strike" });
        Assert.True(tuning.Tags.Has("duration"));
    }

    [Fact]
    public void AnExecutionEatsVulnerableStacksAndHitsHarderForEach()
    {
        var bench = CombatBench.Create(attacker: "proving_pitfighter", victim: "proving_bulwark", distance: 1.5f);
        var sim = bench.Sim;
        var book = sim.World.Get<WeaveBook>(bench.Attacker)!;
        var slot = book.Add(new WeaveSpec { Preset = "execution" });
        var runtime = sim.Service<WeaveRuntime>();
        var at = sim.World.Get<Transform>(bench.Victim)!.Position;
        var facing = Vector2.Normalize(new Vector2(at.X - 10, at.Y - 10));
        double Swing()
        {
            var before = bench.HitsOn(bench.Victim).Count;
            Assert.True(runtime.TryBegin(sim, bench.Attacker, slot, bench.Victim, at, facing, out var reason), reason);
            bench.Run(1.5);
            var hits = bench.HitsOn(bench.Victim);
            Assert.True(hits.Count > before, "the execution landed nothing");
            return hits[^1].Total;
        }
        // Without wounds the blow is the plain blow.
        var plain = Swing();
        for (var i = 0; i < 5; i++)
        {
            bench.Apply(bench.Victim, "vulnerable", duration: 6);
        }
        Assert.Equal(5, bench.Instance(bench.Victim, "vulnerable")!.Stacks);
        bench.Run(3.5);
        var fed = Swing();
        Assert.Null(bench.Instance(bench.Victim, "vulnerable"));
        // Five stacks eaten: the blow lands more than twice as hard, within the dice's spread.
        Assert.True(fed > plain * 1.6, $"plain {plain}, fed {fed}");
    }

    [Fact]
    public void ATuningStrikeLeavesExactlyOneAttunement()
    {
        var bench = CombatBench.Create(attacker: "proving_pitfighter", victim: "proving_bulwark", distance: 1.5f);
        var sim = bench.Sim;
        var book = sim.World.Get<WeaveBook>(bench.Attacker)!;
        var slot = book.Add(new WeaveSpec { Preset = "tuning_strike" });
        var runtime = sim.Service<WeaveRuntime>();
        var at = sim.World.Get<Transform>(bench.Victim)!.Position;
        Assert.True(runtime.TryBegin(sim, bench.Attacker, slot, bench.Victim, at, Vector2.Normalize(new Vector2(at.X - 10, at.Y - 10)), out var reason), reason);
        bench.Run(1.5);
        var attunements = new[] { "attuned_ember", "attuned_tide", "attuned_storm" }.Count(s => bench.Instance(bench.Victim, s) is not null);
        Assert.Equal(1, attunements);
    }

    [Fact]
    public void ThePitfighterAndTheMowerTellTheSameStoryTwice()
    {
        List<string> Duel()
        {
            var sim = ZoneBuilder.CreateArena(CoreContent.Registry, Rng.Hash64("strikes"));
            var factory = sim.Service<PatternFactory>();
            factory.Spawn(sim, "proving_pitfighter", new Vector3(10, 10, 0), new SpawnOptions { Faction = "red" });
            factory.Spawn(sim, "proving_mower", new Vector3(14, 10, 0), new SpawnOptions { Faction = "blue" });
            var log = new List<string>();
            sim.Events.Subscribe<WeaveCast>(w => log.Add($"{sim.Clock.Tick} cast {w.Weave}"));
            sim.Events.Subscribe<StatusApplied>(s => log.Add($"{sim.Clock.Tick} status {s.Status} x{s.Stacks} on {s.Target.Key}"));
            sim.Events.Subscribe<CombatNote>(n => log.Add($"{sim.Clock.Tick} {n.Describe(e => e.Key.ToString())}"));
            sim.Events.Subscribe<DamageDealt>(d => log.Add($"{sim.Clock.Tick} {d.Total:R}"));
            sim.Events.Subscribe<EntityDied>(d => log.Add($"{sim.Clock.Tick} died {d.Entity.Key}"));
            sim.RunSeconds(60);
            return log;
        }
        var first = Duel();
        Assert.Equal(first, Duel());
        Assert.Contains(first, l => l.Contains("cast one_two"));
        Assert.Contains(first, l => l.Contains("cast cross_jab"));
        Assert.Contains(first, l => l.Contains("status frenzy"));
        Assert.Contains(first, l => l.Contains("fury"));
        Assert.Contains(first, l => l.Contains("status ensnared"));
        Assert.Contains(first, l => l.Contains("cast scythe_sweep"));
        Assert.Contains(first, l => l.Contains(" died "));
    }
}
