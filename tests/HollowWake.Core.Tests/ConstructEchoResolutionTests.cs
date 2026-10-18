using System.Numerics;
using System.Text.Json.Nodes;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Sim;
using HollowWake.Core.StatusEffects;
using HollowWake.Core.Tags;
using HollowWake.Core.Weaves;
using Xunit;

namespace HollowWake.Core.Tests;

public class ConstructEchoResolutionTests
{
    private static Entity Echo(CastingArena arena)
    {
        var minions = new MinionArena { Sim = arena.Sim, Owner = arena.Caster };
        minions.Cast(minions.Learn("shade_echo"));
        var echo = Assert.Single(minions.Minions("echoes"));
        arena.Sim.World.Get<Behavior>(echo)!.NextThinkAt = arena.Now + 10;
        arena.Events.Clear();
        return echo;
    }

    private static void Drain(CastingArena arena, Entity echo)
    {
        arena.Sim.World.Get<Behavior>(echo)!.NextThinkAt = arena.Now;
        arena.Sim.Tick();
    }

    [Theory]
    [InlineData("t_channel", 1.1, "t_release", "held")]
    [InlineData("t_charge", 0.7, "t_charge", "fill")]
    [InlineData("t_over", 1.1, "t_over", "stage")]
    public void AReplayKeepsTheResolvedPayloadAndHeldValues(string form, double held, string marker, string variable)
    {
        var arena = CastingArena.Create();
        var echo = Echo(arena);
        var slot = arena.Slot(form);
        arena.Begin(slot);
        arena.RunSeconds(held);
        arena.Runtime.Release(arena.Sim, arena.Caster, arena.SlotIndex(slot));
        var expected = arena.Of<CustomEvent>().ToArray();
        Assert.True(Assert.Single(expected, e => e.Name == marker).Data[variable] > 0);
        arena.Events.Clear();
        Drain(arena, echo);
        var actual = arena.Of<CustomEvent>().ToArray();
        Assert.Equal(expected.Select(e => e.Name), actual.Select(e => e.Name));
        Assert.Equal(expected.Select(e => e.Data.OrderBy(p => p.Key).ToArray()), actual.Select(e => e.Data.OrderBy(p => p.Key).ToArray()));
        Assert.All(actual, e => Assert.Equal(arena.Caster, e.Source));
    }

    [Fact]
    public void AGuardReplayKeepsTheBashAndItsAbsorbedDamage()
    {
        var arena = CastingArena.Create();
        var echo = Echo(arena);
        var slot = arena.Slot("t_guard");
        arena.Begin(slot);
        arena.RunSeconds(0.6);
        arena.Hit(arena.Enemy, arena.Caster, 15);
        arena.Runtime.Release(arena.Sim, arena.Caster, arena.SlotIndex(slot));
        var bash = Assert.Single(arena.Custom("t_bash"));
        Assert.True(bash.Data["absorbed"] > 0);
        arena.Events.Clear();
        Drain(arena, echo);
        Assert.Single(arena.Custom("t_raise"));
        Assert.Equal(bash.Data["absorbed"], Assert.Single(arena.Custom("t_bash")).Data["absorbed"]);
    }

    [Theory]
    [InlineData("stunned", 0.1)]
    [InlineData("frozen", 0.1)]
    [InlineData("stunned", 4)]
    [InlineData("frozen", 4)]
    public void IncapacitationCancelsPendingUsesAndRejectsNewOnesEvenBetweenThinks(string status, double duration)
    {
        var arena = CastingArena.Create();
        var echo = Echo(arena);
        var slot = arena.Slot("t_marker");
        arena.Begin(slot);
        arena.Sim.Service<StatusService>().Apply(arena.Sim, echo, status, arena.Enemy, new StatusApplication { Duration = duration, FixedDuration = true });
        arena.Begin(slot);
        arena.RunSeconds(duration + 0.2);
        Assert.False(arena.Sim.World.Get<Stats>(echo)!.HasFlag(status));
        Drain(arena, echo);
        Assert.DoesNotContain(arena.Of<WeaveCast>(), e => e.Caster == echo);
        // A fresh use after recovery still works.
        arena.Begin(slot);
        Drain(arena, echo);
        Assert.Single(arena.Of<WeaveCast>(), e => e.Caster == echo);
    }

    [Fact]
    public void AReplayTriggeredOwnerUseWaitsUntilTheNextThink()
    {
        var arena = CastingArena.Create();
        var echo = Echo(arena);
        var slot = arena.Slot("t_marker");
        arena.Begin(slot);
        var triggered = false;
        arena.Sim.Events.Subscribe<WeaveCast>(e =>
        {
            if (e.Caster != echo || triggered) return;
            triggered = true;
            arena.Begin(slot);
        });
        Drain(arena, echo);
        Assert.True(triggered);
        Assert.Single(arena.Of<WeaveCast>(), e => e.Caster == echo);
        Drain(arena, echo);
        Assert.Equal(2, arena.Of<WeaveCast>().Count(e => e.Caster == echo));
    }

    [Fact]
    public void AReplacementCannotReceiveTheOldEchoPendingQueue()
    {
        var arena = CastingArena.Create();
        var previous = Echo(arena);
        arena.Begin(arena.Slot("t_marker"));
        var oldBody = arena.Sim.World.Get<Minion>(previous)!;
        Assert.Equal(1, oldBody.PendingReplayCount);
        var replacement = Echo(arena);
        Assert.NotEqual(previous, replacement);
        Assert.Equal(0, oldBody.PendingReplayCount);
        Drain(arena, replacement);
        Assert.Empty(arena.Of<WeaveCast>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeathReleasesTheRetainedPayloadsImmediately(bool ownerDies)
    {
        var arena = CastingArena.Create();
        var echo = Echo(arena);
        arena.Begin(arena.Slot("t_marker"));
        var body = arena.Sim.World.Get<Minion>(echo)!;
        Assert.Equal(1, body.PendingReplayCount);
        arena.Sim.Service<DeathService>().Kill(arena.Sim, ownerDies ? arena.Caster : echo, Entity.None);
        Assert.Equal(0, body.PendingReplayCount);
    }

    [Theory]
    [InlineData("marrowhooks")]
    [InlineData("t_consume")]
    public void AReplayCannotSpendAnotherBody(string preset)
    {
        var arena = MinionArena.Create();
        arena.Cast(arena.Learn("t_shades"));
        arena.Cast(arena.Learn("shade_echo"));
        var echo = Assert.Single(arena.Minions("echoes"));
        foreach (var offset in new[] { 1, 2 })
        {
            var corpse = arena.Foe("hob", arena.OwnerAt.Position + new Vector3(offset, 0, 0));
            arena.Sim.Service<DeathService>().Kill(arena.Sim, corpse, arena.Owner);
        }
        arena.Events.Clear();
        arena.Cast(arena.Learn(preset));
        arena.Sim.RunSeconds(0.5);
        Assert.DoesNotContain(arena.Events.OfType<WeaveCast>(), e => e.Caster == echo && e.Weave == preset);
        if (preset == "marrowhooks") Assert.Single(arena.Events.OfType<CorpseConsumed>());
        else
        {
            Assert.Single(arena.Events.OfType<CustomEvent>(), e => e.Name == "t_consumed");
            Assert.Single(arena.Minions("t_shades"));
        }
    }

    [Fact]
    public void EchoExclusionsComeFromContent()
    {
        foreach (var exclude in new[] { true, false })
        {
            var content = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out _);
            var rules = content.Get<RulesDef>("minions")!;
            var values = (JsonObject)rules.Values!.DeepClone();
            values["echo_excluded_tags"] = exclude ? new JsonArray("movement") : new JsonArray();
            content.With(rules with { Values = values });
            content.With(new FactionDef { Id = "red", Relations = { ["blue"] = Stance.Hostile } });
            content.With(new FactionDef { Id = "blue", Relations = { ["red"] = Stance.Hostile } });
            content.With(new FormDef
            {
                Id = "echo_rule_probe", CastMode = CastMode.Instant, Targeting = Targeting.Self,
                MinThreads = 0, Tags = TagSet.Of("movement"),
                Effects = { CastingTestContent.Spec("""{"type":"event","name":"echo_rule_probe"}""") },
            });
            var arena = CastingArena.Create(registry: content);
            var echo = Echo(arena);
            arena.Begin(arena.Slot(new WeaveSpec { Form = "echo_rule_probe" }));
            Drain(arena, echo);
            Assert.Equal(exclude ? 0 : 1, arena.Of<WeaveCast>().Count(e => e.Caster == echo));
        }
    }

    [Fact]
    public void BurstReplaysUseDistinctRepeatableDamageRolls()
    {
        double[] Run()
        {
            var content = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out _);
            content.With(new FactionDef { Id = "red", Relations = { ["blue"] = Stance.Hostile } });
            content.With(new FactionDef { Id = "blue", Relations = { ["red"] = Stance.Hostile } });
            content.With(new FormDef
            {
                Id = "echo_roll", CastMode = CastMode.Instant, Targeting = Targeting.Enemy, MinThreads = 0,
                Attributes = { ["range"] = 20 },
                Effects = { CastingTestContent.Spec("""{"type":"damage","amount":"lerp(10,100,roll)","threads":["mind"],"can_crit":false}""") },
            });
            var arena = CastingArena.Create(registry: content);
            var echo = Echo(arena);
            var slot = arena.Slot(new WeaveSpec { Form = "echo_roll" });
            for (var i = 0; i < 3; i++) arena.Begin(slot);
            arena.Events.Clear();
            Drain(arena, echo);
            var damage = arena.Of<DamageDealt>().ToArray();
            Assert.Equal(3, damage.Length);
            Assert.All(damage, e => Assert.True(e.Total > 0));
            return damage.Select(e => e.Roll!.Value).ToArray();
        }
        var rolls = Run();
        Assert.Equal(3, rolls.Distinct().Count());
        Assert.Equal(rolls, Run());
    }
}
