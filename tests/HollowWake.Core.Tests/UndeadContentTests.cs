using System.Numerics;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Maths;
using HollowWake.Core.Minions;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;
using HollowWake.Core.World;
using HollowWake.Core.WorldGen;
using Xunit;

namespace HollowWake.Core.Tests;

public class UndeadContentTests
{
    public static IEnumerable<object[]> Bodies() => CoreContent.Registry.Each<PatternDef>()
        .Where(p => p.Extends == "undead_body").Select(p => new object[] { p.Id });

    [Theory]
    [MemberData(nameof(Bodies))]
    public void WildBodiesHaveClassifiersRewardsAndActuallyFight(string pattern)
    {
        var arena = MinionArena.Create();
        arena.OwnerStats.Attributes.SetBase("life.max", 100000);
        arena.OwnerStats.FillResources();
        var foe = arena.Foe(pattern, new Vector3(24, 20, 0), thinks: true);
        var id = arena.Sim.World.Get<Identity>(foe)!;
        Assert.True(id.Tags.Has("undead"));
        Assert.True(id.Tags.Has("body"));
        Assert.True(id.Tags.Has("family"));
        Assert.True(id.Tags.Has("role"));
        Assert.True(id.Tags.Has("habitat"));
        Assert.NotNull(arena.Sim.World.Get<Drops>(foe));
        for (var i = 0; i < 400; i++) arena.Sim.Tick();
        Assert.True(arena.Hit(foe, arena.Owner), pattern + " never landed damage through its AI kit");
    }

    [Theory]
    [InlineData("summon_skeleton", "skeleton_warrior", "minion_melee", 3)]
    [InlineData("summon_skeleton_archer", "skeleton_archer", "minion_ranged", 2)]
    [InlineData("summon_skeletal_sentinel", "skeletal_sentinel", "minion_melee", 2)]
    [InlineData("summon_skeletal_duelist", "skeletal_duelist", "minion_melee", 3)]
    [InlineData("summon_abomination", "court_abomination", "minion_melee", 1)]
    [InlineData("summon_ossuary_lich", "ossuary_lich", "minion_ranged", 1)]
    [InlineData("summon_blade_wraith", "blade_wraith", "minion_melee", 3)]
    [InlineData("summon_barrow_hound", "barrow_hound", "minion_melee", 2)]
    public void SummonsUseTheirOwnKitOwnerAllegianceAndCapacity(string weave, string pattern, string behavior, int cap)
    {
        var arena = MinionArena.Create();
        var slot = arena.Learn(weave);
        for (var i = 0; i < cap + 1; i++) arena.Cast(slot);
        var bodies = arena.Minions();
        Assert.Equal(cap, bodies.Count);
        var foe = arena.Foe("skeleton_warrior", new Vector3(24, 20, 0));
        arena.Sim.World.Get<Stats>(foe)!.Attributes.SetBase("life.max", 100000);
        arena.Sim.World.Get<Stats>(foe)!.FillResources();
        foreach (var body in bodies)
        {
            Assert.Equal(pattern, arena.Sim.World.Get<Identity>(body)!.Pattern);
            Assert.Equal("red", Relations.FactionOf(arena.Sim, body));
            Assert.False(Relations.IsHostile(arena.Sim, body, arena.Owner));
            Assert.True(Relations.IsHostile(arena.Sim, body, foe));
            Assert.Equal(behavior, arena.Sim.World.Get<Behavior>(body)!.BehaviorId);
            Assert.Null(arena.Sim.World.Get<Drops>(body));
        }
        for (var i = 0; i < 350; i++) arena.Sim.Tick();
        Assert.Contains(bodies, body => arena.Hit(body, foe));
    }

    [Fact]
    public void MagePoolIsSeededDiverseAndKeepsEachScholarsKit()
    {
        static string[] Roll(ulong seed)
        {
            var arena = MinionArena.Create(seed);
            var slot = arena.Learn("summon_skeletal_mages");
            arena.Cast(slot);
            arena.Cast(slot);
            arena.Cast(slot);
            Assert.Equal(4, arena.Minions().Count);
            return arena.Minions().Select(e =>
            {
                Assert.Equal("minion_ranged", arena.Sim.World.Get<Behavior>(e)!.BehaviorId);
                Assert.Null(arena.Sim.World.Get<Drops>(e));
                return arena.Sim.World.Get<Identity>(e)!.Pattern!;
            }).ToArray();
        }
        Assert.Equal(Roll(17), Roll(17));
        var all = Enumerable.Range(1, 12).SelectMany(seed => Roll((ulong)seed)).ToHashSet();
        Assert.Equal(new[] { "skeletal_cryomancer", "skeletal_pyromancer", "skeletal_stormcaller", "skeletal_venomancer" }, all.OrderBy(s => s));
    }

    [Fact]
    public void BoneGolemContractReservesRespawnsAndSharesTheWardenSlot()
    {
        var arena = MinionArena.Create();
        arena.Cast(arena.Learn("summon_bone_golem"));
        var golem = Assert.Single(arena.Minions());
        Assert.Equal("bone_golem", arena.Sim.World.Get<Identity>(golem)!.Pattern);
        Assert.Equal(30, arena.OwnerStats.Get("mana.reserved"));
        arena.Sim.Service<DeathService>().Kill(arena.Sim, golem, Entity.None);
        for (var i = 0; i < 180; i++) arena.Sim.Tick();
        var replacement = Assert.Single(arena.Minions());
        Assert.NotEqual(golem, replacement);
        Assert.Equal("bone_golem", arena.Sim.World.Get<Identity>(replacement)!.Pattern);
        arena.Cast(arena.Learn("bind_stone_warden"));
        Assert.Equal("stone_warden", arena.Sim.World.Get<Identity>(Assert.Single(arena.Minions()))!.Pattern);
        Assert.Equal(30, arena.OwnerStats.Get("mana.reserved"));
    }

    [Fact]
    public void WraithDecayBelongsToTheSummonAndWildSpiritsLeaveNoCorpse()
    {
        var arena = MinionArena.Create();
        var wild = arena.Foe("decay_wraith", new Vector3(40, 40, 0));
        var stats = arena.Sim.World.Get<Stats>(wild)!;
        var life = stats.Resource("life");
        arena.Cast(arena.Learn("summon_wraith"));
        Assert.Equal("decay_wraith", arena.Sim.World.Get<Identity>(Assert.Single(arena.Minions()))!.Pattern);
        for (var i = 0; i < 500; i++) arena.Sim.Tick();
        Assert.Empty(arena.Minions());
        Assert.Equal(life, stats.Resource("life"));
        arena.Sim.Service<DeathService>().Kill(arena.Sim, wild, Entity.None);
        Assert.False(Corpses.Is(arena.Sim.World, wild));
    }

    [Fact]
    public void RaiseZombieRequiresAndConsumesACorpseWithoutCopyingItsRewards()
    {
        var arena = MinionArena.Create();
        var slot = arena.Learn("raise_zombie");
        Assert.False(arena.Sim.Service<WeaveRuntime>().TryBegin(arena.Sim, arena.Owner, slot, Entity.None, arena.OwnerAt.Position, Vector2.Zero, out _));
        var corpse = arena.Foe("skeleton_warrior", new Vector3(22, 20, 0));
        arena.Sim.Service<DeathService>().Kill(arena.Sim, corpse, Entity.None);
        Assert.True(Corpses.Is(arena.Sim.World, corpse));
        arena.Cast(slot, corpse, arena.At(corpse));
        Assert.False(Corpses.Is(arena.Sim.World, corpse));
        var risen = Assert.Single(arena.Minions());
        Assert.Equal("zombie", arena.Sim.World.Get<Identity>(risen)!.Pattern);
        Assert.Null(arena.Sim.World.Get<Drops>(risen));
    }

    [Fact]
    public void EveryBodyHasAnActiveHabitatAndItsOwnAppearance()
    {
        var content = CoreContent.Registry;
        var spawns = content.Each<PoiDef>().SelectMany(p => p.Inhabitants)
            .Concat(content.Each<BiomeDef>().SelectMany(b => b.Spawns))
            .Concat(content.Each<BiomeDef>().SelectMany(b => b.SpawnTables).Distinct().SelectMany(id => content.Require<SpawnTableDef>(id).Spawns))
            .Select(s => s.Pattern).ToHashSet();
        foreach (var row in Bodies())
        {
            var id = (string)row[0];
            Assert.Contains(id, spawns);
            Assert.NotNull(content.Get<AppearanceDef>(id));
            Assert.Equal("remnant", content.Require<PatternDef>(id).Faction);
        }
    }

    [Fact]
    public void PoiPopulationHonorsLevelWindowsAndReplays()
    {
        static string[] Generate(int level)
        {
            var content = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out _);
            content.With(new RegionOverrideDef { Id = "hallows_home", X = 0, Y = 0, Archetype = "hallows", Level = level, Faction = "weftmarch_compact" });
            content.With(new ZoneOverrideDef { Id = "ninewells", X = 0, Y = 0, Pois = { new PoiPlacement { Poi = "ossuary", X = 50, Y = 50 } } });
            var gen = new WorldGenerator(content, Rng.Hash64("undead-habitats"));
            return gen.Layout.ChunksOf(new ZoneCoord(0, 0)).SelectMany(c => gen.GenerateChunk(c).Spawns)
                .Where(s => s.Group?.Contains("ossuary", StringComparison.Ordinal) == true)
                .Select(s => $"{s.Pattern}:{s.X}:{s.Y}:{s.PackSize}").ToArray();
        }
        var young = Generate(1);
        Assert.Contains(young, s => s.StartsWith("skeleton_warrior:"));
        Assert.DoesNotContain(young, s => s.StartsWith("bone_golem:"));
        var old = Generate(8);
        Assert.Contains(old, s => s.StartsWith("bone_golem:"));
        Assert.Equal(old, Generate(8));
    }
}
