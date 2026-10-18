using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Maths;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;

namespace HollowWake.Core.Evaluation;

/// <summary>Two patterns to fight, spawned facing each other in a seeded arena.</summary>
public sealed record Pairing(string A, string B)
{
    /// <summary>Parses <c>a:b</c>.</summary>
    public static Pairing Parse(string text)
    {
        var i = text.IndexOf(':');
        if (i <= 0 || i == text.Length - 1)
        {
            throw new ArgumentException($"a pairing is written a:b, not '{text}'.");
        }
        return new Pairing(text[..i].Trim(), text[(i + 1)..].Trim());
    }
}

/// <summary>
/// The bands a matched pairing must stay inside, read from rules group <c>evaluation</c>: the median time to
/// kill, the share of fights either side may win, the share that may end in a draw, and the share of its kit each
/// side must use.
/// </summary>
public sealed record Bands(double TtkMin, double TtkMax, double WinRateCeiling, double DrawCeiling, double VarietyFloor)
{
    public static Bands Of(ContentRegistry content) => new(
        content.Rules.Number("evaluation.ttk_min", 5),
        content.Rules.Number("evaluation.ttk_max", 45),
        content.Rules.Number("evaluation.win_rate_ceiling", 0.8),
        content.Rules.Number("evaluation.draw_ceiling", 0.2),
        content.Rules.Number("evaluation.variety_floor", 0.25));
}

/// <summary>
/// One side of one fight: what it dealt, cast and used. <c>Kit</c> is how many weaves its book holds, so distinct
/// weaves over kit is the share of its kit it used; <c>Spread</c> is how evenly it spread its casts over the weaves it
/// did cast, one being even.
/// </summary>
public sealed record Side(
    string Pattern,
    double DamageDealt,
    int Hits,
    int Crits,
    int Casts,
    int DistinctWeaves,
    int Kit,
    double Spread,
    IReadOnlyList<string> Weaves,
    IReadOnlyList<string> Mechanisms,
    IReadOnlyList<string> StatusesApplied,
    double LifeLeft);

/// <summary>One seeded fight: who won (<c>a</c>, <c>b</c> or <c>draw</c>), when it ended, and each side's tally.</summary>
public sealed record Fight(string A, string B, int Index, ulong Seed, string Outcome, double Seconds, Side SideA, Side SideB);

/// <summary>A pairing over its fights: rates, the time-to-kill spread, variety, what each side used, and the bands it broke.</summary>
public sealed record PairReport(
    string A,
    string B,
    int Fights,
    double WinRateA,
    double WinRateB,
    double DrawRate,
    double TtkMedian,
    double TtkLow,
    double TtkHigh,
    double VarietyA,
    double VarietyB,
    double SpreadA,
    double SpreadB,
    IReadOnlyList<string> WeavesA,
    IReadOnlyList<string> WeavesB,
    IReadOnlyList<string> MechanismsA,
    IReadOnlyList<string> MechanismsB,
    IReadOnlyList<string> Breaches);

/// <summary>The whole run, serializable, so a gate can keep it as evidence beside the bench rows.</summary>
public sealed record TournamentReport(int Seeds, double Seconds, Bands Bands, IReadOnlyList<PairReport> Pairs, IReadOnlyList<Fight> Fights)
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.Never };

    public bool Passed => Pairs.All(p => p.Breaches.Count == 0);

    public string ToJson() => JsonSerializer.Serialize(this, Json);
}

/// <summary>
/// Seeded duels across pairings, measured from the events the simulation publishes: time to kill, win and draw
/// rates, damage, casts, the distinct weaves and the mechanisms each side used, and life left. Every fight runs
/// in its own arena from a seed derived from the pairing and the fight's index, so the same run gives the same
/// report, and nothing here reads a body's pattern beyond its name.
/// </summary>
public static class Tournament
{
    private static readonly Vector3 SpawnA = new(10, 10, 0);
    private static readonly Vector3 SpawnB = new(14, 10, 0);

    public static TournamentReport Run(ContentRegistry content, IReadOnlyList<Pairing> pairings, int seeds, double seconds, bool checkBands)
    {
        if (seeds < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(seeds), "a tournament needs at least one seed.");
        }
        var bands = Bands.Of(content);
        var fights = new List<Fight>();
        var pairs = new List<PairReport>();
        foreach (var pairing in pairings)
        {
            var own = new List<Fight>();
            for (var i = 0; i < seeds; i++)
            {
                own.Add(Duel(content, pairing, i, seconds));
            }
            fights.AddRange(own);
            pairs.Add(Summarize(pairing, own, bands, checkBands));
        }
        return new TournamentReport(seeds, seconds, bands, pairs, fights);
    }

    /// <summary>One fight of a pairing: index <paramref name="index"/> seeds it, so a pairing's fights differ and repeat.</summary>
    public static Fight Duel(ContentRegistry content, Pairing pairing, int index, double seconds)
    {
        var seed = Rng.Hash64($"tournament:{pairing.A}:{pairing.B}:{index}");
        // The two sides fight under the same two factions the fight tool and the tests use.
        content.With(new FactionDef { Id = "red", Relations = { ["blue"] = Stance.Hostile } }).With(new FactionDef { Id = "blue", Relations = { ["red"] = Stance.Hostile } });
        var sim = ZoneBuilder.CreateArena(content, seed);
        var factory = sim.Service<PatternFactory>();
        var a = factory.Spawn(sim, pairing.A, SpawnA, new SpawnOptions { Faction = "red" });
        var b = factory.Spawn(sim, pairing.B, SpawnB, new SpawnOptions { Faction = "blue" });
        if (a.IsNone || b.IsNone)
        {
            throw new InvalidOperationException($"a pairing names an unknown pattern: {pairing.A} or {pairing.B}.");
        }
        var tallies = new Dictionary<long, Tally> { [a.Key] = new(), [b.Key] = new() };
        double? ended = null;
        var outcome = "draw";
        sim.Events.Subscribe<DamageDealt>(d =>
        {
            if (!d.Evaded && tallies.TryGetValue(d.Source.Key, out var t))
            {
                t.Damage += d.Total;
                t.Hits++;
                if (d.Crit)
                {
                    t.Crits++;
                }
            }
        });
        sim.Events.Subscribe<WeaveCast>(w =>
        {
            if (tallies.TryGetValue(w.Caster.Key, out var t))
            {
                t.Casts++;
                t.Weaves.Add(w.Weave);
            }
        });
        sim.Events.Subscribe<StatusApplied>(s =>
        {
            if (tallies.TryGetValue(s.Source.Key, out var t))
            {
                t.Statuses.Add(s.Status);
            }
        });
        sim.Events.Subscribe<EntityDied>(d =>
        {
            if (ended is null && (d.Entity == a || d.Entity == b))
            {
                ended = sim.Clock.Time;
                outcome = d.Entity == a ? "b" : "a";
            }
        });
        while (ended is null && sim.Clock.Time < seconds)
        {
            sim.Tick();
        }
        return new Fight(pairing.A, pairing.B, index, seed, outcome, ended ?? seconds, SideOf(sim, a, pairing.A, tallies[a.Key]), SideOf(sim, b, pairing.B, tallies[b.Key]));
    }

    private static Side SideOf(Simulation sim, Entity body, string pattern, Tally t)
    {
        var weaves = t.Weaves.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var mechanisms = new SortedSet<string>(StringComparer.Ordinal);
        if (sim.World.Get<WeaveBook>(body) is { } book)
        {
            foreach (var key in weaves)
            {
                var compiled = book.Slots.FirstOrDefault(s => s.Key == key)?.Compiled;
                if (compiled is null)
                {
                    continue;
                }
                foreach (var m in compiled.Mechanisms.Items)
                {
                    mechanisms.Add(m);
                }
            }
        }
        var kit = sim.World.Get<WeaveBook>(body)?.Slots.Count(s => s.Compiled is not null) ?? 0;
        var spread = Spread(t.Weaves);
        var stats = sim.World.Get<Stats>(body);
        var max = stats?.ResourceMax("life") ?? 0;
        var life = sim.World.Has<Dead>(body) || max <= 0 ? 0 : Math.Clamp(stats!.Resource("life") / max, 0, 1);
        return new Side(pattern, Math.Round(t.Damage, 2), t.Hits, t.Crits, t.Casts, weaves.Count, kit, Math.Round(spread, 3), weaves, mechanisms.ToList(),
            t.Statuses.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList(), Math.Round(life, 3));
    }

    private static PairReport Summarize(Pairing pairing, List<Fight> fights, Bands bands, bool checkBands)
    {
        var n = fights.Count;
        var winsA = fights.Count(f => f.Outcome == "a");
        var winsB = fights.Count(f => f.Outcome == "b");
        var draws = n - winsA - winsB;
        var ttk = fights.Where(f => f.Outcome != "draw").Select(f => f.Seconds).Order().ToList();
        // Variety is the share of its kit a side used, averaged over its fights.
        var variety = (Func<Fight, Side> side) => fights.Average(f => side(f).Kit == 0 ? 0 : Math.Min(1, (double)side(f).DistinctWeaves / side(f).Kit));
        var varietyA = variety(f => f.SideA);
        var varietyB = variety(f => f.SideB);
        var weavesA = fights.SelectMany(f => f.SideA.Weaves).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var weavesB = fights.SelectMany(f => f.SideB.Weaves).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var mechanismsA = fights.SelectMany(f => f.SideA.Mechanisms).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var mechanismsB = fights.SelectMany(f => f.SideB.Mechanisms).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var median = Quantile(ttk, 0.5);
        var breaches = new List<string>();
        if (checkBands)
        {
            if ((double)winsA / n > bands.WinRateCeiling)
            {
                breaches.Add($"{pairing.A} wins {winsA} of {n}, above the ceiling {bands.WinRateCeiling:0.##}");
            }
            if ((double)winsB / n > bands.WinRateCeiling)
            {
                breaches.Add($"{pairing.B} wins {winsB} of {n}, above the ceiling {bands.WinRateCeiling:0.##}");
            }
            if ((double)draws / n > bands.DrawCeiling)
            {
                breaches.Add($"{draws} of {n} fights are draws, above the ceiling {bands.DrawCeiling:0.##}");
            }
            if (ttk.Count > 0 && (median < bands.TtkMin || median > bands.TtkMax))
            {
                breaches.Add($"the median time to kill {median:0.#} s is outside {bands.TtkMin:0.#} to {bands.TtkMax:0.#} s");
            }
            if (varietyA < bands.VarietyFloor)
            {
                breaches.Add($"{pairing.A} uses {varietyA:0.##} of its kit, below the floor {bands.VarietyFloor:0.##}");
            }
            if (varietyB < bands.VarietyFloor)
            {
                breaches.Add($"{pairing.B} uses {varietyB:0.##} of its kit, below the floor {bands.VarietyFloor:0.##}");
            }
        }
        return new PairReport(pairing.A, pairing.B, n, Round((double)winsA / n), Round((double)winsB / n), Round((double)draws / n),
            Round(median), Round(Quantile(ttk, 0.25)), Round(Quantile(ttk, 0.75)), Round(varietyA), Round(varietyB), Round(fights.Average(f => f.SideA.Spread)), Round(fights.Average(f => f.SideB.Spread)),
            weavesA, weavesB, mechanismsA, mechanismsB, breaches);
    }

    /// <summary>How evenly casts spread over the weaves cast: the entropy of their shares over the most it could be; one for a single weave.</summary>
    private static double Spread(List<string> casts)
    {
        if (casts.Count == 0)
        {
            return 0;
        }
        var counts = casts.GroupBy(w => w, StringComparer.Ordinal).Select(g => (double)g.Count()).ToList();
        if (counts.Count == 1)
        {
            return 1;
        }
        var entropy = counts.Sum(c => { var p = c / casts.Count; return -p * Math.Log(p); });
        return entropy / Math.Log(counts.Count);
    }

    /// <summary>A quantile of a sorted list by nearest rank; zero for an empty list.</summary>
    private static double Quantile(List<double> sorted, double q)
    {
        if (sorted.Count == 0)
        {
            return 0;
        }
        var rank = (int)Math.Ceiling(q * sorted.Count) - 1;
        return sorted[Math.Clamp(rank, 0, sorted.Count - 1)];
    }

    private static double Round(double v) => Math.Round(v, 3);

    private sealed class Tally
    {
        public double Damage;
        public int Hits;
        public int Crits;
        public int Casts;
        public readonly List<string> Weaves = new();
        public readonly List<string> Statuses = new();
    }
}
