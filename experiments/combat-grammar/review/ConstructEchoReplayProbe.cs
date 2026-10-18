using System.Numerics;
using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;
using Xunit;

namespace HollowWake.Core.Tests;

// Review reproduction, not a passing gate. Copy into the core test project to run it.
public class ConstructEchoReplayProbe
{
    [Fact]
    public void AnEchoRetainsPaidCastsThatArriveBetweenItsThinks()
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
                runtime.TryBegin(arena.Sim, arena.Owner, spit, foe, arena.At(foe), Vector2.Zero, out _);
            }
            arena.Sim.Tick();
        }
        // Let the final owner cast finish and give the echo time to drain any pending replay.
        arena.Sim.RunSeconds(0.5);
        var ownerCasts = arena.Events.OfType<WeaveCast>().Count(e => e.Caster == arena.Owner && e.Weave == spit.Key);
        var echoCasts = arena.Events.OfType<WeaveCast>().Count(e => e.Caster == echo && e.Weave == spit.Key);
        Assert.True(ownerCasts > 20, $"Expected multiple paid casts between thinks, got {ownerCasts}.");
        Assert.Equal(ownerCasts, echoCasts);
    }
}
