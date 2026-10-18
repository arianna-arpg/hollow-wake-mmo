using System.Text.Json;
using HollowWake.Core.Definitions;

namespace HollowWake.Client.UI.Logic;

public sealed class EntrySceneSpec
{
    public string Activity { get; set; } = "";
    public double Radius { get; set; }
    public double SeatGap { get; set; }
    public double RankGap { get; set; }
    public double ArcFrom { get; set; }
    public double ArcTo { get; set; }
    public double DwellRadius { get; set; }
    public double BodyRadius { get; set; }
    public double DwellSeconds { get; set; }
    public double Speed { get; set; }
    public double TurnRate { get; set; }
    public double WrapRadius { get; set; }
    public double WrapReentry { get; set; }
    public double WrapClear { get; set; }
    public double Units { get; set; }
    public double CameraSpan { get; set; }
    public double BodyHeight { get; set; }
    public double BodyWidth { get; set; }
    public double WispRadius { get; set; }
    public double CameraHeight { get; set; }
    public double CameraOffset { get; set; }
    public double CameraLead { get; set; }
    public double ApparitionOpacity { get; set; }
    public double MaxFrame { get; set; }
    public string WispSource { get; set; } = "";
    public Dictionary<string, float> Layout { get; set; } = new();
    public Dictionary<string, string> Palette { get; set; } = new();
    public Dictionary<string, string> Labels { get; set; } = new();
    public static EntrySceneSpec Read(UiEntrySceneDef d) => JsonSerializer.Deserialize<EntrySceneSpec>(d.Meta!.ToJsonString(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
}

public sealed record VesselChoice(string Id, string Calling, string Name, string Description, string Look, string? Character, bool Available, int Rank);
public sealed class VesselSeat(VesselChoice choice, double x, double y)
{
    public VesselChoice Choice { get; } = choice;
    public double X { get; } = x;
    public double Y { get; } = y;
    public double Yaw { get; set; } = Math.Atan2(-y, -x);
}
public sealed record SelectionRing(double Radius, double[] Angles, bool Closed);

/// <summary>Engine-free spatial choice mechanism. No account state, world entity, combat or reward exists here.</summary>
public sealed class SpatialSelection(EntrySceneSpec spec)
{
    public EntrySceneSpec Spec { get; } = spec;
    public List<VesselSeat> Seats { get; } = new();
    public double X { get; private set; }
    public double Y { get; private set; }
    public double Progress { get; private set; }
    public int Engaged { get; private set; } = -1;
    public int Opened { get; private set; } = -1;
    public double Rim { get; private set; }
    private int _latched = -1;
    private (double X, double Y)? _target;

    // The prototype's muRankRing, with its dials supplied by content.
    public static SelectionRing Ring(int n, double radius, double gap, double from, double to)
    {
        var span = to - from; var center = from + span / 2;
        if (n <= 0) return new(radius, [], false);
        if (n == 1) return new(radius, [center], false);
        double[] Angles(double start, double width) => Enumerable.Range(0, n).Select(i => start + width * i / (n - 1)).ToArray();
        if (2 * radius * Math.Sin(span / (2 * (n - 1))) >= gap - 1e-9) return new(radius, Angles(from, span), false);
        var need = 2 * (n - 1) * Math.Asin(Math.Min(1, gap / (2 * radius)));
        var full = 2 * Math.PI * (n - 1) / n;
        if (need <= full) return new(radius, Angles(center - need / 2, need), false);
        return new(Math.Max(radius, gap / (2 * Math.Sin(Math.PI / n))), Angles(center - full / 2, full), true);
    }

    public void SetChoices(IEnumerable<VesselChoice> choices)
    {
        Seats.Clear(); X = Y = Progress = 0; Engaged = Opened = _latched = -1; _target = null;
        var radius = Spec.Radius; var outer = 0.0;
        foreach (var rank in choices.GroupBy(c => c.Rank).OrderBy(g => g.Key))
        {
            var list = rank.ToArray(); var ring = Ring(list.Length, radius, Spec.SeatGap, Spec.ArcFrom, Spec.ArcTo);
            for (var i = 0; i < list.Length; i++) Seats.Add(new(list[i], Math.Cos(ring.Angles[i]) * ring.Radius, Math.Sin(ring.Angles[i]) * ring.Radius));
            outer = ring.Radius; radius = ring.Radius + Spec.RankGap;
        }
        Rim = Math.Max(Spec.WrapRadius, outer + Spec.WrapClear);
    }

    public void MoveTo(double x, double y) { _target = (x, y); }
    public void Open(int index) { if (index >= 0 && index < Seats.Count) { Opened = _latched = index; Progress = 0; _target = null; } }
    public void Close() { Opened = -1; Progress = 0; _target = null; }
    public void Suspend() { _target = null; Progress = 0; }

    public void Step(double dt, double dx, double dy, bool suspended = false)
    {
        if (suspended || Opened >= 0) { Suspend(); return; }
        dt = Math.Clamp(dt, 0, Spec.MaxFrame);
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length > 0) _target = null;
        if (length == 0 && _target is { } target)
        {
            dx = target.X - X; dy = target.Y - Y; length = Math.Sqrt(dx * dx + dy * dy);
            if (length <= Spec.Speed * dt) { X = target.X; Y = target.Y; _target = null; dx = dy = length = 0; }
        }
        if (length > 0) { X += dx / length * Spec.Speed * dt; Y += dy / length * Spec.Speed * dt; }
        var distance = Math.Sqrt(X * X + Y * Y);
        if (distance > Rim) { var back = Rim - (Spec.WrapRadius - Spec.WrapReentry); X *= -back / distance; Y *= -back / distance; _target = null; }
        var nearest = -1; var best = Spec.DwellRadius + Spec.BodyRadius;
        for (var i = 0; i < Seats.Count; i++)
        {
            var s = Seats[i]; var vx = X - s.X; var vy = Y - s.Y; var d = Math.Sqrt(vx * vx + vy * vy);
            var desired = Math.Atan2(vy, vx); var delta = Math.Atan2(Math.Sin(desired - s.Yaw), Math.Cos(desired - s.Yaw));
            s.Yaw += Math.Clamp(delta, -Spec.TurnRate * dt, Spec.TurnRate * dt);
            if (d < best) { best = d; nearest = i; }
        }
        if (_latched >= 0 && nearest != _latched) _latched = -1;
        if (nearest != Engaged || length > 0) Progress = 0;
        Engaged = nearest;
        if (nearest < 0 || length > 0 || nearest == _latched || !Seats[nearest].Choice.Available) return;
        Progress += dt / Spec.DwellSeconds;
        if (Progress >= 1) Open(nearest);
    }
}
