using System.Numerics;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Movement;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;
using HollowWake.Core.World;
using Xunit;
using Xunit.Abstractions;

namespace HollowWake.Core.Tests;

public class ElementalDisplacementTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("bash")]
    [InlineData("winters_sweep")]
    public void ARealShoveDoesNotThrowAnEmberwispBeyondItsHomeLeash(string attack)
    {
        var a = DeliveryArena.Create();
        var target = a.Body(11.5f, 10, pattern: "emberwisp");
        var stats = a.Sim.World.Get<Stats>(target)!;
        stats.Attributes.SetBase("life.max", 10000);
        stats.FillResources();
        var at = a.Sim.World.Get<Transform>(target)!;
        var start = at.Position;
        var spec = attack == "bash" ? new WeaveSpec { Form = "bash", Threads = { "stone" } } : new WeaveSpec { Preset = attack };
        var slot = a.Sim.World.Get<WeaveBook>(a.Caster)!.Add(spec);
        Assert.True(a.Sim.Service<WeaveRuntime>().TryBegin(a.Sim, a.Caster, slot, target, start, Vector2.UnitX, out var reason), reason);
        a.Tick(100);
        Assert.Contains(a.Events.OfType<DamageDealt>(), h => h.Source == a.Caster && h.Target == target && h.Total > 0);
        var distance = Vector3.Distance(start, at.Position);
        var leash = CoreContent.Registry.Require<BehaviorDef>("aggressive_ranged").LeashRange!.Evaluate(stats);
        output.WriteLine($"{attack}: Emberwisp moved {distance:F5} m; home leash {leash:F1} m.");
        Assert.InRange(distance, 0.01, leash);
    }

    [Fact]
    public void ALightElementalCannotTunnelThroughAPlacedWall()
    {
        var a = DeliveryArena.Create();
        var target = a.Body(30, 10, pattern: "emberwisp");
        var at = a.Sim.World.Get<Transform>(target)!;
        var wall = a.Sim.World.Create();
        for (var y = 0; y < 32; y++) a.Sim.Service<BlockedCells>().Block(new CellCoord(32, y), wall);
        a.Run("""{"type":"move","kind":"knockback","distance":4}""", at.Position, target: target);
        a.Tick(45);
        output.WriteLine($"Emberwisp ended at x={at.Position.X:F5}; wall at x=32.");
        Assert.InRange(at.Position.X, 30, 31.999);
    }
}
