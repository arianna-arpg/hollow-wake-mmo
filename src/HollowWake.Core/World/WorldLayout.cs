namespace HollowWake.Core.World;

/// <summary>A terrain cell, one world unit square. Entity positions are floats; cells are their floors.</summary>
public readonly record struct CellCoord(int X, int Y)
{
    public static CellCoord Of(double x, double y) => new((int)Math.Floor(x), (int)Math.Floor(y));
    public CellCoord Offset(int dx, int dy) => new(X + dx, Y + dy);
    public override string ToString() => $"cell({X},{Y})";
}

/// <summary>The unit of generation, streaming and persistence.</summary>
public readonly record struct ChunkCoord(int X, int Y)
{
    public override string ToString() => $"chunk({X},{Y})";
}

/// <summary>The unit of simulation and server scaling.</summary>
public readonly record struct ZoneCoord(int X, int Y)
{
    public override string ToString() => $"zone({X},{Y})";
    public string Key => $"{X}_{Y}";
}

/// <summary>The unit of macro generation, politics and world events.</summary>
public readonly record struct RegionCoord(int X, int Y)
{
    public override string ToString() => $"region({X},{Y})";
    public string Key => $"{X}_{Y}";
}

/// <summary>
/// The world hierarchy: world, regions, zones, chunks, cells. Sizes come from rules so a server can run
/// small zones for a handful of players or large ones for many. All coordinates are signed and the
/// world extends in every direction from the origin.
/// </summary>
public sealed record WorldLayout
{
    public int ChunkSize { get; init; } = 32;
    public int ZoneChunks { get; init; } = 8;
    public int RegionZones { get; init; } = 8;

    public int ZoneSize => ChunkSize * ZoneChunks;
    public int RegionSize => ZoneSize * RegionZones;

    public static readonly WorldLayout Default = new();

    public ChunkCoord ChunkOf(CellCoord c) => new(FloorDiv(c.X, ChunkSize), FloorDiv(c.Y, ChunkSize));
    public ChunkCoord ChunkOf(double x, double y) => ChunkOf(CellCoord.Of(x, y));
    public ZoneCoord ZoneOf(CellCoord c) => new(FloorDiv(c.X, ZoneSize), FloorDiv(c.Y, ZoneSize));
    public ZoneCoord ZoneOf(double x, double y) => ZoneOf(CellCoord.Of(x, y));
    public ZoneCoord ZoneOf(ChunkCoord c) => new(FloorDiv(c.X, ZoneChunks), FloorDiv(c.Y, ZoneChunks));
    public RegionCoord RegionOf(CellCoord c) => new(FloorDiv(c.X, RegionSize), FloorDiv(c.Y, RegionSize));
    public RegionCoord RegionOf(ZoneCoord z) => new(FloorDiv(z.X, RegionZones), FloorDiv(z.Y, RegionZones));
    public RegionCoord RegionOf(double x, double y) => RegionOf(CellCoord.Of(x, y));

    public CellCoord ChunkOrigin(ChunkCoord c) => new(c.X * ChunkSize, c.Y * ChunkSize);
    public CellCoord ZoneOrigin(ZoneCoord z) => new(z.X * ZoneSize, z.Y * ZoneSize);
    public CellCoord RegionOrigin(RegionCoord r) => new(r.X * RegionSize, r.Y * RegionSize);

    public (double X, double Y) ZoneCenter(ZoneCoord z) => (z.X * ZoneSize + ZoneSize / 2.0, z.Y * ZoneSize + ZoneSize / 2.0);
    public (double X, double Y) RegionCenter(RegionCoord r) => (r.X * RegionSize + RegionSize / 2.0, r.Y * RegionSize + RegionSize / 2.0);

    public IEnumerable<ChunkCoord> ChunksOf(ZoneCoord z)
    {
        for (var y = 0; y < ZoneChunks; y++)
        {
            for (var x = 0; x < ZoneChunks; x++)
            {
                yield return new ChunkCoord(z.X * ZoneChunks + x, z.Y * ZoneChunks + y);
            }
        }
    }

    /// <summary>Local cell index within a chunk for a world cell.</summary>
    public int LocalIndex(CellCoord c)
    {
        var lx = Mod(c.X, ChunkSize);
        var ly = Mod(c.Y, ChunkSize);
        return ly * ChunkSize + lx;
    }

    public static int FloorDiv(int a, int b) => (int)Math.Floor((double)a / b);
    public static int Mod(int a, int b) => ((a % b) + b) % b;
}
