using System.Text.RegularExpressions;
using HollowWake.Core.Definitions;

namespace HollowWake.Core.Content.Kinds;

internal static class CastForecastKinds
{
    public static void Register() => ContentKinds.Register<CastForecastStyleDef>("ui_cast_forecasts", (d, c) =>
    {
        if (d.Segments is < 16 or > 256) c.Error("Forecast segments must be between 16 and 256.");
        if (d.MaxSegments < d.Segments || d.MaxSegments > 1024 || !double.IsFinite(d.MaxChordError) || d.MaxChordError <= 0)
            c.Error("Forecast tessellation requires a positive error tolerance and a bounded segment budget.");
        if (new[] { d.Stroke, d.UnderlayScale, d.DashFraction, d.DirectionSize, d.HeightMarkerSize, d.Lift, d.BarWidth, d.BarHeight, d.LabelHeight, d.FontSize }.Any(v => !double.IsFinite(v) || v <= 0))
            c.Error("Forecast presentation dimensions must be finite and positive.");
        if (d.DashFraction >= 1 || d.UnderlayScale <= 1) c.Error("Forecast uncertainty needs a broken boundary and a wider under-stroke.");
        foreach (var color in new[] { d.Color, d.CandidateColor, d.UnknownColor, d.UnderlayColor })
            if (color is null || !Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$")) c.Error("Invalid forecast color.");
        foreach (var key in new CastForecastStyleDef { Id = "defaults" }.Labels.Keys)
            if (d.Labels is null || !d.Labels.TryGetValue(key, out var label) || label is null || key != "primary" && string.IsNullOrWhiteSpace(label)) c.Error($"Missing forecast label '{key}'.");
    });
}
