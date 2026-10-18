using System.Globalization;
using System.Numerics;
using System.Text.Json.Nodes;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Maths;

namespace HollowWake.Core.Tests.Grammar;

/// <summary>The nine dimensions of the combat grammar: each decides one thing about a cast, through the axes and choices inside it.</summary>
internal enum Dimension
{
    Geometry,
    Kinematics,
    Payload,
    Economy,
    Timing,
    Targeting,
    Agency,
    Causality,
    Coupling,
}

/// <summary>An axis is one quantity with a unit inside a dimension; a choice is one word of a registered set.</summary>
internal enum ParameterKind
{
    Axis,
    Choice,
}

/// <summary>
/// One sampled parameter of a delivery: an axis drawn from <see cref="Low"/>..<see cref="High"/> in its
/// <see cref="Unit"/> (whole numbers when <see cref="Whole"/>), or a choice drawn from <see cref="Words"/>.
/// <see cref="Source"/> says where the range comes from: the values shipped content gives it and the clamp its handler
/// applies. A parameter with an <see cref="Attribute"/> falls back to that weave attribute when the node leaves it out,
/// so a point may set it there instead; an <see cref="AttributeOnly"/> axis is reached only through the attribute.
/// </summary>
internal sealed record Parameter(string Name, ParameterKind Kind, Dimension Dimension)
{
    public string Unit { get; init; } = "";
    public double Low { get; init; }
    public double High { get; init; }
    public bool Whole { get; init; }
    public string[] Words { get; init; } = Array.Empty<string>();

    /// <summary>The weave attribute the handler reads when the node leaves the parameter out.</summary>
    public string? Attribute { get; init; }

    /// <summary>Reached only through <see cref="Attribute"/>.</summary>
    public bool AttributeOnly { get; init; }

    /// <summary>What the handler takes when the node leaves the parameter out and no attribute stands behind it; null for none.</summary>
    public object? Default { get; init; }

    /// <summary>The share of morph points that leave the parameter out, so the default is sampled too.</summary>
    public double Absent { get; init; }

    /// <summary>The share of morph points that draw exactly zero, for an axis whose zero means something of its own (a permanent summon).</summary>
    public double Zero { get; init; }

    /// <summary>Overrides of the family's default sample that make the parameter live in the crowd scenario.</summary>
    public IReadOnlyDictionary<string, object> Live { get; init; } = new Dictionary<string, object>();

    /// <summary>The two values the sensitivity check compares, when not the range's ends.</summary>
    public (object Low, object High)? Pair { get; init; }

    /// <summary>
    /// The parameter draws on the cast's random stream, which is keyed by the weave, so two weaves holding the same value
    /// roll differently: such a parameter is compared between its values over the point's seeds, and not between its
    /// node and its attribute.
    /// </summary>
    public bool Rolls { get; init; }

    /// <summary>The words of a choice that draw on the cast's random stream: compared with the other words over the point's seeds rather than on one draw.</summary>
    public string[] Rolling { get; init; } = Array.Empty<string>();

    public string Source { get; init; } = "";

    public static Parameter Axis(string name, Dimension dimension, string unit, double low, double high, string source) =>
        new(name, ParameterKind.Axis, dimension) { Unit = unit, Low = low, High = high, Source = source };

    public static Parameter Count(string name, Dimension dimension, string unit, int low, int high, string source) =>
        new(name, ParameterKind.Axis, dimension) { Unit = unit, Low = low, High = high, Whole = true, Source = source };

    public static Parameter Choice(string name, Dimension dimension, IEnumerable<string> words, string source) =>
        new(name, ParameterKind.Choice, dimension) { Words = words.ToArray(), Source = source };

    /// <summary>"axis 'speed'" or "choice 'shape'", for messages.</summary>
    public string Describe => $"{(Kind == ParameterKind.Axis ? "axis" : "choice")} '{Name}'";

    /// <summary>One value of the parameter, drawn on the stream.</summary>
    public object Draw(Rng rng)
    {
        if (Kind == ParameterKind.Choice)
        {
            return Words[rng.Next(Words.Length)];
        }
        var zero = rng.NextDouble() < Zero;
        var v = Low + rng.NextDouble() * (High - Low);
        return zero ? 0.0 : Whole ? Math.Round(v) : Math.Round(v, 3);
    }

    /// <summary>The values the sensitivity check runs: the range's ends (or its pair), or every word.</summary>
    public IReadOnlyList<object> Probes() => Kind == ParameterKind.Choice
        ? Words
        : Pair is { } pair ? new[] { pair.Low, pair.High } : new object[] { Low, High };

    /// <summary>Whether a value of the parameter draws on the cast's random stream: a rolling word, or a rolling axis away from zero.</summary>
    public bool RollsAt(object value) => Kind == ParameterKind.Choice
        ? value is string word && Rolling.Contains(word)
        : Rolls && Convert.ToDouble(value, CultureInfo.InvariantCulture) != 0;
}

/// <summary>
/// A further sensitivity row for a parameter: the same values compared from the default sample under other live
/// overrides, so the parameter must also move the crowd there (a shove's distance, beside a dash's).
/// </summary>
internal sealed record Row(string Parameter, string Label, IReadOnlyDictionary<string, object> Live);

/// <summary>
/// Two words of a choice the crowd scenario cannot tell apart, with why: the pairwise check accepts their collapse,
/// and fails when they show apart after all, so the list stays honest.
/// </summary>
internal sealed record Alike(string Parameter, string A, string B, string Reason)
{
    public bool Covers(string parameter, string a, string b) => Parameter == parameter && ((A == a && B == b) || (A == b && B == a));

    /// <summary>Every pair among the words, each with the same reason.</summary>
    public static IEnumerable<Alike> Among(string parameter, string reason, params string[] words)
    {
        for (var i = 0; i < words.Length; i++)
        {
            for (var j = i + 1; j < words.Length; j++)
            {
                yield return new Alike(parameter, words[i], words[j], reason);
            }
        }
    }
}

/// <summary>What a list the family fills carries: a plain damage hit, a counted marker (lists that run without a target), or a small burst of damage (plain lists run at a point).</summary>
internal enum Rider
{
    Damage,
    Marker,
    Burst,
}

/// <summary>A list of the delivery node the family fills, the rider it carries, and the share of morph points that fill it.</summary>
internal sealed record ListFill(string Name, Rider Rider, double Chance = 1);

/// <summary>
/// What one press may do at most, derived from the point's parameters and the rules, and how long what it makes may
/// live. <see cref="Live"/> caps what the press made that stands at once (flights, fields, marks, bodies);
/// <see cref="Why"/> spells the derivation so a breach names it.
/// </summary>
internal sealed record Budget(double Damage, double Live, double Created, double Seconds, string Why)
{
    public bool Finite => double.IsFinite(Damage) && double.IsFinite(Live) && double.IsFinite(Created) && double.IsFinite(Seconds);

    /// <summary>Caps on how often a marked list may run: list name to the most runs.</summary>
    public IReadOnlyDictionary<string, double> Markers { get; init; } = new Dictionary<string, double>();
}

/// <summary>A mechanism the compiled weave must state, or must not, because of a point's values.</summary>
internal readonly record struct Statement(string Mechanism, bool Holds, string Because);

/// <summary>
/// One family of the combat grammar, a delivery or an effect that raises bodies: its effect, the shell it rides, where
/// the press aims, its parameter table, the default sample the sensitivity check starts from, the lists it fills, how it
/// sits in the weave, what one press may do, and what the compiled weave must state.
/// </summary>
internal sealed class Family
{
    public required string Name { get; init; }

    /// <summary>The delivery's effect type.</summary>
    public required string Effect { get; init; }

    /// <summary>The shell form (drop weight zero) the point's weave rides.</summary>
    public required string Shell { get; init; }

    /// <summary>The mechanism probe that says the weave has this delivery; <see cref="States"/> must speak of it.</summary>
    public required string Probe { get; init; }

    /// <summary>The aim point, relative to the caster; the press always aims east.</summary>
    public required Vector2 Aim { get; init; }

    public required Parameter[] Parameters { get; init; }

    /// <summary>The default sample: the values every sensitivity point starts from.</summary>
    public required IReadOnlyDictionary<string, object> Defaults { get; init; }

    public required ListFill[] Lists { get; init; }

    /// <summary>The preset's own attributes: what the shell reads its cost, cooldown and timing from.</summary>
    public required IReadOnlyDictionary<string, double> Attributes { get; init; }

    /// <summary>Puts the delivery node into the weave's effect list (most families: at the root).</summary>
    public required Func<Point, JsonObject, JsonArray> Carrier { get; init; }

    public required Func<View, Budget> Budget { get; init; }

    public required Func<View, IEnumerable<Statement>> States { get; init; }

    /// <summary>Parameters the crowd scenario cannot show, each with why. The check still runs them and fails when one moves after all.</summary>
    public IReadOnlyDictionary<string, string> Exempt { get; init; } = new Dictionary<string, string>();

    /// <summary>Pairs of a choice's words the crowd scenario cannot tell apart, each with why. The check fails when a listed pair shows apart after all.</summary>
    public IReadOnlyList<Alike> Alike { get; init; } = Array.Empty<Alike>();

    /// <summary>Further sensitivity rows, each comparing a parameter's values under its own live overrides.</summary>
    public IReadOnlyList<Row> Rows { get; init; } = Array.Empty<Row>();

    /// <summary>Parameters the table leaves unsampled for now (flags, nodes, presentation), each with why: a later pass turns them into axes or choices.</summary>
    public IReadOnlyDictionary<string, string> Unsampled { get; init; } = new Dictionary<string, string>();

    public Parameter Parameter(string name) => Parameters.First(a => a.Name == name);

    public override string ToString() => Name;
}

/// <summary>
/// One point in a family's parameter space: the values it sets (a parameter left out takes the handler's default),
/// which of them it sets on the weave's attribute rather than the node, and which lists it fills. Its weave and its
/// caster pattern are written into the scratch layer under <see cref="Id"/>.
/// </summary>
internal sealed class Point
{
    public required Family Family { get; init; }
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required ulong Seed { get; init; }
    public Dictionary<string, object> Values { get; } = new(StringComparer.Ordinal);
    public HashSet<string> ViaAttribute { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Lists { get; } = new(StringComparer.Ordinal);

    public string Caster => Id + "_caster";

    /// <summary>The value the point sets, or null when the parameter is left out.</summary>
    public object? Value(string parameter) => Values.TryGetValue(parameter, out var v) ? v : null;

    /// <summary>The point's seeds: its own, then more derived from it, for comparing what a rolling value does over several draws.</summary>
    public IEnumerable<ulong> Seeds(int count)
    {
        yield return Seed;
        for (var i = 1; i < count; i++)
        {
            yield return Rng.Combine(Seed, (ulong)i);
        }
    }

    public override string ToString()
    {
        var values = Family.Parameters.Where(a => Values.ContainsKey(a.Name))
            .Select(a => $"{a.Name}{(ViaAttribute.Contains(a.Name) || a.AttributeOnly ? "@attr" : "")}={Format(Values[a.Name])}");
        return $"{Family.Name} {Label} (weave {Id}, seed {Seed:x16}) {{{string.Join(", ", values)}}} lists [{string.Join(", ", Lists.OrderBy(l => l, StringComparer.Ordinal))}]";
    }

    public static string Format(object value) => value switch
    {
        double d => d.ToString("0.###", CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };
}

/// <summary>
/// A point as its handler sees it: each parameter's effective value, from the node, from the weave attribute (folded
/// through the attribute catalog's bounds), or the default the handler takes when it is left out, and the rules.
/// </summary>
internal sealed class View
{
    public View(Point point, ContentRegistry content)
    {
        Point = point;
        Content = content;
    }

    public Point Point { get; }
    public ContentRegistry Content { get; }

    /// <summary>Seconds per tick of the arena.</summary>
    public const double Dt = 1.0 / MorphArena.TicksPerSecond;

    public bool Sets(string parameter) => Point.Values.ContainsKey(parameter);

    public double Rule(string name, double fallback) => Content.Rules.Number(name, fallback);

    /// <summary>The axis's effective number; NaN when it is left out and nothing stands behind it.</summary>
    public double Num(string name)
    {
        var parameter = Point.Family.Parameter(name);
        if (Point.Values.TryGetValue(name, out var v))
        {
            var d = Convert.ToDouble(v, CultureInfo.InvariantCulture);
            return parameter.Attribute is { } attribute && (parameter.AttributeOnly || Point.ViaAttribute.Contains(name)) ? Bounded(attribute, d) : d;
        }
        if (parameter.Attribute is { } attr)
        {
            // The weave's base: the preset's own, else the shell form's, else the attribute catalog's.
            if (Point.Family.Attributes.TryGetValue(attr, out var preset))
            {
                return Bounded(attr, preset);
            }
            if (Content.Get<FormDef>(Point.Family.Shell)?.Attributes.TryGetValue(attr, out var shell) == true)
            {
                return Bounded(attr, shell);
            }
            var def = Content.Get<AttributeDef>(attr);
            return Bounded(attr, def?.Base ?? 0);
        }
        return parameter.Default is null ? double.NaN : Convert.ToDouble(parameter.Default, CultureInfo.InvariantCulture);
    }

    public double Num(string name, double fallback)
    {
        var v = Num(name);
        return double.IsNaN(v) ? fallback : v;
    }

    public string Word(string name)
    {
        if (Point.Values.TryGetValue(name, out var v))
        {
            return (string)v;
        }
        return Point.Family.Parameter(name).Default as string ?? "";
    }

    public bool Fills(string list) => Point.Lists.Contains(list);

    /// <summary>A value folded through the attribute catalog's bounds, as the weave's attribute fold reads it.</summary>
    private double Bounded(string attribute, double value)
    {
        var def = Content.Get<AttributeDef>(attribute);
        if (def?.Min is { } min)
        {
            value = Math.Max(min, value);
        }
        if (def?.Max is { } max)
        {
            value = Math.Min(max, value);
        }
        return value;
    }
}

/// <summary>
/// A defect the morph test has found and keeps finding: a violation of <see cref="Invariant"/> in
/// <see cref="Family"/> whose message holds <see cref="Contains"/> (and whose point meets <see cref="When"/>) is
/// expected, with <see cref="Reason"/>. A finding that stops reproducing fails the test, so the list stays honest.
/// </summary>
internal sealed record Finding(string Family, string Invariant, string Contains, string Reason)
{
    /// <summary>A condition on the point, so the finding is expected only where the point meets it.</summary>
    public Func<View, bool>? When { get; init; }

    /// <summary>A condition on what the run showed (the violation's outcome), so the finding matches only a violation that shows it.</summary>
    public Func<Violation, bool>? Shows { get; init; }

    public bool Matches(Violation v) => v.Point.Family.Name == Family && v.Invariant == Invariant && v.Message.Contains(Contains, StringComparison.Ordinal)
        && (When is null || When(v.View)) && (Shows is null || Shows(v));
}

/// <summary>One invariant a point broke, with what happened, and the run that showed it when there was one.</summary>
internal sealed record Violation(Point Point, View View, string Invariant, string Message)
{
    public MorphArena.Outcome? Outcome { get; init; }

    public override string ToString() => $"[{Invariant}] {Message}\n    point: {Point}";
}
