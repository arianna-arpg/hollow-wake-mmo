using System.Numerics;
using HollowWake.Core.Tags;

namespace HollowWake.Core.World;

/// <summary>Supplies chunks on demand: a generator, a store of modified chunks, or a network stream.</summary>
public interface IChunkProvider
{
    ChunkData Load(ChunkCoord coord);
}

/// <summary>
/// The terrain a simulation or a client walks on: chunks loaded on demand from a provider, queried by
/// world coordinates. Ground resolution handles stacked surfaces, so a bridge over a river and a cave
/// under a hill are the same question: which surface is under me at this height.
/// </summary>
public sealed class Terrain
{
    private readonly Dictionary<ChunkCoord, ChunkData> _chunks = new();
    private readonly IChunkProvider _provider;

    public WorldLayout Layout { get; }

    public Terrain(WorldLayout layout, IChunkProvider provider)
    {
        Layout = layout;
        _provider = provider;
    }

    public int LoadedChunkCount => _chunks.Count;
    public IEnumerable<ChunkData> LoadedChunks => _chunks.Values;

    public bool IsLoaded(ChunkCoord c) => _chunks.ContainsKey(c);

    public ChunkData GetChunk(ChunkCoord c)
    {
        if (!_chunks.TryGetValue(c, out var chunk))
        {
            chunk = _provider.Load(c);
            _chunks[c] = chunk;
        }
        return chunk;
    }

    public bool TryGetLoaded(ChunkCoord c, out ChunkData chunk) => _chunks.TryGetValue(c, out chunk!);

    public void Unload(ChunkCoord c) => _chunks.Remove(c);

    public void Clear() => _chunks.Clear();

    public void Preload(IEnumerable<ChunkCoord> coords)
    {
        foreach (var c in coords)
        {
            GetChunk(c);
        }
    }

    // ------------------------------------------------------------ cell queries

    public (ChunkData Chunk, int Index) Locate(CellCoord cell)
    {
        var chunk = GetChunk(Layout.ChunkOf(cell));
        return (chunk, Layout.LocalIndex(cell));
    }

    public float GroundHeight(CellCoord cell)
    {
        var (chunk, i) = Locate(cell);
        return chunk.Height[i];
    }

    /// <summary>Bilinearly interpolated base height, for smooth slopes between cell centers.</summary>
    public float HeightAt(double x, double y)
    {
        var fx = x - 0.5;
        var fy = y - 0.5;
        var x0 = (int)Math.Floor(fx);
        var y0 = (int)Math.Floor(fy);
        var tx = (float)(fx - x0);
        var ty = (float)(fy - y0);
        var h00 = GroundHeight(new CellCoord(x0, y0));
        var h10 = GroundHeight(new CellCoord(x0 + 1, y0));
        var h01 = GroundHeight(new CellCoord(x0, y0 + 1));
        var h11 = GroundHeight(new CellCoord(x0 + 1, y0 + 1));
        var a = h00 + (h10 - h00) * tx;
        var b = h01 + (h11 - h01) * tx;
        return a + (b - a) * ty;
    }

    public bool IsWalkable(CellCoord cell)
    {
        var (chunk, i) = Locate(cell);
        return chunk.IsWalkable(i);
    }

    public TagSet TagsAt(CellCoord cell)
    {
        var (chunk, i) = Locate(cell);
        return chunk.TagsAt(i);
    }

    public string BiomeAt(CellCoord cell)
    {
        var (chunk, i) = Locate(cell);
        return chunk.BiomeAt(i);
    }

    public CellFlags FlagsAt(CellCoord cell)
    {
        var (chunk, i) = Locate(cell);
        return chunk.Flags[i];
    }

    public IEnumerable<Surface> SurfacesAt(CellCoord cell)
    {
        var (chunk, i) = Locate(cell);
        return chunk.SurfacesAt(i);
    }

    /// <summary>
    /// Finds the surface that supports a body at (x, y) whose feet are at height z: the highest walkable
    /// surface not more than <paramref name="stepUp"/> above z. Bodies above every surface are airborne;
    /// the surface below them is returned with <c>airborne</c> true so gravity knows where to land.
    /// </summary>
    public bool TryFindGround(double x, double y, double z, double stepUp, out Surface ground, out bool airborne)
    {
        var cell = CellCoord.Of(x, y);
        Surface? best = null;
        var smoothGround = HeightAt(x, y);
        foreach (var s in SurfacesAt(cell))
        {
            var h = s.Tags.Has("ground") || !IsLayer(s, cell) ? smoothGround : s.Height;
            var candidate = new Surface(h, s.Walkable, s.Tags);
            if (!candidate.Walkable)
            {
                continue;
            }
            if (candidate.Height <= z + stepUp)
            {
                if (best is null || candidate.Height > best.Value.Height)
                {
                    best = candidate;
                }
            }
        }
        if (best is null)
        {
            ground = default;
            airborne = true;
            return false;
        }
        ground = best.Value;
        airborne = z > ground.Height + 0.001;
        return true;
    }

    private bool IsLayer(Surface s, CellCoord cell)
    {
        var (chunk, i) = Locate(cell);
        return chunk.Layers.TryGetValue(i, out var layers) && layers.Contains(s);
    }

    /// <summary>Can a body standing at height z in cell a step into cell b without climbing more than stepUp?</summary>
    public bool CanStep(CellCoord from, CellCoord to, double z, double stepUp, double maxDrop)
    {
        if (!TryFindGround(to.X + 0.5, to.Y + 0.5, z, stepUp, out var ground, out _))
        {
            return false;
        }
        var (chunk, i) = Locate(to);
        if ((chunk.Flags[i] & CellFlags.Blocked) != 0 && ground.Tags.Has("ground"))
        {
            return false;
        }
        return z - ground.Height <= maxDrop;
    }

    public Vector3 SnapToGround(Vector3 position, double stepUp = 0.6)
    {
        if (TryFindGround(position.X, position.Y, position.Z, stepUp, out var g, out _))
        {
            return new Vector3(position.X, position.Y, g.Height);
        }
        return position with { Z = HeightAt(position.X, position.Y) };
    }
}
