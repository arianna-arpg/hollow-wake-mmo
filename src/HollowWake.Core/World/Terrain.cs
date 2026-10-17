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
/// <remarks>
/// Cell queries are the hottest calls in movement and pathfinding, so they avoid allocation and remember
/// the last chunk they touched: neighbouring queries almost always land in the same chunk. A chunk
/// knows its own coordinate, so the memo is a single reference and stays consistent for readers.
/// </remarks>
public sealed class Terrain
{
    private readonly Dictionary<ChunkCoord, ChunkData> _chunks = new();
    private readonly IChunkProvider _provider;
    private readonly int _chunkSize;
    private ChunkData? _last;

    public WorldLayout Layout { get; }

    public Terrain(WorldLayout layout, IChunkProvider provider)
    {
        Layout = layout;
        _provider = provider;
        _chunkSize = layout.ChunkSize;
    }

    public int LoadedChunkCount => _chunks.Count;
    public IEnumerable<ChunkData> LoadedChunks => _chunks.Values;

    public bool IsLoaded(ChunkCoord c) => _chunks.ContainsKey(c);

    public ChunkData GetChunk(ChunkCoord c)
    {
        var last = _last;
        if (last is not null && last.Coord == c)
        {
            return last;
        }
        if (!_chunks.TryGetValue(c, out var chunk))
        {
            chunk = _provider.Load(c);
            _chunks[c] = chunk;
        }
        if (chunk.Coord == c)
        {
            _last = chunk;
        }
        return chunk;
    }

    public bool TryGetLoaded(ChunkCoord c, out ChunkData chunk) => _chunks.TryGetValue(c, out chunk!);

    public void Unload(ChunkCoord c)
    {
        _chunks.Remove(c);
        if (_last is { } last && last.Coord == c)
        {
            _last = null;
        }
    }

    public void Clear()
    {
        _chunks.Clear();
        _last = null;
    }

    public void Preload(IEnumerable<ChunkCoord> coords)
    {
        foreach (var c in coords)
        {
            GetChunk(c);
        }
    }

    // ------------------------------------------------------------ cell queries

    /// <summary>
    /// The chunk holding a world cell and the cell's local coordinates in it. Integer floor division
    /// gives exactly what <see cref="WorldLayout.ChunkOf(CellCoord)"/> and <see cref="WorldLayout.LocalIndex"/> give.
    /// </summary>
    private ChunkData ChunkAt(int x, int y, out int lx, out int ly)
    {
        var size = _chunkSize;
        var cx = x >= 0 ? x / size : (x + 1) / size - 1;
        var cy = y >= 0 ? y / size : (y + 1) / size - 1;
        lx = x - cx * size;
        ly = y - cy * size;
        return GetChunk(new ChunkCoord(cx, cy));
    }

    private ChunkData ChunkAt(int x, int y, out int index)
    {
        var chunk = ChunkAt(x, y, out var lx, out var ly);
        index = ly * _chunkSize + lx;
        return chunk;
    }

    public (ChunkData Chunk, int Index) Locate(CellCoord cell)
    {
        var chunk = ChunkAt(cell.X, cell.Y, out int index);
        return (chunk, index);
    }

    public float GroundHeight(CellCoord cell)
    {
        var chunk = ChunkAt(cell.X, cell.Y, out int i);
        return chunk.Height[i];
    }

    private float GroundHeight(int x, int y)
    {
        var chunk = ChunkAt(x, y, out int i);
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
        float h00, h10, h01, h11;
        var size = _chunkSize;
        var chunk = ChunkAt(x0, y0, out var lx, out var ly);
        if (lx < size - 1 && ly < size - 1)
        {
            // All four samples share a chunk: one lookup instead of four.
            var heights = chunk.Height;
            var i = ly * size + lx;
            h00 = heights[i];
            h10 = heights[i + 1];
            h01 = heights[i + size];
            h11 = heights[i + size + 1];
        }
        else
        {
            h00 = chunk.Height[ly * size + lx];
            h10 = GroundHeight(x0 + 1, y0);
            h01 = GroundHeight(x0, y0 + 1);
            h11 = GroundHeight(x0 + 1, y0 + 1);
        }
        var a = h00 + (h10 - h00) * tx;
        var b = h01 + (h11 - h01) * tx;
        return a + (b - a) * ty;
    }

    public bool IsWalkable(CellCoord cell)
    {
        var chunk = ChunkAt(cell.X, cell.Y, out int i);
        return chunk.IsWalkable(i);
    }

    public TagSet TagsAt(CellCoord cell)
    {
        var chunk = ChunkAt(cell.X, cell.Y, out int i);
        return chunk.TagsAt(i);
    }

    public string BiomeAt(CellCoord cell)
    {
        var chunk = ChunkAt(cell.X, cell.Y, out int i);
        return chunk.BiomeAt(i);
    }

    public CellFlags FlagsAt(CellCoord cell)
    {
        var chunk = ChunkAt(cell.X, cell.Y, out int i);
        return chunk.Flags[i];
    }

    public IEnumerable<Surface> SurfacesAt(CellCoord cell)
    {
        var chunk = ChunkAt(cell.X, cell.Y, out int i);
        return chunk.SurfacesAt(i);
    }

    /// <summary>
    /// Finds the surface that supports a body at (x, y) whose feet are at height z: the highest walkable
    /// surface not more than <paramref name="stepUp"/> above z. Bodies above every surface are airborne;
    /// the surface below them is returned with <c>airborne</c> true so gravity knows where to land.
    /// </summary>
    public bool TryFindGround(double x, double y, double z, double stepUp, out Surface ground, out bool airborne) =>
        TryFindGround(x, y, z, stepUp, out ground, out airborne, out _);

    /// <summary>
    /// <see cref="TryFindGround(double, double, double, double, out Surface, out bool)"/> that also returns
    /// the cell's flags, so callers that check blocking do not locate the cell twice.
    /// </summary>
    public bool TryFindGround(double x, double y, double z, double stepUp, out Surface ground, out bool airborne, out CellFlags flags)
    {
        var chunk = ChunkAt((int)Math.Floor(x), (int)Math.Floor(y), out int index);
        flags = chunk.Flags[index];
        var smoothGround = HeightAt(x, y);
        var limit = z + stepUp;
        var found = false;
        var best = default(Surface);
        var baseWalkable = chunk.IsWalkable(index);
        if (chunk.Layers.Count == 0 || !chunk.Layers.TryGetValue(index, out var layers))
        {
            // Only the base surface, which always takes the smooth height.
            if (baseWalkable && smoothGround <= limit)
            {
                best = new Surface(smoothGround, true, chunk.TagsAt(index));
                found = true;
            }
        }
        else
        {
            // Surfaces from lowest to highest with the base among them, as SurfacesAt orders them.
            // A surface tagged ground, or one that is not a layer, takes the smooth height; layers stay flat.
            var baseSurface = new Surface(chunk.Height[index], baseWalkable, chunk.TagsAt(index));
            var emitted = false;
            foreach (var layer in layers)
            {
                if (!emitted && baseSurface.Height <= layer.Height)
                {
                    var h = baseSurface.Tags.Has("ground") || !layers.Contains(baseSurface) ? smoothGround : baseSurface.Height;
                    Consider(baseSurface, h, limit, ref best, ref found);
                    emitted = true;
                }
                Consider(layer, layer.Tags.Has("ground") ? smoothGround : layer.Height, limit, ref best, ref found);
            }
            if (!emitted)
            {
                var h = baseSurface.Tags.Has("ground") || !layers.Contains(baseSurface) ? smoothGround : baseSurface.Height;
                Consider(baseSurface, h, limit, ref best, ref found);
            }
        }
        if (!found)
        {
            ground = default;
            airborne = true;
            return false;
        }
        ground = best;
        airborne = z > ground.Height + 0.001;
        return true;
    }

    private static void Consider(Surface s, float height, double limit, ref Surface best, ref bool found)
    {
        if (!s.Walkable || !(height <= limit))
        {
            return;
        }
        if (!found || height > best.Height)
        {
            best = new Surface(height, s.Walkable, s.Tags);
            found = true;
        }
    }

    /// <summary>Can a body standing at height z in cell a step into cell b without climbing more than stepUp?</summary>
    public bool CanStep(CellCoord from, CellCoord to, double z, double stepUp, double maxDrop)
    {
        if (!TryFindGround(to.X + 0.5, to.Y + 0.5, z, stepUp, out var ground, out _, out var flags))
        {
            return false;
        }
        if ((flags & CellFlags.Blocked) != 0 && ground.Tags.Has("ground"))
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
