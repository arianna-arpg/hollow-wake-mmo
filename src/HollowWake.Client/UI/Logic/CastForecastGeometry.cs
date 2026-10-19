using System.Numerics;
using HollowWake.Core.Definitions;
using HollowWake.Protocol;

namespace HollowWake.Client.UI.Logic;

/// <summary>Engine-free forecast strokes in the region's local core frame (Z up).
/// World position, heading and cosmetic lift belong to the renderer's transform.</summary>
public static class CastForecastGeometry
{
    /// <summary>Whether existing meshes (including their eligibility color) can be reused.
    /// Inputs have already passed the timeline's geometry gate; style is fixed for a renderer's lifetime.</summary>
    public static bool SameMesh(CastForecastRegion? previous, bool previousCommitted, CastForecastRegion? current, bool committed)
    {
        if (previous is null || current is null) return previous is null && current is null;
        return previousCommitted == committed && previous.Shape == current.Shape && Equals(previous.Size, current.Size) &&
            previous.TargetTest == current.TargetTest && previous.CenterAllowance == current.CenterAllowance &&
            previous.ViewerEligibility == current.ViewerEligibility &&
            CastForecastTimeline.VerticalBounds(previous) == CastForecastTimeline.VerticalBounds(current);
    }

    public static List<(Vector3 A, Vector3 B)> Build(CastForecastRegion region, bool committed, CastForecastStyleDef style)
    {
        var segments = CastForecastTimeline.SegmentsFor(region, style.Segments, style.MaxSegments, style.MaxChordError);
        if (segments == 0) return new();
        var outline = CastForecastTimeline.Outline(region, segments);
        var parts = new List<(Vector3 A, Vector3 B)>();
        var (bottom, top) = CastForecastTimeline.VerticalBounds(region);
        var levels = region.TargetTest == "body" ? (bottom == top ? new[] { bottom } : new[] { bottom, top }) : new[] { 0f };
        var arrow = (float)style.DirectionSize;
        parts.Add((new(0, -arrow / 2, levels[0]), new(arrow, 0, levels[0])));
        parts.Add((new(arrow, 0, levels[0]), new(0, arrow / 2, levels[0])));
        if (committed) parts.Add((new(0, arrow / 2, levels[0]), new(0, -arrow / 2, levels[0])));
        // Broken perimeters describe live candidate coverage, including holes, at each sampled level.
        foreach (var level in levels) foreach (var loop in outline.Holes.Prepend(outline.Outer)) for (var i = 0; i < loop.Count; i++)
        {
            var a = loop[i]; var b = loop[(i + 1) % loop.Count];
            parts.Add((new(a.X, a.Y, level), new(a.X + (b.X - a.X) * (float)style.DashFraction,
                a.Y + (b.Y - a.Y) * (float)style.DashFraction, level)));
        }
        for (var i = 0; i < outline.Outer.Count; i += Math.Max(1, outline.Outer.Count / 4))
        {
            var p = outline.Outer[i];
            var marker = Math.Min((top - bottom) / 2, (float)style.HeightMarkerSize);
            parts.Add((new(p.X, p.Y, bottom), new(p.X, p.Y, bottom + marker)));
            parts.Add((new(p.X, p.Y, top - marker), new(p.X, p.Y, top)));
        }
        return parts;
    }
}
