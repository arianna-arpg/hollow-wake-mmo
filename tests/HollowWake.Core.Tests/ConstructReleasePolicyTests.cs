using System.Numerics;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Expressions;
using HollowWake.Core.Sim;
using Xunit;

namespace HollowWake.Core.Tests;

public class ConstructReleasePolicyTests
{
    private static (CastingArena Arena, Entity Echo, WeaveSlot Slot) Setup(bool selector = false)
    {
        var content = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out _);
        content.With(new FactionDef { Id = "red", Relations = { ["blue"] = Stance.Hostile } });
        content.With(new FactionDef { Id = "blue", Relations = { ["red"] = Stance.Hostile } });
        content.With(CastForecastContent.Registry.Get<FormDef>("forecast_instant_target")! with
        {
            Id = "echo_release_probe", Cooldown = Expr.Constant(0),
            ReleaseCheck = new() { Policy = selector ? "delivery" : "body" },
            Effects = new() { CastForecastContent.Spec(selector ? """
                {"type":"area","shape":"sector","radius":4,"angle":60,"center":"source","filter":"enemy",
                 "effects":[{"type":"damage","amount":10,"threads":["stone"],"can_crit":false,
                  "after_damage":[{"type":"event","name":"echo_payload","data":{"amount":"last_damage"}}]}]}
                """ : """{"type":"event","name":"echo_payload"}""") },
        });
        var arena = CastingArena.Create(registry: content, distance: 3);
        var minions = new MinionArena { Sim = arena.Sim, Owner = arena.Caster };
        arena.Sim.Events.SubscribeAll(minions.Events.Add);
        minions.Cast(minions.Learn("shade_echo"), point: minions.OwnerAt.Position);
        var echo = Assert.Single(minions.Minions("echoes"));
        // An ordinary delay between the owner's resolution and the echo's next scheduled think.
        arena.Sim.World.Get<Behavior>(echo)!.NextThinkAt = arena.Now + 0.3;
        var slot = arena.Slot(new WeaveSpec { Form = "echo_release_probe" });
        arena.Begin(slot);
        Assert.Single(arena.Custom("echo_payload"));
        arena.Events.Clear();
        return (arena, echo, slot);
    }

    [Fact]
    public void AQueuedEchoCannotDeliverThroughANewWallOrPayAgain()
    {
        var (arena, echo, _) = Setup();
        var mana = arena.Stats.Resource("mana");
        CastForecastContent.Wall(arena);
        arena.RunSeconds(0.5);
        Assert.Empty(arena.Custom("echo_payload"));
        Assert.Contains(arena.Of<WeaveMissed>(), e => e.Reason == "line_of_sight");
        Assert.DoesNotContain(arena.Of<WeaveCast>(), e => e.Caster == echo);
        Assert.Equal(mana, arena.Stats.Resource("mana"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReplayRangeIsMeasuredFromTheEcho(bool ownerMovesAway)
    {
        var (arena, echo, _) = Setup();
        var mana = arena.Stats.Resource("mana");
        CastForecastContent.Move(arena, ownerMovesAway ? arena.Caster : echo, new Vector3(40, 10, 0));
        arena.RunSeconds(0.5);
        Assert.Equal(ownerMovesAway ? 1 : 0, arena.Custom("echo_payload").Count);
        if (ownerMovesAway) Assert.Equal(arena.Caster, Assert.Single(arena.Custom("echo_payload")).Source);
        else Assert.Contains(arena.Of<WeaveMissed>(), e => e.Reason == "out_of_reach");
        Assert.Equal(mana, arena.Stats.Resource("mana"));
    }

    [Fact]
    public void AQueuedSelectorKeepsItsAimAndRidersFollowTheActualVictim()
    {
        var (arena, _, _) = Setup(selector: true);
        CastForecastContent.Move(arena, arena.Enemy, new Vector3(10, 13, 0));
        var replacement = arena.Spawn("hob", new Vector3(12, 10, 0));
        arena.RunSeconds(0.5);
        var damage = Assert.Single(arena.Of<DamageDealt>());
        var rider = Assert.Single(arena.Custom("echo_payload"));
        Assert.Equal(replacement, damage.Target);
        Assert.Equal(replacement, rider.Target);
        Assert.Equal(arena.Caster, damage.Source);
        Assert.Equal(arena.Caster, rider.Source);
        Assert.True(rider.Data["amount"] > 0);
        Assert.DoesNotContain(arena.Of<DamageDealt>(), e => e.Target == arena.Enemy);
    }
}
