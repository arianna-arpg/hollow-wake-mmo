using System.Numerics;
using HollowWake.Core.Attributes;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;
using Xunit;

namespace HollowWake.Core.Tests;

public class ConstructEchoQueueTests
{
    [Fact]
    public void AnEchoRetainsPaidCastsBetweenItsThinks()
    {
        var arena = MinionArena.Create(9);
        var foe = arena.Foe("tangle", arena.OwnerAt.Position + new Vector3(9, 0, 0));
        arena.Sim.World.Get<Stats>(foe)!.Attributes.Add(new Modifier
        {
            Attribute = "damage.taken", Form = ModifierForm.More, Value = -1, Source = "review",
        });
        arena.Cast(arena.Learn("shade_echo"), point: arena.OwnerAt.Position);
        var echo = Assert.Single(arena.Minions("echoes"));
        arena.Sim.RunSeconds(0.5);
        arena.OwnerStats.Attributes.Add(new Modifier
        {
            Attribute = "cast.speed", Form = ModifierForm.More, Value = 39, Source = "review",
        });
        var book = arena.Sim.World.Get<WeaveBook>(arena.Owner)!;
        var spit = book.Find("cinder_spit")!;
        var runtime = arena.Sim.Service<WeaveRuntime>();
        arena.Events.Clear();
        for (var tick = 0; tick < 2 * arena.Sim.Clock.TicksPerSecond; tick++)
        {
            if (book.Casting is null)
            {
                arena.OwnerStats.FillResources();
                Assert.True(runtime.TryBegin(arena.Sim, arena.Owner, spit, foe, arena.At(foe), Vector2.Zero, out var reason), reason);
            }
            arena.Sim.Tick();
        }
        arena.Sim.RunSeconds(0.5);
        var paid = arena.Events.OfType<WeaveCast>().Count(e => e.Caster == arena.Owner && e.Weave == spit.Key);
        var replays = arena.Events.OfType<WeaveCast>().Count(e => e.Caster == echo && e.Weave == spit.Key);
        Assert.Equal(40, paid);
        Assert.Equal(paid, replays);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InstantUsesBeforeTheFirstThinkRetainTheirOrderAndResolvedWeave(bool removeSlot)
    {
        var arena = MinionArena.Create(9);
        arena.Cast(arena.Learn("shade_echo"), point: arena.OwnerAt.Position);
        var echo = Assert.Single(arena.Minions("echoes"));
        var slot = arena.Learn("snare_burst");
        var originalKey = slot.Key;
        var expected = Burst(arena, slot);
        var book = arena.Sim.World.Get<WeaveBook>(arena.Owner)!;
        if (removeSlot) book.Slots.Remove(slot);
        else
        {
            slot.Spec = new WeaveSpec { Preset = "cinder_spit" };
            slot.Compiled = null;
        }
        book.Changed();
        arena.Sim.RunSeconds(0.5);
        var replays = arena.Events.OfType<WeaveCast>().Where(e => e.Caster == echo).ToArray();
        Assert.Equal(expected, replays.Select(e => e.Point));
        Assert.All(replays, e => Assert.Equal(originalKey, e.Weave));
        arena.Sim.RunSeconds(0.5);
        Assert.Equal(3, arena.Events.OfType<WeaveCast>().Count(e => e.Caster == echo));
    }

    [Fact]
    public void ANewEchoDoesNotReplayUsesFromBeforeItWasBorn()
    {
        var arena = MinionArena.Create(9);
        Burst(arena, arena.Learn("snare_burst"));
        arena.Cast(arena.Learn("shade_echo"), point: arena.OwnerAt.Position);
        var echo = Assert.Single(arena.Minions("echoes"));
        arena.Sim.RunSeconds(0.5);
        Assert.DoesNotContain(arena.Events.OfType<WeaveCast>(), e => e.Caster == echo);
    }

    [Fact]
    public void EachLivingEchoGetsEveryUseAndAReplacementGetsNoOldQueue()
    {
        var arena = MinionArena.Create(9);
        var summon = arena.Learn("shade_echo");
        arena.Cast(summon, point: arena.OwnerAt.Position);
        var first = Assert.Single(arena.Minions("echoes"));
        // Two independently capped groups, so neither deployment replaces the other.
        arena.Sim.World.Get<Minion>(first)!.Group = "earlier-echoes";
        arena.Cast(summon, point: arena.OwnerAt.Position);
        var second = Assert.Single(arena.Minions("echoes"));
        var expected = Burst(arena, arena.Learn("snare_burst"));
        arena.Sim.RunSeconds(0.5);
        foreach (var echo in new[] { first, second })
        {
            Assert.Equal(expected, arena.Events.OfType<WeaveCast>().Where(e => e.Caster == echo).Select(e => e.Point));
        }
        arena.Cast(summon, point: arena.OwnerAt.Position);
        var replacement = Assert.Single(arena.Minions("echoes"));
        Assert.NotEqual(second, replacement);
        arena.Sim.RunSeconds(0.5);
        Assert.DoesNotContain(arena.Events.OfType<WeaveCast>(), e => e.Caster == replacement);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PendingUsesDoNotSurviveTheEchoOrItsOwner(bool ownerDies)
    {
        var arena = MinionArena.Create(9);
        arena.Cast(arena.Learn("shade_echo"), point: arena.OwnerAt.Position);
        var echo = Assert.Single(arena.Minions("echoes"));
        Burst(arena, arena.Learn("snare_burst"));
        if (ownerDies) arena.Sim.Service<DeathService>().Kill(arena.Sim, arena.Owner, Entity.None);
        else arena.Service.Dismiss(echo);
        arena.Sim.RunSeconds(0.5);
        Assert.DoesNotContain(arena.Events.OfType<WeaveCast>(), e => e.Caster == echo);
    }

    private static Vector3[] Burst(MinionArena arena, WeaveSlot slot)
    {
        arena.OwnerStats.Attributes.Add(new Modifier
        {
            Attribute = "mana.max", Form = ModifierForm.Flat, Value = 100, Source = "queue-test",
        });
        arena.OwnerStats.FillResources();
        var points = new[] { new Vector3(21, 20, 0), new Vector3(22, 20, 0), new Vector3(23, 20, 0) };
        foreach (var point in points)
        {
            Assert.True(arena.Sim.Service<WeaveRuntime>().TryBegin(arena.Sim, arena.Owner, slot, Entity.None,
                point, Vector2.UnitX, out var reason), reason);
        }
        return points;
    }
}
