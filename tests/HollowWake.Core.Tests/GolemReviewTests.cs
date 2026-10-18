using System.Numerics;
using System.Text.Json.Nodes;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Maths;
using HollowWake.Core.Movement;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.Territory;
using HollowWake.Core.World;
using HollowWake.Core.WorldGen;
using Xunit;
using Xunit.Abstractions;

namespace HollowWake.Core.Tests;

public class GolemReviewTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("golem_kiln")]
    [InlineData("golem_quarry")]
    [InlineData("golem_foundry")]
    [InlineData("golem_sawmill")]
    [InlineData("amalgam_suture_works")]
    public void LoadedWorksExplicitlyKeepSiteLevelThroughBirthAndRespawn(string site)
    {
        foreach (var regionLevel in new[] { 1, 21 })
        {
            var content = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out _);
            var def = content.Require<PoiDef>(site);
            Assert.All(def.Inhabitants, inhabitant => Assert.True(inhabitant.UseSiteLevel));
            content.With(new RegionOverrideDef { Id = "hallows_home", X = 0, Y = 0, Archetype = "hallows", Level = regionLevel, Faction = "weftmarch_compact" });
            content.With(new ZoneOverrideDef { Id = "ninewells", X = 0, Y = 0, Pois = { new PoiPlacement { Poi = site, X = 50, Y = 50 } } });
            var generator = new WorldGenerator(content, Rng.Hash64("golem-residents"));
            var sim = ZoneBuilder.CreateZone(content, generator, new WorldState(new GenerationOverlays()), new ZoneCoord(0, 0));
            ZoneBuilder.LoadZone(sim); sim.Tick();
            var spawners = sim.World.Query<Spawner>().Where(s => s.C1.Group?.Contains(site, StringComparison.Ordinal) == true && def.Inhabitants.Any(i => i.Pattern == s.C1.Pattern)).ToArray();
            Assert.NotEmpty(spawners);
            var residents = spawners.SelectMany(s => s.C1.Alive).ToArray(); Assert.NotEmpty(residents);
            Assert.All(residents, e => Assert.Equal(regionLevel + def.Level, sim.World.Get<Stats>(e)!.Level));
            foreach (var resident in residents) sim.Service<DeathService>().Kill(sim, resident, Entity.None);
            foreach (var spawner in spawners) spawner.C1.RespawnAt = sim.Clock.Time;
            sim.Tick();
            var replacements = spawners.SelectMany(s => s.C1.Alive).ToArray();
            Assert.Equal(residents.Length, replacements.Length);
            Assert.All(replacements, e =>
            {
                Assert.DoesNotContain(e, residents);
                Assert.Equal(regionLevel + def.Level, sim.World.Get<Stats>(e)!.Level);
            });
            output.WriteLine($"{site}: region {regionLevel}, {residents.Length} initial and respawned residents at explicit site level {regionLevel + def.Level}");
        }
    }

    [Theory]
    [InlineData("unbound_constructs", "clay_golem")]
    [InlineData("grafted_court", "amalgam_horror")]
    public void WorksAndCourtHaveReciprocalStancesAndActuallyReturnFire(string faction, string pattern)
    {
        var sim = ZoneBuilder.CreateArena(CoreContent.Registry, 41);
        foreach (var other in new[] { "players", "wilds", "the_loosed", "tangled", "remnant" })
        {
            Assert.Equal(Stance.Hostile, Relations.StanceBetween(sim, faction, other));
            Assert.Equal(Stance.Hostile, Relations.StanceBetween(sim, other, faction));
        }
        foreach (var enemy in new[] { "hob", "remnant_shade" })
        {
            var a = MinionArena.Create();
            var actor = a.Sim.Service<PatternFactory>().Spawn(a.Sim, pattern, new Vector3(60, 60, 0));
            var target = a.Sim.Service<PatternFactory>().Spawn(a.Sim, enemy, new Vector3(63, 60, 0));
            a.Sim.World.Remove<Behavior>(target);
            var health = a.Sim.World.Get<Stats>(target)!;
            health.Attributes.SetBase("life.max", 100000); health.FillResources();
            for (var i = 0; i < 400; i++) a.Sim.Tick();
            Assert.True(a.Hit(actor, target), pattern + " failed to target " + enemy);
        }
    }

    [Fact]
    public void SparseSitesUseRegionsAndWeightsWithoutExcludingShrinesAcrossTheZone()
    {
        var sites = CoreContent.Registry.Each<PoiDef>().Where(p => p.Tags.Has("golem_site") || p.Tags.Has("amalgam_site")).ToArray();
        Assert.All(sites, p => { Assert.False(p.Regions.IsEmpty); Assert.InRange(p.MinSpacing, 2 * p.Radius, 70); });
        var g = new WorldGenerator(CoreContent.Registry, Rng.Hash64("aureth"));
        var witnesses = new HashSet<string>();
        for (var y = -32; y <= 32 && witnesses.Count < sites.Length; y++)
            for (var x = -32; x <= 32 && witnesses.Count < sites.Length; x++)
            {
                var zone = new ZoneCoord(x, y);
                var local = g.PoisOf(zone).ToArray();
                foreach (var p in local.Where(p => sites.Any(s => s.Id == p.Def.Id)))
                    foreach (var other in local.Where(o => o.Id != p.Id))
                    {
                        var distance = Math.Sqrt(Math.Pow(p.X - other.X, 2) + Math.Pow(p.Y - other.Y, 2));
                        if (distance < 180 && witnesses.Add(p.Def.Id))
                            output.WriteLine($"{p.Def.Id} coexists with {other.Def.Id} at {distance:F1} m in {zone}");
                    }
            }
        Assert.Equal(sites.Length, witnesses.Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(20)]
    public void WardenRetainsLegacyStatsWhileItsExplicitMassAndAppearanceAreDeliberate(int level)
    {
        var content = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out _);
        // Exact pre-PR #39 block from 90cb7fc; independent of the migrated pattern/template.
        content.With(new PatternDef
        {
            Id = "t_original_warden",
            Extends = "creature",
            Faction = "players",
            Radius = .7,
            Height = 2.2,
            Components = { ["stats"] = JsonNode.Parse("""{"attributes":{"might":16,"grace":5,"will":4,"vigor":18,"move.speed":4.2,"armor":40,"weapon.damage.min":7,"weapon.damage.max":12,"weapon.attack.time":1.3},"modifiers":[{"attribute":"damage.taken","form":"more","value":-0.1}]}""")!.AsObject() }
        });
        var sim = ZoneBuilder.CreateArena(content, 1);
        var factory = sim.Service<PatternFactory>();
        var old = factory.Spawn(sim, "t_original_warden", new Vector3(20, 20, 0), new SpawnOptions { Level = level });
        var current = factory.Spawn(sim, "stone_warden", new Vector3(30, 20, 0), new SpawnOptions { Level = level });
        var a = sim.World.Get<Stats>(old)!; var b = sim.World.Get<Stats>(current)!;
        foreach (var key in new[] { "might", "grace", "will", "vigor", "move.speed", "armor", "weapon.damage.min", "weapon.damage.max", "weapon.attack.time", "damage.taken", "life.max", "mana.max", "mana.regen", "stamina.max", "stamina.regen" })
            Assert.Equal(a.Get(key), b.Get(key), 6);
        Assert.Equal(80, Physicality.Mass(sim, old), 3);
        Assert.Equal(1905.904, Physicality.Mass(sim, current), 3);
        Assert.Equal("assembled_golem", content.Require<AppearanceDef>("stone_warden").Body);
        Assert.Equal(0, content.Require<AppearanceDef>("stone_warden").Variation.Scale);
        output.WriteLine($"Warden L{level}: life {b.Get("life.max")}, mana {b.Get("mana.max")}, regen {b.Get("mana.regen")}/{b.Get("stamina.regen")}; mass {Physicality.Mass(sim, current)} kg");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    public void NamedBindingsUseOwnerLevelAndScaledCombatStats(int level)
    {
        foreach (var row in GolemPrototypeTests.Contracts())
        {
            var id = (string)row[0]; var a = MinionArena.Create(); a.OwnerStats.Level = level;
            a.Cast(a.Learn("summon_" + id));
            var stats = a.Sim.World.Get<Stats>(Assert.Single(a.Minions()))!;
            Assert.Equal(level, stats.Level);
            Assert.Equal(7 + (level - 1) * .7, stats.Get("weapon.damage.min"), 5);
            Assert.InRange(stats.Get("life.max"), 140 + (level - 1) * 14, 300 + (level - 1) * 14);
            output.WriteLine($"{id} owner L{level}: life {stats.Get("life.max"):F2}, weapon {stats.Get("weapon.damage.min"):F2}-{stats.Get("weapon.damage.max"):F2}");
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    public void SalvageRollsShareProgressionInsteadOfASeveralFoldLifeLottery(int level)
    {
        var sim = ZoneBuilder.CreateArena(CoreContent.Registry, 1); var life = new List<double>();
        foreach (var row in GolemContentTests.Bodies().Where(r => (string)r[0] != "stone_warden"))
        {
            var e = sim.Service<PatternFactory>().Spawn(sim, (string)row[0], new Vector3(20, 20, 0), new SpawnOptions { Level = level });
            var stats = sim.World.Get<Stats>(e)!; life.Add(stats.Get("life.max"));
            Assert.Equal(7 + (level - 1) * .7, stats.Get("weapon.damage.min"), 5);
        }
        Assert.InRange(life.Max() / life.Min(), 1, 1.35);
        output.WriteLine($"Salvage L{level}: life {life.Min():F1}-{life.Max():F1}; eight kits share weapon progression");
    }

    [Fact]
    public void LegacyWardenFightsThroughItsActualOwnerBoundPath()
    {
        var a = MinionArena.Create(); a.Sim.World.Get<Identity>(a.Owner)!.Faction = "players";
        a.Cast(a.Learn("bind_stone_warden")); var warden = Assert.Single(a.Minions());
        var foe = a.Sim.Service<PatternFactory>().Spawn(a.Sim, "skeleton_warrior", new Vector3(24, 20, 0));
        a.Sim.World.Remove<Behavior>(foe);
        var stats = a.Sim.World.Get<Stats>(foe)!; stats.Attributes.SetBase("life.max", 100000); stats.FillResources();
        Assert.True(Relations.IsHostile(a.Sim, warden, foe));
        for (var i = 0; i < 500; i++) a.Sim.Tick();
        Assert.True(a.Hit(warden, foe));
    }
}
