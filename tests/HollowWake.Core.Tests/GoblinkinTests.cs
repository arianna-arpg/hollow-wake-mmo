using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using HollowWake.Core.Attributes;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Maths;
using HollowWake.Core.Movement;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.World;
using HollowWake.Core.WorldGen;
using Xunit;

namespace HollowWake.Core.Tests;

public class GoblinkinTests
{
    public static IEnumerable<object[]> Bodies() => CoreContent.Registry.Each<PatternDef>()
        .Where(p => p.Extends == "goblinkin_body").Select(p => new object[] { p.Id });

    [Theory, MemberData(nameof(Bodies))]
    public void WarbandBodiesHaveHabitatAppearancePhysicalityAndWorkingAi(string pattern)
    {
        var a = MinionArena.Create();
        a.OwnerStats.Attributes.SetBase("life.max", 100000);
        a.OwnerStats.FillResources();
        var body = a.Foe(pattern, new Vector3(24, 20, 0), thinks: true);
        var id = a.Sim.World.Get<Identity>(body)!;
        foreach (var tag in new[] { "goblinkin", "body", "family", "role", "habitat", "size" }) Assert.True(id.Tags.Has(tag), pattern + ":" + tag);
        Assert.NotNull(a.Sim.World.Get<PhysicalBody>(body));
        Assert.True(Physicality.Mass(a.Sim, body) > 0);
        Assert.NotNull(a.Sim.World.Get<Drops>(body));
        var content = CoreContent.Registry;
        Assert.Equal("goblinkin", content.Require<PatternDef>(pattern).Faction);
        var look = content.Require<AppearanceDef>(pattern);
        var profile = a.Sim.World.Get<PhysicalBody>(body)!.Profile;
        Assert.Equal(profile.Width / profile.Height, look.Shape["breadth"], 6);
        Assert.Equal(profile.Depth / profile.Height, look.Shape["depth"], 6);
        Assert.Equal(0, look.Variation.Scale);
        var populations = ParkedPopulationContent.Registry;
        var authored = populations.Each<PoiDef>().SelectMany(p => p.Inhabitants)
            .Concat(populations.Each<BiomeDef>().SelectMany(b => b.SpawnTables).Distinct().SelectMany(t => populations.Require<SpawnTableDef>(t).Spawns));
        Assert.Contains(authored, s => s.Pattern == pattern);
        for (var i = 0; i < 450; i++) a.Sim.Tick();
        Assert.True(a.Hit(body, a.Owner), pattern + " did not land damage through its own AI kit");
    }

    [Fact]
    public void MassUsesVolumeMixtureAndAllModifierFormsThenTracksUniformResize()
    {
        var a = DeliveryArena.Create();
        var body = a.Body(12, 10, pattern: "goblin_sapper");
        var p = a.Sim.World.Get<PhysicalBody>(body)!.Profile;
        var expected = p.Width * p.Depth * p.Height * p.Fill * p.Heft * (1000 * .86 + 650 * .12 + 7800 * .02);
        Assert.Equal(expected, Physicality.Mass(a.Sim, body), 3);
        var at = a.Sim.World.Get<Transform>(body)!;
        at.Radius *= 2; at.Height *= 2;
        Assert.Equal(expected * 8, Physicality.Mass(a.Sim, body), 2);
        var stats = a.Sim.World.Get<Stats>(body)!;
        stats.Attributes.Add(new Modifier { Attribute = "mass", Value = 10 });
        stats.Attributes.Add(new Modifier { Attribute = "mass", Form = ModifierForm.Increased, Value = .5 });
        stats.Attributes.Add(new Modifier { Attribute = "mass", Form = ModifierForm.More, Value = .2 });
        Assert.Equal((expected * 8 + 10) * 1.5 * 1.2, Physicality.Mass(a.Sim, body), 2);
        stats.Attributes.Add(new Modifier { Attribute = "mass", Form = ModifierForm.Override, Value = 12 });
        Assert.Equal(12, Physicality.Mass(a.Sim, body));
    }

    [Fact]
    public void LegacyMassDefaultsAndOverridesWorkAndPoiseBreakRemovesAnchoring()
    {
        var a = DeliveryArena.Create();
        var e = a.Body(12, 10);
        var s = a.Sim.World.Get<Stats>(e)!;
        Assert.Equal(80, Physicality.Mass(a.Sim, e));
        s.Attributes.SetBase("mass", 160);
        Assert.Equal(160, Physicality.Mass(a.Sim, e));
        s.Attributes.SetBase("poise.max", 100); s.SetResource("poise", 50);
        Assert.Equal(3.2, Physicality.EffectiveWeight(a.Sim, e), 6);
        s.Flags.Add("pool.poise.broken");
        Assert.Equal(2, Physicality.EffectiveWeight(a.Sim, e));
    }

    [Theory]
    [InlineData("knockback")]
    [InlineData("pull")]
    [InlineData("buffet")]
    public void SharedShovePathResistsHeavyBodiesAndPreservesSeededReplay(string kind)
    {
        static float Travel(string kind, double mass)
        {
            var a = DeliveryArena.Create(); var e = a.Body(30, 10);
            a.Sim.World.Get<Stats>(e)!.Attributes.SetBase("mass", mass);
            var start = a.Sim.World.Get<Transform>(e)!.Position;
            a.Run($$"""{"type":"move","kind":"{{kind}}","distance":4}""", start, target:e);
            a.Tick(45);
            return Vector3.Distance(start, a.Sim.World.Get<Transform>(e)!.Position);
        }
        var normal = Travel(kind, 80); var heavy = Travel(kind, 160);
        Assert.InRange(normal, 3.8f, 4.2f);
        Assert.InRange(heavy, 1.8f, 2.2f);
        Assert.Equal(heavy, Travel(kind, 160));
    }

    [Fact]
    public void LargePushersHaveCappedAuthorityAndTinyPullsStopBeforeTheAnchor()
    {
        var a = DeliveryArena.Create(); var e = a.Body(16, 10);
        a.Sim.World.Get<Stats>(a.Caster)!.Attributes.SetBase("mass", 100000);
        Assert.Equal(2.75, Physicality.ShoveScale(a.Sim, a.Caster, e), 6);
        a.Sim.World.Get<Stats>(e)!.Attributes.SetBase("mass", 1);
        a.Run("""{"type":"move","kind":"pull","distance":20}""", new Vector3(16,10,0), target:e);
        a.Tick(45);
        Assert.InRange(a.Sim.World.Get<Transform>(e)!.Position.X, 10.9f, 11.2f);
    }

    [Fact]
    public void RaidersWithdrawWhenWoundedAndFactionRelationsAreSymmetric()
    {
        var a = DeliveryArena.Create(); var e = a.Body(13, 10, faction:"goblinkin", pattern:"goblin_skirmisher");
        var friend = a.Body(14, 12, faction:"goblinkin", pattern:"goblin_brute");
        a.Sim.World.Get<Identity>(a.Caster)!.Faction = "players";
        Assert.True(Relations.IsHostile(a.Sim, a.Caster, e));
        Assert.True(Relations.IsHostile(a.Sim, e, a.Caster));
        Assert.False(Relations.IsHostile(a.Sim, e, friend));
        a.Sim.World.Add(e, new Behavior { BehaviorId = "goblin_raider", Home = new Vector3(13,10,0) });
        var s = a.Sim.World.Get<Stats>(e)!; s.SetResource("life", s.ResourceMax("life") * .2);
        a.Tick(25);
        Assert.Equal("withdraw", a.Sim.World.Get<Behavior>(e)!.State);
    }

    [Fact]
    public void FortGatesTheColossusByLevelAndPopulationReplays()
    {
        static string[] Generate(int level)
        {
            var content = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out _);
            content.With(new RegionOverrideDef { Id="hallows_home", X=0, Y=0, Archetype="hallows", Level=level, Faction="weftmarch_compact" });
            content.With(new ZoneOverrideDef { Id="ninewells", X=0, Y=0, Pois={new PoiPlacement {Poi="scrap_fort",X=50,Y=50}} });
            var g = new WorldGenerator(content,Rng.Hash64("warren-fort"));
            return g.Layout.ChunksOf(new ZoneCoord(0,0)).SelectMany(c=>g.GenerateChunk(c).Spawns)
                .Where(s=>s.Group?.Contains("scrap_fort",StringComparison.Ordinal)==true)
                .Select(s=>$"{s.Pattern}:{s.X}:{s.Y}:{s.PackSize}").ToArray();
        }
        Assert.DoesNotContain(Generate(1), s=>s.StartsWith("goblin_colossus:"));
        var high=Generate(25);
        Assert.Contains(high, s=>s.StartsWith("goblin_colossus:"));
        Assert.Equal(high,Generate(25));
    }
    [Fact]
    public void CinderKegActuallyExplodesAgainstAnEnemyAndSparesAllies()
    {
        var a = MinionArena.Create();
        var foe = a.Foe("goblin_brute",new Vector3(26,20,0));
        var ally = a.Sim.Service<PatternFactory>().Spawn(a.Sim,"goblin_skirmisher",new Vector3(26,21,0),new SpawnOptions {Faction="red"});
        a.Sim.World.Remove<Behavior>(ally);
        a.Cast(a.Learn("goblin_cinder_keg"),foe,a.At(foe));
        for(var i=0;i<100;i++) a.Sim.Tick();
        Assert.True(a.Hit(a.Owner,foe));
        Assert.False(a.Hit(a.Owner,ally));
    }

    [Fact]
    public void AuthorityModifiersAffectPlainEffectsAndRarityMassTracksVolume()
    {
        var a=DeliveryArena.Create(); var e=a.Body(12,10);
        a.Sim.World.Get<Stats>(a.Caster)!.Attributes.Add(new Modifier {Attribute="shove.authority",Form=ModifierForm.More,Value=1});
        a.Run("""{"type":"move","kind":"knockback","distance":2}""",new Vector3(12,10,0),target:e);
        a.Tick(45);
        Assert.InRange(a.Sim.World.Get<Transform>(e)!.Position.X,15.8f,16.2f);
        var factory=a.Sim.Service<PatternFactory>();
        var normal=factory.Spawn(a.Sim,"goblin_brute",new Vector3(20,20,0),new SpawnOptions {Rarity="normal"});
        var rare=factory.Spawn(a.Sim,"goblin_brute",new Vector3(25,20,0),new SpawnOptions {Rarity="rare"});
        var ratio=a.Sim.World.Get<Transform>(rare)!.Height/a.Sim.World.Get<Transform>(normal)!.Height;
        Assert.True(ratio>1);
        Assert.Equal(Physicality.Mass(a.Sim,normal)*Math.Pow(ratio,3),Physicality.Mass(a.Sim,rare),2);
    }

    [Theory]
    [InlineData("width",0)]
    [InlineData("depth",-1)]
    [InlineData("height",3)]
    [InlineData("fill",1.2)]
    [InlineData("heft",0)]
    public void PhysicalValidationRejectsInvalidInheritedOverrides(string field,double value)
    {
        var content=ContentRegistry.Load(new[]{CoreContent.FindContentDir()},out _);
        var basis=content.Require<PatternDef>("goblin_skirmisher");
        var child=new PatternDef {Id="bad_body",Extends=basis.Id,Radius=basis.Radius,Height=basis.Height,
            Components={ ["physical"]=new JsonObject{[field]=value} }};
        content.With(child);
        var report=new ValidationReport();
        ContentKinds.TryGet("patterns",out var kind);
        kind!.Validate!(child,new ValidationContext(content,report,"patterns",child.Id,null));
        Assert.True(report.HasErrors,report.ToString());
    }

    [Fact]
    public void PhysicalValidationRejectsMissingMaterialsAndWrongMixtureTotals()
    {
        foreach(var material in new[]{"flesh","unobtainium"})
        {
            var content=ContentRegistry.Load(new[]{CoreContent.FindContentDir()},out _);
            var basis=content.Require<PatternDef>("goblin_skirmisher");
            var child=new PatternDef {Id="bad_mix",Extends=basis.Id,Radius=basis.Radius,Height=basis.Height,
                Components={ ["physical"]=JsonNode.Parse($$"""{"materials":[{"material":"{{material}}","fraction":0.5}]}""")!.AsObject() }};
            content.With(child);
            var report=new ValidationReport();ContentKinds.TryGet("patterns",out var kind);
            kind!.Validate!(child,new ValidationContext(content,report,"patterns",child.Id,null));
            Assert.True(report.HasErrors);
        }
    }

}
