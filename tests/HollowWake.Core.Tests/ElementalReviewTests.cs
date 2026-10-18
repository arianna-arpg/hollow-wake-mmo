using System.Numerics;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Maths;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.World;
using HollowWake.Core.WorldGen;
using Xunit;

namespace HollowWake.Core.Tests;

public class ElementalReviewTests
{
    [Fact]
    public void NativeFactionStancesAreReciprocalIncludingTheLoosed()
    {
        var sim = ZoneBuilder.CreateArena(CoreContent.Registry);
        foreach (var faction in CoreContent.Registry.Each<FactionDef>())
            Assert.Equal(Relations.StanceBetween(sim, faction.Id, "unbound_elements"),
                Relations.StanceBetween(sim, "unbound_elements", faction.Id));
        Assert.Equal(Stance.Hostile, Relations.StanceBetween(sim, "unbound_elements", "the_loosed"));
    }

    [Fact]
    public void LoosedAndElementalAiBothDealDamageWithoutFactionOverrides()
    {
        var sim = ZoneBuilder.CreateArena(CoreContent.Registry);
        var factory = sim.Service<PatternFactory>();
        var loosed = factory.Spawn(sim, "loosed_kindler", new Vector3(10, 10, 0), new SpawnOptions { RollRarity = false });
        var elemental = factory.Spawn(sim, "ember_elemental", new Vector3(14, 10, 0), new SpawnOptions { RollRarity = false });
        var hits = new List<DamageDealt>();
        sim.Events.Subscribe<DamageDealt>(hits.Add);
        sim.RunSeconds(20);
        Assert.Contains(hits, h => h.Source == loosed && h.Target == elemental && h.Total > 0);
        Assert.Contains(hits, h => h.Source == elemental && h.Target == loosed && h.Total > 0);
    }

    [Theory, MemberData(nameof(ElementalTests.Sites), MemberType = typeof(ElementalTests))]
    public void ManifestationSitesHaveTheirOwnBudgetAndNoHumanStampRewards(string id)
    {
        var site = CoreContent.Registry.Require<PoiDef>(id);
        Assert.False(site.Tags.Has("ruin"));
        Assert.False(site.Tags.Has("shrine"));
        Assert.False(site.Tags.Has("elemental"));
        Assert.True(site.Tags.Has("site.manifestation"));
        Assert.Equal(0, site.Level);
        Assert.True(site.Names.Distinct().Count() > 1);
        Assert.InRange(site.MinSpacing, site.Radius * 2, 48);
        Assert.All(site.Landmarks, stamp => Assert.All(CoreContent.Registry.Require<LandmarkDef>(stamp).Legend.Values, cell => {
            Assert.Null(cell.Spawn);
            Assert.True(cell.Walkable);
            Assert.False(cell.Tags.Has("wall"));
        }));
        Assert.Contains(CoreContent.Registry.Each<RegionArchetypeDef>(), r => r.PoiBudget.GetValueOrDefault("site.manifestation") > 0);
        Assert.All(site.Inhabitants, resident => Assert.True(resident.Level?.Min >= CoreContent.Registry.Require<PatternDef>(resident.Pattern).Level));
    }

    [Fact]
    public void ScoutRuinNeverSelectsACloserManifestation()
    {
        var content = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out _);
        content.With(content.Require<PoiDef>("ruin") with { Level = 0 });
        content.With(new RegionOverrideDef { Id = "hallows_home", X = 0, Y = 0, Archetype = "hallows", Level = 25, Faction = "weftmarch_compact" });
        var pin = new ZoneOverrideDef { Id = "ninewells", X = 0, Y = 0 };
        pin.Pois.Add(new PoiPlacement { Poi = "hamlet", X = 100, Y = 100 });
        pin.Pois.Add(new PoiPlacement { Poi = "ruin", X = 200, Y = 100 });
        foreach (var row in ElementalTests.Sites()) pin.Pois.Add(new PoiPlacement { Poi = (string)row[0], X = 120, Y = 100 });
        content.With(pin);
        for (ulong seed = 1; seed <= 12; seed++)
        {
            var g = new WorldGenerator(content, seed);
            var zone = g.Zone(new ZoneCoord(0, 0));
            var quest = Assert.Single(zone.Quests, q => q.Id.Contains("scout_ruin", StringComparison.Ordinal));
            foreach (var objective in quest.Objectives.Where(o => o.Poi is not null))
            {
                var target = Assert.Single(g.PoisAround(new ZoneCoord(0, 0)), p => p.Id == objective.Poi);
                Assert.True(target.Tags.Has("ruin"));
                Assert.False(target.Tags.Has("site.manifestation"));
            }
        }
    }
}
