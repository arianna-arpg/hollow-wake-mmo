using HollowWake.Core.Tags;

namespace HollowWake.Core.World;

/// <summary>One walkable (or not) surface in a cell. Cells can stack several: ground, a bridge, a floor.</summary>
public readonly record struct Surface(float Height, bool Walkable, TagSet Tags)
{
    public bool Has(string tag) => Tags.Has(tag);
}

[Flags]
public enum CellFlags : byte
{
    None = 0,
    Walkable = 1,
    Water = 2,
    Blocked = 4,
    Road = 8,
    Structure = 16,
    Flattened = 32,
}

public sealed record SpawnPoint(string Pattern, float X, float Y, int PackSize, double RespawnSeconds, string? Group)
{
    public int? Level { get; init; }
}

public sealed record FloraPlacement(string Pattern, float X, float Y, float Scale);

/// <summary>
/// Generated and modified terrain for one chunk: a base surface per cell with height, flags, a biome and
/// a surface tag set, plus optional extra layers (bridges, floors) per cell. Compact arrays keep a zone
/// of chunks cheap; tag sets are interned per chunk.
/// </summary>
public sealed class ChunkData
{
    public ChunkCoord Coord { get; }
    public int Size { get; }
    public float[] Height { get; }
    public CellFlags[] Flags { get; }
    public ushort[] SurfaceTag { get; }
    public ushort[] BiomeIndex { get; }
    public List<TagSet> TagTable { get; } = new();
    public List<string> BiomeTable { get; } = new();
    public Dictionary<int, List<Surface>> Layers { get; } = new();
    public List<SpawnPoint> Spawns { get; } = new();
    public List<FloraPlacement> Flora { get; } = new();
    public List<PoiStamp> Stamps { get; } = new();
    public int GenVersion { get; set; }
    public bool Modified { get; set; }
    public float WaterLevel { get; set; } = float.NegativeInfinity;

    public ChunkData(ChunkCoord coord, int size)
    {
        Coord = coord;
        Size = size;
        var n = size * size;
        Height = new float[n];
        Flags = new CellFlags[n];
        SurfaceTag = new ushort[n];
        BiomeIndex = new ushort[n];
        TagTable.Add(TagSet.Empty);
        BiomeTable.Add("");
    }

    public int Index(int localX, int localY) => localY * Size + localX;

    public ushort Intern(TagSet tags)
    {
        for (var i = 0; i < TagTable.Count; i++)
        {
            if (TagTable[i].Equals(tags))
            {
                return (ushort)i;
            }
        }
        TagTable.Add(tags);
        return (ushort)(TagTable.Count - 1);
    }

    public ushort InternBiome(string biome)
    {
        var i = BiomeTable.IndexOf(biome);
        if (i >= 0)
        {
            return (ushort)i;
        }
        BiomeTable.Add(biome);
        return (ushort)(BiomeTable.Count - 1);
    }

    public TagSet TagsAt(int index) => TagTable[SurfaceTag[index]];
    public string BiomeAt(int index) => BiomeTable[BiomeIndex[index]];

    public bool IsWalkable(int index) => (Flags[index] & CellFlags.Walkable) != 0 && (Flags[index] & CellFlags.Blocked) == 0;

    public void AddLayer(int index, Surface surface)
    {
        if (!Layers.TryGetValue(index, out var list))
        {
            list = new List<Surface>();
            Layers[index] = list;
        }
        list.Add(surface);
        list.Sort((a, b) => a.Height.CompareTo(b.Height));
    }

    /// <summary>Every surface in a cell from lowest to highest, ground first when it is lowest.</summary>
    public IEnumerable<Surface> SurfacesAt(int index)
    {
        var ground = new Surface(Height[index], IsWalkable(index), TagsAt(index));
        if (!Layers.TryGetValue(index, out var layers))
        {
            yield return ground;
            yield break;
        }
        var emitted = false;
        foreach (var l in layers)
        {
            if (!emitted && ground.Height <= l.Height)
            {
                yield return ground;
                emitted = true;
            }
            yield return l;
        }
        if (!emitted)
        {
            yield return ground;
        }
    }
}

/// <summary>Where a point of interest stamped a landmark into this chunk, for clients and tools.</summary>
public sealed record PoiStamp(string PoiId, string Landmark, int CenterX, int CenterY, float BaseHeight);
