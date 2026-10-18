using System.Numerics;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Maths;
using HollowWake.Core.Movement;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.World;
using HollowWake.Core.WorldGen;
using Xunit;
using Xunit.Abstractions;

namespace HollowWake.Core.Tests;

public class KoboldTests(ITestOutputHelper output)
{
    public static IEnumerable<object[]> Bodies() => CoreContent.Registry.Each<PatternDef>()
        .Where(p => p.Extends == "kobold_body").Select(p => new object[] { p.Id });

    [Theory, MemberData(nameof(Bodies))]
    public void EveryRoleHasScaledPhysicalityHabitatRewardsAndSustainedAiDamage(string pattern)
    {
        var a = MinionArena.Create();
        a.OwnerStats.Attributes.SetBase("life.max", 100000);
        a.OwnerStats.FillResources();
        var body = a.Foe(pattern, new Vector3(24, 20, 0), thinks: true);
        var identity = a.Sim.World.Get<Identity>(body)!;
        foreach (var tag in new[] { "kobold", "reptile", "dragonkin", "body.flesh", "body.scaled", "family.kobold", "role", "habitat", "size", "locomotion.ground" })
            Assert.True(identity.Tags.Has(tag), pattern + ":" + tag);
        Assert.False(identity.Tags.Has("goblinkin"));
        var def = CoreContent.Registry.Require<PatternDef>(pattern);
        Assert.Equal("kobold_scaleclans", def.Faction);
        Assert.NotNull(a.Sim.World.Get<Drops>(body));
        var profile = a.Sim.World.Get<PhysicalBody>(body)!.Profile;
        var look = CoreContent.Registry.Require<AppearanceDef>(pattern);
        Assert.Equal("scaled_biped", look.Body);
        Assert.Equal(profile.Width / profile.Height, look.Shape["breadth"], 6);
        Assert.Equal(profile.Depth / profile.Height, look.Shape["depth"], 6);
        Assert.Equal(0, look.Variation.Scale);
        var sites = CoreContent.Registry.Each<PoiDef>().Where(p => identity.Tags.Has("habitat." + p.Id)).ToArray();
        Assert.NotEmpty(sites);
        Assert.All(sites, p => Assert.Contains(p.Inhabitants, s => s.Pattern == pattern));
        // The second window proves that the ordinary resource and cooldown loop keeps working.
        for (var i = 0; i < 250; i++) a.Sim.Tick();
        Assert.True(a.Hit(body, a.Owner), pattern + " never landed a hit through AI");
        // Reposition the same target after displacement; do not refill the attacker's resources.
        a.Move(a.Owner, new Vector3(20, 20, 0));
        a.Move(body, new Vector3(24, 20, 0));
        a.Events.Clear();
        for (var i = 0; i < 250; i++) a.Sim.Tick();
        Assert.True(a.Hit(body, a.Owner), pattern + " stopped dealing damage after the opening window");
        output.WriteLine($"{pattern}: mass={Physicality.Mass(a.Sim, body):F2} kg; sustained damaging events={a.Events.OfType<DamageDealt>().Count(d => d.Source == body && d.Total > 0)}");
    }

    [Fact]
    public void NativeFactionIsHostileToPlayersAndNeutralToOtherWarbandsInBothDirections()
    {
        var a = DeliveryArena.Create();
        foreach (var faction in new[] { "players", "lantern_chancery", "weftmarch_compact", "remnant", "tangled" })
        {
            Assert.Equal(Stance.Hostile, Relations.StanceBetween(a.Sim, "kobold_scaleclans", faction));
            Assert.Equal(Stance.Hostile, Relations.StanceBetween(a.Sim, faction, "kobold_scaleclans"));
        }
        foreach (var faction in new[] { "goblinkin", "orc_warbands", "wilds" })
        {
            Assert.Equal(Stance.Neutral, Relations.StanceBetween(a.Sim, "kobold_scaleclans", faction));
            Assert.Equal(Stance.Neutral, Relations.StanceBetween(a.Sim, faction, "kobold_scaleclans"));
        }
        Assert.Equal(Stance.Allied, Relations.StanceBetween(a.Sim, "kobold_scaleclans", "kobold_scaleclans"));
    }

    [Fact]
    public void LightAndArmoredBodiesHaveMeasuredMassAndDifferentLiveShoveDistances()
    {
        (double mass, float travel) Push(string pattern)
        {
            var a = DeliveryArena.Create();
            var e = a.Body(30, 10, pattern: pattern);
            var at = a.Sim.World.Get<Transform>(e)!;
            var start = at.Position;
            var mass = Physicality.Mass(a.Sim, e);
            a.Run("""{"type":"move","kind":"knockback","distance":4}""", start, target: e);
            a.Tick(45);
            var travel = Vector3.Distance(start, at.Position);
            output.WriteLine($"{pattern}: {mass:F3} kg, shove travel {travel:F3} m");
            return (mass, travel);
        }
        var small = Push("kobold_scavenger");
        var heavy = Push("kobold_ironback");
        Assert.InRange(small.mass, 19, 20);
        Assert.InRange(heavy.mass, 148, 150);
        Assert.True(small.travel > heavy.travel * 4);
        Assert.InRange(CoreContent.Registry.Require<PatternDef>("kobold_scavenger").Height / 1.8, .4, .5);
        Assert.Equal(1.8, CoreContent.Registry.Require<PatternDef>("skeleton_warrior").Height, 5);
    }

    [Theory]
    [InlineData("kobold_scrap_camp")]
    [InlineData("kobold_reed_camp")]
    [InlineData("kobold_cinder_camp")]
    [InlineData("kobold_scale_hold")]
    public void AuthoredSitesSpawnEveryEligibleRoleAndReplayWithLevelGates(string site)
    {
        string[] Generate(int level)
        {
            var content = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out _);
            content.With(new RegionOverrideDef { Id = "hallows_home", X = 0, Y = 0, Archetype = "hallows", Level = level, Faction = "weftmarch_compact" });
            content.With(new ZoneOverrideDef { Id = "ninewells", X = 0, Y = 0, Pois = { new PoiPlacement { Poi = site, X = 50, Y = 50 } } });
            var g = new WorldGenerator(content, Rng.Hash64("copper-crest-sites"));
            return g.Layout.ChunksOf(new ZoneCoord(0, 0)).SelectMany(c => g.GenerateChunk(c).Spawns)
                .Where(s => s.Group?.Contains(site, StringComparison.Ordinal) == true && s.Pattern.StartsWith("kobold_", StringComparison.Ordinal))
                .Select(s => $"{s.Pattern}:{s.X}:{s.Y}:{s.PackSize}").ToArray();
        }
        var high = Generate(25);
        var def = CoreContent.Registry.Require<PoiDef>(site);
        Assert.All(def.Inhabitants, s => Assert.Contains(high, row => row.StartsWith(s.Pattern + ":", StringComparison.Ordinal)));
        Assert.Equal(high, Generate(25));
        var low = Generate(1);
        foreach (var s in def.Inhabitants.Where(s => CoreContent.Registry.Require<PatternDef>(s.Pattern).Level > 1 + def.Level))
            Assert.DoesNotContain(low, row => row.StartsWith(s.Pattern + ":", StringComparison.Ordinal));
        Assert.InRange(high.Length, 1, 4);
    }

    [Theory]
    [InlineData("kobold_scale_hold", -15, -40)]
    [InlineData("kobold_scrap_camp", -8, -40)]
    [InlineData("kobold_cinder_camp", -25, -31)]
    [InlineData("kobold_reed_camp", -7, -23)]
    public void DedicatedSitesActuallyOccurInUnmodifiedGeneratedHabitats(string site, int x, int y)
    {
        var g = new WorldGenerator(CoreContent.Registry, Rng.Hash64("aureth"));
        var poi = Assert.Single(g.Zone(new ZoneCoord(x, y)).Pois, p => p.Def.Id == site);
        output.WriteLine($"{site}: zone({x},{y}), at({poi.X},{poi.Y}), level {poi.Level}");
        Assert.Contains(g.Layout.ChunksOf(poi.Zone).SelectMany(c => g.GenerateChunk(c).Spawns),
            s => s.Pattern.StartsWith("kobold_", StringComparison.Ordinal) && s.Group?.Contains(site, StringComparison.Ordinal) == true);
    }
}
