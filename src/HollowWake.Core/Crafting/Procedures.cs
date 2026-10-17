using System.Text.Json.Nodes;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Expressions;
using HollowWake.Core.Maths;

namespace HollowWake.Core.Crafting;

/// <summary>What the player did during the minigame: timestamps, choices, named values.</summary>
public sealed record ProcedureInput
{
    public List<double> Times { get; init; } = new();
    public List<int> Choices { get; init; } = new();
    public Dictionary<string, double> Values { get; init; } = new(StringComparer.Ordinal);
}

/// <summary>A server-issued challenge the client plays and the server scores from the same data.</summary>
public sealed record ProcedureChallenge
{
    public required string Type { get; init; }
    public required ulong Seed { get; init; }
    public JsonObject Data { get; init; } = new();
    public double TimeLimit { get; init; } = 10;
    public double IssuedAt { get; init; }
}

/// <summary>
/// A crafting minigame as a pure contract: create a seeded challenge, score an input. Because the server
/// scores deterministically from the same challenge it issued, the client can be as fancy as it likes
/// and still cannot cheat.
/// </summary>
public interface IProcedure
{
    string Type { get; }
    ProcedureChallenge Create(TypedSpec spec, Rng rng, IExprScope scope, double now);
    double Score(ProcedureChallenge challenge, ProcedureInput input);
}

public sealed class ProcedureRegistry
{
    private readonly Dictionary<string, IProcedure> _procedures = new(StringComparer.Ordinal);

    public void Register(IProcedure p) => _procedures[p.Type] = p;
    public bool TryGet(string type, out IProcedure? p) => _procedures.TryGetValue(type, out p);
    public IEnumerable<string> Types => _procedures.Keys;

    public static ProcedureRegistry CreateDefault()
    {
        var r = new ProcedureRegistry();
        r.Register(new NoneProcedure());
        r.Register(new TimingProcedure());
        r.Register(new SequenceProcedure());
        r.Register(new AlignmentProcedure());
        return r;
    }
}

/// <summary>No minigame: full score. For trivial recipes and automation.</summary>
public sealed class NoneProcedure : IProcedure
{
    public string Type => "none";
    public ProcedureChallenge Create(TypedSpec spec, Rng rng, IExprScope scope, double now) => new() { Type = Type, Seed = rng.NextUInt64(), IssuedAt = now, TimeLimit = 60 };
    public double Score(ProcedureChallenge challenge, ProcedureInput input) => 1;
}

/// <summary>
/// Hit a series of timing windows. The challenge lists window centers and half-widths in seconds from
/// the start; the input lists the times pressed. Score is the mean closeness over the windows.
/// </summary>
public sealed class TimingProcedure : IProcedure
{
    public string Type => "timing";

    public ProcedureChallenge Create(TypedSpec spec, Rng rng, IExprScope scope, double now)
    {
        var count = Math.Clamp(JsonNum.IntOf(spec.Params["windows"], 4), 1, 32);
        var gapMin = JsonNum.Of(spec.Params["gap_min"], 0.8);
        var gapMax = JsonNum.Of(spec.Params["gap_max"], 1.6);
        var width = JsonNum.Of(spec.Params["width"], 0.18);
        var windows = new JsonArray();
        var t = 1.0;
        for (var i = 0; i < count; i++)
        {
            t += rng.Range(gapMin, gapMax);
            windows.Add(new JsonObject { ["at"] = Math.Round(t, 3), ["width"] = width });
        }
        return new ProcedureChallenge
        {
            Type = Type,
            Seed = rng.NextUInt64(),
            Data = new JsonObject { ["windows"] = windows },
            TimeLimit = t + 3,
            IssuedAt = now,
        };
    }

    public double Score(ProcedureChallenge challenge, ProcedureInput input)
    {
        if (challenge.Data["windows"] is not JsonArray windows || windows.Count == 0)
        {
            return 0;
        }
        var times = input.Times.OrderBy(x => x).ToList();
        double total = 0;
        var used = new bool[times.Count];
        foreach (var w in windows.OfType<JsonObject>())
        {
            var at = JsonNum.Of(w["at"], 0);
            var width = Math.Max(0.01, JsonNum.Of(w["width"], 0.2));
            var best = -1;
            var bestDist = double.MaxValue;
            for (var i = 0; i < times.Count; i++)
            {
                if (used[i])
                {
                    continue;
                }
                var d = Math.Abs(times[i] - at);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }
            if (best >= 0 && bestDist <= width)
            {
                used[best] = true;
                total += 1 - bestDist / width * 0.5;
            }
        }
        var extra = Math.Max(0, times.Count - windows.Count);
        return Math.Clamp(total / windows.Count - extra * 0.1, 0, 1);
    }
}

/// <summary>Repeat a shown sequence of glyph symbols. Score is the fraction correct, less a time penalty.</summary>
public sealed class SequenceProcedure : IProcedure
{
    public string Type => "sequence";

    public ProcedureChallenge Create(TypedSpec spec, Rng rng, IExprScope scope, double now)
    {
        var length = Math.Clamp(JsonNum.IntOf(spec.Params["length"], 5), 1, 24);
        var symbols = Math.Clamp(JsonNum.IntOf(spec.Params["symbols"], 4), 2, 12);
        var limit = JsonNum.Of(spec.Params["time_limit"], 12);
        var seq = new JsonArray();
        for (var i = 0; i < length; i++)
        {
            seq.Add(rng.Next(symbols));
        }
        return new ProcedureChallenge
        {
            Type = Type,
            Seed = rng.NextUInt64(),
            Data = new JsonObject { ["sequence"] = seq, ["symbols"] = symbols },
            TimeLimit = limit,
            IssuedAt = now,
        };
    }

    public double Score(ProcedureChallenge challenge, ProcedureInput input)
    {
        if (challenge.Data["sequence"] is not JsonArray seq || seq.Count == 0)
        {
            return 0;
        }
        var correct = 0;
        for (var i = 0; i < seq.Count && i < input.Choices.Count; i++)
        {
            if (input.Choices[i] == JsonNum.IntOf(seq[i], -1))
            {
                correct++;
            }
        }
        var elapsed = input.Values.GetValueOrDefault("elapsed", 0);
        var penalty = elapsed > challenge.TimeLimit ? 0.5 : 0;
        return Math.Clamp((double)correct / seq.Count - penalty, 0, 1);
    }
}

/// <summary>Align a set of dials to target angles. Score is the mean angular closeness within tolerance.</summary>
public sealed class AlignmentProcedure : IProcedure
{
    public string Type => "alignment";

    public ProcedureChallenge Create(TypedSpec spec, Rng rng, IExprScope scope, double now)
    {
        var dials = Math.Clamp(JsonNum.IntOf(spec.Params["dials"], 3), 1, 8);
        var tolerance = JsonNum.Of(spec.Params["tolerance"], 12);
        var targets = new JsonArray();
        for (var i = 0; i < dials; i++)
        {
            targets.Add(Math.Round(rng.NextDouble() * 360, 1));
        }
        return new ProcedureChallenge
        {
            Type = Type,
            Seed = rng.NextUInt64(),
            Data = new JsonObject { ["targets"] = targets, ["tolerance"] = tolerance },
            TimeLimit = JsonNum.Of(spec.Params["time_limit"], 20),
            IssuedAt = now,
        };
    }

    public double Score(ProcedureChallenge challenge, ProcedureInput input)
    {
        if (challenge.Data["targets"] is not JsonArray targets || targets.Count == 0)
        {
            return 0;
        }
        var tolerance = Math.Max(1, JsonNum.Of(challenge.Data["tolerance"], 12));
        double total = 0;
        for (var i = 0; i < targets.Count; i++)
        {
            var target = JsonNum.Of(targets[i], 0);
            var actual = input.Values.GetValueOrDefault("dial" + i, double.NaN);
            if (double.IsNaN(actual))
            {
                continue;
            }
            var diff = Math.Abs(((actual - target) % 360 + 540) % 360 - 180);
            if (diff <= tolerance)
            {
                total += 1 - diff / tolerance * 0.5;
            }
        }
        return Math.Clamp(total / targets.Count, 0, 1);
    }
}
