using HollowWake.Core.Attributes;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Effects;
using HollowWake.Core.Sim;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>Charges: gains up to their maximum, decay after their duration, drains that refuse gains, spending.</summary>
public class ChargeTests
{
    [Fact]
    public void GainsStopAtTheMaximumAndMirrorIntoTheGauge()
    {
        var bench = CombatBench.Create();
        var charges = bench.Sim.Service<ChargeService>();
        Assert.Equal(3, charges.Max(bench.Sim, bench.Attacker, "fury"));
        Assert.Equal(3, charges.Gain(bench.Sim, bench.Attacker, "fury", 5));
        Assert.Equal(3, charges.Count(bench.Sim, bench.Attacker, "fury"));
        Assert.Equal(3, bench.AttackerStats.Gauge("charge.fury"));
        Assert.Equal(0, charges.Gain(bench.Sim, bench.Attacker, "fury", 1));
        var gained = bench.Events.OfType<ChargeGained>().Single();
        Assert.Equal(3, gained.Amount);

        // The maximum is an attribute any modifier raises.
        bench.Modify(bench.Attacker, "charge.fury.max", 2);
        Assert.Equal(2, charges.Gain(bench.Sim, bench.Attacker, "fury", 5));
        Assert.Equal(5, bench.AttackerStats.Gauge("charge.fury"));

        // Kinds without a declared maximum hold any amount.
        Assert.Equal(40, charges.Gain(bench.Sim, bench.Attacker, "t_unbounded", 40));
    }

    [Fact]
    public void ChargesDecayOnceTheirDurationRunsOut()
    {
        var bench = CombatBench.Create();
        var charges = bench.Sim.Service<ChargeService>();
        // Fury lasts eight seconds after the last gain, then loses one a second.
        charges.Gain(bench.Sim, bench.Attacker, "fury", 3);
        bench.Run(7.8);
        Assert.Equal(3, charges.Count(bench.Sim, bench.Attacker, "fury"));
        bench.Run(1.3);
        Assert.Equal(2, charges.Count(bench.Sim, bench.Attacker, "fury"));
        // A gain restarts the duration.
        charges.Gain(bench.Sim, bench.Attacker, "fury", 1);
        bench.Run(7.5);
        Assert.Equal(3, charges.Count(bench.Sim, bench.Attacker, "fury"));
        bench.Run(4);
        Assert.Equal(0, charges.Count(bench.Sim, bench.Attacker, "fury"));
        Assert.Equal(0, bench.AttackerStats.Gauge("charge.fury"));
        Assert.Contains(bench.Events.OfType<ChargeLost>(), l => l.Reason == "decayed");

        // Focus decays all at once when its ten seconds run out.
        charges.Gain(bench.Sim, bench.Attacker, "focus", 3);
        bench.Run(9.8);
        Assert.Equal(3, charges.Count(bench.Sim, bench.Attacker, "focus"));
        bench.Run(0.4);
        Assert.Equal(0, charges.Count(bench.Sim, bench.Attacker, "focus"));
    }

    [Fact]
    public void DrainsBurnChargesDownAndRefuseGainsMeanwhile()
    {
        var bench = CombatBench.Create();
        var charges = bench.Sim.Service<ChargeService>();
        charges.Gain(bench.Sim, bench.Attacker, "resolve", 3);
        charges.Drain(bench.Sim, bench.Attacker, "resolve", 2);
        Assert.Equal(0, charges.Gain(bench.Sim, bench.Attacker, "resolve", 1));
        bench.Run(0.6);
        Assert.Equal(2, charges.Count(bench.Sim, bench.Attacker, "resolve"));
        bench.Run(1.0);
        Assert.Equal(0, charges.Count(bench.Sim, bench.Attacker, "resolve"));
        Assert.Contains(bench.Events.OfType<ChargeLost>(), l => l.Reason == "drained");
        // With the charges gone the drain is over and gains count again.
        Assert.Equal(1, charges.Gain(bench.Sim, bench.Attacker, "resolve", 1));
    }

    [Fact]
    public void SpendingTakesExactlyTheAmountOrNothing()
    {
        var bench = CombatBench.Create();
        var charges = bench.Sim.Service<ChargeService>();
        charges.Gain(bench.Sim, bench.Attacker, "fury", 3);
        Assert.True(charges.Spend(bench.Sim, bench.Attacker, "fury", 2));
        Assert.Equal(1, charges.Count(bench.Sim, bench.Attacker, "fury"));
        Assert.False(charges.Spend(bench.Sim, bench.Attacker, "fury", 2));
        Assert.Equal(1, charges.Count(bench.Sim, bench.Attacker, "fury"));
        Assert.Equal(1, charges.SpendAll(bench.Sim, bench.Attacker, "fury"));
        Assert.Equal(0, bench.AttackerStats.Gauge("charge.fury"));
        Assert.Equal(2, bench.Events.OfType<ChargeSpent>().Count());
    }

    [Fact]
    public void TheChargeEffectGainsSpendsAndDrains()
    {
        var bench = CombatBench.Create();
        var charges = bench.Sim.Service<ChargeService>();
        bench.Run("""{"type": "charge", "id": "fury", "gain": 2}""");
        Assert.Equal(2, charges.Count(bench.Sim, bench.Attacker, "fury"));
        var ctx = bench.Run("""{"type": "charge", "id": "fury", "spend": 1}""");
        Assert.Equal(1, ctx.Vars[CastVars.ChargesSpent]);
        ctx = bench.Run("""{"type": "charge", "id": "fury", "spend": 5}""");
        Assert.Equal(0, ctx.Vars[CastVars.ChargesSpent]);
        ctx = bench.Run("""{"type": "charge", "id": "fury", "all": true}""");
        Assert.Equal(1, ctx.Vars[CastVars.ChargesSpent]);

        // On the target instead of the source, and a drain through the effect.
        bench.Run("""{"type": "charge", "id": "resolve", "gain": 3, "self": false}""");
        Assert.Equal(3, charges.Count(bench.Sim, bench.Victim, "resolve"));
        bench.Run("""{"type": "charge", "id": "resolve", "drain": 3, "self": false}""");
        bench.Run(1.1);
        Assert.Equal(0, charges.Count(bench.Sim, bench.Victim, "resolve"));
    }

    [Fact]
    public void ModifiersScaleByCharges()
    {
        var bench = CombatBench.Create();
        bench.AttackerStats.Attributes.Add(new Modifier { Attribute = "damage", Form = ModifierForm.Increased, Value = 0.1, Gauge = "charge.fury", Source = "test" });
        Assert.Equal(10, bench.Hit(10).Total, 9);
        bench.Sim.Service<ChargeService>().Gain(bench.Sim, bench.Attacker, "fury", 3);
        Assert.Equal(13, bench.Hit(10).Total, 9);
    }
}
