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

public class GolemPrototypeTests(ITestOutputHelper output)
{
    private static readonly string[] Summons = { "stone_golem", "fire_golem", "ice_golem", "lightning_golem", "blood_golem" };
    public static IEnumerable<object[]> Bodies() => Summons.Append("amalgam_horror").Select(id => new object[] { id });
    public static IEnumerable<object[]> Contracts() => Summons.Select(id => new object[] { id });
    public static IEnumerable<object[]> Kits() => Bodies().SelectMany(row =>
        CoreContent.Registry.Require<PatternDef>((string)row[0]).Components["weaves"]!["list"]!.AsArray()
            .Select(skill => new object[] { row[0], skill!.GetValue<string>() }));

    [Theory, MemberData(nameof(Bodies))]
    public void ProfilesMatchActualMassAndPairedAppearance(string id)
    {
        var a = DeliveryArena.Create();
        var e = a.Body(20, 20, pattern: id);
        var p = a.Sim.World.Get<PhysicalBody>(e)!.Profile;
        var look = CoreContent.Registry.Require<AppearanceDef>(id);
        Assert.Equal("assembled_golem", look.Body);
        Assert.Equal(p.Width / p.Height, look.Shape["breadth"], 6);
        Assert.Equal(p.Depth / p.Height, look.Shape["depth"], 6);
        Assert.Equal(0, look.Variation.Scale);
        var density = p.Materials.Sum(m => m.Fraction * CoreContent.Registry.Require<BodyMaterialDef>(m.Material).Density);
        Assert.Equal(p.Width * p.Depth * p.Height * p.Fill * p.Heft * density, Physicality.Mass(a.Sim, e), 3);
        var tags = a.Sim.World.Get<Identity>(e)!.Tags;
        foreach (var tag in new[] { "construct", "family", "body", "assembly", "role", "size", "habitat", "locomotion.ground", "no_corpse" })
            Assert.True(tags.Has(tag), id + ":" + tag);
        output.WriteLine($"{id}: mass={Physicality.Mass(a.Sim, e):F3} kg");
        a.Sim.Service<DeathService>().Kill(a.Sim, e, Entity.None);
        Assert.False(Corpses.Is(a.Sim.World, e));
    }

    [Theory, MemberData(nameof(Kits))]
    public void EveryAttackSustainsDamageThroughItsRealSpawnPathAndReplays(string id, string skill)
    {
        string[] Run()
        {
            var a = MinionArena.Create();
            Entity actor, target;
            if (id == "amalgam_horror")
            {
                a.Sim.World.Get<Identity>(a.Owner)!.Faction = "players";
                actor = a.Sim.Service<PatternFactory>().Spawn(a.Sim, id, new Vector3(24, 20, 0));
                target = a.Owner;
                Assert.NotNull(a.Sim.World.Get<Drops>(actor));
            }
            else
            {
                a.Cast(a.Learn("summon_" + id));
                actor = Assert.Single(a.Minions());
                target = a.Foe("skeleton_warrior", new Vector3(24, 20, 0));
                Assert.Null(a.Sim.World.Get<Drops>(actor));
            }
            Assert.True(Relations.IsHostile(a.Sim, actor, target));
            var stats = a.Sim.World.Get<Stats>(target)!;
            stats.Attributes.SetBase("life.max", 100000); stats.FillResources();
            a.Sim.World.Get<WeaveBook>(actor)!.Slots.RemoveAll(s => s.Spec.Preset != skill);
            for (var i = 0; i < 500; i++) a.Sim.Tick();
            var hits = a.Events.OfType<DamageDealt>().Where(d => d.Source == actor && d.Target == target && d.Total > 0).ToArray();
            Assert.True(hits.Length >= 2, id + ":" + skill + " did not sustain hits");
            output.WriteLine($"{id}/{skill}: {hits.Length} hits, {hits.Sum(d => d.Total):F2} damage");
            return hits.Select(d => $"{d.Total:R}:{d.Crit}:{d.Roll:R}:{d.CastId}").ToArray();
        }
        Assert.Equal(Run(), Run());
    }

    [Theory, MemberData(nameof(Contracts))]
    public void NamedContractsFightRespawnAndReplaceExistingWardensWithoutRewards(string id)
    {
        var def = CoreContent.Registry.Require<PatternDef>(id);
        Assert.Equal("players", def.Faction);
        Assert.True(DeliveryArena.Create().Sim.Service<PatternFactory>().Resolve(id).Def.Tags.Has("habitat.bound_service"));
        Assert.DoesNotContain(CoreContent.Registry.Each<PoiDef>(), p => p.Inhabitants.Any(s => s.Pattern == id));
        Assert.DoesNotContain(CoreContent.Registry.Each<BiomeDef>(), b => b.Spawns.Any(s => s.Pattern == id));
        var a = MinionArena.Create();
        a.Cast(a.Learn("bind_stone_warden"));
        a.Cast(a.Learn("summon_" + id));
        var first = Assert.Single(a.Minions());
        Assert.Equal(id, a.Sim.World.Get<Identity>(first)!.Pattern);
        Assert.Equal("red", Relations.FactionOf(a.Sim, first));
        Assert.Equal(30, a.OwnerStats.Get("mana.reserved"));
        Assert.Null(a.Sim.World.Get<Drops>(first));
        var foe = a.Foe("skeleton_warrior", new Vector3(24, 20, 0));
        var stats = a.Sim.World.Get<Stats>(foe)!;
        stats.Attributes.SetBase("life.max", 100000); stats.FillResources();
        for (var i = 0; i < 300; i++) a.Sim.Tick();
        Assert.True(a.Hit(first, foe), id + " mixed kit");
        a.Sim.Service<DeathService>().Kill(a.Sim, first, a.Owner);
        Assert.DoesNotContain(a.Events.OfType<ExperienceGained>(), e => e.Entity == a.Owner);
        Assert.DoesNotContain(a.Events.OfType<ItemDropped>(), e => e.From == first);
        for (var i = 0; i < 180; i++) a.Sim.Tick();
        var second = Assert.Single(a.Minions());
        Assert.NotEqual(first, second);
        Assert.Equal(id, a.Sim.World.Get<Identity>(second)!.Pattern);
        Assert.Null(a.Sim.World.Get<Drops>(second));
        a.Cast(a.Learn("summon_bone_golem"));
        Assert.Equal("bone_golem", a.Sim.World.Get<Identity>(Assert.Single(a.Minions()))!.Pattern);
        a.Cast(a.Learn("bind_salvaged_golem"));
        Assert.Single(a.Minions());
        for (var i = 0; i < 200; i++) a.Sim.Tick();
        Assert.Single(a.Minions());
        Assert.Equal(30, a.OwnerStats.Get("mana.reserved"));
    }

    [Theory]
    [InlineData("fire_golem", "ember", .75)]
    [InlineData("ice_golem", "tide", .75)]
    [InlineData("lightning_golem", "storm", .6)]
    [InlineData("blood_golem", "umbra", .4)]
    [InlineData("amalgam_horror", "umbra", .4)]
    public void ExplicitResistanceReducesRealHitsWithoutMakingBodiesImmune(string id, string thread, double resist)
    {
        var a = DeliveryArena.Create();
        var victim = a.Body(20, 20, pattern: id);
        Assert.Equal(resist, a.Sim.World.Get<Stats>(victim)!.Get("resist." + thread), 6);
        a.Run($$"""{"type":"damage","amount":100,"threads":["{{thread}}"],"can_crit":false}""", new Vector3(20, 20, 0), target: victim);
        var hit = Assert.Single(a.Events.OfType<DamageDealt>(), e => e.Target == victim);
        Assert.Equal(100 * (1 - resist), hit.Total, 5);
    }

    [Fact]
    public void BloodGolemActuallyRecoversLifeFromItsAttacks()
    {
        var a = MinionArena.Create(); a.Cast(a.Learn("summon_blood_golem"));
        var body = Assert.Single(a.Minions());
        var stats = a.Sim.World.Get<Stats>(body)!;
        stats.SetResource("life", stats.ResourceMax("life") * .4);
        var target = a.Foe("skeleton_warrior", new Vector3(24, 20, 0));
        var victim = a.Sim.World.Get<Stats>(target)!;
        victim.Attributes.SetBase("life.max", 100000); victim.FillResources();
        for (var i = 0; i < 150; i++) a.Sim.Tick();
        var paid = a.Events.OfType<LeechPaid>().Where(e => e.Source == body && e.Resource == "life" && e.Kind == "leech").ToArray();
        Assert.NotEmpty(paid); Assert.All(paid, p => Assert.True(p.Amount > 0));
        output.WriteLine($"Blood leech: {paid.Sum(p => p.Amount):F3} life returned");
    }

    [Fact]
    public void NewBodiesRetainDistinctMassAndShoveResponses()
    {
        var distances = new List<float>();
        foreach (var id in new[] { "fire_golem", "ice_golem", "stone_golem" })
        {
            var a = DeliveryArena.Create(); var e = a.Body(30, 10, pattern: id);
            var start = a.Sim.World.Get<Transform>(e)!.Position;
            a.Run("""{"type":"move","kind":"knockback","distance":8}""", start, target: e);
            a.Tick(45);
            var distance = Vector3.Distance(start, a.Sim.World.Get<Transform>(e)!.Position);
            distances.Add(distance); output.WriteLine($"{id}: shove={distance:F6} m");
        }
        Assert.True(distances[0] > distances[1] && distances[1] > distances[2] && distances[2] > 0);
    }

    [Fact]
    public void AmalgamationSiteAppearsNaturallyAndActuallyPopulatesAtItsLevel()
    {
        const string site = "amalgam_suture_works";
        var natural = new WorldGenerator(CoreContent.Registry, Rng.Hash64("aureth"));
        var found = false;
        for (var y = -64; y <= 64 && !found; y++)
            for (var x = -64; x <= 64 && !found; x++)
                foreach (var p in natural.PoisOf(new ZoneCoord(x, y)).Where(p => p.Def.Id == site))
                {
                    var inhabitants = natural.Layout.ChunksOf(new ZoneCoord(x, y))
                        .SelectMany(c => natural.GenerateChunk(c).Spawns)
                        .Where(s => s.Pattern == "amalgam_horror").ToArray();
                    Assert.NotEmpty(inhabitants);
                    output.WriteLine($"{site}: zone({x},{y}), at ({p.X},{p.Y}), level {p.Level}, actual spawn points {inhabitants.Length}");
                    found = true;
                }
        Assert.True(found);
        string[] Populate(int level)
        {
            var content = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out _);
            content.With(new RegionOverrideDef { Id = "hallows_home", X = 0, Y = 0, Archetype = "hallows", Level = level, Faction = "weftmarch_compact" });
            content.With(new ZoneOverrideDef { Id = "ninewells", X = 0, Y = 0, Pois = { new PoiPlacement { Poi = site, X = 50, Y = 50 } } });
            var g = new WorldGenerator(content, Rng.Hash64("golem-amalgamation"));
            return g.Layout.ChunksOf(new ZoneCoord(0, 0)).SelectMany(c => g.GenerateChunk(c).Spawns)
                .Where(s => s.Pattern == "amalgam_horror").Select(s => $"{s.Pattern}:{s.X}:{s.Y}:{s.PackSize}").ToArray();
        }
        Assert.NotEmpty(Populate(1));
        Assert.Equal(Populate(1), Populate(1));
    }
}
