using System.Numerics;
using HollowWake.Core.Ai;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Crafting;
using HollowWake.Core.Effects;
using HollowWake.Core.Interaction;
using HollowWake.Core.Items;
using HollowWake.Core.Loot;
using HollowWake.Core.Movement;
using HollowWake.Core.Patterns;
using HollowWake.Core.Progression;
using HollowWake.Core.Quests;
using HollowWake.Core.StatusEffects;
using HollowWake.Core.Systems;
using HollowWake.Core.Tags;
using HollowWake.Core.Territory;
using HollowWake.Core.Weaves;
using HollowWake.Core.World;
using HollowWake.Core.WorldGen;

namespace HollowWake.Core.Sim;

/// <summary>
/// Assembles a complete simulation: services, systems, terrain. The same builder serves the server's
/// zones, the tools' headless arenas and the tests, so there is exactly one answer to "what does a
/// running simulation consist of".
/// </summary>
public static class ZoneBuilder
{
    /// <summary>A simulation for one zone of a generated world.</summary>
    public static Simulation CreateZone(ContentRegistry content, WorldGenerator generator, WorldState world, ZoneCoord zone, ISimLog? log = null, int? ticksPerSecond = null, IChunkProvider? chunkProvider = null)
    {
        var provider = chunkProvider ?? new GeneratedChunkProvider(generator);
        var terrain = new Terrain(generator.Layout, provider);
        var seed = Maths.Rng.Combine(generator.Seed, Maths.Rng.Hash64("zone:" + zone.Key));
        var tps = ticksPerSecond ?? (int)content.Rules.Number("server.tick_rate", 20);
        var sim = new Simulation(content, terrain, seed, tps, generator.Layout) { Zone = zone, Generator = generator, Log = log ?? NullSimLog.Instance };
        Wire(sim, world);
        return sim;
    }

    /// <summary>A flat arena with no world around it, for tools and tests.</summary>
    public static Simulation CreateArena(ContentRegistry content, ulong seed = 1, int size = 64, float height = 0, ISimLog? log = null, int ticksPerSecond = 20)
    {
        var layout = WorldLayout.Default;
        var terrain = new Terrain(layout, new FlatChunkProvider(layout.ChunkSize, height, TagSet.Of("ground", "arena")));
        var sim = new Simulation(content, terrain, seed, ticksPerSecond, layout) { Log = log ?? NullSimLog.Instance };
        Wire(sim, new WorldState(new GenerationOverlays()));
        _ = size;
        return sim;
    }

    public static void Wire(Simulation sim, WorldState world)
    {
        var content = sim.Content;
        var effects = EffectRegistry.CreateDefault();
        var items = new ItemGenerator(content);
        var compiler = new WeaveCompiler(content, effects);
        sim.AddService(world);
        sim.AddService(world.Overlays);
        sim.AddService(effects);
        sim.AddService(items);
        sim.AddService(items.Resolver);
        sim.AddService(compiler);
        sim.AddService(new WeaveRuntime(compiler));
        sim.AddService(new StatusService(effects));
        sim.AddService(new TriggerHub(sim));
        sim.AddService(new ChargeService());
        sim.AddService(new ProcService(sim));
        sim.AddService(IntentHandlers.CreateDefault());
        sim.AddService(new PatternFactory(content));
        sim.AddService(new BlockedCells());
        sim.AddService(new LootService(items));
        sim.AddService(new DeathService());
        sim.AddService(new ProgressionService());
        sim.AddService(new EquipmentService(items.Resolver));
        sim.AddService(new QuestService());
        sim.AddService(new InteractionService());
        sim.AddService(new TerritoryService(world));
        sim.AddService(ProcedureRegistry.CreateDefault());
        sim.AddService(new CraftingService(sim.Service<ProcedureRegistry>()));
        sim.AddService(new Characters.CharacterFactory());
        sim.AddService(new Pathfinder(sim.Terrain)
        {
            StepUp = content.Rules.Number("move.step_up", 0.6),
            MaxDrop = content.Rules.Number("move.max_drop", 3),
            MaxNodes = (int)content.Rules.Number("move.path_max_nodes", 4000),
            CellCost = cell => sim.Terrain.TagsAt(cell).Has("road") ? 0.6 : sim.Terrain.TagsAt(cell).Has("bog") ? 2.5 : 1.0,
        });

        // Systems run by phase, then by order inside a phase (see each system's Phase and Order).
        sim.AddSystem(new IntentSystem());
        sim.AddSystem(new SpawnerSystem());
        sim.AddSystem(new MinionSystem());
        sim.AddSystem(new BehaviorSystem());
        sim.AddSystem(new CastingSystem());
        sim.AddSystem(new MovementSystem());
        sim.AddSystem(new DisplacementSystem());
        sim.AddSystem(new ProjectileSystem());
        sim.AddSystem(new FieldSystem());
        sim.AddSystem(new TetherSystem());
        sim.AddSystem(new StatusSystem());
        sim.AddSystem(new UpkeepSystem());
        sim.AddSystem(new QuestSystem());
        sim.AddSystem(new DiscoverySystem());
        sim.AddSystem(new TerritorySystem());
        sim.AddSystem(new LifetimeSystem());
    }

    /// <summary>
    /// Loads a zone's generated contents into its simulation: chunks, spawners, flora, point-of-interest
    /// markers, founts and generated quests.
    /// </summary>
    public static ZoneLoadReport LoadZone(Simulation sim)
    {
        if (sim.Generator is null || sim.Zone is null)
        {
            throw new InvalidOperationException("Simulation is not bound to a zone.");
        }
        var generator = sim.Generator;
        var zone = sim.Zone.Value;
        var info = generator.Zone(zone);
        var report = new ZoneLoadReport { Zone = zone, Info = info };
        var factory = sim.Service<PatternFactory>();
        var world = sim.Service<WorldState>();
        var pad = (int)sim.Content.Rules.Number("server.zone_chunk_padding", 1);

        // Chunks of the zone plus a padding ring so border queries never generate on the hot path.
        var origin = sim.Layout.ZoneOrigin(zone);
        var firstChunk = sim.Layout.ChunkOf(origin);
        for (var cy = -pad; cy < sim.Layout.ZoneChunks + pad; cy++)
        {
            for (var cx = -pad; cx < sim.Layout.ZoneChunks + pad; cx++)
            {
                var chunk = sim.Terrain.GetChunk(new ChunkCoord(firstChunk.X + cx, firstChunk.Y + cy));
                report.Chunks++;
                var inside = cx >= 0 && cy >= 0 && cx < sim.Layout.ZoneChunks && cy < sim.Layout.ZoneChunks;
                if (!inside)
                {
                    continue;
                }
                foreach (var flora in chunk.Flora)
                {
                    var e = factory.Spawn(sim, flora.Pattern, new Vector3(flora.X, flora.Y, 0));
                    if (!e.IsNone)
                    {
                        if (sim.World.TryGet<Prop>(e, out var prop))
                        {
                            prop.Scale = flora.Scale;
                        }
                        report.Flora++;
                    }
                }
                foreach (var sp in chunk.Spawns)
                {
                    var e = sim.World.Create();
                    var pos = sim.Terrain.SnapToGround(new Vector3(sp.X, sp.Y, 0), 4);
                    sim.World.Add(e, new Transform { Position = pos, Radius = 0.1f });
                    sim.World.Add(e, new Identity { Name = "spawner", Tags = TagSet.Of("spawner"), Look = null });
                    sim.World.Add(e, new Spawner { Pattern = sp.Pattern, PackSize = sp.PackSize, RespawnSeconds = sp.RespawnSeconds, Group = sp.Group, RollRarity = sp.Group is null, RespawnAt = sim.Clock.Time });
                    report.Spawners++;
                }
            }
        }

        foreach (var poi in info.Pois)
        {
            var center = sim.Terrain.SnapToGround(new Vector3((float)poi.X, (float)poi.Y, poi.BaseHeight), 4);
            var marker = sim.World.Create();
            sim.World.Add(marker, new Transform { Position = center, Radius = (float)poi.Def.Radius });
            sim.World.Add(marker, new Identity { Name = poi.Name, Tags = poi.Tags, Faction = poi.Faction, Look = "poi:" + poi.Def.Id });
            sim.World.Add(marker, new Interactable { Kind = "marker", Ref = poi.Id, Range = (float)poi.Def.Radius + 4, Data = { ["name"] = poi.Name, ["poi"] = poi.Def.Id } });
            sim.Spatial.Update(marker, center);
            report.Pois++;
            if (poi.Def.Fount is not null)
            {
                var fe = sim.World.Create();
                sim.World.Add(fe, new Transform { Position = center, Radius = 1.2f, Height = 2.5f });
                sim.World.Add(fe, new Identity { Name = poi.Name + " Fount", Tags = TagSet.Of("fount", "landmark"), Look = "fount" });
                var fount = sim.World.Add(fe, new Fount { Id = poi.Def.Fount + "@" + poi.Id, Name = poi.Name, Radius = (float)Math.Max(poi.Def.Radius * 3, 48) });
                foreach (var (currency, amount) in BiomeYields(sim, poi))
                {
                    fount.Yield[currency] = amount;
                }
                var holding = world.HoldingOfFount(fount.Id);
                if (holding is not null)
                {
                    fount.HoldingId = holding.Id;
                    fount.Influence = 1;
                }
                sim.World.Add(fe, new Interactable { Kind = "fount", Ref = fount.Id, Range = 3f, Data = { ["name"] = poi.Name } });
                sim.Spatial.Update(fe, center);
                report.Founts++;
            }
        }
        sim.Service<QuestService>().RegisterGenerated(info.Quests);
        report.Quests = info.Quests.Count;
        return report;
    }

    private static IEnumerable<(string, double)> BiomeYields(Simulation sim, PoiInstance poi)
    {
        var biomeId = sim.Terrain.BiomeAt(CellCoord.Of(poi.X, poi.Y));
        if (sim.Content.TryGet<Definitions.BiomeDef>(biomeId, out var biome) && biome is not null)
        {
            foreach (var (k, v) in biome.Yields)
            {
                yield return (k, v);
            }
        }
    }
}

public sealed class ZoneLoadReport
{
    public ZoneCoord Zone { get; init; }
    public ZoneInfo? Info { get; init; }
    public int Chunks;
    public int Flora;
    public int Spawners;
    public int Pois;
    public int Founts;
    public int Quests;

    public override string ToString() => $"{Zone}: {Chunks} chunks, {Pois} pois, {Founts} founts, {Spawners} spawners, {Flora} flora, {Quests} quests";
}

/// <summary>Flat terrain for arenas and tests.</summary>
public sealed class FlatChunkProvider : IChunkProvider
{
    private readonly int _size;
    private readonly float _height;
    private readonly TagSet _tags;

    public FlatChunkProvider(int size, float height, TagSet tags)
    {
        _size = size;
        _height = height;
        _tags = tags;
    }

    public ChunkData Load(ChunkCoord coord)
    {
        var chunk = new ChunkData(coord, _size);
        var tag = chunk.Intern(_tags);
        for (var i = 0; i < chunk.Height.Length; i++)
        {
            chunk.Height[i] = _height;
            chunk.Flags[i] = CellFlags.Walkable;
            chunk.SurfaceTag[i] = tag;
        }
        return chunk;
    }
}
