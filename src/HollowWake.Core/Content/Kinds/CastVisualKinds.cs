using System.Text.RegularExpressions;
using HollowWake.Core.Definitions;

namespace HollowWake.Core.Content.Kinds;

internal static class CastVisualKinds
{
    public static void Register() => ContentKinds.Register<CastVisualsDef>("ui_cast_visuals", (d, c) =>
    {
        if (d.MaxEffects is < 1 or > 4096 || d.MaxLayersPerEvent is < 1 or > 32 || !double.IsFinite(d.MaxEventAge) || d.MaxEventAge <= 0 ||
            !double.IsFinite(d.MaxPreparation) || d.MaxPreparation <= 0) c.Error("Invalid effect budget or expiry.");
        if (d.Profiles is null) { c.Error("Profiles are required."); return; }
        if (d.Outcomes is null || d.Outcomes.Any(string.IsNullOrWhiteSpace)) { c.Error("Outcome names must be non-empty."); return; }
        if (d.Outcomes.Distinct(StringComparer.Ordinal).Count() != d.Outcomes.Count ||
            d.Outcomes.Any(p => p is "start" or "sync" or "end" or "release" or "impact" or "recovery" or "cancel" or "interrupt" or "outcome"))
            c.Error("Outcome names must be unique and separate from the cast lifecycle.");
        if (d.ThreadPhases is null) { c.Error("Thread phases cannot be null."); return; }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (d.Profiles.Any(p => p?.Id?.StartsWith("thread.", StringComparison.Ordinal) == true))
            c.Error("Profile ids starting with 'thread.' are reserved for generated Thread profiles; override their layer using another id.");
        foreach (var p in d.Profiles.Concat(new[] { new CastVisualProfile { Id = "thread.template", Phases = d.ThreadPhases } }))
        {
            if (p is null || p.Match is null || p.MatchMechanisms is null || p.Phases is null) { c.Error("A profile requires match tags, mechanisms and phases."); continue; }
            if (string.IsNullOrWhiteSpace(p.Layer)) c.Error("A profile requires a layer.");
            if (string.IsNullOrWhiteSpace(p.Id) || !ids.Add(p.Id)) c.Error("Profile ids must be non-empty and unique.");
            if (p.Color is null || !Regex.IsMatch(p.Color, "^#[0-9a-fA-F]{6}$")) c.Error($"Profile '{p.Id}' has an invalid color.");
            if (p.Match.Concat(p.MatchMechanisms).Any(string.IsNullOrWhiteSpace)) c.Error($"Profile '{p.Id}' contains an empty match tag or mechanism.");
            foreach (var (phase, v) in p.Phases)
            {
                if (v is null) { c.Error($"Phase '{phase}' is empty."); continue; }
                if (phase is not ("start" or "release" or "impact" or "recovery" or "cancel" or "outcome") && !d.Outcomes.Contains(phase)) c.Error($"Unknown phase '{phase}'.");
                if (v.Primitive is null || !CastVisualPrimitives.Contains(v.Primitive)) c.Error($"Unknown visual primitive '{v.Primitive}'.");
                if (v.Pieces is < 3 or > 128 || !double.IsFinite(v.Arc) || v.Arc <= 0 || v.Arc > 360) c.Error("Invalid primitive pieces or arc.");
                if (new[] { v.Seconds, v.Size, v.Height, v.Expand, v.Spin, v.Opacity, v.Stroke }.Any(x => !double.IsFinite(x)) ||
                    v.Seconds <= 0 || v.Size <= 0 || v.Stroke <= 0 || v.Height < 0 || v.Opacity is < 0 or > 1 || v.Expand < -1)
                    c.Error($"Profile '{p.Id}' has invalid phase values.");
                if (v.Primitive == "segments" && (v.Segments is null || v.Segments.Count is < 1 or > 256 || v.Segments.Any(s => s is null || s.Length != 6 || s.Any(x => !float.IsFinite(x)))))
                    c.Error($"Profile '{p.Id}' segments must contain six finite coordinates.");
            }
        }
        foreach (var phase in new[] { "start", "release", "impact", "recovery", "cancel" }.Concat(d.Outcomes.Count > 0 ? new[] { "outcome" } : Array.Empty<string>()))
            if (!d.Profiles.Any(p => p?.Fallback == true && p.Match?.Count == 0 && p.MatchMechanisms?.Count == 0 && p.Phases?.ContainsKey(phase) == true))
                c.Error($"A neutral fallback for phase '{phase}' is required.");
    });
}
