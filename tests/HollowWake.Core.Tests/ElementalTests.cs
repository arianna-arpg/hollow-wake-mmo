using System.Numerics;
using System.Globalization;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Maths;
using HollowWake.Core.Movement;
using HollowWake.Core.Minions;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;
using HollowWake.Core.World;
using HollowWake.Core.WorldGen;
using Xunit;
using Xunit.Abstractions;

namespace HollowWake.Core.Tests;

public class ElementalTests(ITestOutputHelper output)
{
    public static IEnumerable<object[]> Bodies() => CoreContent.Registry.Each<PatternDef>()
        .Where(p => p.Extends == "elemental_body").Select(p => new object[] { p.Id });
    public static IEnumerable<object[]> Sites() => CoreContent.Registry.Each<PoiDef>()
        .Where(p => p.Tags.Has("elemental")).Select(p => new object[] { p.Id });
    public static IEnumerable<object[]> Kits() => Bodies().SelectMany(row => {
        var a = MinionArena.Create();
        var e = a.Foe((string)row[0], new Vector3(24,20,0));
        return a.Sim.World.Get<WeaveBook>(e)!.Slots.Select(s => new object[] { row[0], s.Spec.Preset! }).ToArray();
    });

    [Theory, MemberData(nameof(Bodies))]
    public void PhysicalBodiesHaveHonestClassifiersRewardsAndHabitatReferences(string pattern)
    {
        var a = DeliveryArena.Create(); var e = a.Body(20,20,pattern:pattern);
        var id = a.Sim.World.Get<Identity>(e)!;
        foreach (var tag in new[]{"elemental","monster","body","family","role","size","thread","habitat","locomotion.ground","no_corpse"})
            Assert.True(id.Tags.Has(tag),pattern+":"+tag);
        Assert.False(id.Tags.Has("golem")); Assert.False(id.Tags.Has("undead"));
        Assert.False(id.Tags.Has("flying")); Assert.False(id.Tags.Has("phasing"));
        Assert.Equal("unbound_elements",CoreContent.Registry.Require<PatternDef>(pattern).Faction);
        var p = a.Sim.World.Get<PhysicalBody>(e)!.Profile;
        var look = CoreContent.Registry.Require<AppearanceDef>(pattern);
        Assert.Equal(p.Width/p.Height,look.Shape["breadth"],6);
        Assert.Equal(p.Depth/p.Height,look.Shape["depth"],6);
        Assert.Equal(0,look.Variation.Scale);
        Assert.NotNull(a.Sim.World.Get<Drops>(e));
        Assert.Contains(CoreContent.Registry.Each<PoiDef>(),site =>
            id.Tags.Has("habitat."+site.Id) && site.Inhabitants.Any(s=>s.Pattern==pattern) &&
            site.Biomes.Items.All(b=>id.Tags.Has("habitat."+b)));
        Assert.True(Physicality.Mass(a.Sim,e)>0);
    }

    [Theory, MemberData(nameof(Kits))]
    public void EveryAssignedSkillDealsDamageThroughAiAndKeepsActing(string pattern,string preset)
    {
        static (string digest,int early,int late) Run(string pattern,string preset)
        {
            var a=MinionArena.Create();
            a.OwnerStats.Attributes.SetBase("life.max",100000);a.OwnerStats.FillResources();
            var e=a.Foe(pattern,new Vector3(24,20,0),thinks:true);
            var book=a.Sim.World.Get<WeaveBook>(e)!;
            book.Slots.Clear();book.Add(new WeaveSpec{Preset=preset});
            for(var i=0;i<300;i++)a.Sim.Tick();
            var early=a.Events.OfType<DamageDealt>().Count(d=>d.Source==e && d.Target==a.Owner && d.Total>0);
            a.Events.Clear();
            for(var i=0;i<600;i++)a.Sim.Tick();
            var hits=a.Events.OfType<DamageDealt>().Where(d=>d.Source==e && d.Target==a.Owner && d.Total>0).ToArray();
            var digest=string.Join("|",hits.Select(d=>d.Total.ToString("R",CultureInfo.InvariantCulture)+":"+string.Join(",",d.ByThread.Keys)));
            return(digest,early,hits.Length);
        }
        var first=Run(pattern,preset);
        Assert.True(first.early>0,pattern+"/"+preset+" never hit");
        Assert.True(first.late>0,pattern+"/"+preset+" stopped acting after the initial resource pool");
        Assert.Equal(first,Run(pattern,preset));
        output.WriteLine($"{pattern}/{preset}: early hits={first.early}, late hits={first.late}; exact repeat.");
    }

    [Theory, MemberData(nameof(Bodies))]
    public void CompleteKitsFightUsingNativeFactionAndRemainDamageable(string pattern)
    {
        var a=MinionArena.Create();a.OwnerStats.Attributes.SetBase("life.max",100000);a.OwnerStats.FillResources();
        a.Sim.World.Get<Identity>(a.Owner)!.Faction="players";
        var e=a.Sim.Service<PatternFactory>().Spawn(a.Sim,pattern,new Vector3(24,20,0));
        Assert.True(Relations.IsHostile(a.Sim,a.Owner,e));Assert.True(Relations.IsHostile(a.Sim,e,a.Owner));
        for(var i=0;i<450;i++)a.Sim.Tick();
        Assert.True(a.Hit(e,a.Owner));
        foreach(var thread in CoreContent.Registry.Each<ThreadDef>())
        {
            var d=DeliveryArena.Create();var target=d.Body(20,20,pattern:pattern);
            d.Run("{\"type\":\"damage\",\"amount\":100,\"threads\":[\""+thread.Id+"\"],\"can_crit\":false}",new Vector3(20,20,0),target:target);
            Assert.Contains(d.Events.OfType<DamageDealt>(),hit=>hit.Target==target && hit.Total>0);
        }
    }

    [Theory, MemberData(nameof(Sites))]
    public void SparseSitesGenerateResidentsAtTheirLevelAndReplayExactly(string siteId)
    {
        static string[] Generate(string siteId,int level)
        {
            var content=ContentRegistry.Load(new[]{CoreContent.FindContentDir()},out _);
            content.With(new RegionOverrideDef{Id="hallows_home",X=0,Y=0,Archetype="hallows",Level=level,Faction="weftmarch_compact"});
            content.With(new ZoneOverrideDef{Id="ninewells",X=0,Y=0,Pois={new PoiPlacement{Poi=siteId,X=50,Y=50}}});
            var g=new WorldGenerator(content,Rng.Hash64("unbound-sites"));
            return g.Layout.ChunksOf(new ZoneCoord(0,0)).SelectMany(c=>g.GenerateChunk(c).Spawns)
                .Where(s=>s.Group?.Contains(siteId,StringComparison.Ordinal)==true)
                .Select(s=>$"{s.Pattern}:{s.X:R}:{s.Y:R}:{s.PackSize}").ToArray();
        }
        var def=CoreContent.Registry.Require<PoiDef>(siteId);
        var high=Generate(siteId,25);
        Assert.Equal(high,Generate(siteId,25));
        foreach(var spawn in def.Inhabitants) Assert.Contains(high,s=>s.StartsWith(spawn.Pattern+":"));
        Assert.All(def.Inhabitants,s=>Assert.Equal(1,s.Density));
        var low=Generate(siteId,1);
        foreach(var spawn in def.Inhabitants.Where(s=>s.Level?.Min>1+def.Level))
            Assert.DoesNotContain(low,s=>s.StartsWith(spawn.Pattern+":"));
        output.WriteLine(siteId+": "+string.Join(" | ",high));
    }

    [Fact]
    public void ExistingWorldBudgetsCanSelectUnboundSitesNaturally()
    {
        var g=new WorldGenerator(CoreContent.Registry,Rng.Hash64("aureth"));
        var found=new HashSet<string>();
        for(var y=-48;y<=48;y++)for(var x=-48;x<=48;x++)
            foreach(var poi in g.Zone(new ZoneCoord(x,y)).Pois.Where(p=>p.Def.Tags.Has("elemental")))
            {
                if(!found.Add(poi.Def.Id))continue;
                var sample=g.Sample(poi.X,poi.Y);
                var biome=g.Biome(sample,g.Region(g.Layout.RegionOf(poi.X,poi.Y)));
                Assert.NotNull(biome);
                // The planner fits the candidate before snapping its saved position to the cell
                // center. A biome boundary can cross that cell; inspect the site's local footprint.
                var habitat=false;
                for(var dx=-1;dx<=1;dx++)for(var dy=-1;dy<=1;dy++)
                {
                    var near=g.Biome(g.Sample(poi.X+dx*0.5,poi.Y+dy*0.5),g.Region(g.Layout.RegionOf(poi.X,poi.Y)));
                    for(var b=near;b is not null;b=b.Parent is null ? null : CoreContent.Registry.Get<BiomeDef>(b.Parent))
                        habitat|=poi.Def.Biomes.Has(b.Id);
                }
                Assert.True(habitat,poi.Def.Id+" has no matching biome in its placement cell");
                output.WriteLine($"{x},{y}: {poi.Def.Id} at {poi.X:R},{poi.Y:R}, biome={biome.Id}, site level={poi.Level}");
            }
        foreach(var site in Sites())Assert.Contains((string)site[0],found);
    }

    [Theory, MemberData(nameof(Bodies))]
    public void BoundBodiesUseOwnerAllegianceLoseWildRewardsAndLeaveNoCorpse(string pattern)
    {
        var a=DeliveryArena.Create();
        var wild=a.Body(20,20,pattern:pattern);
        a.Sim.Service<DeathService>().Kill(a.Sim,wild,Entity.None);
        Assert.False(Corpses.Is(a.Sim.World,wild));
        a.Run("{\"type\":\"summon\",\"pattern\":\""+pattern+"\",\"count\":1,\"max_active\":1,\"duration\":30,\"group\":\"test_elemental\"}",new Vector3(12,10,0));
        var bound=Assert.Single(a.Sim.World.Query<Minion>()).Entity;
        Assert.Null(a.Sim.World.Get<Drops>(bound));
        Assert.True(Relations.IsAlly(a.Sim,a.Caster,bound));
        Assert.NotNull(a.Sim.World.Get<PhysicalBody>(bound));
        a.Sim.Service<DeathService>().Kill(a.Sim,bound,Entity.None);
        Assert.False(Corpses.Is(a.Sim.World,bound));
    }

    [Fact]
    public void MaterialDensityAndPoiseProduceDistinctMassAndRealShoveTravel()
    {
        static (double mass,float distance) Push(string pattern)
        {
            var a=DeliveryArena.Create();var e=a.Body(30,10,pattern:pattern);
            var at=a.Sim.World.Get<Transform>(e)!;var start=at.Position;
            var mass=Physicality.Mass(a.Sim,e);
            a.Run("""{"type":"move","kind":"knockback","distance":4}""",start,target:e);a.Tick(45);
            return(mass,Vector3.Distance(start,at.Position));
        }
        var wisp=Push("emberwisp");var wind=Push("gale_elemental");var water=Push("tide_surge");var geode=Push("geode_shellback");
        Assert.True(wisp.mass<wind.mass && wind.mass<water.mass && water.mass<geode.mass);
        Assert.True(wisp.distance>wind.distance && wind.distance>water.distance && water.distance>geode.distance);
        Assert.Equal(geode,Push("geode_shellback"));
        foreach(var pattern in Bodies().Select(r=>(string)r[0])){var p=Push(pattern);output.WriteLine($"{pattern}: mass={p.mass:F3} kg, shove={p.distance:F5} m");}
    }

    [Fact]
    public void ReviewedLocalZonesKeepTheirPlansWhileNeighborSitesJoinTheRuinBudget()
    {
        var report=new ValidationReport();
        var raw=ContentLoader.LoadRaw(new[]{CoreContent.FindContentDir()},report);
        foreach(var row in Sites())raw["pois"].Remove((string)row[0]);
        var baseline=ContentRegistry.FromRaw(raw,report);
        Assert.False(report.HasErrors,report.ToString());
        var before=new WorldGenerator(baseline,Rng.Hash64("aureth"));
        var after=new WorldGenerator(CoreContent.Registry,Rng.Hash64("aureth"));
        static string Describe(PoiInstance p)=>$"{p.Zone}:{p.Def.Id}:{p.X:R}:{p.Y:R}:{p.Landmark}:{p.Name}:L{p.Level}";
        foreach(var zone in new[]{new ZoneCoord(0,0),new ZoneCoord(1,0),new ZoneCoord(0,-1),new ZoneCoord(3,2)})
        {
            Assert.Equal(before.Zone(zone).Pois.Select(Describe),after.Zone(zone).Pois.Select(Describe));
            var old=before.PoisAround(zone).Select(Describe).ToArray();
            var next=after.PoisAround(zone).Select(Describe).ToArray();
            foreach(var p in old.Except(next))output.WriteLine(zone+" neighbor removed: "+p);
            foreach(var p in next.Except(old))output.WriteLine(zone+" neighbor added: "+p);
        }
    }
}
