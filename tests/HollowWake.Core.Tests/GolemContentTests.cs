using System.Numerics;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Maths;
using HollowWake.Core.Minions;
using HollowWake.Core.Movement;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.World;
using HollowWake.Core.WorldGen;
using Xunit;
using Xunit.Abstractions;

namespace HollowWake.Core.Tests;

public class GolemContentTests(ITestOutputHelper output)
{
    public static IEnumerable<object[]> Bodies() => CoreContent.Registry.Each<PatternDef>()
        .Where(p => p.Extends == "golem_body" && p.Id != "bound_golem_body" || p.Id == "stone_warden").Select(p => new object[] { p.Id });

    public static IEnumerable<object[]> WildKits() => CoreContent.Registry.Each<PatternDef>()
        .Where(p => p.Extends == "golem_body" && p.Id != "bound_golem_body")
        .SelectMany(p => p.Components["weaves"]!["list"]!.AsArray().Select(s => new object[] { p.Id, s!.GetValue<string>() }));

    [Theory, MemberData(nameof(Bodies))]
    public void PhysicalAssembliesHaveClassifiersMassAndMatchingPresentation(string pattern)
    {
        var a = DeliveryArena.Create();
        var e = a.Body(20, 20, pattern: pattern);
        var id = a.Sim.World.Get<Identity>(e)!;
        foreach (var tag in new[] { "construct", "golem", "family", "body", "assembly", "role", "size", "habitat", "locomotion.ground", "no_corpse" })
            Assert.True(id.Tags.Has(tag), pattern + ":" + tag);
        var p = a.Sim.World.Get<PhysicalBody>(e)!.Profile;
        var look = CoreContent.Registry.Require<AppearanceDef>(pattern);
        Assert.Equal("assembled_golem", look.Body);
        Assert.Equal(p.Width / p.Height, look.Shape["breadth"], 6);
        Assert.Equal(p.Depth / p.Height, look.Shape["depth"], 6);
        Assert.Equal(0, look.Variation.Scale);
        Assert.Equal(p.Height, a.Sim.World.Get<Transform>(e)!.Height, 5);
        var density = p.Materials.Sum(m => m.Fraction * CoreContent.Registry.Require<BodyMaterialDef>(m.Material).Density);
        Assert.Equal(p.Width * p.Depth * p.Height * p.Fill * p.Heft * density, Physicality.Mass(a.Sim,e), 3);
        output.WriteLine($"{pattern}: {Physicality.Mass(a.Sim,e):F3} kg");
        var stats = CoreContent.Registry.Require<PatternDef>(pattern).Components["stats"]!["attributes"]!.AsObject();
        Assert.DoesNotContain(stats, s => s.Key.StartsWith("resist.") || s.Key.StartsWith("immunity."));
        a.Sim.Service<DeathService>().Kill(a.Sim,e,Entity.None);
        Assert.False(Corpses.Is(a.Sim.World,e));
    }

    [Theory, MemberData(nameof(WildKits))]
    public void EveryAssignedAttackActuallyLandsThroughOrdinaryAiAndReplays(string pattern, string skill)
    {
        string[] Run()
        {
            var a = MinionArena.Create();
            a.OwnerStats.Attributes.SetBase("life.max",100000); a.OwnerStats.FillResources();
            a.Sim.World.Get<Identity>(a.Owner)!.Faction = "players";
            // Spawn with authored allegiance, not the test arena's synthetic hostile override.
            var foe = a.Sim.Service<PatternFactory>().Spawn(a.Sim,pattern,new Vector3(24,20,0));
            Assert.True(Relations.IsHostile(a.Sim,foe,a.Owner));
            Assert.NotNull(a.Sim.World.Get<Drops>(foe));
            var book = a.Sim.World.Get<WeaveBook>(foe)!;
            book.Slots.RemoveAll(s => s.Spec.Preset != skill);
            Assert.Single(book.Slots);
            // Isolate each equipped attack so another successful skill cannot hide a broken one.
            for(var i=0;i<500;i++) a.Sim.Tick();
            var hits=a.Events.OfType<DamageDealt>().Where(d=>d.Source==foe && d.Target==a.Owner && d.Total>0).ToArray();
            Assert.True(hits.Length>=2,pattern+":"+skill+" did not sustain hits");
            Assert.Contains(a.Events.OfType<WeaveCast>(),c=>c.Caster==foe);
            output.WriteLine($"{pattern}/{skill}: {hits.Length} hits, {hits.Sum(d=>d.Total):F2} damage");
            return hits.Select(d=>$"{d.Total:R}:{d.Crit}:{d.Roll:R}:{d.CastId}").ToArray();
        }
        Assert.Equal(Run(),Run());
    }

    [Fact]
    public void MixedKitsStillCloseAndDamageWithoutAnIsolatedSlot()
    {
        foreach(var row in Bodies().Where(r=>(string)r[0]!="stone_warden"))
        {
            var a=MinionArena.Create(); a.OwnerStats.Attributes.SetBase("life.max",100000); a.OwnerStats.FillResources();
            var foe=a.Foe((string)row[0],new Vector3(26,20,0),thinks:true);
            for(var i=0;i<500;i++) a.Sim.Tick();
            Assert.True(a.Hit(foe,a.Owner),(string)row[0]);
        }
    }

    [Theory, MemberData(nameof(Bodies))]
    public void HabitatsHaveRealReferencesAndTheLegacyWardenStaysSummonOnly(string pattern)
    {
        var def=CoreContent.Registry.Require<PatternDef>(pattern);
        var sites=CoreContent.Registry.Each<PoiDef>().Where(p=>p.Inhabitants.Any(s=>s.Pattern==pattern)).ToArray();
        if(pattern=="stone_warden")
        {
            Assert.Empty(sites); Assert.Equal("players",def.Faction);
            Assert.Null(DeliveryArena.Create().Sim.Content.Require<PatternDef>(pattern).Components.GetValueOrDefault("drops"));
            Assert.Contains(CoreContent.Registry.Require<WeaveDef>("bind_stone_warden").Effects,
                e=>e.Type=="summon" && e.Params["pattern"]?.GetValue<string>()==pattern);
        }
        else
        {
            Assert.Equal("unbound_constructs",def.Faction);
            var site=Assert.Single(sites);
            Assert.True(def.Tags.Has("habitat."+site.Id));
            Assert.NotEmpty(site.Biomes);
            Assert.InRange(site.Weight,0.01,0.4);
            Assert.Equal(new double[]{1,1},Assert.Single(site.Inhabitants,s=>s.Pattern==pattern).PackSize);
        }
    }

    [Theory]
    [InlineData("golem_kiln")]
    [InlineData("golem_quarry")]
    [InlineData("golem_foundry")]
    [InlineData("golem_sawmill")]
    [InlineData("amalgam_suture_works")]
    public void AuthoredSitesPopulateAtTheirLevelAndReplay(string site)
    {
        string[] Generate(int level)
        {
            var content=ContentRegistry.Load(new[]{CoreContent.FindContentDir()},out _);
            var original=content.Require<PoiDef>(site);
            // Preserve the authored offset: even the lowest region must have real occupants.
            content.With(new RegionOverrideDef{Id="hallows_home",X=0,Y=0,Archetype="hallows",Level=level,Faction="weftmarch_compact"});
            content.With(new ZoneOverrideDef{Id="ninewells",X=0,Y=0,Pois={new PoiPlacement{Poi=site,X=50,Y=50}}});
            var g=new WorldGenerator(content,Rng.Hash64("golem-sites"));
            return g.Layout.ChunksOf(new ZoneCoord(0,0)).SelectMany(c=>g.GenerateChunk(c).Spawns)
                .Where(s=>s.Group?.Contains(site,StringComparison.Ordinal)==true && original.Inhabitants.Any(i=>i.Pattern==s.Pattern))
                .Select(s=>$"{s.Pattern}:{s.X}:{s.Y}:{s.PackSize}").ToArray();
        }
        Assert.NotEmpty(Generate(1));
        Assert.Equal(Generate(1),Generate(1));
        var old=Generate(15);
        foreach(var inhabitant in CoreContent.Registry.Require<PoiDef>(site).Inhabitants)
            Assert.Contains(old,s=>s.StartsWith(inhabitant.Pattern+":"));
        Assert.Equal(old,Generate(15));
        output.WriteLine(site+": "+string.Join(", ",old));
    }

    [Fact]
    public void EverySiteCanBeChosenNaturallyInItsAuthoredBiome()
    {
        var g=new WorldGenerator(CoreContent.Registry,Rng.Hash64("aureth"));
        var wanted=CoreContent.Registry.Each<PoiDef>().Where(p=>p.Tags.Has("golem_site")).Select(p=>p.Id).ToHashSet();
        for(var y=-64;y<=64 && wanted.Count>0;y++)
            for(var x=-64;x<=64 && wanted.Count>0;x++)
                foreach(var p in g.PoisOf(new ZoneCoord(x,y)))
                    if(wanted.Remove(p.Def.Id))
                    {
                        Assert.Contains(g.Layout.ChunksOf(new ZoneCoord(x,y)).SelectMany(c=>g.GenerateChunk(c).Spawns),
                            s=>s.Group==p.Id && p.Def.Inhabitants.Any(i=>i.Pattern==s.Pattern));
                        output.WriteLine($"{p.Def.Id}: zone({x},{y}), at ({p.X},{p.Y}), level {p.Level}");
                    }
        Assert.Empty(wanted);
    }

    [Fact]
    public void TimberClayStoneAndIronHaveMeaningfullyDifferentShoveResponses()
    {
        (double mass,float travel) Push(string pattern)
        {
            var a=DeliveryArena.Create(); var e=a.Body(30,10,pattern:pattern);
            var start=a.Sim.World.Get<Transform>(e)!.Position;
            var mass=Physicality.Mass(a.Sim,e);
            a.Run("""{"type":"move","kind":"knockback","distance":8}""",start,target:e);
            a.Tick(45);
            var travel=Vector3.Distance(start,a.Sim.World.Get<Transform>(e)!.Position);
            output.WriteLine($"{pattern}: mass={mass:F3} kg, shove={travel:F6} m");
            return(mass,travel);
        }
        var timber=Push("timber_golem");var clay=Push("clay_golem");var stone=Push("quarry_golem");var iron=Push("iron_golem");
        Assert.True(timber.mass<clay.mass && clay.mass<stone.mass && stone.mass<iron.mass);
        Assert.True(timber.travel>clay.travel && clay.travel>stone.travel && stone.travel>iron.travel && iron.travel>0);
        Assert.Equal(iron,Push("iron_golem"));
    }

    [Fact]
    public void SalvagePoolReusesAllBodiesWithOwnerAllegianceAndNoFarmableRewards()
    {
        string Roll(ulong seed)
        {
            var a=MinionArena.Create(seed);
            a.Cast(a.Learn("bind_salvaged_golem"));
            var e=Assert.Single(a.Minions());
            var id=a.Sim.World.Get<Identity>(e)!.Pattern!;
            Assert.Equal("red",Relations.FactionOf(a.Sim,e));
            Assert.Null(a.Sim.World.Get<Drops>(e));
            Assert.Equal(id=="crystal_golem"?"minion_ranged":"minion_melee",a.Sim.World.Get<Behavior>(e)!.BehaviorId);
            var target=a.Foe("skeleton_warrior",new Vector3(24,20,0));
            var stats=a.Sim.World.Get<Stats>(target)!; stats.Attributes.SetBase("life.max",100000);stats.FillResources();
            for(var i=0;i<400;i++)a.Sim.Tick();
            Assert.True(a.Hit(e,target),id+" summon did not land its kit");
            return id;
        }
        Assert.Equal(Roll(17),Roll(17));
        var rolled=Enumerable.Range(1,40).Select(seed=>Roll((ulong)seed)).Distinct().OrderBy(s=>s);
        Assert.Equal(Bodies().Select(r=>(string)r[0]).Where(s=>s!="stone_warden").OrderBy(s=>s),rolled);
    }

    [Fact]
    public void SalvageContractRespawnsAndExcludesBothLegacyWardenContracts()
    {
        var a=MinionArena.Create();
        a.Cast(a.Learn("bind_salvaged_golem"));
        var first=Assert.Single(a.Minions());
        Assert.Equal(30,a.OwnerStats.Get("mana.reserved"));
        a.Sim.Service<DeathService>().Kill(a.Sim,first,a.Owner);
        Assert.DoesNotContain(a.Events.OfType<ExperienceGained>(),e=>e.Entity==a.Owner);
        Assert.DoesNotContain(a.Events.OfType<ItemDropped>(),e=>e.From==first);
        for(var i=0;i<180;i++)a.Sim.Tick();
        var second=Assert.Single(a.Minions());
        Assert.NotEqual(first,second); Assert.Null(a.Sim.World.Get<Drops>(second));
        a.Cast(a.Learn("bind_stone_warden"));
        var warden=Assert.Single(a.Minions());
        Assert.Equal("stone_warden",a.Sim.World.Get<Identity>(warden)!.Pattern);
        Assert.Null(a.Sim.World.Get<Drops>(warden));
        Assert.Equal(16,a.Sim.World.Get<Stats>(warden)!.Get("might"));
        Assert.Equal(30,a.OwnerStats.Get("mana.reserved"));
        a.Cast(a.Learn("summon_bone_golem"));
        Assert.Equal("bone_golem",a.Sim.World.Get<Identity>(Assert.Single(a.Minions()))!.Pattern);
        for(var i=0;i<200;i++)a.Sim.Tick();
        Assert.Single(a.Minions());
        Assert.Equal(30,a.OwnerStats.Get("mana.reserved"));
    }
}
