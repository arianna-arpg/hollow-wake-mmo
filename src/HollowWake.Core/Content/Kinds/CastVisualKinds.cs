using System.Text.RegularExpressions;
using HollowWake.Core.Definitions;

namespace HollowWake.Core.Content.Kinds;

internal static class CastVisualKinds
{
    public static void Register() => ContentKinds.Register<CastVisualsDef>("ui_cast_visuals", (d, c) =>
    {
        if (d.MaxEffects is < 1 or > 4096 || !double.IsFinite(d.MaxEventAge) || d.MaxEventAge <= 0 ||
            !double.IsFinite(d.MaxPreparation) || d.MaxPreparation <= 0) c.Error("Invalid effect budget or expiry.");
        if (d.Profiles is null) { c.Error("Profiles are required."); return; }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in d.Profiles)
        {
            if (p is null || p.Match is null || p.Phases is null) { c.Error("A profile requires match tags and phases."); continue; }
            if (string.IsNullOrWhiteSpace(p.Id) || !ids.Add(p.Id)) c.Error("Profile ids must be non-empty and unique.");
            if (p.Color is null || !Regex.IsMatch(p.Color, "^#[0-9a-fA-F]{6}$")) c.Error($"Profile '{p.Id}' has an invalid color.");
            if (p.Match.Any(string.IsNullOrWhiteSpace)) c.Error($"Profile '{p.Id}' contains an empty match tag.");
            foreach (var (phase, v) in p.Phases)
            {
                if (v is null) { c.Error($"Phase '{phase}' is empty."); continue; }
                if (phase is not ("start" or "release" or "impact" or "recovery" or "cancel")) c.Error($"Unknown phase '{phase}'.");
                if (new[] { v.Seconds, v.Size, v.Height, v.Expand, v.Spin, v.Opacity, v.Stroke }.Any(x => !double.IsFinite(x)) ||
                    v.Seconds <= 0 || v.Size <= 0 || v.Stroke <= 0 || v.Height < 0 || v.Opacity is < 0 or > 1 || v.Expand < -1)
                    c.Error($"Profile '{p.Id}' has invalid phase values.");
                if (v.Segments is null || v.Segments.Count is < 1 or > 256 || v.Segments.Any(s => s is null || s.Length != 6 || s.Any(x => !float.IsFinite(x))))
                    c.Error($"Profile '{p.Id}' segments must contain six finite coordinates.");
            }
        }
        foreach (var phase in new[] { "start", "release", "impact", "recovery", "cancel" })
            if (!d.Profiles.Any(p => p?.Match?.Count == 0 && p.Phases?.ContainsKey(phase) == true))
                c.Error($"A neutral fallback for phase '{phase}' is required.");
    });
}
