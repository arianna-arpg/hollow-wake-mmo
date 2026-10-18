using System.Numerics;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Maths;
using HollowWake.Core.Movement;
using HollowWake.Core.Patterns;
using HollowWake.Core.World;
using HollowWake.Core.WorldGen;
using Xunit;

namespace HollowWake.Core.Tests;

public class KinTests
{
    public static IEnumerable<object[]> Bodies() => CoreContent.Registry.Each<PatternDef>()
        .Where(p => p.Extends is "gnoll_body" or "beastkin_body").Select(p => new object[] { p.Id });

    [Theory, MemberData(nameof(Bodies))]
    public void KitsFightAndBodyHabitatFactionAndAppearanceResolveTogether(string pattern)
    {
        var a = MinionArena.Create();
        a.OwnerStats.Attributes.SetBase("life.max", 100000); a.OwnerStats.FillResources();
        var body = a.Foe(pattern, new Vector3(24,20,0), thinks:true);
        var id = a.Sim.World.Get<Identity>(body)!;
        var def = CoreContent.Registry.Require<PatternDef>(pattern);
        var family = def.Extends == "gnoll_body" ? "gnoll" : "beastkin";
        foreach (var tag in new[] { family, "humanoid", "body.flesh", "family."+family, "role", "habitat", "size", "locomotion.ground" }) Assert.True(id.Tags.Has(tag), pattern+":"+tag);
        Assert.False(id.Tags.Has("quest.cull"));
        Assert.Equal(family == "gnoll" ? "laughing_packs" : "horned_tribes", def.Faction);
        Assert.NotNull(a.Sim.World.Get<Drops>(body));
        var profile = a.Sim.World.Get<PhysicalBody>(body)!.Profile;
        var look = CoreContent.Registry.Require<AppearanceDef>(pattern);
        Assert.Equal(family, look.Body);
        Assert.Equal(profile.Width/profile.Height, look.Shape["breadth"], 6);
        Assert.Equal(profile.Depth/profile.Height, look.Shape["depth"], 6);
        Assert.Equal(0, look.Variation.Scale);
        Assert.Contains(CoreContent.Registry.Each<PoiDef>(), p => p.Inhabitants.Any(s=>s.Pattern==pattern) && id.Tags.Has("habitat."+p.Id));
        for (var i=0; i<450; i++) a.Sim.Tick();
        Assert.True(a.Hit(body,a.Owner), pattern+" did not land damage through AI");
    }

    [Fact]
    public void SeparateFactionsAllyOnWinterRoadsAndKeepTheirOwnHostility()
    {
        var a=DeliveryArena.Create();
        a.Sim.World.Get<Identity>(a.Caster)!.Faction="players";
        var gnoll=a.Body(12,10,faction:"laughing_packs",pattern:"gnoll_prowler");
        var kin=a.Body(15,10,faction:"horned_tribes",pattern:"beastkin_chaser");
        var goblin=a.Body(18,10,faction:"goblinkin",pattern:"goblin_skirmisher");
        Assert.True(Relations.IsAlly(a.Sim,gnoll,kin)); Assert.True(Relations.IsAlly(a.Sim,kin,gnoll));
        Assert.True(Relations.IsAlly(a.Sim,gnoll,goblin)); Assert.True(Relations.IsAlly(a.Sim,goblin,gnoll));
        foreach(var e in new[]{gnoll,kin})
        {
            Assert.True(Relations.IsHostile(a.Sim,a.Caster,e));
            Assert.True(Relations.IsHostile(a.Sim,e,a.Caster));
        }
    }

    [Fact]
    public void SmallScavengerAndMassiveBullwardenResistTheSameShoveDifferently()
    {
        static (double mass,float travel) Push(string pattern)
        {
            var a=DeliveryArena.Create();var e=a.Body(30,10,pattern:pattern);
            var at=a.Sim.World.Get<Transform>(e)!;var start=at.Position;
            var mass=Physicality.Mass(a.Sim,e);
            a.Run("""{"type":"move","kind":"knockback","distance":4}""",start,target:e);
            a.Tick(45);
            return (mass,Vector3.Distance(start,at.Position));
        }
        var small=Push("gnoll_bonepicker");var hunter=Push("gnoll_prowler");var bull=Push("beastkin_bullwarden");
        Assert.True(small.mass<hunter.mass && hunter.mass<bull.mass);
        Assert.True(small.travel>hunter.travel && hunter.travel>bull.travel*3);
        Assert.InRange(CoreContent.Registry.Require<PatternDef>("gnoll_prowler").Height/1.8,.9,1.1);
        Assert.True(CoreContent.Registry.Require<PatternDef>("beastkin_bullwarden").Height>2.5);
    }

    [Theory]
    [InlineData("gnoll_pyre","gnoll_packfather")]
    [InlineData("beastkin_hold","beastlord_khan")]
    public void HabitatChampionsAreLevelGatedAndRepeatable(string site,string champion)
    {
        string[] Generate(int level)
        {
            var content=ContentRegistry.Load(new[]{CoreContent.FindContentDir()},out _);
            content.With(new RegionOverrideDef{Id="hallows_home",X=0,Y=0,Archetype="hallows",Level=level,Faction="weftmarch_compact"});
            content.With(new ZoneOverrideDef{Id="ninewells",X=0,Y=0,Pois={new PoiPlacement{Poi=site,X=50,Y=50}}});
            var g=new WorldGenerator(content,Rng.Hash64("horn-and-hyena"));
            return g.Layout.ChunksOf(new ZoneCoord(0,0)).SelectMany(c=>g.GenerateChunk(c).Spawns)
                .Where(s=>s.Group?.Contains(site,StringComparison.Ordinal)==true)
                .Select(s=>$"{s.Pattern}:{s.X}:{s.Y}:{s.PackSize}").ToArray();
        }
        Assert.DoesNotContain(Generate(1),s=>s.StartsWith(champion+":"));
        var high=Generate(25);Assert.Contains(high,s=>s.StartsWith(champion+":"));Assert.Equal(high,Generate(25));
    }
}
