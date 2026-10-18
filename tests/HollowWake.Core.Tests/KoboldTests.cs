using System.Numerics;
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
    public void NativeFactionHasAnExplicitReciprocalStanceForEveryShippedFaction()
    {
        var content = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out _);
        var sim = ZoneBuilder.CreateArena(content);
        var kobolds = content.Require<FactionDef>("kobold_scaleclans");
        foreach (var faction in content.Each<FactionDef>())
        {
            Assert.True(kobolds.Relations.TryGetValue(faction.Id, out var expected), "Unreviewed faction: " + faction.Id);
            Assert.Equal(expected, Relations.StanceBetween(sim, kobolds.Id, faction.Id));
            Assert.Equal(expected, Relations.StanceBetween(sim, faction.Id, kobolds.Id));
        }
        Assert.Equal(Stance.Hostile, kobolds.Relations["the_loosed"]);
        Assert.Equal(Stance.Neutral, kobolds.Relations["wilds"]);
        Assert.Equal(Stance.Allied, kobolds.Relations[kobolds.Id]);
    }

    [Fact]
    public void LoosedAndKoboldsAcquireAndDamageEachOtherWithoutAPlayerOrInjectedTarget()
    {
        var sim = ZoneBuilder.CreateArena(CoreContent.Registry, 127);
        var factory = sim.Service<PatternFactory>();
        var kobold = factory.Spawn(sim, "kobold_tunnelguard", new Vector3(20, 20, 0), new SpawnOptions { RollRarity = false });
        var loosed = factory.Spawn(sim, "loosed_brigand", new Vector3(23, 20, 0), new SpawnOptions { RollRarity = false });
        Assert.Equal("the_loosed", Relations.FactionOf(sim, loosed));
        foreach (var e in new[] { kobold, loosed })
        {
            var stats = sim.World.Get<Stats>(e)!;
            stats.Attributes.SetBase("life.max", 100000);
            stats.FillResources();
        }
        var hits = new List<DamageDealt>();
        sim.Events.Subscribe<DamageDealt>(hits.Add);
        sim.Run(300);
        Assert.Contains(hits, h => h.Source == kobold && h.Target == loosed && h.Total > 0);
        Assert.Contains(hits, h => h.Source == loosed && h.Target == kobold && h.Total > 0);
    }

    [Fact]
    public void LightAndArmoredBodiesUseCanonicalMassAndBoundedLiveShove()
    {
        (double mass, float travel) Push(string pattern)
        {
            var a = DeliveryArena.Create();
            var e = a.Body(30, 10, pattern: pattern);
            var at = a.Sim.World.Get<Transform>(e)!;
            var start = at.Position;
            var mass = Physicality.Mass(a.Sim, e);
            Assert.Equal(mass, a.Sim.World.Get<Stats>(e)!.Get("mass"), 8);
            var scale = Physicality.ShoveScale(a.Sim, a.Caster, e);
            var ceiling = a.Sim.Content.Rules.Number("physicality.shove_scale_max", 0);
            Assert.Equal(3, ceiling);
            Assert.InRange(scale, 0, ceiling);
            a.Run("""{"type":"move","kind":"knockback","distance":4}""", start, target: e);
            a.Tick(45);
            var travel = Vector3.Distance(start, at.Position);
            Assert.InRange(travel, 4 * (float)scale - .2f, 4 * (float)scale + .01f);
            Assert.InRange(travel, 0, 4 * (float)ceiling + .01f);
            output.WriteLine($"{pattern}: canonical mass {mass:F3} kg, shove scale {scale:F3}, travel {travel:F3} m");
            return (mass, travel);
        }
        var small = Push("kobold_scavenger");
        var heavy = Push("kobold_ironback");
        Assert.InRange(small.mass, 19, 20);
        Assert.InRange(heavy.mass, 148, 150);
        Assert.InRange(small.travel, 11.8f, 12.01f);
        Assert.InRange(heavy.travel, 1, 1.3f);
        Assert.InRange(CoreContent.Registry.Require<PatternDef>("kobold_scavenger").Height / 1.8, .4, .5);
        Assert.Equal(1.8, CoreContent.Registry.Require<PatternDef>("skeleton_warrior").Height, 5);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SmallKoboldShoveStopsAtPlacedAndTerrainWalls(bool terrain)
    {
        var a = DeliveryArena.Create();
        var body = a.Body(30, 10.5f, pattern: "kobold_scavenger");
        var cell = new CellCoord(32, 10);
        if (terrain)
        {
            var (chunk, index) = a.Sim.Terrain.Locate(cell);
            chunk.Flags[index] = CellFlags.Structure | CellFlags.Blocked;
        }
        else a.Sim.Service<BlockedCells>().Block(cell, a.Sim.World.Create());
        a.Run("""{"type":"move","kind":"knockback","distance":4}""", new Vector3(30, 10.5f, 0), target: body);
        a.Tick(60);
        Assert.InRange(a.Sim.World.Get<Transform>(body)!.Position.X, 31.5f, 31.99999f);
        Assert.Single(a.Of<BodyCollided>(), e => e.Entity == body);
        Assert.Equal(Vector3.Zero, a.Sim.World.Get<Motion>(body)!.Impulse);
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
            var placed = Assert.Single(g.Zone(new ZoneCoord(0, 0)).Pois, p => p.Def.Id == site && p.X == 50.5 && p.Y == 50.5);
            var spawns = g.Layout.ChunksOf(new ZoneCoord(0, 0)).SelectMany(c => g.GenerateChunk(c).Spawns)
                .Where(s => s.Group == placed.Id && s.Pattern.StartsWith("kobold_", StringComparison.Ordinal)).ToArray();
            Assert.All(spawns, s => Assert.Equal(placed.Level, s.Level));
            return spawns.Select(s => $"{s.Pattern}:{s.X}:{s.Y}:{s.PackSize}:{s.Level}").ToArray();
        }
        var high = Generate(25);
        var def = CoreContent.Registry.Require<PoiDef>(site);
        Assert.All(def.Inhabitants.Where(s => s.Level?.Contains(25 + def.Level) != false),
            s => Assert.Contains(high, row => row.StartsWith(s.Pattern + ":", StringComparison.Ordinal)));
        Assert.Equal(high, Generate(25));
        var low = Generate(1);
        Assert.NotEmpty(low);
        Assert.Equal(low, Generate(1));
        foreach (var s in def.Inhabitants.Where(s => s.Level?.Contains(1 + def.Level) == false))
            Assert.DoesNotContain(low, row => row.StartsWith(s.Pattern + ":", StringComparison.Ordinal));
        Assert.InRange(high.Length, 1, 4);
    }

    [Fact]
    public void HomeRegionHoldHasALevelSevenGuardAtBirthAndRespawnWithoutPrematureBosses()
    {
        var content = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out _);
        content.With(new RegionOverrideDef { Id = "hallows_home", X = 0, Y = 0, Archetype = "hallows", Level = 1, Faction = "weftmarch_compact" });
        content.With(new ZoneOverrideDef { Id = "ninewells", X = 0, Y = 0, Pois = { new PoiPlacement { Poi = "kobold_scale_hold", X = 50, Y = 50 } } });
        var generator = new WorldGenerator(content, Rng.Hash64("copper-crest-sites"));
        var sim = ZoneBuilder.CreateZone(content, generator, new WorldState(new GenerationOverlays()), new ZoneCoord(0, 0));
        var report = ZoneBuilder.LoadZone(sim);
        var hold = Assert.Single(report.Info!.Pois, p => p.Def.Id == "kobold_scale_hold");
        Assert.Equal(7, hold.Level);
        sim.Tick();
        var residents = sim.World.Query<Spawner>().Where(pair => pair.Item2.Group == hold.Id && pair.Item2.Pattern.StartsWith("kobold_", StringComparison.Ordinal))
            .SelectMany(pair => pair.Item2.Alive).ToArray();
        var guard = Assert.Single(residents);
        Assert.True(sim.World.IsAlive(guard));
        Assert.False(sim.World.Has<Dead>(guard));
        Assert.Equal("kobold_tunnelguard", sim.World.Get<Identity>(guard)!.Pattern);
        Assert.Equal("kobold_scaleclans", Relations.FactionOf(sim, guard));
        Assert.Equal(hold.Level, sim.World.Get<Stats>(guard)!.Level);
        var spawner = Assert.Single(sim.World.Query<Spawner>(), p => p.C1.Alive.Contains(guard)).C1;
        Assert.Equal(hold.Level, spawner.Level);
        sim.Service<DeathService>().Kill(sim, guard, Entity.None);
        spawner.RespawnAt = sim.Clock.Time;
        sim.Tick();
        var replacement = Assert.Single(spawner.Alive);
        Assert.NotEqual(guard, replacement);
        Assert.Equal(hold.Level, sim.World.Get<Stats>(replacement)!.Level);
        Assert.Equal("kobold_tunnelguard", sim.World.Get<Identity>(replacement)!.Pattern);
        output.WriteLine($"Home region hold: Tunnelguard born and respawned at site level {hold.Level}");
    }

    [Theory]
    [InlineData("kobold_scale_hold", -15, -40)]
    [InlineData("kobold_scrap_camp", -8, -40)]
    [InlineData("kobold_cinder_camp", -25, -31)]
    [InlineData("kobold_reed_camp", -7, -23)]
    public void DedicatedSitesActuallyOccurInUnmodifiedGeneratedHabitats(string site, int x, int y)
    {
        var g = new WorldGenerator(CoreContent.Registry, Rng.Hash64("aureth"));
        var sites = g.Zone(new ZoneCoord(x, y)).Pois.Where(p => p.Def.Id == site).ToArray();
        Assert.NotEmpty(sites);
        var sim = ZoneBuilder.CreateZone(CoreContent.Registry, g, new WorldState(new GenerationOverlays()), new ZoneCoord(x, y));
        ZoneBuilder.LoadZone(sim);
        sim.Tick();
        foreach (var poi in sites)
        {
            var spawns = g.Layout.ChunksOf(poi.Zone).SelectMany(c => g.GenerateChunk(c).Spawns)
                .Where(s => s.Pattern.StartsWith("kobold_", StringComparison.Ordinal) && s.Group == poi.Id).ToArray();
            Assert.NotEmpty(spawns);
            Assert.All(spawns, s => Assert.Equal(poi.Level, s.Level));
            var residents = sim.World.Query<Spawner>().Where(p => p.C1.Group == poi.Id && p.C1.Pattern.StartsWith("kobold_", StringComparison.Ordinal))
                .SelectMany(p => p.C1.Alive).ToArray();
            Assert.NotEmpty(residents);
            Assert.All(residents, e => Assert.Equal(poi.Level, sim.World.Get<Stats>(e)!.Level));
            output.WriteLine($"{site}: zone({x},{y}), at({poi.X},{poi.Y}), level {poi.Level}, {spawns.Length} occupied spawn points");
        }
    }
}
