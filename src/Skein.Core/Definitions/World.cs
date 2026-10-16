using System.Text.Json.Serialization;
using Skein.Core.Attributes;
using Skein.Core.Expressions;
using Skein.Core.Tags;

namespace Skein.Core.Definitions;

/// <summary>A closed numeric window, [min, max].</summary>
public sealed record Window
{
    public double Min { get; init; } = double.NegativeInfinity;
    public double Max { get; init; } = double.PositiveInfinity;
    public bool Contains(double v) => v >= Min && v <= Max;
}

public sealed record FloraSpec
{
    public required string Pattern { get; init; }
    /// <summary>Expected instances per 100 cells at full noise.</summary>
    public double Density { get; init; } = 1;
    /// <summary>Noise frequency for clumping; 0 scatters uniformly.</summary>
    public double Clumping { get; init; }
    public Window? Slope { get; init; }
    /// <summary>Surface tags required (grass, sand, rock, shallow).</summary>
    public TagSet On { get; init; } = TagSet.Empty;
}

public sealed record SpawnSpec
{
    public required string Pattern { get; init; }
    /// <summary>Expected packs per zone (or per chunk for flora-like density).</summary>
    public double Density { get; init; } = 1;
    [JsonPropertyName("pack_size")]
    public double[] PackSize { get; init; } = { 1, 1 };
    public double Respawn { get; init; } = 120;
    public Window? Level { get; init; }
    public TagSet On { get; init; } = TagSet.Empty;
    public Expr? Condition { get; init; }
}

/// <summary>
/// A biome or sub-biome. Selection walks the biome table: every biome whose windows contain the
/// cell's elevation, temperature, moisture and thread density is a candidate; the most specific
/// (deepest parent chain, then highest priority) wins. Sub-biomes name a parent and narrow its windows.
/// </summary>
public sealed record BiomeDef : Definition
{
    public string? Parent { get; init; }
    public int Priority { get; init; }
    public Window? Elevation { get; init; }
    public Window? Temperature { get; init; }
    public Window? Moisture { get; init; }
    public Window? Slope { get; init; }
    /// <summary>Per-thread density windows: {ember: {min: 0.6}} for scorched lands.</summary>
    public Dictionary<string, Window> Threads { get; init; } = new();
    /// <summary>Region archetypes this biome may appear in; empty means any.</summary>
    public TagSet Regions { get; init; } = TagSet.Empty;
    /// <summary>Surface tags written onto cells: grass, forest_floor, sand, rock, snow, bog.</summary>
    public TagSet Surface { get; init; } = TagSet.Empty;
    /// <summary>Quantise heights into terraces of this size for stepped z-levels; 0 leaves slopes smooth.</summary>
    public double Terrace { get; init; }
    public double Roughness { get; init; } = 1;
    public List<FloraSpec> Flora { get; init; } = new();
    public List<SpawnSpec> Spawns { get; init; } = new();
    /// <summary>Spawn tables shared across biomes, by id.</summary>
    [JsonPropertyName("spawn_tables")]
    public List<string> SpawnTables { get; init; } = new();
    public string Color { get; init; } = "#55aa55";
    public Dictionary<string, double> Yields { get; init; } = new();
}

public sealed record SpawnTableDef : Definition
{
    public List<SpawnSpec> Spawns { get; init; } = new();
}

/// <summary>
/// Macro shape of a region: how its elevation is sculpted, its climate bias, its age, which factions
/// and points of interest it favors. Regions are assigned archetypes by the world generator's region pass.
/// </summary>
public sealed record RegionArchetypeDef : Definition
{
    public double Weight { get; init; } = 1;
    /// <summary>Elevation shaping: base lift, amplitude multiplier, and ridge/terrace style.</summary>
    [JsonPropertyName("elevation_lift")]
    public double ElevationLift { get; init; }
    [JsonPropertyName("elevation_scale")]
    public double ElevationScale { get; init; } = 1;
    public string Shape { get; init; } = "rolling";
    [JsonPropertyName("temperature_bias")]
    public double TemperatureBias { get; init; }
    [JsonPropertyName("moisture_bias")]
    public double MoistureBias { get; init; }
    public Dictionary<string, double> Threads { get; init; } = new();
    /// <summary>Monster level band relative to distance from the world origin.</summary>
    [JsonPropertyName("level_offset")]
    public int LevelOffset { get; init; }
    /// <summary>Point of interest budget per zone of this archetype, by poi tag.</summary>
    [JsonPropertyName("poi_budget")]
    public Dictionary<string, double> PoiBudget { get; init; } = new();
    public TagSet Factions { get; init; } = TagSet.Empty;
    /// <summary>Only appear at least this far from the origin, in regions.</summary>
    [JsonPropertyName("min_distance")]
    public int MinDistance { get; init; }
    [JsonPropertyName("max_distance")]
    public int MaxDistance { get; init; } = int.MaxValue;
    public List<string> Names { get; init; } = new();
}

/// <summary>
/// A point of interest template: a town, ruin, shrine, knot, camp, castle, fount. Placed by the zone
/// pass under constraints, it stamps landmarks, spawns inhabitants, and seeds quests.
/// </summary>
public sealed record PoiDef : Definition
{
    public double Weight { get; init; } = 1;
    /// <summary>Footprint radius in cells, used for spacing and flattening.</summary>
    public double Radius { get; init; } = 8;
    [JsonPropertyName("min_spacing")]
    public double MinSpacing { get; init; } = 48;
    public Window? Elevation { get; init; }
    public Window? Slope { get; init; }
    [JsonPropertyName("water_distance")]
    public Window? WaterDistance { get; init; }
    public TagSet Biomes { get; init; } = TagSet.Empty;
    public TagSet Regions { get; init; } = TagSet.Empty;
    /// <summary>Landmark prefabs placed at the center, chosen by weight.</summary>
    public List<string> Landmarks { get; init; } = new();
    public bool Flatten { get; init; } = true;
    public List<SpawnSpec> Inhabitants { get; init; } = new();
    /// <summary>Quest templates this point of interest can seed.</summary>
    public List<string> Quests { get; init; } = new();
    /// <summary>Marks a hub: safe, has services, players may bind here.</summary>
    public bool Hub { get; init; }
    public bool Safe { get; init; }
    public string? Fount { get; init; }
    public TagSet Factions { get; init; } = TagSet.Empty;
    public List<string> Names { get; init; } = new();
    public int Level { get; init; }
}

public sealed record LegendCell
{
    /// <summary>Height offset from the stamp base, in world units.</summary>
    public double Height { get; init; }
    public bool Walkable { get; init; } = true;
    /// <summary>When false, the cell keeps the natural terrain (used for the stamp's empty space).</summary>
    public bool Surface { get; init; } = true;
    public TagSet Tags { get; init; } = TagSet.Empty;
    public string? Spawn { get; init; }
    /// <summary>A raised surface placed above the terrain instead of replacing it (bridges, floors).</summary>
    public bool Layer { get; init; }
}

/// <summary>
/// A hand-authored structure as rows of characters with a legend: a bridge, a tower, a shrine, a wall.
/// Stamps can carry multiple surfaces per cell, which is how bridges and upper floors become z-levels.
/// </summary>
public sealed record LandmarkDef : Definition
{
    public List<string> Rows { get; init; } = new();
    public Dictionary<string, LegendCell> Legend { get; init; } = new();
    [JsonPropertyName("anchor_x")]
    public int AnchorX { get; init; }
    [JsonPropertyName("anchor_y")]
    public int AnchorY { get; init; }
    public double Weight { get; init; } = 1;
    public bool Flatten { get; init; } = true;
}

/// <summary>A hand-placed override for a generated region at a region coordinate: name, archetype, lore.</summary>
public sealed record RegionOverrideDef : Definition
{
    public int X { get; init; }
    public int Y { get; init; }
    public string? Archetype { get; init; }
    public string? Faction { get; init; }
    public Dictionary<string, double> Threads { get; init; } = new();
    public int? Level { get; init; }
}

/// <summary>A hand-placed override for a generated zone: pinned points of interest, spawns, flags.</summary>
public sealed record ZoneOverrideDef : Definition
{
    public int X { get; init; }
    public int Y { get; init; }
    public bool Safe { get; init; }
    public List<PoiPlacement> Pois { get; init; } = new();
    public List<SpawnSpec> Spawns { get; init; } = new();
    public List<string> Quests { get; init; } = new();
    [JsonPropertyName("player_start")]
    public bool PlayerStart { get; init; }
}

public sealed record PoiPlacement
{
    public required string Poi { get; init; }
    /// <summary>Cell offset within the zone.</summary>
    public int X { get; init; }
    public int Y { get; init; }
    public string? Name { get; init; }
    public string? Landmark { get; init; }
}

/// <summary>
/// A world event: an overlay applied to a scope (regions, zones, a radius) for a duration that changes
/// generation parameters, spawns, faction control and rules while active. Scheduled by the server or
/// triggered by play (a fount bound, a knot cleared, a war declared).
/// </summary>
public sealed record WorldEventDef : Definition
{
    public double Weight { get; init; } = 1;
    public Expr? Duration { get; init; }
    public string Scope { get; init; } = "region";
    /// <summary>Generator parameter overrides while active: elevation_scale, water_level, thread.ember...</summary>
    public Dictionary<string, double> Generation { get; init; } = new();
    public List<SpawnSpec> Spawns { get; init; } = new();
    /// <summary>Modifiers applied to every entity inside the scope.</summary>
    public List<Modifier> Modifiers { get; init; } = new();
    public string? Faction { get; init; }
    public List<string> Quests { get; init; } = new();
    public TagSet Regions { get; init; } = TagSet.Empty;
    [JsonPropertyName("on_start")]
    public List<TypedSpec> OnStart { get; init; } = new();
    [JsonPropertyName("on_end")]
    public List<TypedSpec> OnEnd { get; init; } = new();
}
