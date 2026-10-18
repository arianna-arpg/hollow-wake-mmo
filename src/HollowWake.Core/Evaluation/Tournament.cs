using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HollowWake.Core.Combat;
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
    /// <summary>The rule listing the proving pairs: matched pairings built to be even, which a tournament runs by default.</summary>
    public const string ProvingPairs = "evaluation.proving_pairs";

    /// <summary>The rule listing the standard duels: the pairings whose seeded traces are the determinism baseline.</summary>
    public const string StandardDuels = "evaluation.standard_duels";

    /// <summary>Parses <c>a:b</c>, trimmed; both names must be there once the spaces around them are gone.</summary>
    public static Pairing Parse(string text)
    {
        var trimmed = text.Trim();
        var i = trimmed.IndexOf(':');
        var a = i < 0 ? "" : trimmed[..i].Trim();
        var b = i < 0 ? "" : trimmed[(i + 1)..].Trim();
        if (a.Length == 0 || b.Length == 0)
        {
            throw new ArgumentException($"a pairing is written a:b, not '{text}'.");
        }
        return new Pairing(a, b);
    }

    /// <summary>The pairings a rule lists, each written <c>a:b</c>: <see cref="ProvingPairs"/> or <see cref="StandardDuels"/>.</summary>
    public static IReadOnlyList<Pairing> Listed(ContentRegistry content, string rule) => content.Rules.Strings(rule).Select(Parse).ToList();

    /// <summary>Writes <c>a:b</c>, the form <see cref="Parse"/> reads and baselines key by.</summary>
    public override string ToString() => $"{A}:{B}";
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
/// One side of one fight: what it dealt, cast and used. A side is its body and whatever it owns, its minions,
/// constructs, projectiles and fields, found up the owner chain the way kill credit is
/// (<see cref="DeathService.CreditTarget"/>), so damage, hits, crits, casts and statuses count whichever of them did
/// them and a summoner's kit is measured as its kit. Damage, hits, crits and statuses are a side's only when the side
/// that owns their target is another (the foe, or a body neither side owns): what a side's own sources do to itself,
/// a status ticking on once its applier is gone (the simulation then sources the tick from its bearer), a pool
/// breaking, a buff it lays on itself, is its <c>SelfDamage</c> and <c>SelfStatuses</c> instead. A hit is any
/// application of damage that was not evaded, blocked ones included. <c>Kit</c> is how many weaves its body's book
/// holds; <c>Weaves</c> are the weaves its body cast and those of its book that anything it owns cast, so distinct
/// weaves over kit is the share of its kit it used, and a minion's own weaves count as casts but not as its owner's
/// kit. <c>Spread</c> is how evenly those casts fell over the weaves cast, one being even.
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
    double SelfDamage,
    IReadOnlyList<string> SelfStatuses,
    double LifeLeft);

/// <summary>
/// One seeded fight: who won (<c>a</c>, <c>b</c> or <c>draw</c>), when it ended, the hash of its ordered event log
/// (sixteen hex digits, see <see cref="Tournament.Duel"/>), and each side's tally. A fight ends at the first tick after
/// which a body is dead, or at the cap; it is a draw when both bodies stand at the cap, or when both are dead after
/// that tick, a double knockout, which ends before the cap.
/// </summary>
public sealed record Fight(string A, string B, int Index, ulong Seed, string Outcome, double Seconds, string Hash, Side SideA, Side SideB);

/// <summary>
/// A pairing over its fights: its trace hash (folded over its fights' hashes in seed order), rates, the time-to-kill
/// spread, variety, what each side used, the self-sourced damage each side took over its fights and the self-sourced
/// statuses it bore, and the bands it broke.
/// </summary>
public sealed record PairReport(
    string A,
    string B,
    int Fights,
    string Hash,
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
    double SelfDamageA,
    double SelfDamageB,
    IReadOnlyList<string> SelfStatusesA,
    IReadOnlyList<string> SelfStatusesB,
    IReadOnlyList<string> Breaches);

/// <summary>
/// The whole run, serializable, so a gate can keep it as evidence beside the bench rows. <c>Hash</c> is folded over
/// its pairings' hashes in the order they ran.
/// </summary>
public sealed record TournamentReport(int Seeds, double Seconds, string Hash, Bands Bands, IReadOnlyList<PairReport> Pairs, IReadOnlyList<Fight> Fights)
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.Never };

    public bool Passed => Pairs.All(p => p.Breaches.Count == 0);

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    /// <summary>The report as the command prints it: a row per pairing, what each side used and the bands it broke, numbers in the invariant culture.</summary>
    public IEnumerable<string> Table()
    {
        yield return $"{"pairing",-44} {"fights",6} {"a wins",7} {"b wins",7} {"draws",6} {"ttk med",8} {"low",6} {"high",6} {"kit a",6} {"kit b",6} {"spr a",6} {"spr b",6}";
        foreach (var p in Pairs)
        {
            yield return FormattableString.Invariant($"{p.A + " v " + p.B,-44} {p.Fights,6} {p.WinRateA,7:0.00} {p.WinRateB,7:0.00} {p.DrawRate,6:0.00} {p.TtkMedian,8:0.#} {p.TtkLow,6:0.#} {p.TtkHigh,6:0.#} {p.VarietyA,6:0.00} {p.VarietyB,6:0.00} {p.SpreadA,6:0.00} {p.SpreadB,6:0.00}");
            yield return FormattableString.Invariant($"    {p.A}: {string.Join(" ", p.WeavesA)} | {string.Join(" ", p.MechanismsA)} | self {p.SelfDamageA:0.#} {string.Join(" ", p.SelfStatusesA)}");
            yield return FormattableString.Invariant($"    {p.B}: {string.Join(" ", p.WeavesB)} | {string.Join(" ", p.MechanismsB)} | self {p.SelfDamageB:0.#} {string.Join(" ", p.SelfStatusesB)}");
            foreach (var breach in p.Breaches)
            {
                yield return $"    BREACH {breach}";
            }
        }
    }

    /// <summary>The trace hashes as the command prints them: one line per pairing keyed <c>a:b</c>, then the run's.</summary>
    public IEnumerable<string> HashLines()
    {
        yield return FormattableString.Invariant($"trace hashes over {Seeds} seeds of {Seconds:0.#} s");
        foreach (var p in Pairs)
        {
            yield return $"    {new Pairing(p.A, p.B).ToString(),-44} {p.Hash}";
        }
        yield return $"    {"run",-44} {Hash}";
    }
}

/// <summary>
/// Recorded trace hashes, the baseline a later run is checked against: the seeds and the cap the run fought with,
/// and each pairing's hash keyed <c>a:b</c>. <c>tournament --hashes --out</c> writes one, <c>--check-hashes</c>
/// reads one; the file is small and sorted so a review can diff it.
/// </summary>
public sealed record TraceBaseline(int Seeds, double Seconds, IReadOnlyDictionary<string, string> Hashes)
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static TraceBaseline Of(TournamentReport report)
    {
        var hashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var p in report.Pairs)
        {
            hashes[new Pairing(p.A, p.B).ToString()] = p.Hash;
        }
        return new TraceBaseline(report.Seeds, report.Seconds, hashes);
    }

    /// <summary>The baseline as JSON with LF line ends and a final newline, whatever the platform.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, Json).Replace("\r\n", "\n") + "\n";

    public static TraceBaseline FromJson(string json) =>
        JsonSerializer.Deserialize<TraceBaseline>(json, Json) is { Hashes: not null } baseline
            ? baseline
            : throw new InvalidDataException("a trace baseline is a JSON object with Seeds, Seconds and Hashes.");

    /// <summary>
    /// Every way a run differs from this baseline, one line each: a different number of seeds or cap, a pairing
    /// whose hash changed, one the baseline does not hold, and one it holds that the run did not fight. Empty when
    /// the run is identical.
    /// </summary>
    public IReadOnlyList<string> Differences(TournamentReport report)
    {
        var lines = new List<string>();
        if (report.Seeds != Seeds || report.Seconds != Seconds)
        {
            lines.Add(FormattableString.Invariant($"the baseline fought {Seeds} seeds of {Seconds} s, this run {report.Seeds} seeds of {report.Seconds} s"));
        }
        var fought = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in report.Pairs)
        {
            var key = new Pairing(p.A, p.B).ToString();
            if (!fought.Add(key))
            {
                continue;
            }
            if (!Hashes.TryGetValue(key, out var was))
            {
                lines.Add($"{key} is not in the baseline (now {p.Hash})");
            }
            else if (was != p.Hash)
            {
                lines.Add($"{key} changed from {was} to {p.Hash}");
            }
        }
        foreach (var key in Hashes.Keys.Where(k => !fought.Contains(k)).Order(StringComparer.Ordinal))
        {
            lines.Add($"{key} is in the baseline but was not fought");
        }
        return lines;
    }
}

/// <summary>
/// Seeded duels across pairings, measured from the events the simulation publishes: time to kill, win and draw
/// rates, damage, casts, the distinct weaves and the mechanisms each side used, and life left. Every fight runs
/// in its own arena from a seed derived from the pairing and the fight's index, so the same run gives the same
/// report, and nothing here reads a body's pattern beyond its name.
/// </summary>
public static class Tournament
{
    /// <summary>How far apart the two bodies spawn, in metres.</summary>
    private const float SpawnDistance = 4;

    /// <summary>What a tournament's seed text opens with: <c>tournament:a:b:i</c>.</summary>
    private const string SeedPrefix = "tournament";

    /// <summary>
    /// The seed text of a pairing's fight <paramref name="index"/>, <c>tournament:a:b:i</c>: its arena is seeded with
    /// the text's hash, and <c>hollowwake fight a b --seed</c> with the same text stages the same fight.
    /// </summary>
    public static string SeedText(Pairing pairing, int index) => FormattableString.Invariant($"{SeedPrefix}:{pairing.A}:{pairing.B}:{index}");

    /// <summary>Reads a seed text <see cref="SeedText"/> wrote back to its pairing and index; false for any other text.</summary>
    public static bool TryReadSeed(string text, out Pairing pairing, out int index)
    {
        pairing = new Pairing("", "");
        index = -1;
        var parts = text.Split(':');
        if (parts.Length != 4 || parts[0] != SeedPrefix || parts[1].Length == 0 || parts[2].Length == 0
            || !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var read))
        {
            return false;
        }
        pairing = new Pairing(parts[1], parts[2]);
        index = read;
        return true;
    }

    /// <summary>
    /// Whether b spawns before a in a pairing's fight <paramref name="index"/>: the side spawned first is created
    /// first and so thinks and acts first on every tick the two share, so it alternates, a on even indices and b on
    /// odd ones, and neither side is first in every fight.
    /// </summary>
    public static bool BFirst(int index) => index % 2 == 1;

    /// <summary>
    /// Spawns a pairing's two bodies for a duel: <see cref="SpawnDistance"/> apart on the east-west line through the
    /// arena's origin, mirrored about it with a to the west, under two hostile factions added to the registry only
    /// where it lacks them; b first when <paramref name="bFirst"/> says so. Each body is then turned toward the
    /// other, so neither starts the fight facing away. Throws when a pattern is unknown.
    /// </summary>
    public static (Entity A, Entity B) Spawn(Simulation sim, Pairing pairing, bool bFirst)
    {
        var content = sim.Content;
        // The two sides fight under the same two factions the fight tool and the tests use, added only where the
        // registry lacks them, so a registry shared with other work is not written on every fight.
        if (!content.Has<FactionDef>("red"))
        {
            content.With(new FactionDef { Id = "red", Relations = { ["blue"] = Stance.Hostile } });
        }
        if (!content.Has<FactionDef>("blue"))
        {
            content.With(new FactionDef { Id = "blue", Relations = { ["red"] = Stance.Hostile } });
        }
        var factory = sim.Service<PatternFactory>();
        var west = new Vector3(-SpawnDistance / 2, 0, 0);
        var east = new Vector3(SpawnDistance / 2, 0, 0);
        Entity a;
        Entity b;
        if (bFirst)
        {
            b = factory.Spawn(sim, pairing.B, east, new SpawnOptions { Faction = "blue" });
            a = factory.Spawn(sim, pairing.A, west, new SpawnOptions { Faction = "red" });
        }
        else
        {
            a = factory.Spawn(sim, pairing.A, west, new SpawnOptions { Faction = "red" });
            b = factory.Spawn(sim, pairing.B, east, new SpawnOptions { Faction = "blue" });
        }
        if (a.IsNone || b.IsNone)
        {
            throw new InvalidOperationException($"a pairing names an unknown pattern: {pairing.A} or {pairing.B}.");
        }
        var ta = sim.World.Get<Transform>(a)!;
        var tb = sim.World.Get<Transform>(b)!;
        ta.Face(tb.Position);
        tb.Face(ta.Position);
        return (a, b);
    }

    /// <summary>Fights every pairing <paramref name="seeds"/> times, after checking every pattern they name exists, so a bad pairing fails before any fight.</summary>
    public static TournamentReport Run(ContentRegistry content, IReadOnlyList<Pairing> pairings, int seeds, double seconds, bool checkBands)
    {
        if (seeds < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(seeds), "a tournament needs at least one seed.");
        }
        foreach (var pairing in pairings)
        {
            if (Refusal(content, pairing) is { } why)
            {
                throw new ArgumentException(why);
            }
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
        return new TournamentReport(seeds, seconds, Fold(pairs.Select(p => p.Hash)), bands, pairs, fights);
    }

    /// <summary>Why a pairing cannot be fought, naming it and the pattern no definition carries; null when it can.</summary>
    public static string? Refusal(ContentRegistry content, Pairing pairing)
    {
        foreach (var pattern in new[] { pairing.A, pairing.B })
        {
            if (!content.Has<PatternDef>(pattern))
            {
                return $"pairing {pairing} names unknown pattern '{pattern}'";
            }
        }
        return null;
    }

    /// <summary>Folds hashes in order into one: the FNV-1a hash of their lines.</summary>
    private static string Fold(IEnumerable<string> hashes) => Hex(Rng.Hash64(string.Join("\n", hashes)));

    private static string Hex(ulong hash) => hash.ToString("x16", CultureInfo.InvariantCulture);

    /// <summary>
    /// One fight of a pairing: index <paramref name="index"/> seeds it (<see cref="SeedText"/>) and decides which
    /// side spawns first (<see cref="BFirst"/>), so a pairing's fights differ and repeat. The fight's <c>Hash</c> is
    /// the FNV-1a hash (<see cref="Rng.Hash64"/>) of its ordered event log, one line per spawn, despawn, cast,
    /// release, interrupt, hit, heal, status applied and death, and a last line for the end with both bodies' life
    /// and position; see <see cref="Trace"/>.
    /// </summary>
    public static Fight Duel(ContentRegistry content, Pairing pairing, int index, double seconds)
    {
        var seed = Rng.Hash64(SeedText(pairing, index));
        var sim = ZoneBuilder.CreateArena(content, seed);
        // The trace listens before the spawns, so the log opens with them.
        var trace = new Trace(sim);
        var (a, b) = Spawn(sim, pairing, BFirst(index));
        trace.A = a;
        trace.B = b;
        var tallies = new Dictionary<long, Tally> { [a.Key] = new(a), [b.Key] = new(b) };
        // Whatever a side owns works for it: the side owning an entity is found up the owner chain from a minion,
        // construct, projectile or field the way kill credit is.
        Tally? Owning(Entity e) => tallies.TryGetValue(DeathService.CreditTarget(sim, e).Key, out var t) ? t : null;
        double? ended = null;
        var aDied = false;
        var bDied = false;
        // Damage and statuses are a side's work only on a target another side owns, or none does. A source and a
        // target of one side is that side's own doing to itself: a status ticking on once its applier is gone (the
        // simulation sources the tick from its bearer then), a pool breaking, a self-buff. That is booked apart.
        sim.Events.Subscribe<DamageDealt>(d =>
        {
            if (d.Evaded || Owning(d.Source) is not { } by)
            {
                return;
            }
            if (ReferenceEquals(by, Owning(d.Target)))
            {
                by.SelfDamage += d.Total;
                return;
            }
            by.Damage += d.Total;
            by.Hits++;
            if (d.Crit)
            {
                by.Crits++;
            }
        });
        sim.Events.Subscribe<WeaveCast>(w =>
        {
            if (Owning(w.Caster) is { } t)
            {
                t.Casts++;
                // Its weaves are its kit's: what its body cast, and what anything it owns cast from its body's book.
                if (w.Caster == t.Body || sim.World.Get<WeaveBook>(t.Body)?.Slots.Any(s => s.Key == w.Weave) == true)
                {
                    t.Weaves.Add(w.Weave);
                }
            }
        });
        sim.Events.Subscribe<StatusApplied>(s =>
        {
            if (Owning(s.Source) is { } by)
            {
                (ReferenceEquals(by, Owning(s.Target)) ? by.SelfStatuses : by.Statuses).Add(s.Status);
            }
        });
        // The fight ends with the tick a body died in; a body that dies later in that same tick is down too, so both
        // dead after it is a double knockout, scored a draw.
        sim.Events.Subscribe<EntityDied>(d =>
        {
            if (d.Entity == a || d.Entity == b)
            {
                ended ??= sim.Clock.Time;
                aDied |= d.Entity == a;
                bDied |= d.Entity == b;
            }
        });
        while (ended is null && sim.Clock.Time < seconds)
        {
            sim.Tick();
        }
        var outcome = aDied == bDied ? "draw" : aDied ? "b" : "a";
        trace.End(outcome);
        return new Fight(pairing.A, pairing.B, index, seed, outcome, ended ?? seconds, Hex(trace.Hash()), SideOf(sim, a, pairing.A, tallies[a.Key]), SideOf(sim, b, pairing.B, tallies[b.Key]));
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
            Sorted(t.Statuses), Math.Round(t.SelfDamage, 2), Sorted(t.SelfStatuses), Math.Round(life, 3));
    }

    /// <summary>The distinct names in ordinal order.</summary>
    private static List<string> Sorted(IEnumerable<string> names) => names.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

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
            // Breaches are written in the invariant culture, whatever culture the host runs in.
            if ((double)winsA / n > bands.WinRateCeiling)
            {
                breaches.Add(FormattableString.Invariant($"{pairing.A} wins {winsA} of {n}, above the ceiling {bands.WinRateCeiling:0.##}"));
            }
            if ((double)winsB / n > bands.WinRateCeiling)
            {
                breaches.Add(FormattableString.Invariant($"{pairing.B} wins {winsB} of {n}, above the ceiling {bands.WinRateCeiling:0.##}"));
            }
            if ((double)draws / n > bands.DrawCeiling)
            {
                breaches.Add(FormattableString.Invariant($"{draws} of {n} fights are draws, above the ceiling {bands.DrawCeiling:0.##}"));
            }
            if (ttk.Count > 0 && (median < bands.TtkMin || median > bands.TtkMax))
            {
                breaches.Add(FormattableString.Invariant($"the median time to kill {median:0.#} s is outside {bands.TtkMin:0.#} to {bands.TtkMax:0.#} s"));
            }
            if (varietyA < bands.VarietyFloor)
            {
                breaches.Add(FormattableString.Invariant($"{pairing.A} uses {varietyA:0.##} of its kit, below the floor {bands.VarietyFloor:0.##}"));
            }
            if (varietyB < bands.VarietyFloor)
            {
                breaches.Add(FormattableString.Invariant($"{pairing.B} uses {varietyB:0.##} of its kit, below the floor {bands.VarietyFloor:0.##}"));
            }
        }
        return new PairReport(pairing.A, pairing.B, n, Fold(fights.OrderBy(f => f.Index).Select(f => f.Hash)), Round((double)winsA / n), Round((double)winsB / n), Round((double)draws / n),
            Round(median), Round(Quantile(ttk, 0.25)), Round(Quantile(ttk, 0.75)), Round(varietyA), Round(varietyB), Round(fights.Average(f => f.SideA.Spread)), Round(fights.Average(f => f.SideB.Spread)),
            weavesA, weavesB, mechanismsA, mechanismsB,
            Math.Round(fights.Sum(f => f.SideA.SelfDamage), 2), Math.Round(fights.Sum(f => f.SideB.SelfDamage), 2),
            Sorted(fights.SelectMany(f => f.SideA.SelfStatuses)), Sorted(fights.SelectMany(f => f.SideB.SelfStatuses)), breaches);
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

    private sealed class Tally(Entity body)
    {
        /// <summary>The side's body: what owns everything credited here, and whose book is its kit.</summary>
        public readonly Entity Body = body;
        public double Damage;
        public int Hits;
        public int Crits;
        public int Casts;
        public readonly List<string> Weaves = new();
        public readonly List<string> Statuses = new();

        /// <summary>Damage and statuses whose source and target both belong to this side.</summary>
        public double SelfDamage;
        public readonly List<string> SelfStatuses = new();
    }

    /// <summary>
    /// A fight's ordered event log, one line per event in the order the simulation published them, each opening with
    /// its tick: <c>spawn</c> (what, where) and <c>despawn</c>, <c>cast</c> (caster, weave, target, aimed point),
    /// <c>release</c> (caster, weave, mode, reason, whether it fired, held, fill, stage) and <c>interrupt</c> (caster,
    /// weave), <c>hit</c> (source, target, amount, and c, e or b for a crit, an evasion or a block), <c>heal</c>
    /// (source, target, resource, amount), <c>status</c> (source, target, status, stacks, duration), <c>died</c> (body,
    /// killer), and a last <c>end</c> with the outcome and each body's life and position, so a fight that reaches the
    /// cap with both standing still moves its hash when they stand differently. The two bodies are named a and b;
    /// anything else is named by its pattern, or its name when it has none (a projectile, a field), with a. or b.
    /// before it when one of the two owns it. So the hash follows what the fight did rather than how the world
    /// numbered its entities. Amounts, durations and positions are written to hundredths in the invariant culture, so
    /// the text, and its hash, do not depend on the host's culture.
    /// </summary>
    private sealed class Trace
    {
        private readonly Simulation _sim;
        private readonly StringBuilder _log = new();

        /// <summary>The two sides, set once spawned; until then a body is named by its pattern.</summary>
        public Entity A = Entity.None;
        public Entity B = Entity.None;

        public Trace(Simulation sim)
        {
            _sim = sim;
            sim.Events.Subscribe<EntitySpawned>(s => Line(FormattableString.Invariant($"spawn {Name(s.Entity, s.Pattern)} {s.Position.X:0.##} {s.Position.Y:0.##}")));
            sim.Events.Subscribe<EntityDespawned>(s => Line($"despawn {Name(s.Entity, s.Pattern)}"));
            sim.Events.Subscribe<WeaveCast>(w => Line(FormattableString.Invariant($"cast {Name(w.Caster)} {w.Weave} {Name(w.Target)} {w.Point.X:0.##} {w.Point.Y:0.##}")));
            sim.Events.Subscribe<WeaveReleased>(r => Line(FormattableString.Invariant($"release {Name(r.Caster)} {r.Weave} {r.Mode} {r.Reason} {(r.Fired ? "fired" : "held")} {r.Held:0.##} {r.Fill:0.##} {r.Stage}")));
            sim.Events.Subscribe<WeaveInterrupted>(i => Line($"interrupt {Name(i.Caster)} {i.Weave}"));
            sim.Events.Subscribe<DamageDealt>(d => Line(FormattableString.Invariant($"hit {Name(d.Source)} {Name(d.Target)} {d.Total:0.##} {Marks(d)}")));
            sim.Events.Subscribe<Healed>(h => Line(FormattableString.Invariant($"heal {Name(h.Source)} {Name(h.Target)} {h.Resource} {h.Amount:0.##}")));
            sim.Events.Subscribe<StatusApplied>(s => Line(FormattableString.Invariant($"status {Name(s.Source)} {Name(s.Target)} {s.Status} {s.Stacks} {s.Duration:0.##}")));
            sim.Events.Subscribe<EntityDied>(d => Line($"died {Name(d.Entity)} {Name(d.Killer)}"));
        }

        /// <summary>The last line: the outcome, then each body's life and position as the fight ends.</summary>
        public void End(string outcome) => Line($"end {outcome} a {Standing(A)} b {Standing(B)}");

        private string Standing(Entity body)
        {
            var life = _sim.World.Get<Stats>(body)?.Resource("life") ?? 0;
            var at = _sim.World.Get<Transform>(body)?.Position ?? Vector3.Zero;
            return FormattableString.Invariant($"{life:0.##} {at.X:0.##} {at.Y:0.##}");
        }

        public ulong Hash() => Rng.Hash64(_log.ToString());

        private void Line(string text) => _log.Append(_sim.Clock.Tick.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(text).Append('\n');

        private static string Marks(DamageDealt d) => d.Crit || d.Evaded || d.Blocked ? (d.Crit ? "c" : "") + (d.Evaded ? "e" : "") + (d.Blocked ? "b" : "") : "-";

        private string Name(Entity e, string? fallback = null)
        {
            if (e.IsNone)
            {
                return "-";
            }
            if (e == A)
            {
                return "a";
            }
            if (e == B)
            {
                return "b";
            }
            var identity = _sim.World.Get<Identity>(e);
            var what = identity?.Pattern ?? (identity is { Name.Length: > 0 } ? identity.Name : fallback ?? "entity");
            if (identity is null || identity.Owner.IsNone)
            {
                return what;
            }
            return identity.Owner == A ? "a." + what : identity.Owner == B ? "b." + what : what;
        }
    }
}
