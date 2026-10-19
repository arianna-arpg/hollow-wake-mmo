using System.Numerics;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Movement;
using HollowWake.Core.Patterns;
using HollowWake.Core.Content;
using HollowWake.Core.Maths;
using HollowWake.Core.World;
using HollowWake.Core.WorldGen;
using Xunit;

namespace HollowWake.Core.Tests;

public class OrcTests
{
    public static IEnumerable<object[]> Bodies() => CoreContent.Registry.Each<PatternDef>()
        .Where(p => p.Extends == "orc_body").Select(p => new object[] { p.Id });

    [Theory, MemberData(nameof(Bodies))]
    public void OrcKitsFightAndTheirPhysicalProfilesMatchTheirAuthoredSilhouettes(string pattern)
    {
        var a = MinionArena.Create();
        a.OwnerStats.Attributes.SetBase("life.max", 100000); a.OwnerStats.FillResources();
        var body = a.Foe(pattern, new Vector3(24,20,0), thinks:true);
        var id = a.Sim.World.Get<Identity>(body)!;
        foreach (var tag in new[] { "orc", "body.flesh", "family.orc", "role", "habitat", "size" }) Assert.True(id.Tags.Has(tag), pattern+":"+tag);
        var def = CoreContent.Registry.Require<PatternDef>(pattern);
        Assert.Equal("orc_warbands", def.Faction);
        Assert.NotNull(a.Sim.World.Get<Drops>(body));
        var profile = a.Sim.World.Get<PhysicalBody>(body)!.Profile;
        var look = CoreContent.Registry.Require<AppearanceDef>(pattern);
        Assert.Equal("orc", look.Body);
        Assert.Equal(profile.Width/profile.Height, look.Shape["breadth"], 6);
        Assert.Equal(profile.Depth/profile.Height, look.Shape["depth"], 6);
        Assert.Equal(0, look.Variation.Scale);
        Assert.Contains(ParkedPopulationContent.Registry.Each<PoiDef>(), p => p.Inhabitants.Any(s=>s.Pattern==pattern) && id.Tags.Has("habitat."+p.Id));
        for (var i=0; i<450; i++) a.Sim.Tick();
        Assert.True(a.Hit(body,a.Owner), pattern+" did not land damage through AI");
    }

    [Fact]
    public void OrcsAllyWithGoblinsButBothTreatPlayersAsHostile()
    {
        var a=DeliveryArena.Create();
        a.Sim.World.Get<Identity>(a.Caster)!.Faction="players";
        var orc=a.Body(12,10,faction:"orc_warbands",pattern:"orc_grunt");
        var goblin=a.Body(14,10,faction:"goblinkin",pattern:"goblin_skirmisher");
        Assert.True(Relations.IsAlly(a.Sim,orc,goblin));
        Assert.True(Relations.IsAlly(a.Sim,goblin,orc));
        Assert.True(Relations.IsHostile(a.Sim,a.Caster,orc));
        Assert.True(Relations.IsHostile(a.Sim,orc,a.Caster));
        Assert.True(Relations.IsHostile(a.Sim,goblin,a.Caster));
        foreach (var faction in new[] { "wilds", "the_loosed" })
        {
            var other=a.Body(16,10,faction:faction);
            Assert.True(Relations.IsHostile(a.Sim,orc,other));
            Assert.True(Relations.IsHostile(a.Sim,other,orc));
        }
    }

    [Fact]
    public void OrdinaryStatureAndLiveShovesDistinguishGoblinOrcAndIronhide()
    {
        static (float height,double mass,float travel) Push(string pattern)
        {
            var a=DeliveryArena.Create();var e=a.Body(30,10,pattern:pattern);
            var at=a.Sim.World.Get<Transform>(e)!;var start=at.Position;
            var mass=Physicality.Mass(a.Sim,e);
            a.Run("""{"type":"move","kind":"knockback","distance":4}""",start,target:e);
            a.Tick(45);
            return (at.Height,mass,Vector3.Distance(start,at.Position));
        }
        var goblin=Push("goblin_skirmisher");var orc=Push("orc_grunt");var iron=Push("orc_bulwark");
        var referenceHeight = CoreContent.Registry.Rules.Number("physicality.reference_height", 0);
        Assert.Equal(referenceHeight,CoreContent.Registry.Require<PatternDef>("skeleton_warrior").Height,5);
        Assert.InRange(goblin.height/referenceHeight, .45, .55);
        Assert.InRange(orc.height/referenceHeight, 1, 1.15);
        Assert.True(orc.mass>goblin.mass*5);
        Assert.True(iron.mass>orc.mass);
        Assert.True(goblin.travel>orc.travel*3);
        Assert.True(orc.travel>iron.travel);
    }

    [Fact]
    public void SaddledGnashersHaveRoomForAGoblinAndKeepTheWhelpSmall()
    {
        var a=DeliveryArena.Create();
        var small=a.Body(12,10,pattern:"gnasher_whelp");
        var gob=a.Body(14,10,pattern:"goblin_skirmisher");
        foreach(var pattern in new[]{"saddle_gnasher","ironjaw_gnasher"})
        {
            var e=a.Body(20,10,pattern:pattern);var p=a.Sim.World.Get<PhysicalBody>(e)!.Profile;
            Assert.True(p.Width*.48>a.Sim.World.Get<PhysicalBody>(gob)!.Profile.Width);
            Assert.True(Physicality.Mass(a.Sim,e)>Physicality.Mass(a.Sim,gob)*10);
            Assert.Contains("saddle",CoreContent.Registry.Require<AppearanceDef>(pattern).Features);
        }
        Assert.InRange(a.Sim.World.Get<Transform>(small)!.Height,.35f,.37f);
    }
    [Fact]
    public void HoldsGateChampionsAndMixedWarbandsReplayDeterministically()
    {
        static string[] Generate(int level)
        {
            var content=ParkedPopulationContent.Create();
            content.With(new RegionOverrideDef{Id="hallows_home",X=0,Y=0,Archetype="hallows",Level=level,Faction="weftmarch_compact"});
            content.With(new ZoneOverrideDef{Id="ninewells",X=0,Y=0,Pois={new PoiPlacement{Poi="orc_hold",X=50,Y=50}}});
            var g=new WorldGenerator(content,Rng.Hash64("iron-tusk-hold"));
            return g.Layout.ChunksOf(new ZoneCoord(0,0)).SelectMany(c=>g.GenerateChunk(c).Spawns)
                .Where(s=>s.Group?.Contains("orc_hold",StringComparison.Ordinal)==true)
                .Select(s=>$"{s.Pattern}:{s.X}:{s.Y}:{s.PackSize}").ToArray();
        }
        Assert.DoesNotContain(Generate(1),s=>s.StartsWith("orc_warlord:"));
        var high=Generate(25);
        Assert.Contains(high,s=>s.StartsWith("orc_warlord:"));
        Assert.Contains(high,s=>s.StartsWith("goblin_sapper:"));
        Assert.Equal(high,Generate(25));
    }

}
