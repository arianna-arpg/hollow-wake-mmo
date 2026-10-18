using System.Globalization;
using System.Numerics;
using HollowWake.Core.Components;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.World;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>Movement fixtures from pre-intent main a810b0a, without platform-libm facing or wandering.</summary>
public class AiTrajectoryTests
{
    [Theory]
    [InlineData("detour")]
    [InlineData("forest")]
    [InlineData("zigzag-moving")]
    public void ChasesWalkThePreIntentTrajectoryTickForTick(string scenario)
    {
        var actual = Trace(scenario);
        var expected = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "AiTrajectories", scenario + ".txt"));
        for (var i = 0; i < Math.Max(expected.Length, actual.Length); i++)
        {
            var before = i < expected.Length ? expected[i] : "(end)";
            var after = i < actual.Length ? actual[i] : "(end)";
            Assert.True(before == after, $"{scenario} first differs at tick {i}:\nbase a810b0a: {before}\ncurrent:       {after}");
        }
    }

    private static string[] Trace(string scenario)
    {
        var sim = ZoneBuilder.CreateArena(CoreContent.Registry, 3);
        var factory = sim.Service<PatternFactory>();
        var (hunter, at, preyAt, seconds) = scenario switch
        {
            "detour" => ("wolf", new Vector3(10.5f, 10.5f, 0), new Vector3(17.5f, 10.5f, 0), 8),
            "forest" => ("wolf", new Vector3(5.5f, 5.5f, 0), new Vector3(30.5f, 24.5f, 0), 12),
            "zigzag-moving" => ("hob", new Vector3(5.5f, 10.5f, 0), new Vector3(30.5f, 10.5f, 0), 15),
            _ => throw new ArgumentException("Unknown trajectory scenario", nameof(scenario)),
        };
        var h = factory.Spawn(sim, hunter, at, new SpawnOptions { Faction = "red" });
        var p = factory.Spawn(sim, "boar", preyAt, new SpawnOptions { Faction = "blue" });
        sim.World.Remove<Behavior>(p);
        // Start in chase and keep the target alive, so neither actor enters a random wander after a kill.
        var mind = sim.World.Get<Behavior>(h)!;
        mind.Target = p;
        mind.State = "chase";
        mind.Memory["taunted_until"] = seconds + 1;
        var preyStats = sim.World.Get<Stats>(p)!;
        preyStats.Attributes.SetBase("life.max", 1000000);
        preyStats.SetResource("life", preyStats.ResourceMax("life"));
        switch (scenario)
        {
            case "detour":
                for (var y = 2; y <= 18; y++) Block(sim, 14, y);
                break;
            case "forest":
                for (var x = 8; x <= 28; x += 3)
                for (var y = 6; y <= 24; y += 2)
                {
                    Block(sim, x, y);
                    Block(sim, x, y + 1);
                }
                break;
            case "zigzag-moving":
                for (var y = 0; y <= 16; y++) Block(sim, 10, y);
                for (var y = 5; y <= 22; y++) Block(sim, 16, y);
                for (var y = 0; y <= 16; y++) Block(sim, 22, y);
                break;
        }
        var transform = sim.World.Get<Transform>(h)!;
        var motion = sim.World.Get<Motion>(h)!;
        var preyMotion = sim.World.Get<Motion>(p)!;
        var lines = new List<string>();
        for (var i = 0; i < seconds * sim.Clock.TicksPerSecond; i++)
        {
            if (scenario == "zigzag-moving")
            {
                // Axis-aligned, scripted movement exercises replanning without Sin/Cos wander destinations.
                preyMotion.MoveDir = (i / (2 * sim.Clock.TicksPerSecond)) % 2 == 0 ? Vector2.UnitY : -Vector2.UnitY;
            }
            sim.Tick();
            Assert.DoesNotContain(mind.State, new[] { "idle", "wander", "return" });
            // Full-precision XYZ and path counts remain exact; yaw is outside the trajectory claim.
            lines.Add(string.Create(CultureInfo.InvariantCulture,
                $"{i} {transform.Position.X:R} {transform.Position.Y:R} {transform.Position.Z:R} {motion.Path.Count}"));
        }
        return lines.ToArray();
    }

    private static void Block(Simulation sim, int x, int y)
    {
        var (chunk, i) = sim.Terrain.Locate(new CellCoord(x, y));
        chunk.Flags[i] |= CellFlags.Blocked;
    }
}
