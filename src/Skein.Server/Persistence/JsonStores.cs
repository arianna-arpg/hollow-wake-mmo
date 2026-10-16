using System.Text.Json;
using Skein.Core.Characters;
using Skein.Core.Content;
using Skein.Core.Territory;
using Skein.Core.World;

namespace Skein.Server.Persistence;

public sealed class Account
{
    public required string Id { get; init; }
    public string Name { get; set; } = "";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastLoginAt { get; set; } = DateTimeOffset.UtcNow;
    public List<string> Characters { get; set; } = new();
}

public interface IAccountStore
{
    Account? Load(string id);
    void Save(Account account);
}

public interface ICharacterStore
{
    CharacterSheet? Load(string id);
    void Save(CharacterSheet sheet);
    string NewId();
}

/// <summary>Persisted world-level state: holdings, active events, the item uid counter.</summary>
public sealed class WorldSave
{
    public Dictionary<string, HoldingSave> Holdings { get; set; } = new();
    public List<EventSave> Events { get; set; } = new();
    public long NextItemUid { get; set; } = 1;
    public double WorldTime { get; set; }
}

public sealed class HoldingSave
{
    public required string Id { get; init; }
    public string Name { get; set; } = "";
    public required string Owner { get; init; }
    public string? Faction { get; set; }
    public List<string> Founts { get; set; } = new();
    public List<string> Decrees { get; set; } = new();
    public List<PlacedStructure> Structures { get; set; } = new();
    public Dictionary<string, double> Treasury { get; set; } = new();
    public double Influence { get; set; }
    public double CreatedAt { get; set; }
}

public sealed class EventSave
{
    public required string Id { get; init; }
    public required string Definition { get; init; }
    public double EndsAt { get; set; }
    public List<int[]> Regions { get; set; } = new();
}

public interface IWorldStore
{
    WorldSave? Load();
    void Save(WorldSave save);
    ChunkData? LoadChunk(ChunkCoord coord);
    void SaveChunk(ChunkData chunk);
}

/// <summary>
/// One JSON file per record under the data directory. Enough for a small world; the interfaces are
/// what the rest of the server depends on, so a database store is a drop-in later.
/// </summary>
public sealed class JsonFileStores : IAccountStore, ICharacterStore, IWorldStore
{
    private readonly string _root;
    private readonly JsonSerializerOptions _options;

    public JsonFileStores(string root)
    {
        _root = root;
        _options = new JsonSerializerOptions(ContentJson.Options) { WriteIndented = true };
        Directory.CreateDirectory(Path.Combine(_root, "accounts"));
        Directory.CreateDirectory(Path.Combine(_root, "characters"));
        Directory.CreateDirectory(Path.Combine(_root, "chunks"));
    }

    private static string Safe(string id) => string.Concat(id.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));

    public Account? Load(string id) => Read<Account>(Path.Combine(_root, "accounts", Safe(id) + ".json"));
    public void Save(Account account) => Write(Path.Combine(_root, "accounts", Safe(account.Id) + ".json"), account);

    CharacterSheet? ICharacterStore.Load(string id) => Read<CharacterSheet>(Path.Combine(_root, "characters", Safe(id) + ".json"));
    public void Save(CharacterSheet sheet) => Write(Path.Combine(_root, "characters", Safe(sheet.Id) + ".json"), sheet);
    public string NewId() => "c" + Guid.NewGuid().ToString("N")[..12];

    public WorldSave? Load() => Read<WorldSave>(Path.Combine(_root, "world.json"));
    public void Save(WorldSave save) => Write(Path.Combine(_root, "world.json"), save);

    public ChunkData? LoadChunk(ChunkCoord coord)
    {
        var dto = Read<ChunkDto>(Path.Combine(_root, "chunks", $"{coord.X}_{coord.Y}.json"));
        return dto?.ToChunk();
    }

    public void SaveChunk(ChunkData chunk) => Write(Path.Combine(_root, "chunks", $"{chunk.Coord.X}_{chunk.Coord.Y}.json"), ChunkDto.From(chunk));

    private T? Read<T>(string path) where T : class
    {
        if (!File.Exists(path))
        {
            return null;
        }
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), _options);
    }

    private void Write<T>(string path, T value)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, _options));
        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>Serializable form of a chunk. Only modified chunks are stored; the rest regenerate.</summary>
    public sealed class ChunkDto
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Size { get; set; }
        public float[] Height { get; set; } = Array.Empty<float>();
        public byte[] Flags { get; set; } = Array.Empty<byte>();
        public ushort[] SurfaceTag { get; set; } = Array.Empty<ushort>();
        public ushort[] BiomeIndex { get; set; } = Array.Empty<ushort>();
        public List<string> TagTable { get; set; } = new();
        public List<string> BiomeTable { get; set; } = new();
        public List<LayerDto> Layers { get; set; } = new();
        public List<SpawnPoint> Spawns { get; set; } = new();
        public List<FloraPlacement> Flora { get; set; } = new();
        public int GenVersion { get; set; }
        public float WaterLevel { get; set; }

        public sealed record LayerDto(int Index, float Height, bool Walkable, string Tags);

        public static ChunkDto From(ChunkData c) => new()
        {
            X = c.Coord.X,
            Y = c.Coord.Y,
            Size = c.Size,
            Height = c.Height,
            Flags = c.Flags.Select(f => (byte)f).ToArray(),
            SurfaceTag = c.SurfaceTag,
            BiomeIndex = c.BiomeIndex,
            TagTable = c.TagTable.Select(t => string.Join(" ", t)).ToList(),
            BiomeTable = c.BiomeTable.ToList(),
            Layers = c.Layers.SelectMany(kv => kv.Value.Select(s => new LayerDto(kv.Key, s.Height, s.Walkable, string.Join(" ", s.Tags)))).ToList(),
            Spawns = c.Spawns.ToList(),
            Flora = c.Flora.ToList(),
            GenVersion = c.GenVersion,
            WaterLevel = c.WaterLevel,
        };

        public ChunkData ToChunk()
        {
            var c = new ChunkData(new ChunkCoord(X, Y), Size) { GenVersion = GenVersion, Modified = true, WaterLevel = WaterLevel };
            Array.Copy(Height, c.Height, Math.Min(Height.Length, c.Height.Length));
            for (var i = 0; i < Math.Min(Flags.Length, c.Flags.Length); i++)
            {
                c.Flags[i] = (CellFlags)Flags[i];
            }
            c.TagTable.Clear();
            foreach (var t in TagTable)
            {
                c.TagTable.Add(Core.Tags.TagSet.Parse(t));
            }
            if (c.TagTable.Count == 0)
            {
                c.TagTable.Add(Core.Tags.TagSet.Empty);
            }
            c.BiomeTable.Clear();
            c.BiomeTable.AddRange(BiomeTable.Count == 0 ? new[] { "" } : BiomeTable);
            Array.Copy(SurfaceTag, c.SurfaceTag, Math.Min(SurfaceTag.Length, c.SurfaceTag.Length));
            Array.Copy(BiomeIndex, c.BiomeIndex, Math.Min(BiomeIndex.Length, c.BiomeIndex.Length));
            foreach (var l in Layers)
            {
                c.AddLayer(l.Index, new Surface(l.Height, l.Walkable, Core.Tags.TagSet.Parse(l.Tags)));
            }
            c.Spawns.AddRange(Spawns);
            c.Flora.AddRange(Flora);
            return c;
        }
    }
}
