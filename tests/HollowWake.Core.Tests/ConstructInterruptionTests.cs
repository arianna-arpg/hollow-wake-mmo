using System.Numerics;
using HollowWake.Core.Components;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;
using Xunit;

namespace HollowWake.Core.Tests;

public class ConstructInterruptionTests
{
    [Theory]
    [InlineData("stunned")]
    [InlineData("frozen")]
    [InlineData("forbid:spell")]
    public void AnInterruptedTotemDoesNotReleaseItsBolt(string flag)
    {
        var arena = MinionArena.Create(9);
        arena.Foe("tangle", arena.OwnerAt.Position + new Vector3(8, 0, 0));
        arena.Cast(arena.Learn("plant_ember_totem"), point: arena.OwnerAt.Position + new Vector3(2, 0, 0));
        var totem = Assert.Single(arena.Minions("ember_totems"));
        var book = arena.Sim.World.Get<WeaveBook>(totem)!;
        for (var tick = 0; tick < arena.Sim.Clock.TicksPerSecond && book.Casting is null; tick++)
            arena.Sim.Tick();
        Assert.NotNull(book.Casting);
        arena.Events.Clear();
        arena.Sim.World.Get<Stats>(totem)!.Flags.Add(flag);
        arena.Sim.Tick();
        Assert.Null(book.Casting);
        Assert.Contains(arena.Events.OfType<WeaveInterrupted>(), e => e.Caster == totem);
        arena.Sim.RunSeconds(1);
        Assert.DoesNotContain(arena.Events.OfType<WeaveCast>(), e => e.Caster == totem);
    }

}
