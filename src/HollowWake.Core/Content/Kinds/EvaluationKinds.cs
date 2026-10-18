using System.Text.Json.Nodes;
using HollowWake.Core.Definitions;
using HollowWake.Core.Evaluation;

namespace HollowWake.Core.Content.Kinds;

/// <summary>
/// Content for the evaluation harness (docs/design/evaluation.md): rules group <c>evaluation</c>, whose every rule
/// the harness reads with no value of its own to fall back on (<see cref="EvaluationRules"/>). Validators are added
/// to the existing kinds with <see cref="ContentKinds.AddValidator{T}"/>.
/// </summary>
internal static class EvaluationKinds
{
    public static void Register()
    {
        ContentKinds.AddValidator<RulesDef>(Rules);
    }

    /// <summary>
    /// Rules group <c>evaluation</c>: every rule in it is one the harness reads, so a misspelled one is named rather than
    /// ignored; the proving pairs and the standard duels are pairings written a:b of patterns that exist; a duel's
    /// spawn distance is above zero and its two factions are two different ids; a run's default seeds are a whole
    /// number of at least one and its default cap above zero; and every band is written, a number and in range: the
    /// time to kill from zero up with its maximum at least its minimum, the ceilings and the floor shares from 0 to 1.
    /// </summary>
    private static void Rules(RulesDef d, ValidationContext c)
    {
        if (d.Id != EvaluationRules.Group)
        {
            return;
        }
        foreach (var (key, _) in d.Values ?? new JsonObject())
        {
            if (!EvaluationRules.All.Contains(key, StringComparer.Ordinal))
            {
                c.Error($"'{key}' is not an evaluation rule; the harness reads {string.Join(", ", EvaluationRules.All)}.");
            }
        }
        foreach (var rule in new[] { EvaluationRules.ProvingPairs, EvaluationRules.StandardDuels })
        {
            foreach (var text in c.Registry.Rules.Strings(EvaluationRules.Path(rule)))
            {
                Pairing pairing;
                try
                {
                    pairing = Pairing.Parse(text);
                }
                catch (ArgumentException ex)
                {
                    c.Error($"{rule}: {ex.Message}");
                    continue;
                }
                c.Ref<PatternDef>(pairing.A, rule);
                c.Ref<PatternDef>(pairing.B, rule);
            }
        }
        var numbers = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var name in EvaluationRules.Bands.Append(EvaluationRules.SpawnDistance).Append(EvaluationRules.Seeds).Append(EvaluationRules.Seconds))
        {
            var value = c.Registry.Rules.Number(EvaluationRules.Path(name), double.NaN);
            if (double.IsNaN(value))
            {
                c.Error($"{name} is missing or not a number; the harness reads it from content alone.");
                continue;
            }
            numbers[name] = value;
        }
        if (numbers.TryGetValue(EvaluationRules.SpawnDistance, out var distance) && distance <= 0)
        {
            c.Error(FormattableString.Invariant($"{EvaluationRules.SpawnDistance} is {distance}; the two bodies spawn some distance apart, above 0."));
        }
        if (numbers.TryGetValue(EvaluationRules.Seeds, out var seeds) && (seeds < 1 || seeds != Math.Floor(seeds)))
        {
            c.Error(FormattableString.Invariant($"{EvaluationRules.Seeds} is a whole number of at least 1, not {seeds}."));
        }
        if (numbers.TryGetValue(EvaluationRules.Seconds, out var seconds) && seconds <= 0)
        {
            c.Error(FormattableString.Invariant($"{EvaluationRules.Seconds} is a cap in seconds above 0, not {seconds}."));
        }
        if (numbers.TryGetValue(EvaluationRules.TtkMin, out var least) && least < 0)
        {
            c.Error(FormattableString.Invariant($"{EvaluationRules.TtkMin} is {least}, below 0."));
        }
        if (numbers.TryGetValue(EvaluationRules.TtkMin, out least) && numbers.TryGetValue(EvaluationRules.TtkMax, out var most) && most < least)
        {
            c.Error(FormattableString.Invariant($"{EvaluationRules.TtkMax} {most} is below {EvaluationRules.TtkMin} {least}."));
        }
        foreach (var share in EvaluationRules.Shares)
        {
            if (numbers.TryGetValue(share, out var value) && (value < 0 || value > 1))
            {
                c.Error(FormattableString.Invariant($"{share} is a share from 0 to 1, not {value}."));
            }
        }
        var factions = c.Registry.Rules.Strings(EvaluationRules.Path(EvaluationRules.Factions));
        if (factions.Count != 2 || factions.Any(f => f.Length == 0) || factions[0] == factions[1])
        {
            c.Error($"{EvaluationRules.Factions} lists the two factions a duel's sides fight under, two different ids, a's then b's, not [{string.Join(", ", factions)}].");
        }
    }
}
