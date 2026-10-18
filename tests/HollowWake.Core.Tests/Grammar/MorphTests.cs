using System.Globalization;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace HollowWake.Core.Tests.Grammar;

/// <summary>
/// The morph test: the combat grammar says a cast is a point in a product of dimensions and that the engine's invariants
/// hold at every point. For each delivery and agency family it samples points from the family's parameter table
/// (MorphFamilies.cs: axes, each one quantity with a unit inside a dimension, and choices) on a constant seed and
/// compiles each as content in a scratch layer beside core (MorphLayer.cs). Every point must validate, compile strictly
/// and state what it is as mechanisms; every sampled point, pressed once in the crowd scenario (MorphArena.cs), must run
/// the same twice on one seed, lose no life outside the one damage pipeline, stay within the fan-out its parameters
/// allow, leave nothing behind once its expected life is over, and pay its cost and cooldown. Then, parameter by
/// parameter, each must move something the crowd shows, and an axis with a weave attribute behind it must show the same
/// through the attribute as through the node. Known defects are listed in <see cref="Known"/> with their reasons; one
/// that stops reproducing fails the test so the list stays honest.
/// </summary>
public class MorphTests
{
    private readonly ITestOutputHelper _output;

    public MorphTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public static TheoryData<string> FamilyNames()
    {
        var data = new TheoryData<string>();
        foreach (var family in Families.All)
        {
            data.Add(family.Name);
        }
        return data;
    }

    private static bool OnNode(View v, string parameter) => v.Sets(parameter) && !v.Point.ViaAttribute.Contains(parameter);

    /// <summary>A summon or deploy that raises more bodies in one press than its group may hold.</summary>
    private static bool CountPastCap(View v) =>
        Math.Clamp(Math.Round(v.Num("count")), 1, v.Rule("minions.max_per_cast", 32)) > Math.Max(1, Math.Round(v.Num("max_active")));

    /// <summary>The run moved bodies of the crowd: a shove that moves nothing is not the anchor finding.</summary>
    private static bool Displaced(Violation v) => v.Outcome is { Moved: > 0 };

    private const string OnePressUncapped =
        "SummonRecipe.Wave asks MinionService.MakeRoom to fit a cast's bodies, and MakeRoom dismisses only bodies already standing: it never caps the cast's own count, so one summon or deploy whose count passes max_active stands every body it raises, though minions.md says past the capacity the oldest bodies are dismissed";

    /// <summary>
    /// Defects the morph test found and keeps finding, and what it lists as exempt: each matches violations of one
    /// invariant in one family by a piece of their message (and, where given, by the point). The defects are findings to
    /// fix, not behaviour to keep.
    /// </summary>
    internal static readonly Finding[] Known =
    {
        new("projectile", "states", "'chain' does not hold though chains",
            "the chain probe (Weaves/Probes/DeliveryProbes.cs) reads only the weave attribute projectile.chain, never a projectile node's chains parameter, though the trajectory probes read both")
        {
            When = v => OnNode(v, "chains"),
        },
        new("projectile", "states", "'flight:children' does not hold though forks",
            "flight:children (Weaves/Mechanisms.cs) reads only the weave attribute projectile.forks, never a projectile node's forks parameter")
        {
            When = v => OnNode(v, "forks"),
        },
        new("projectile", "states", "'flight:spends' holds though rehit",
            "flight:spends (Weaves/Mechanisms.cs) is the absence of the weave attribute projectile.rehit and never sees a projectile node's rehit parameter, so a flight that never ends on a hit still says its hits spend it")
        {
            When = v => OnNode(v, "rehit"),
        },
        new("projectile", "states", "'trajectory' does not hold though",
            "WeaveProbe.Raises counts only positive bases and modifiers, so a kinematic axis set negative on the weave (a slowing accel, a recurve or spiral the other way) is not stated; shipped lava_glob sheds speed through projectile.accel -5.8")
        {
            When = v => Families.Trajectory.All(a => v.Num(a, 0) == 0 || (v.Num(a, 0) < 0 && !OnNode(v, a))),
        },
        new("projectile", "doors", "through projectile.count holds 20, where the node holds 64",
            "projectile.count's catalog stops at 20 (attributes.yaml) where the handler clamps a node's count at 64 (DeliveryHandlers.cs): one axis clamps differently through its two doors, against the grammar's rule 1 (one mechanism, one door)"),
        new("projectile", "doors", "through projectile.speed holds 1, where the node holds 0.5",
            "projectile.speed's catalog floors at 1 (attributes.yaml) where the handler floors a node's speed at 0.5 (DeliveryHandlers.cs): one axis clamps differently through its two doors, against rule 1"),
        new("area", "doors", "through area.radius holds 0.25, where the node holds 0.1",
            "area.radius's catalog floors at 0.25 (attributes.yaml) where the area handler floors a node's radius at 0.1 (DeliveryHandlers.cs): one axis clamps differently through its two doors, against rule 1"),
        new("projectile", "timing", "the last salvo beat flew at tick",
            "each salvo beat is scheduled from the one before (DeliveryHandlers.cs Salvo.Beat calls Later with the interval), and Later rounds each wait up to a whole tick, so a salvo whose interval is no whole number of ticks drifts late, up to a tick a beat; EffectHelpers.Later says a sum of short waits lands on the tick it names")
        {
            When = Families.SalvoDrifts,
        },
        new("move", "sensitive", "choice 'anchor': 'from' and 'point' show the crowd the same",
            "inside a hit list the context's point is the struck body (EffectContext.WithTarget moves it there), so anchor point measures from the body itself and the shove runs along the facing; areas and fields set no from, so anchor from measures from the caster. For bodies on the aim line beyond the caster all three anchors push the same way; deliveries.md says point is the aim point and from a field's center")
        {
            Shows = Displaced,
        },
        new("move", "sensitive", "choice 'anchor': 'caster' and 'point' show the crowd the same",
            "the anchor finding above, seen from the caster: point measures from the struck body, and for bodies on the aim line beyond the caster that pushes them the way the caster's position does")
        {
            Shows = Displaced,
        },
        new("move", "sensitive", "choice 'anchor': 'from' and 'caster' show the crowd the same",
            "an area sets no from on its hit list (nor does a field on its pulse), so anchor from measures from the caster, where deliveries.md says from is where the effect acts")
        {
            Shows = Displaced,
        },
        new("summon", "bounded", "stood at once, past the cap", OnePressUncapped) { When = CountPastCap },
        new("deploy", "bounded", "stood at once, past the cap", OnePressUncapped) { When = CountPastCap },
        new("summon", "sensitive", "axis 'max_active' moves nothing",
            "within one press max_active caps nothing (MakeRoom dismisses only bodies already standing), so the crowd scenario's single press cannot show it; it bites only on a later press"),
        new("deploy", "sensitive", "axis 'max_active' moves nothing",
            "within one press max_active caps nothing (MakeRoom dismisses only bodies already standing), so the crowd scenario's single press cannot show it; it bites only on a later press"),
        new("area", "pipeline", "with no DamageDealt on it",
            "the resource effect takes life with no hit: it adds to the resource directly (DamageHandlers.cs ResourceHandler), so a drain on a struck body lowers its life outside the stage ladder; the grammar's invariant 1 names it as the baseline exception")
        {
            When = v => v.Fills("effects+drain"),
        },
        new("area", "depth", "status ticks restart the depth",
            "a status runs its tick effects and the applier's tick riders in a context at depth 0 (StatusService.cs Execute), whatever depth applied it; rule 8's baseline")
        {
            When = v => v.Fills("effects+status"),
        },
        new("area", "depth", "follow-ups restart the depth",
            "a use schedules its follow-ups at depth 1 (SlotBehaviors.cs ScheduleFollowUp), whatever depth the use ran at, so a triggered use's follow-up runs shallower than the use; rule 8's baseline")
        {
            When = v => v.Fills(MorphLayer.Relay),
        },
        new("area", "depth", "cast events restart the depth",
            "a cast raises the cast trigger kind at depth 0 (Sim/Triggers.cs OnCast: WeaveCast carries no depth), so a proc answering a triggered use's cast runs at depth 1 whatever depth the use ran at; the runtime lifts its own triggered seats by the depth it keeps while a triggered cast resolves, but procs and status hooks read the event's; rule 8's baseline")
        {
            When = v => v.Fills(MorphLayer.Relay),
        },
        new("deploy", "states", "'construct:massed' does not hold though count",
            "construct:massed (Weaves/Mechanisms.cs Construct) reads only a deploy or spawn node's count, never the weave attribute summon.count that a deploy's count falls back to")
        {
            When = v => !OnNode(v, "count") && Math.Round(v.Num("count")) > 1,
        },
        new(Finding.AnyFamily, "states", "'landing' does not hold though",
            "the landing probe (Weaves/Mechanisms.cs) holds only for a projectile's gravity or a node carrying an on_land list, so a leap, a field placed after a delay or a telegraphed strike that carries none is never stated as landing, though the probe's own description names lobbed flights, leaps and delayed placements")
        {
            When = v => Families.Lands(v) && !v.Fills("on_land"),
        },
        new(Finding.AnyFamily, "states", "'landing' holds though",
            "the landing probe holds for any node carrying an on_land list, so a flight that is not lobbed, a field placed at once (whose on_land never runs) or a move that is not a leap (a dash runs it where it stops) is stated as landing, though none lands as the probe describes landing")
        {
            When = v => !Families.Lands(v) && v.Fills("on_land"),
        },
        new("spawn", "left behind", "bare spawn",
            "exempt: a spawn without a lifetime places a body with no owner's terms, so it outlives every scenario by design; a finding, not a failure")
        {
            When = v => !v.Sets("lifetime"),
        },
    };

    /// <summary>Validation, strict compilation and the mechanisms each point's values must state, over every point of the family.</summary>
    [Theory]
    [MemberData(nameof(FamilyNames))]
    public void EveryPointValidatesCompilesAndStatesItsMechanisms(string name)
    {
        var layer = MorphLayer.Shared;
        var violations = new List<Violation>();
        var examined = layer.All(name).Select(p => new View(p, layer.Content)).ToList();
        foreach (var view in examined)
        {
            violations.AddRange(MorphChecks.Static(layer, view.Point).Select(b => new Violation(view.Point, view, b.Invariant, b.Message)));
        }
        var family = Families.Named(name);
        _output.WriteLine($"{name} on {family.Shell}, stated by '{family.Probe}': {examined.Count} points checked");
        foreach (var p in family.Parameters)
        {
            var range = p.Kind == ParameterKind.Axis ? $"{p.Low:0.###}..{p.High:0.###} {p.Unit}" : string.Join("|", p.Words);
            var exempt = family.Exempt.TryGetValue(p.Name, out var why) ? $"; exempt: {why}" : "";
            _output.WriteLine($"  {p.Dimension.ToString().ToLowerInvariant()} {p.Describe} {range}{(p.Attribute is null ? "" : $" (or {p.Attribute})")}: {p.Source}{exempt}");
        }
        foreach (var (parameter, why) in family.Unsampled)
        {
            _output.WriteLine($"  unsampled '{parameter}': {why}");
        }
        // Every registered mechanism whose answer the table's samples move must be stated, both ways, at every point.
        var held = examined.Select(v => (View: v, Held: MorphChecks.Held(layer, v.Point))).Where(h => h.Held is not null).ToList();
        foreach (var mechanism in MorphChecks.Variants)
        {
            if (held.Select(h => h.Held![mechanism]).Distinct().Count() < 2)
            {
                continue;
            }
            var unstated = held.FirstOrDefault(h => !family.States(h.View).Any(s => s.Mechanism == mechanism));
            if (unstated.View is { } view)
            {
                violations.Add(new Violation(view.Point, view, "states",
                    $"'{mechanism}' holds at some of the family's points and not at others, and the table does not state it here (it {(unstated.Held![mechanism] ? "holds" : "does not hold")})"));
            }
        }
        Settle(name, examined, violations, "validates", "compiles", "states");
    }

    /// <summary>Each sampled point, pressed once: deterministic, one damage pipeline, bounded, terminating, nothing left behind, paying, and nothing logged.</summary>
    [Theory]
    [MemberData(nameof(FamilyNames))]
    public void EverySampledPointHoldsTheInvariants(string name)
    {
        var layer = MorphLayer.Shared;
        var violations = new List<Violation>();
        var examined = layer.Morph[name].Where(p => Compiles(layer, p)).Select(p => new View(p, layer.Content)).ToList();
        foreach (var view in examined)
        {
            var point = view.Point;
            var first = MorphArena.Run(layer.Content, point);
            var second = MorphArena.Run(layer.Content, point);
            _output.WriteLine($"{point}\n    {first.Hits} hits (cap {first.Budget?.Damage:0.#}), {first.MaxLive} standing at most (cap {first.Budget?.Live:0.#}), {first.Created} made, life {first.Budget?.Seconds:0.##} s: {first.Observable}");
            var broken = first.Broken.ToList();
            if (Diverges(first.Events, second.Events) is { } where)
            {
                broken.Add(("deterministic", where));
            }
            violations.AddRange(broken.Select(b => new Violation(point, view, b.Invariant, b.Message)));
        }
        Settle(name, examined, violations, "deterministic", "pipeline", "bounded", "terminates", "left behind", "pays", "depth", "timing", "runs");
    }

    /// <summary>How many of a point's seeds a rolling value is compared over.</summary>
    private const int Draws = 4;

    /// <summary>
    /// Each axis and choice, between its values with everything else at the family's default sample, moves something
    /// the crowd shows, unless it is exempt for a stated reason. An axis compares its two values; a choice compares its
    /// words two at a time, and a pair that shows the same fails unless the family lists it as alike with why. A value
    /// that rolls on the cast's stream is compared over the point's seeds rather than on one draw.
    /// </summary>
    [Theory]
    [MemberData(nameof(FamilyNames))]
    public void EveryAxisAndChoiceMovesTheCrowd(string name)
    {
        var layer = MorphLayer.Shared;
        var family = Families.Named(name);
        var violations = new List<Violation>();
        var examined = new List<View>();
        foreach (var sense in layer.Sensitivity[name])
        {
            if (!sense.Points.All(p => Compiles(layer, p)))
            {
                continue;
            }
            examined.AddRange(sense.Points.Select(p => new View(p, layer.Content)));
            var parameter = sense.Parameter;
            var draws = sense.Points.Any(p => parameter.RollsAt(p.Values[parameter.Name])) ? Draws : 1;
            var shown = sense.Points.Select(p => new Shown(p, Point.Format(p.Values[parameter.Name]), p.Seeds(draws).Select(seed => MorphArena.Run(layer.Content, p, seed)).ToList())).ToList();
            foreach (var s in shown)
            {
                _output.WriteLine($"{s.Point.Label}: {s.Text}");
            }
            var exempt = family.Exempt.TryGetValue(parameter.Name, out var why);
            var sample = sense.Points[0];
            var row = sense.Row is null ? "" : $" in the {sense.Row} row";
            if (parameter.Kind == ParameterKind.Axis)
            {
                var moves = !shown[0].Observables.SequenceEqual(shown[1].Observables, StringComparer.Ordinal);
                if (!moves && !exempt)
                {
                    violations.Add(new Violation(sample, new View(sample, layer.Content), "sensitive",
                        $"{parameter.Describe}{row} moves nothing the crowd shows between {string.Join(" and ", shown.Select(s => s.Point.Label))}: {shown[0].Text}") { Outcome = shown[0].Runs[0] });
                }
                else if (moves && exempt)
                {
                    violations.Add(new Violation(sample, new View(sample, layer.Content), "sensitive",
                        $"{parameter.Describe}{row} is exempt ({why}) but moves the crowd: {string.Join(" / ", shown.Select(s => $"{s.Point.Label}: {s.Text}"))}") { Outcome = shown[0].Runs[0] });
                }
                continue;
            }
            for (var i = 0; i < shown.Count; i++)
            {
                for (var j = i + 1; j < shown.Count; j++)
                {
                    var a = shown[i];
                    var b = shown[j];
                    var same = a.Observables.SequenceEqual(b.Observables, StringComparer.Ordinal);
                    var alike = family.Alike.FirstOrDefault(p => p.Covers(parameter.Name, a.Word, b.Word));
                    if (same && !exempt && alike is null)
                    {
                        violations.Add(new Violation(a.Point, new View(a.Point, layer.Content), "sensitive",
                            $"{parameter.Describe}{row}: '{a.Word}' and '{b.Word}' show the crowd the same{(draws > 1 ? $" over {draws} seeds" : "")}: {a.Text}") { Outcome = a.Runs[0] });
                    }
                    else if (!same && (exempt || alike is not null))
                    {
                        violations.Add(new Violation(a.Point, new View(a.Point, layer.Content), "sensitive",
                            $"{parameter.Describe}{row}: '{a.Word}' and '{b.Word}' are listed as alike ({(exempt ? why : alike!.Reason)}) but show apart: {a.Point.Label}: {a.Text} / {b.Point.Label}: {b.Text}") { Outcome = a.Runs[0] });
                    }
                }
            }
        }
        Settle(name, examined, violations, "sensitive");
    }

    /// <summary>What one value of a parameter showed the crowd, on each seed it ran on.</summary>
    private sealed record Shown(Point Point, string Word, List<MorphArena.Outcome> Runs)
    {
        public IEnumerable<string> Observables => Runs.Select(r => r.Observable);

        public string Text => string.Join(" || ", Observables);
    }

    /// <summary>
    /// An axis with a weave attribute behind it shows the same whether a value is set on the node or on the attribute,
    /// unless the axis rolls on the cast's stream. Where the attribute catalog's bounds move the value, the two doors
    /// clamp differently, which breaks the grammar's rule 1 (one mechanism, one door) and is reported, and the door is
    /// compared with the node set to the value the door holds.
    /// </summary>
    [Theory]
    [MemberData(nameof(FamilyNames))]
    public void EveryAttributeAxisReadsAsItsParameter(string name)
    {
        var layer = MorphLayer.Shared;
        var violations = new List<Violation>();
        var examined = new List<View>();
        foreach (var sense in layer.Sensitivity[name].Where(s => s.Doors.Count > 0 && !s.Parameter.Rolls))
        {
            var parameter = sense.Parameter;
            for (var i = 0; i < sense.Points.Count; i++)
            {
                var door = sense.Doors[i];
                var twin = sense.Twins[i];
                var node = twin ?? sense.Points[i];
                if (!Compiles(layer, node) || !Compiles(layer, door))
                {
                    continue;
                }
                var view = new View(door, layer.Content);
                examined.Add(view);
                var value = Convert.ToDouble(door.Values[parameter.Name], CultureInfo.InvariantCulture);
                var held = view.Num(parameter.Name);
                if (held != value)
                {
                    violations.Add(new Violation(door, view, "doors",
                        $"{parameter.Describe} = {Point.Format(value)} through {parameter.Attribute} holds {Point.Format(held)}, where the node holds {Point.Format(value)}: its two doors clamp differently"));
                }
                var throughNode = MorphArena.Run(layer.Content, node).Observable;
                var throughAttribute = MorphArena.Run(layer.Content, door).Observable;
                if (throughNode != throughAttribute)
                {
                    violations.Add(new Violation(door, view, "doors",
                        $"{parameter.Describe} = {Point.Format(value)} shows '{throughAttribute}' through {parameter.Attribute} but '{throughNode}' on the node ({node.Label})"));
                }
            }
        }
        Settle(name, examined, violations, "doors");
    }

    /// <summary>Whether a point's layer entries validate and its weave compiles: points that do not are reported by the static theory and skipped by the rest.</summary>
    private static bool Compiles(MorphLayer layer, Point p) => !MorphChecks.Static(layer, p).Any(b => b.Invariant is "validates" or "compiles");

    /// <summary>Null when two runs logged the same events, else where they first part.</summary>
    private static string? Diverges(List<string> a, List<string> b)
    {
        for (var i = 0; i < Math.Max(a.Count, b.Count); i++)
        {
            var x = i < a.Count ? a[i] : "(end)";
            var y = i < b.Count ? b[i] : "(end)";
            if (x != y)
            {
                return $"two runs on one seed part at event {i}: '{x}' then '{y}'";
            }
        }
        return null;
    }

    /// <summary>
    /// Fails on every violation no known finding explains, and on every known finding of these invariants that no longer
    /// reproduces: one without a point condition must match some violation, and one with a condition must match whenever
    /// some examined point meets the condition (a finding no examined point reaches is left untested, as deeper sweeps
    /// reach it).
    /// </summary>
    private static void Settle(string family, IEnumerable<View> examined, List<Violation> violations, params string[] invariants)
    {
        var known = Known.Where(f => f.Of(family) && invariants.Contains(f.Invariant)).ToList();
        var unexplained = violations.Where(v => !known.Any(f => f.Matches(v))).ToList();
        var views = examined.ToList();
        var stale = known.Where(f => !violations.Any(f.Matches) && (f.When is null || views.Any(f.When))).ToList();
        if (unexplained.Count == 0 && stale.Count == 0)
        {
            return;
        }
        var text = new StringBuilder();
        foreach (var v in unexplained)
        {
            text.AppendLine(v.ToString());
        }
        foreach (var f in stale)
        {
            text.AppendLine($"[{f.Invariant}] the known finding '{f.Contains}' ({f.Reason}) no longer reproduces: remove it from MorphTests.Known");
        }
        Assert.Fail($"{family}: {unexplained.Count} violations, {stale.Count} stale findings\n{text}");
    }
}
