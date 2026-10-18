using HollowWake.Core.Definitions;
using HollowWake.Protocol;

namespace HollowWake.Client.World;

/// <summary>Pure lifecycle consumer, mirrored by the browser's castCueTimeline.ts.</summary>
public sealed class CastCueTimeline
{
    public sealed record Cue(CastCueMsg Event, string Phase, CastVisualPhase Style, string Color, double Until, string Layer, string Profile);
    public List<Cue> Active { get; } = new();
    private long _sequence = -1;
    private readonly CastVisualsDef _config;
    private readonly CastVisualProfile[] _profiles;

    public CastCueTimeline(CastVisualsDef config)
    {
        _config = config;
        _profiles = config.Profiles.OrderBy(p => p.Fallback).ThenByDescending(p => p.Priority).ThenBy(p => p.Id, StringComparer.Ordinal).ToArray();
    }
    public void Clear() { Active.Clear(); _sequence = -1; }
    public void RemoveEntity(long key) => Active.RemoveAll(c => c.Event.Source == key || c.Event.Target == key);
    public void Advance(double now) => Active.RemoveAll(c => c.Until <= now);

    public bool Receive(CastCueMsg e, double now)
    {
        if (e.Sequence <= _sequence) return false;
        _sequence = e.Sequence;
        if ((!e.Outcome && e.CastId == 0) || now - e.ServerTime > _config.MaxEventAge) return false;
        var phase = e.Outcome ? (_config.Outcomes.Contains(e.Phase) && _profiles.Any(p => p.Phases.ContainsKey(e.Phase)) ? e.Phase : "outcome")
            : e.Phase switch { "sync" => "start", "end" => e.Fired ? "recovery" : "cancel", "interrupt" => "cancel", _ => e.Phase };
        if (!e.Outcome && phase is not ("start" or "release" or "impact" or "recovery" or "cancel")) return false;
        if (!e.Outcome && phase is "start" or "recovery" or "cancel")
            Active.RemoveAll(c => c.Phase == "start" && c.Event.Source == e.Source && c.Event.CastId == e.CastId);
        var layers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var profile in _profiles)
        {
            if (layers.Count >= _config.MaxLayersPerEvent) break;
            var style = profile.Phases.GetValueOrDefault(phase) ?? (e.Outcome && profile.Fallback ? profile.Phases.GetValueOrDefault("outcome") : null);
            if ((profile.Fallback && layers.Count > 0) || layers.Contains(profile.Layer) || style is null ||
                !Matches(profile.Match, e.Tags) || !Matches(profile.MatchMechanisms, e.Mechanisms is { } mechanisms ? mechanisms : Array.Empty<string>())) continue;
            var until = !e.Outcome && phase == "start" ? Math.Min(e.ServerTime + _config.MaxPreparation,
                e.Mode == "cast" && e.Duration > 0 ? e.StartedAt + e.Duration + _config.MaxEventAge : double.PositiveInfinity)
                : e.ServerTime + style.Seconds;
            if (until <= now) continue;
            layers.Add(profile.Layer);
            while (Active.Count >= _config.MaxEffects) Active.RemoveAt(0);
            Active.Add(new Cue(e, phase, style, profile.Color, until, profile.Layer, profile.Id));
        }
        return layers.Count > 0;
    }

    private static bool Matches(IEnumerable<string> required, IEnumerable<string> actual) => required.All(t =>
        actual.Any(x => x == t || x.StartsWith(t + ".", StringComparison.Ordinal)));
}
