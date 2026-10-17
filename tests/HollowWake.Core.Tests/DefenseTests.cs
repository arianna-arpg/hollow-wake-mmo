using HollowWake.Core.Attributes;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Sim;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>The defender's ladder and the attacker's build: skips, added damage, leech, pools and poise.</summary>
public class DefenseTests
{
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
}
