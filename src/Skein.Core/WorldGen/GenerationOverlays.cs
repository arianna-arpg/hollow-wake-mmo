using Skein.Core.World;

namespace Skein.Core.WorldGen;

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
}

/// <summary>The active overlays. Changing them bumps <see cref="Version"/> so generated chunks can be refreshed.</summary>
public sealed class GenerationOverlays
{
    private readonly List<GenerationOverlay> _overlays = new();

    public int Version { get; private set; }

    public IReadOnlyList<GenerationOverlay> All => _overlays;

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
    public double Param(WorldLayout layout, string name, double x, double y, double fallback)
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

    /// <summary>Additive parameters (thread density bumps) sum across covering overlays.</summary>
    public double Sum(WorldLayout layout, string name, double x, double y)
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
}
