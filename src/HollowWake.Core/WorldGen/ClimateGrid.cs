using System.Runtime.CompilerServices;
using HollowWake.Core.Maths;
using HollowWake.Core.World;

namespace HollowWake.Core.WorldGen;

/// <summary>
/// The climate of one chunk, evaluated together. Elevation is computed once per point on a grid one
/// cell wider than the chunk on each side, so a cell's slope reads its four neighbors from the grid
/// instead of recomputing them (cell centers are half-integers, so a neighbor's coordinate is exactly a
/// grid point). Temperature, moisture and the Thread densities the biome table reads are computed per
/// cell; Threads no biome reads are skipped. Overlays are resolved once for the chunk's area. Every value
/// is exactly what <see cref="WorldGenerator.Sample"/> computes for a cell center.
/// </summary>
internal sealed class ClimateGrid
{
    // The elevation grid is scratch for one chunk at a time on a thread, and every read slot is written
    // before it is read, so each thread keeps one instead of allocating it per chunk.
    [ThreadStatic]
    private static double[]? t_elevation;

    private readonly WorldGenerator _gen;
    private readonly WorldLayout _layout;
    private readonly int _size;
    private readonly int _stride;
    private readonly int _ox;
    private readonly int _oy;
    private readonly double[] _elevation;
    private readonly OverlayParam _waterLevel;
    private readonly OverlayParam _temperatureOffset;
    private readonly OverlayParam _moistureOffset;
    private readonly OverlayParam[] _slotOffsets;
    private readonly int[] _slotThread;
    private RegionQuad? _quad;

    public ClimateGrid(WorldGenerator gen, CellCoord origin, int size)
    {
        _gen = gen;
        _layout = gen.Layout;
        _size = size;
        _stride = size + 2;
        _ox = origin.X;
        _oy = origin.Y;

        // Every point this chunk evaluates lies in this rectangle: the cell centers and one ring around them.
        var touching = gen.Overlays.Touching(_layout, _ox - 0.5, _oy - 0.5, _ox + size + 0.5, _oy + size + 0.5);
        var elevationOffset = OverlayParam.For(touching, "elevation_offset");
        var elevationMultiplier = OverlayParam.For(touching, "elevation_multiplier");
        _waterLevel = OverlayParam.For(touching, "water_level");
        _temperatureOffset = OverlayParam.For(touching, "temperature_offset");
        _moistureOffset = OverlayParam.For(touching, "moisture_offset");
        var biomes = gen.Biomes;
        _slotThread = new int[biomes.SlotCount];
        _slotOffsets = new OverlayParam[biomes.SlotCount];
        for (var s = 0; s < _slotThread.Length; s++)
        {
            var k = biomes.SlotThreadIndex(s);
            _slotThread[s] = k;
            _slotOffsets[s] = k < 0 ? default : OverlayParam.For(touching, gen.ThreadOverlayKey(k));
        }

        var cells = _stride * _stride;
        _elevation = t_elevation is { } scratch && scratch.Length >= cells ? scratch : t_elevation = new double[cells];
        FillElevation(elevationOffset, elevationMultiplier);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void FillElevation(OverlayParam offset, OverlayParam multiplier)
    {
        RegionQuad? quad = null;
        var gen = _gen;
        var layout = _layout;
        var elevation = _elevation;
        for (var gy = -1; gy <= _size; gy++)
        {
            var y = _oy + gy + 0.5;
            var edgeRow = gy < 0 || gy == _size;
            var row = (gy + 1) * _stride + 1;
            for (var gx = -1; gx <= _size; gx++)
            {
                // The corners are no cell's neighbor.
                if (edgeRow && (gx < 0 || gx == _size))
                {
                    continue;
                }
                var x = _ox + gx + 0.5;
                var e = gen.ElevationBase(x, y, ref quad);
                e += offset.Sum(layout, x, y);
                e *= multiplier.Param(layout, x, y, 1);
                elevation[row + gx] = e;
            }
        }
    }

    /// <summary>
    /// Fills a biome table value vector for the cell at local (lx, ly) and returns its elevation, with
    /// its slope and water level.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public double Cell(int lx, int ly, Span<double> values, out double slope, out double water)
    {
        var gen = _gen;
        var x = _ox + lx + 0.5;
        var y = _oy + ly + 0.5;
        gen.Blend(x, y, ref _quad, out var tx, out var ty);
        var q = _quad!;

        var at = (ly + 1) * _stride + lx + 1;
        var elevation = _elevation[at];
        water = _waterLevel.Param(_layout, x, y, gen.SeaLevel);
        var ex = _elevation[at + 1] - _elevation[at - 1];
        var ey = _elevation[at + _stride] - _elevation[at - _stride];
        slope = Math.Sqrt(ex * ex + ey * ey) / 2.0;

        Noise2D.FbmPair(gen.TemperatureNoise, x / gen.TemperatureScale, y / gen.TemperatureScale, gen.MoistureNoise, x / gen.MoistureScale, y / gen.MoistureScale, 3, 2.0, 0.5, out var tNoise, out var mNoise);
        values[BiomeTable.RelativeElevation] = gen.RelativeElevation(elevation, water);
        values[BiomeTable.Temperature] = WorldGenerator.Temperature(
            tNoise,
            WorldGenerator.W(q.R00.TempBias, q.R10.TempBias, q.R01.TempBias, q.R11.TempBias, tx, ty),
            elevation,
            water,
            gen.LapseRate,
            _temperatureOffset.Sum(_layout, x, y));
        values[BiomeTable.Moisture] = WorldGenerator.Moisture(
            mNoise,
            WorldGenerator.W(q.R00.MoistBias, q.R10.MoistBias, q.R01.MoistBias, q.R11.MoistBias, tx, ty),
            _moistureOffset.Sum(_layout, x, y));
        values[BiomeTable.Slope] = slope;
        for (var s = 0; s < _slotThread.Length; s++)
        {
            var k = _slotThread[s];
            values[BiomeTable.FirstSlot + s] = k < 0
                ? double.NaN
                : WorldGenerator.ThreadDensity(
                    gen.ThreadNoise(k).Fbm(x / gen.ThreadScale, y / gen.ThreadScale, 2, 2.0, 0.5),
                    WorldGenerator.W(q.R00.Threads[k], q.R10.Threads[k], q.R01.Threads[k], q.R11.Threads[k], tx, ty),
                    _slotOffsets[s].Sum(_layout, x, y));
        }
        return elevation;
    }
}
