using HollowWake.Core.Definitions;
using HollowWake.Core.Patterns;
using Xunit;

namespace HollowWake.Core.Tests;

public class KinProjectileTests
{
    public static IEnumerable<object[]> Cases() => DeliveryArena.Registry.Each<PatternDef>()
        .Where(p => p.Extends is "gnoll_body" or "beastkin_body").OrderBy(p => p.Id, StringComparer.Ordinal)
        .SelectMany(p => new[] { -.5f, 0, .5f }.SelectMany(relief =>
            new[] { "firebolt", "piercing_arrow", "glaive", "orb", "volley", "quiver", "drawn_shot" }
                .Select(form => new object[] { p.Id, relief, form })));

    [Theory, MemberData(nameof(Cases))]
    public void AllKinTakeRealFlightsAcrossReliefWithoutEnlargingTheirBodies(string pattern, float relief, string form)
    {
        // Gnolls and Beastkin currently uses the shared cylinder fallback; this is not authored anatomy coverage.
        Assert.False(new PatternFactory(DeliveryArena.Registry).Resolve(pattern).Components.ContainsKey("hit_body"));
        HitBodyProjectileTests.AssertRealCastContact(pattern, relief, form);
    }
}
