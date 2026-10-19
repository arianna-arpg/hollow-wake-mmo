using HollowWake.Core.Definitions;
using HollowWake.Core.Patterns;
using Xunit;
using Xunit.Abstractions;

namespace HollowWake.Core.Tests;

public class FaunaFreeShotTests(ITestOutputHelper output)
{
    public static IEnumerable<object[]> SettledShots() => DeliveryArena.Registry.Each<PatternDef>()
        .Where(p => p.Extends == "fauna_body").OrderBy(p => p.Id, StringComparer.Ordinal)
        .SelectMany(p => new[] { -.125f, 0, .125f }.SelectMany(slope =>
            new[] { "firebolt", "piercing_arrow" }.SelectMany(preset =>
                new[] { 4f, 8f }.Select(distance => new object[] { p.Id, slope, preset, distance }))));

    [Theory, MemberData(nameof(SettledShots))]
    public void GroundSettledFreeShotsReachEveryAuthoredFaunaSize(string pattern, float slope, string preset, float distance)
    {
        // Fauna uses fallback cylinders; keep its authored height/radius and a real humanoid launch.
        Assert.False(new PatternFactory(DeliveryArena.Registry).Resolve(pattern).Components.ContainsKey("hit_body"));
        new FreeShotGroundFloorTests(output).AssertHumanoidFreeShotContact(pattern, slope, preset, distance, 1);
    }

    public static IEnumerable<object[]> AirborneMisses() =>
        new[] { "gutter_rat", "reed_frog", "shore_crab" }
            .SelectMany(pattern => new[] { 0f, .125f }.SelectMany(slope =>
                new[] { "firebolt", "piercing_arrow" }.Select(preset => new object[] { pattern, slope, preset })))
            .Concat(new[]
            {
                new object[] { "hedgehog", 0f, "piercing_arrow" },
                new object[] { "hedgehog", .125f, "firebolt" },
                new object[] { "hedgehog", .125f, "piercing_arrow" },
            });

    [Theory, MemberData(nameof(AirborneMisses))]
    public void StillAirborneFreeShotsCanPassAboveCrittersBeforeReachingTheCursor(string pattern, float slope, string preset)
    {
        // Preserve every still-airborne diagnostic miss and prove clearance over its actual envelope.
        new FreeShotGroundFloorTests(output).AssertHumanoidFreeShotContact(pattern, slope, preset, 4, 6, expectedHit: false);
    }
}
