using System.Runtime.CompilerServices;
using HollowWake.Core.World;

namespace HollowWake.Core.WorldGen;

/// <summary>
/// A change to generation parameters over a scope: a world event flooding a region, a decree raising
/// the Thread density around a bound fount, a server-wide season. Overlays are consulted by every pass
/// through <see cref="GenerationOverlays.Param"/> so content never hard-codes how events touch terrain.
/// </summary>
public sealed record GenerationOverlay
{
    public required string Id { get; init; }
    public HashSet<RegionCoord> Regions { get; init; } = new();
    public HashSet<ZoneCoord> Zones { get; init; } = new();
    /// <summary>Circle in world cells; null for none.</summary>
    public (double X, double Y, double Radius)? Circle { get; init; }
    public bool Global { get; init; }
    public Dictionary<string, double> Params { get; init; } = new(StringComparer.Ordinal);
    public int Priority { get; init; }

    public bool Covers(WorldLayout layout, double x, double y)
    {
        if (Global)
        {
            return true;
        }
        if (Circle is { } c)
        {
            var dx = x - c.X;
            var dy = y - c.Y;
            if (dx * dx + dy * dy <= c.Radius * c.Radius)
            {
                return true;
            }
        }
        if (Zones.Count > 0 && Zones.Contains(layout.ZoneOf(x, y)))
        {
            return true;
        }
        if (Regions.Count > 0 && Regions.Contains(layout.RegionOf(x, y)))
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// False only when no point of the rectangle can be covered. Conservative (a circle gets a cell of
    /// slack) so skipping an overlay on false never changes what <see cref="Covers"/> would decide.
    /// </summary>
    internal bool MayCover(WorldLayout layout, double minX, double minY, double maxX, double maxY)
    {
        if (Global)
        {
            return true;
        }
        if (Circle is { } c)
        {
            var dx = Math.Max(0, Math.Max(minX - c.X, c.X - maxX));
            var dy = Math.Max(0, Math.Max(minY - c.Y, c.Y - maxY));
            var reach = c.Radius + 1;
            if (!(dx * dx + dy * dy > reach * reach))
            {
                return true;
            }
        }
        if (Zones.Count > 0)
        {
            var lo = layout.ZoneOf(minX, minY);
            var hi = layout.ZoneOf(maxX, maxY);
            foreach (var z in Zones)
            {
                if (z.X >= lo.X && z.X <= hi.X && z.Y >= lo.Y && z.Y <= hi.Y)
                {
                    return true;
                }
            }
        }
        if (Regions.Count > 0)
        {
            var lo = layout.RegionOf(minX, minY);
            var hi = layout.RegionOf(maxX, maxY);
            foreach (var r in Regions)
            {
                if (r.X >= lo.X && r.X <= hi.X && r.Y >= lo.Y && r.Y <= hi.Y)
                {
                    return true;
                }
            }
        }
        return false;
    }
}

/// <summary>The active overlays. Changing them bumps <see cref="Version"/> so generated chunks can be refreshed.</summary>
public sealed class GenerationOverlays
{
    private readonly List<GenerationOverlay> _overlays = new();

    public int Version { get; private set; }

    public IReadOnlyList<GenerationOverlay> All => _overlays;

    internal int Count => _overlays.Count;

    public void Add(GenerationOverlay overlay)
    {
        _overlays.RemoveAll(o => o.Id == overlay.Id);
        _overlays.Add(overlay);
        _overlays.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        Version++;
    }

    public bool Remove(string id)
    {
        var removed = _overlays.RemoveAll(o => o.Id == id) > 0;
        if (removed)
        {
            Version++;
        }
        return removed;
    }

    /// <summary>The parameter at a point: the highest-priority covering overlay that sets it, else the default.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Param(WorldLayout layout, string name, double x, double y, double fallback) =>
        _overlays.Count == 0 ? fallback : ParamOfCovering(layout, name, x, y, fallback);

    /// <summary>Additive parameters (thread density bumps) sum across covering overlays.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Sum(WorldLayout layout, string name, double x, double y) =>
        _overlays.Count == 0 ? 0 : SumOfCovering(layout, name, x, y);

    // Kept out of line: with no overlays (the usual case) callers only test the count.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private double ParamOfCovering(WorldLayout layout, string name, double x, double y, double fallback)
    {
        var value = fallback;
        foreach (var o in _overlays)
        {
            if (o.Params.TryGetValue(name, out var v) && o.Covers(layout, x, y))
            {
                value = v;
            }
        }
        return value;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private double SumOfCovering(WorldLayout layout, string name, double x, double y)
    {
        double total = 0;
        foreach (var o in _overlays)
        {
            if (o.Params.TryGetValue(name, out var v) && o.Covers(layout, x, y))
            {
                total += v;
            }
        }
        return total;
    }

    public bool AnyCovering(WorldLayout layout, double x, double y) => _overlays.Any(o => o.Covers(layout, x, y));

    /// <summary>The overlays, in order, that may cover some point of a rectangle; empty when none can.</summary>
    internal GenerationOverlay[] Touching(WorldLayout layout, double minX, double minY, double maxX, double maxY)
    {
        if (_overlays.Count == 0)
        {
            return Array.Empty<GenerationOverlay>();
        }
        List<GenerationOverlay>? touching = null;
        foreach (var o in _overlays)
        {
            if (o.MayCover(layout, minX, minY, maxX, maxY))
            {
                (touching ??= new List<GenerationOverlay>()).Add(o);
            }
        }
        return touching is null ? Array.Empty<GenerationOverlay>() : touching.ToArray();
    }
}

/// <summary>
/// One overlay parameter resolved over an area (a chunk): the overlays that set it and may cover the
/// area, in order. Evaluates exactly like <see cref="GenerationOverlays.Sum"/> and
/// <see cref="GenerationOverlays.Param"/> at any point of that area, but answers without looking anything
/// up when nothing sets the parameter there or every setter is global.
/// </summary>
internal readonly struct OverlayParam
{
    private readonly GenerationOverlay[]? _overlays;
    private readonly double[]? _values;
    private readonly bool _uniform;
    private readonly double _sum;
    private readonly double _last;

    private OverlayParam(GenerationOverlay[] overlays, double[] values)
    {
        _overlays = overlays;
        _values = values;
        _uniform = true;
        double sum = 0;
        foreach (var o in overlays)
        {
            _uniform &= o.Global;
        }
        for (var i = 0; i < values.Length; i++)
        {
            sum += values[i];
        }
        _sum = sum;
        _last = values[^1];
    }

    public static OverlayParam For(GenerationOverlay[] touching, string name)
    {
        if (touching.Length == 0)
        {
            return default;
        }
        List<GenerationOverlay>? setters = null;
        List<double>? values = null;
        foreach (var o in touching)
        {
            if (o.Params.TryGetValue(name, out var v))
            {
                (setters ??= new List<GenerationOverlay>()).Add(o);
                (values ??= new List<double>()).Add(v);
            }
        }
        return setters is null ? default : new OverlayParam(setters.ToArray(), values!.ToArray());
    }

    public double Sum(WorldLayout layout, double x, double y)
    {
        double total = 0;
        if (_overlays is null)
        {
            return total;
        }
        if (_uniform)
        {
            return _sum;
        }
        for (var i = 0; i < _overlays.Length; i++)
        {
            if (_overlays[i].Covers(layout, x, y))
            {
                total += _values![i];
            }
        }
        return total;
    }

    public double Param(WorldLayout layout, double x, double y, double fallback)
    {
        if (_overlays is null)
        {
            return fallback;
        }
        if (_uniform)
        {
            return _last;
        }
        var value = fallback;
        for (var i = 0; i < _overlays.Length; i++)
        {
            if (_overlays[i].Covers(layout, x, y))
            {
                value = _values![i];
            }
        }
        return value;
    }
}
