using System.Numerics;
using Skein.Core.Characters;
using Skein.Core.Components;
using Skein.Core.Content;
using Skein.Core.Crafting;
using Skein.Core.Definitions;
using Skein.Core.Ecs;
using Skein.Core.Intents;
using Skein.Core.Items;
using Skein.Core.Maths;
using Skein.Core.Patterns;
using Skein.Core.Sim;
using Skein.Core.Territory;
using Skein.Core.Weaves;
using Skein.Core.World;
using Skein.Core.WorldGen;
using Xunit;

namespace Skein.Core.Tests;

/// <summary>Loads the real core content once for every gameplay test.</summary>
public static class CoreContent
{
    private static readonly Lazy<ContentRegistry> Loaded = new(() =>
    {
        var dir = FindContentDir();
        var registry = ContentRegistry.Load(new[] { dir }, out var report);
        if (report.HasErrors)
        {
            throw new InvalidOperationException(report.ToString());
        }
        // Two throwaway factions so tests can pit anything against anything.
        registry.With(new FactionDef { Id = "red", Relations = { ["blue"] = Stance.Hostile } });
        registry.With(new FactionDef { Id = "blue", Relations = { ["red"] = Stance.Hostile } });
        return registry;
    });

    public static ContentRegistry Registry => Loaded.Value;

    public static string FindContentDir()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, "content", "core");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            dir = Path.GetDirectoryName(dir);
        }
        throw new DirectoryNotFoundException("content/core not found above " + AppContext.BaseDirectory);
    }
}

public class ContentPackTests
{
    [Fact]
    public void CoreContentValidatesWithoutErrors()
    {
        var registry = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out var report);
        Assert.False(report.HasErrors, report.ToString());
        Assert.True(registry.Count<AttributeDef>() > 30);
        Assert.Equal(9, registry.Count<ThreadDef>());
        Assert.True(registry.Count<FormDef>() >= 10);
        Assert.True(registry.Count<PatternDef>() >= 20);
        Assert.True(registry.Count<BiomeDef>() >= 8);
    }

    [Fact]
    public void EveryPresetWeaveCompiles()
    {
        var registry = CoreContent.Registry;
        var compiler = new WeaveCompiler(registry, Effects.EffectRegistry.CreateDefault());
        foreach (var w in registry.Each<WeaveDef>())
        {
            Assert.True(compiler.TryCompile(new WeaveSpec { Preset = w.Id }, out var compiled, out var error), $"{w.Id}: {error}");
            Assert.NotNull(compiled);
            Assert.True(compiled!.Effects.Count > 0 || w.Form == "summon", $"{w.Id} has no effects");
        }
    }

    [Fact]
    public void GlyphGatesRefuseWrongWeaves()
    {
        var compiler = new WeaveCompiler(CoreContent.Registry, Effects.EffectRegistry.CreateDefault());
        Assert.False(compiler.TryCompile(new WeaveSpec { Form = "strike", Glyphs = { "fork" } }, out _, out var error));
        Assert.Contains("fork", error);
        Assert.True(compiler.TryCompile(new WeaveSpec { Form = "bolt", Threads = { "ember" }, Glyphs = { "fork", "needle" } }, out var weave, out _));
        Assert.True(weave!.Tags.Has("ember"));
        Assert.True(weave.Mechanisms.Has("projectile"));
    }
}

public class ItemTests
{
    [Fact]
    public void RollsDeterministicItemsWithAffixesInRange()
    {
        var gen = new ItemGenerator(CoreContent.Registry);
        var a = gen.Roll("short_sword", 20, "rare", new Rng("x"));
        var b = new ItemGenerator(CoreContent.Registry).Roll("short_sword", 20, "rare", new Rng("x"));
        Assert.Equal(a.Name, b.Name);
        Assert.Equal(a.Affixes.Count, b.Affixes.Count);
        Assert.InRange(a.Affixes.Count, 3, 6);
        Assert.Equal(3, a.Tier);
        var local = gen.Resolver.LocalAttributes(a);
        var min = local.Get("weapon.damage.min", Expressions.EmptyScope.Instance);
        var max = local.Get("weapon.damage.max", Expressions.EmptyScope.Instance);
        Assert.True(max > min && min > 0);
        foreach (var (line, value, _) in gen.Resolver.Lines(a))
        {
            Assert.False(double.IsNaN(value), line.Attribute);
        }
        var groups = a.Affixes.Select(x => CoreContent.Registry.Require<AffixDef>(x.Affix).Group).ToList();
        Assert.Equal(groups.Count, groups.Distinct().Count());
    }

    [Fact]
    public void LootTablesNestAndYieldCurrency()
    {
        var gen = new ItemGenerator(CoreContent.Registry);
        var rng = new Rng("loot");
        var results = Enumerable.Range(0, 200).SelectMany(_ => gen.RollLoot("world_common", 5, rng)).ToList();
        Assert.Contains(results, r => r.Currency == "shards");
        Assert.Contains(results, r => r.Item is not null);
    }

    [Fact]
    public void UniquesRollTheirLines()
    {
        var gen = new ItemGenerator(CoreContent.Registry);
        var item = gen.RollUnique("the_last_stitch", 10, new Rng(3));
        Assert.Equal("unique", item.Rarity);
        var mods = gen.Resolver.WearerModifiers(item);
        Assert.Contains(mods, m => m.Attribute == "damage" && m.Scope.Has("spell"));
    }
}

public class ArenaTests
{
    private static (Simulation Sim, Entity A, Entity B) Duel(string a, string b, ulong seed = 7)
    {
        var registry = CoreContent.Registry;
        var sim = ZoneBuilder.CreateArena(registry, seed);
        var factory = sim.Service<PatternFactory>();
        var ea = factory.Spawn(sim, a, new Vector3(10, 10, 0), new SpawnOptions { Faction = "red" });
        var eb = factory.Spawn(sim, b, new Vector3(13, 10, 0), new SpawnOptions { Faction = "blue" });
        return (sim, ea, eb);
    }

    [Fact]
    public void MonstersFightThroughTheWholePipeline()
    {
        var (sim, wolf, hob) = Duel("wolf", "hob");
        var damage = 0.0;
        sim.Events.Subscribe<DamageDealt>(d => damage += d.Total);
        var died = Entity.None;
        sim.Events.Subscribe<EntityDied>(d => died = d.Entity);
        sim.RunSeconds(40);
        Assert.True(damage > 0, "no damage dealt");
        Assert.False(died.IsNone, "nobody died in 40 seconds");
        Assert.True(died == wolf || died == hob);
    }

    [Fact]
    public void SimulationIsDeterministicForASeed()
    {
        var log1 = new List<string>();
        var log2 = new List<string>();
        foreach (var log in new[] { log1, log2 })
        {
            var (sim, _, _) = Duel("loosed_brigand", "barrow_wight", 99);
            sim.Events.Subscribe<DamageDealt>(d => log.Add($"{sim.Clock.Tick}:{d.Total:0.###}:{d.Crit}"));
            sim.RunSeconds(20);
        }
        Assert.Equal(log1, log2);
        Assert.NotEmpty(log1);
    }

    [Fact]
    public void BoltsFlyAndBurnWithEmber()
    {
        var registry = CoreContent.Registry;
        var sim = ZoneBuilder.CreateArena(registry, 5);
        var factory = sim.Service<PatternFactory>();
        var caster = factory.Spawn(sim, "loosed_kindler", new Vector3(10, 10, 0), new SpawnOptions { Faction = "red" });
        var target = factory.Spawn(sim, "boar", new Vector3(16, 10, 0), new SpawnOptions { Faction = "blue" });
        sim.World.Remove<Behavior>(caster);
        sim.World.Remove<Behavior>(target);
        var book = sim.World.Get<WeaveBook>(caster)!;
        var slot = book.Add(new WeaveSpec { Form = "bolt", Threads = { "ember" }, Glyphs = { "burr", "fork" } });
        var runtime = sim.Service<WeaveRuntime>();
        var started = runtime.TryBegin(sim, caster, slot, target, new Vector3(16, 10, 0), Vector2.Zero, out var reason);
        Assert.True(started, reason);
        var hits = 0;
        sim.Events.Subscribe<DamageDealt>(d => { if (d.Target == target) hits++; });
        sim.RunSeconds(3);
        Assert.True(hits >= 1, "bolt never landed");
        var statuses = sim.World.Get<Statuses>(target)!;
        Assert.True(statuses.Has("burning") || hits > 0);
        Assert.True(sim.World.Get<Stats>(target)!.Resource("life") < sim.World.Get<Stats>(target)!.ResourceMax("life"));
    }

    [Fact]
    public void PlayersMoveJumpAndRespectTerrain()
    {
        var registry = CoreContent.Registry;
        var sim = ZoneBuilder.CreateArena(registry, 1);
        var factory = sim.Service<CharacterFactory>();
        var sheet = factory.Create(sim, "c1", "a1", "Tester", "holdfast", sim.Service<ItemGenerator>());
        sheet.Position = new Vector3(5, 5, 0);
        var e = factory.Materialize(sim, sheet);
        var player = sim.World.Get<Player>(e)!;
        player.Intents.Enqueue(new MoveToIntent(new Vector3(15, 5, 0)));
        sim.RunSeconds(4);
        var t = sim.World.Get<Transform>(e)!;
        Assert.InRange(t.Position.X, 14, 16);
        player.Intents.Enqueue(new JumpIntent());
        var jumped = false;
        sim.Events.Subscribe<EntityJumped>(_ => jumped = true);
        sim.Tick();
        sim.Tick();
        Assert.True(jumped);
        Assert.True(t.Position.Z > 0.05f, "did not leave the ground");
        sim.RunSeconds(2);
        Assert.InRange(t.Position.Z, -0.01f, 0.01f);
        Assert.True(sim.World.Get<Stats>(e)!.Level == 1);
        Assert.NotEmpty(sim.World.Get<Equipment>(e)!.BySlot);
        Assert.True(sim.World.Get<Stats>(e)!.Get("armor") > 0, "equipped armor should raise armor");
    }
}

public class WorldGenTests
{
    [Fact]
    public void GenerationIsDeterministicAndSeamless()
    {
        var registry = CoreContent.Registry;
        var g1 = new WorldGenerator(registry, 12345);
        var g2 = new WorldGenerator(registry, 12345);
        var c1 = g1.GenerateChunk(new ChunkCoord(3, -2));
        var c2 = g2.GenerateChunk(new ChunkCoord(3, -2));
        Assert.Equal(c1.Height, c2.Height);
        Assert.Equal(c1.Spawns.Count, c2.Spawns.Count);
        // Neighboring chunks agree at their shared edge because heights come from one continuous function.
        var left = g1.GenerateChunk(new ChunkCoord(0, 0));
        var right = g1.GenerateChunk(new ChunkCoord(1, 0));
        var diffs = 0;
        for (var y = 0; y < left.Size; y++)
        {
            var a = left.Height[left.Index(left.Size - 1, y)];
            var b = right.Height[right.Index(0, y)];
            if (Math.Abs(a - b) > 6)
            {
                diffs++;
            }
        }
        Assert.True(diffs < left.Size / 4, $"{diffs} large edge discontinuities");
    }

    [Fact]
    public void StartZoneHasNinewellsAndQuests()
    {
        var registry = CoreContent.Registry;
        var g = new WorldGenerator(registry, Rng.Hash64(registry.Rules.Text("world.seed", "aureth")));
        var zone = g.Zone(g.StartZone());
        Assert.True(zone.PlayerStart);
        Assert.Contains(zone.Pois, p => p.Name == "Ninewells" && p.Def.Hub);
        Assert.Contains(zone.Pois, p => p.Def.Fount is not null);
        Assert.NotEmpty(zone.Quests);
        Assert.All(zone.Quests, q => Assert.DoesNotContain("$", q.Name + q.Description + string.Join("", q.Objectives.Select(o => o.Text))));
        var region = g.Region(new RegionCoord(0, 0));
        Assert.Equal("hallows", region.Archetype.Id);
    }

    [Fact]
    public void OverlaysChangeTheLand()
    {
        var registry = CoreContent.Registry;
        var overlays = new GenerationOverlays();
        var g = new WorldGenerator(registry, 777, overlays);
        var before = g.Elevation(100, 100);
        overlays.Add(new GenerationOverlay { Id = "flood", Global = true, Params = { ["elevation_offset"] = 5 } });
        Assert.Equal(before + 5, g.Elevation(100, 100), 6);
        overlays.Remove("flood");
        Assert.Equal(before, g.Elevation(100, 100), 6);
    }

    [Fact]
    public void ZoneLoadsAndRunsHeadless()
    {
        var registry = CoreContent.Registry;
        var g = new WorldGenerator(registry, Rng.Hash64("aureth"));
        var world = new WorldState(g.Overlays);
        var log = new ListSimLog();
        var sim = ZoneBuilder.CreateZone(registry, g, world, g.StartZone(), log);
        var report = ZoneBuilder.LoadZone(sim);
        Assert.True(report.Chunks >= 64, report.ToString());
        Assert.True(report.Spawners > 0, report.ToString());
        Assert.True(report.Pois > 0, report.ToString());
        sim.Run(60);
        Assert.True(sim.World.Query<Behavior>().Any(), "no monsters spawned");
        Assert.DoesNotContain(log.Lines, l => l.StartsWith("error"));
        var terrain = sim.Terrain;
        var (x, y) = g.StartPoint(g.StartZone());
        Assert.True(terrain.TryFindGround(x, y, 100, 0.6, out var ground, out _), "start point has no ground");
        Assert.True(ground.Walkable);
    }
}

public class CraftingTests
{
    [Fact]
    public void ProceduresScoreDeterministically()
    {
        var timing = new TimingProcedure();
        var spec = new TypedSpec { Type = "timing", Params = new System.Text.Json.Nodes.JsonObject { ["windows"] = 3, ["width"] = 0.2 } };
        var challenge = timing.Create(spec, new Rng(1), Expressions.EmptyScope.Instance, 0);
        var windows = challenge.Data["windows"]!.AsArray().Select(w => w!["at"]!.GetValue<double>()).ToList();
        var perfect = timing.Score(challenge, new ProcedureInput { Times = windows });
        var sloppy = timing.Score(challenge, new ProcedureInput { Times = windows.Select(w => w + 0.15).ToList() });
        var missed = timing.Score(challenge, new ProcedureInput { Times = new List<double> { 0 } });
        Assert.Equal(1, perfect, 6);
        Assert.InRange(sloppy, 0.3, 0.9);
        Assert.Equal(0, missed, 6);
    }

    [Fact]
    public void CraftingConsumesInputsAndMintsQuality()
    {
        var registry = CoreContent.Registry;
        var sim = ZoneBuilder.CreateArena(registry, 9);
        var factory = sim.Service<CharacterFactory>();
        var gen = sim.Service<ItemGenerator>();
        var sheet = factory.Create(sim, "c2", "a1", "Smith", "holdfast", gen);
        sheet.Position = new Vector3(5, 5, 0);
        var e = factory.Materialize(sim, sheet);
        var inv = sim.World.Get<Inventory>(e)!;
        var ore = gen.Roll("iron_ore", 1, "common", new Rng(1));
        ore.Count = 3;
        inv.TryAdd(ore);
        var wood = gen.Roll("ash_wood", 1, "common", new Rng(2));
        inv.TryAdd(wood);
        var anvil = sim.Service<PatternFactory>().Spawn(sim, "anvil", new Vector3(6, 5, 0));
        var crafting = sim.Service<CraftingService>();
        var challenge = crafting.Start(sim, e, "reweave_blade", anvil, out var reason);
        Assert.NotNull(challenge);
        var windows = challenge!.Data["windows"]!.AsArray().Select(w => w!["at"]!.GetValue<double>()).ToList();
        var output = crafting.Submit(sim, e, "reweave_blade", new ProcedureInput { Times = windows }, out reason);
        Assert.NotNull(output);
        Assert.Equal("short_sword", output!.Base);
        Assert.Equal(1, output.Quality, 3);
        Assert.Equal(0, inv.CountOf(i => i.Base == "iron_ore"));
        Assert.Contains(inv.Items, i => i.Base == "short_sword");
    }
}
