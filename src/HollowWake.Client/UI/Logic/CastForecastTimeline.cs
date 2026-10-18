using HollowWake.Protocol;

namespace HollowWake.Client.UI.Logic;

/// <summary>Full baseline repair plus terminal lifecycle suppression. No client clock ever releases a cast.</summary>
public sealed class CastForecastTimeline
{
    private string? _epoch;
    private long _sequence = -1;
    private double _time = double.NegativeInfinity;
    private double _validUntil;
    private readonly Dictionary<long, double> _ended = new();
    private readonly Dictionary<long, CastForecastInfo> _active = new();
    private readonly Dictionary<long, long> _revisions = new();
    public IReadOnlyCollection<CastForecastInfo> Active => _active.Values;
    public int Omitted { get; private set; }
    public bool Stale(double now) => now > _validUntil;
    public void Reset(string? epoch = null)
    {
        _epoch = epoch; _sequence = -1; _time = double.NegativeInfinity; _validUntil = 0;
        _active.Clear(); _revisions.Clear(); _ended.Clear(); Omitted = 0;
    }
    public bool Receive(CastForecastMsg message)
    {
        if (_epoch is null || message.Epoch != _epoch || message.Sequence <= _sequence ||
            !double.IsFinite(message.ServerTime) || !double.IsFinite(message.ValidUntil) ||
            message.ServerTime < _time || message.ValidUntil < message.ServerTime) return false;
        _sequence = message.Sequence; _time = message.ServerTime; _validUntil = message.ValidUntil;
        var previous = new Dictionary<long, CastForecastInfo>(_active);
        _active.Clear(); Omitted = message.Omitted;
        foreach (var reference in message.Retained ?? Enumerable.Empty<CastForecastRef>())
        {
            if (_ended.ContainsKey(reference.CastId)) continue;
            if (_revisions.GetValueOrDefault(reference.CastId, -1) == reference.Revision && previous.TryGetValue(reference.CastId, out var kept))
                _active[reference.CastId] = kept;
            else Omitted++; // A skipped full revision is unknown until its periodic repair.
        }
        foreach (var cast in message.Casts)
            if (cast.CastId > 0 && !_ended.ContainsKey(cast.CastId) && Finite(cast))
            { _active[cast.CastId] = cast; _revisions[cast.CastId] = message.Sequence; }
        foreach (var id in _revisions.Keys.Where(id => !_active.ContainsKey(id)).ToArray()) _revisions.Remove(id);
        // A full baseline observed after a terminal event proves the old preparation is gone.
        foreach (var pair in _ended.Where(p => p.Value <= _time).ToArray()) _ended.Remove(pair.Key);
        return true;
    }
    public void Lifecycle(CastCueMsg cue)
    {
        if (cue.CastId <= 0 || cue.Outcome || cue.Phase is not ("end" or "interrupt")) return;
        _active.Remove(cue.CastId);
        _revisions.Remove(cue.CastId);
        // Older complete baselines are rejected by sample time, so only keep newer tombstones.
        if (cue.ServerTime >= _time) _ended[cue.CastId] = cue.ServerTime;
    }
    public void RemoveEntity(long key)
    {
        foreach (var pair in _active.Where(p => p.Value.Source == key).ToArray()) { _active.Remove(pair.Key); _revisions.Remove(pair.Key); }
    }
    private static bool Finite(CastForecastInfo c) => new[] { c.StartedAt, c.X, c.Y, c.Z,
        c.NextEmissionStartedAt ?? 0, c.NextEmissionAt ?? 0, c.ReadyAt ?? 0, c.MaxHoldEndsAt ?? 0 }.All(double.IsFinite);

    public static double Progress(CastForecastInfo cast, double now)
    {
        // Channel cadence is not cast completion. Readiness does not imply release.
        var deadline = cast.ReadyAt ?? cast.NextEmissionAt;
        var start = cast.ReadyAt is not null ? cast.StartedAt : cast.NextEmissionStartedAt ?? cast.StartedAt;
        return deadline is { } end && end > start ? Math.Clamp((now - start) / (end - start), 0, 1) : 0;
    }
    public static string Caption(CastForecastInfo cast, double now, bool stale)
    {
        if (stale) return "stale";
        if (cast.ReadyAt is { } ready && now >= ready) return "ready";
        if (cast.Mode == "channel") return "channel";
        if (cast.NextEmissionAt is { } end && now >= end) return "waiting";
        return Usable(cast.Region) ? "primary" : "unavailable";
    }
    public static bool Usable(CastForecastRegion? r) => r is not null && ShapeOutline.Names.Contains(r.Shape) &&
        r.TargetTest == "center" && r.Size.Radius is > 0 && r.HeightTolerance >= 0 &&
        new[] { r.X, r.Y, r.Z, r.Heading, r.HeightTolerance, r.Size.Radius ?? 0, r.Size.Angle ?? 0,
            r.Size.Width ?? 0, r.Size.Inner ?? 0, r.Size.Curvature ?? 0 }.All(float.IsFinite);
    public static ShapeRegion Outline(CastForecastRegion r, int segments) => ShapeOutline.RegionOf(new ShapeSpec(
        r.Shape, r.Size.Radius!.Value, r.Size.Angle ?? 0, r.Size.Width ?? 0, r.Size.Inner ?? 0, r.Size.Curvature ?? 0), segments);

    /// <summary>Conservative chord bound for every built-in curve. Zero declines an over-budget outline
    /// instead of silently turning a huge modified region into an inaccurate low-polygon footprint.</summary>
    public static int SegmentsFor(CastForecastRegion? r, int minimum, int maximum, double error)
    {
        if (!Usable(r)) return 0;
        var radius = r!.Size.Radius!.Value * Math.Max(1, r.Size.Curvature ?? 0) + (r.Size.Width ?? 0) / 2;
        var angle = Math.Acos(Math.Clamp(1 - error / radius, -1, 1));
        var required = angle > 0 ? Math.Max(minimum, Math.Ceiling(Math.PI / angle)) : double.PositiveInfinity;
        return required <= maximum ? (int)required : 0;
    }
}
