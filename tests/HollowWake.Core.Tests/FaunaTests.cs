using System.Numerics;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Movement;
using HollowWake.Core.Patterns;
using HollowWake.Core.World;
using HollowWake.Core.WorldGen;
using HollowWake.Core.Maths;
using Xunit;

namespace HollowWake.Core.Tests;

public class FaunaTests
{
    public static IEnumerable<object[]> Bodies() => CoreContent.Registry.Each<PatternDef>()
        .Where(p => p.Extends == "fauna_body").Select(p => new object[] { p.Id });

    [Theory, MemberData(nameof(Bodies))]
    public void WildlifeHasPhysicalScaleClassifiersAndReachableHabitats(string pattern)
    {
        var a = DeliveryArena.Create();
        var body = a.Body(20, 20, pattern: pattern, faction: "wilds");
        var id = a.Sim.World.Get<Identity>(body)!;
        foreach (var tag in new[] { "fauna", "beast", "body", "family", "role", "size", "habitat", "locomotion.ground" })
            Assert.True(id.Tags.Has(tag), pattern + ":" + tag);
        Assert.Equal("wilds", CoreContent.Registry.Require<PatternDef>(pattern).Faction);
        var physical = a.Sim.World.Get<PhysicalBody>(body)!.Profile;
        var look = CoreContent.Registry.Require<AppearanceDef>(pattern);
        Assert.Equal(physical.Width / physical.Height, look.Shape["breadth"], 6);
        Assert.Equal(physical.Depth / physical.Height, look.Shape["depth"], 6);
        Assert.Equal(0, look.Variation.Scale);
        Assert.Equal(physical.Height, a.Sim.World.Get<Transform>(body)!.Height, 5);
        Assert.True(Physicality.Mass(a.Sim, body) > 0);
        Assert.Contains(CoreContent.Registry.Each<BiomeDef>(), biome => id.Tags.Has("habitat." + biome.Id) &&
            (biome.Spawns.Any(s => s.Pattern == pattern) || biome.SpawnTables
            .Any(t => CoreContent.Registry.Require<SpawnTableDef>(t).Spawns.Any(s => s.Pattern == pattern))));
    }

    [Theory, MemberData(nameof(Bodies))]
    public void ProvisionalWildlifeBehaviorsFleeOrDeliverTheirRealCombatKits(string pattern)
    {
        var a = MinionArena.Create();
        a.OwnerStats.Attributes.SetBase("life.max", 100000); a.OwnerStats.FillResources();
        var def = CoreContent.Registry.Require<PatternDef>(pattern);
        var critter = def.Tags.Has("critter");
        var e = a.Foe(pattern, new Vector3(24, 20, 0), thinks: true);
        var behavior = a.Sim.World.Get<Behavior>(e)!;
        var start = a.At(e);
        if (critter)
        {
            Assert.Null(a.Sim.World.Get<Drops>(e));
            Assert.Empty(a.Sim.World.Get<WeaveBook>(e)!.Slots);
            // Even injury cannot turn this placeholder into an attacking critter.
            a.Sim.World.Get<Stats>(e)!.SetResource("life", 1);
            for (var i = 0; i < 90; i++) a.Sim.Tick();
            Assert.True(Vector3.Distance(a.At(e), a.At(a.Owner)) > Vector3.Distance(start, a.At(a.Owner)) + 1);
            Assert.False(a.Hit(e, a.Owner));
        }
        else
        {
            Assert.NotNull(a.Sim.World.Get<Drops>(e));
            if (behavior.BehaviorId == "skittish")
            {
                // Exercise the existing cornered-prey state, without pretending its flight planner
                // can detect corners. The AI overhaul owns choosing when defence is appropriate.
                behavior.State = "fight";
                behavior.Target = a.Owner;
            }
            for (var i = 0; i < 450; i++) a.Sim.Tick();
            Assert.True(a.Hit(e, a.Owner), pattern + " failed to use its own kit");
        }
    }

    [Fact]
    public void CullContractsOptIntoCombatantsAndNeverAmbientCritters()
    {
        var tags = CoreContent.Registry.Require<QuestTemplateDef>("cull_beasts").Slots["beast"].PatternTags;
        var eligible = CoreContent.Registry.Each<PatternDef>().Where(p => p.Tags.HasAll(tags)).ToArray();
        Assert.NotEmpty(eligible);
        Assert.DoesNotContain(eligible, p => p.Tags.Has("critter") || p.Id == "fauna_body");
        Assert.Contains(eligible, p => p.Id == "wolf");
        Assert.Contains(eligible, p => p.Id == "brown_bear");
        var generator = new WorldGenerator(CoreContent.Registry, Rng.Hash64("fauna-contracts"));
        var critters = CoreContent.Registry.Each<PatternDef>().Where(p => p.Tags.Has("critter")).Select(p => p.Id).ToHashSet();
        foreach (var zone in new[] { new ZoneCoord(0,0), new ZoneCoord(1,0), new ZoneCoord(3,2) })
            Assert.DoesNotContain(generator.Zone(zone).Quests.SelectMany(q => q.Objectives), o => o.Type == "kill" && critters.Contains(o.Target ?? ""));
    }

    [Fact]
    public void MaterialsAndDimensionsProduceDistinctMassAndLiveDisplacement()
    {
        static (double mass, float travel) Push(string pattern)
        {
            var a = DeliveryArena.Create(); var e = a.Body(30,10,pattern:pattern);
            var start = a.Sim.World.Get<Transform>(e)!.Position;
            var mass = Physicality.Mass(a.Sim,e);
            a.Run("""{"type":"move","kind":"knockback","distance":4}""",start,target:e);
            a.Tick(45);
            return (mass,Vector3.Distance(start,a.Sim.World.Get<Transform>(e)!.Position));
        }
        var hare=Push("meadow_hare"); var wolf=Push("wolf"); var boar=Push("boar");
        var bear=Push("brown_bear"); var ox=Push("plains_aurochs");
        Assert.True(hare.mass < wolf.mass && wolf.mass < boar.mass && boar.mass < bear.mass && bear.mass < ox.mass);
        Assert.True(hare.travel > wolf.travel && wolf.travel > boar.travel && boar.travel > bear.travel && bear.travel > ox.travel);
        Assert.Equal(boar,Push("boar"));
    }

    [Fact]
    public void WildsRelationDoesNotPretendToImplementPredation()
    {
        var a=DeliveryArena.Create();
        var wolf=a.Body(20,20,faction:"wilds",pattern:"wolf");
        var hare=a.Body(21,20,faction:"wilds",pattern:"meadow_hare");
        Assert.True(Relations.IsAlly(a.Sim,wolf,hare));
        a.Sim.World.Get<Identity>(a.Caster)!.Faction="players";
        Assert.True(Relations.IsHostile(a.Sim,hare,a.Caster));
    }
}
