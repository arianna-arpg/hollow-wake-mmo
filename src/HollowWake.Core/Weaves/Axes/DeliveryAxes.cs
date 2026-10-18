using System.Text.Json.Nodes;
using HollowWake.Core.Content;
using HollowWake.Core.Expressions;

namespace HollowWake.Core.Weaves.Axes;

/// <summary>
/// The deliveries family, and with it every effect's parameters: each node of the compiled graph, at every
/// depth, read through its <see cref="AxisBindings"/>. A parameter the graph writes is placed as written (a
/// literal, an attribute reference, a formula; a word), by whoever wrote it (the form's or preset's graph, a
/// glyph's patch, a rider); one it leaves out is placed when the weave states the attribute it falls back to,
/// or when the handler reads a rule in its place. Implicit bindings place what the handler or its system reads
/// with no parameter of its own (an area's aftershocks, a fork's angle), under the same conditions. A binding's
/// condition says when the handler reads the parameter at all, its fallback condition when an unwritten one
/// falls back.
/// </summary>
internal static class DeliveryAxes
{
    /// <summary>One node as it is read: the weave, the node, its bindings and what the report placed of it so far.</summary>
    private sealed class Siblings
    {
        public required AxisReading Reading { get; init; }
        public required AxisNode Node { get; init; }
        public required IReadOnlyList<AxisBinding> Bindings { get; init; }
        public Dictionary<string, AxisValue> Stated { get; } = new(StringComparer.Ordinal);
    }

    public static void Read(AxisReading reading)
    {
        foreach (var node in reading.Nodes)
        {
            var bindings = AxisBindings.For(node.Type);
            if (bindings.Count == 0)
            {
                continue;
            }
            var siblings = new Siblings { Reading = reading, Node = node, Bindings = bindings };
            // Bindings without a condition first, so a condition reads its sibling as the report placed it.
            for (var pass = 0; pass < 2; pass++)
            {
                foreach (var binding in bindings)
                {
                    if ((binding.When is null && binding.FallbackWhen is null) != (pass == 0) || binding.Dimension is not { } dimension || binding.ReadBy is not null)
                    {
                        continue;
                    }
                    if (binding.Kind == ParameterKind.Node && binding.Node is not (NodeShape.Predicate or NodeShape.Reference))
                    {
                        continue;
                    }
                    if (siblings.Stated.ContainsKey(binding.Parameter) || (binding.When is { } when && !Holds(when, siblings)))
                    {
                        continue;
                    }
                    foreach (var value in Values(siblings, binding, dimension))
                    {
                        reading.Add(value);
                        siblings.Stated.TryAdd(binding.Parameter, value);
                    }
                }
            }
        }
    }

    private static IEnumerable<AxisValue> Values(Siblings siblings, AxisBinding binding, Dimension dimension)
    {
        var node = siblings.Node;
        if (binding.Implicit)
        {
            if (Unwritten(siblings, binding, dimension, $"{node.Path}.{binding.Parameter}") is { } value)
            {
                yield return value;
            }
            yield break;
        }
        foreach (var (json, path, key) in Occurrences(node.Node, binding.Parameter, node.Path))
        {
            if (json is null)
            {
                if (!binding.WrittenOnly && key is null && (binding.FallbackWhen is not { } fallsBack || Holds(fallsBack, siblings)) && Unwritten(siblings, binding, dimension, path) is { } fallback)
                {
                    yield return fallback;
                }
                continue;
            }
            if (Written(siblings.Reading, node, binding, dimension, json, path, key) is { } written)
            {
                yield return written;
            }
        }
    }

    private static string AxisName(AxisBinding binding, string? key) => key is null ? binding.Axis : binding.Axis.Replace("*", key);

    /// <summary>A value the graph writes, placed by who wrote it.</summary>
    private static AxisValue? Written(AxisReading reading, AxisNode node, AxisBinding binding, Dimension dimension, JsonNode json, string path, string? key)
    {
        AxisSource nature;
        double? number = null;
        string? word = null;
        string? detail = null;
        string? attribute = binding.Role == AttributeRole.Fallback ? Expand(binding.Attribute, node) : null;
        var multiplied = false;
        if (AxisUnits.IsWord(binding.Unit))
        {
            word = binding.Kind == ParameterKind.Flag && json is JsonObject ? "true" : reading.Words(json);
            nature = binding.Unit == AxisUnits.Condition ? AxisSource.Expression : AxisSource.Literal;
        }
        else if (json is JsonValue literal && JsonNum.TryDouble(literal, out var n))
        {
            number = n;
            nature = AxisSource.Literal;
        }
        else if (JsonNum.StringOf(json) is { } text)
        {
            if (!Expr.TryParse(text, out var expr, out _) || expr is null)
            {
                word = text;
                nature = AxisSource.Expression;
            }
            else if (expr.IsConstant)
            {
                number = expr.ConstantValue;
                nature = AxisSource.Literal;
            }
            else if (AxisReading.WeaveReference(expr) is { } reference)
            {
                number = reading.Fold(reference);
                word = number is null ? text : null;
                attribute = reference;
                detail = text;
                nature = AxisSource.Attribute;
            }
            else
            {
                number = reading.Evaluate(expr);
                word = number is null ? text : null;
                detail = number is null ? null : text;
                nature = AxisSource.Expression;
            }
        }
        else if (json is JsonValue flag && flag.TryGetValue<bool>(out var on))
        {
            number = on ? 1 : 0;
            nature = AxisSource.Literal;
        }
        else
        {
            // A list of numbers (an aim's bearings), or a block, as written.
            word = reading.Words(json);
            nature = AxisSource.Literal;
        }
        // Written as a constant, whoever wrote it: a patch's or a rider's number is as frozen as the form's.
        var constant = nature == AxisSource.Literal && number is not null;
        if (binding.Role is AttributeRole.Multiplier or AttributeRole.Channel && Expand(binding.Attribute, node) is { } scale)
        {
            attribute = scale;
            multiplied = true;
            if (number is { } written && Reach(reading, binding, scale, written) is { } reached)
            {
                detail = Join(detail, reached.Detail);
                number = reached.Number;
            }
        }
        var source = nature;
        if (node.Provenance(binding.Top) is { } who)
        {
            source = who.Source;
            detail = Join(who.By, detail);
        }
        if (number is null && string.IsNullOrEmpty(word))
        {
            return null;
        }
        var origin = node.OriginOf(binding.Top, path);
        return new AxisValue
        {
            Dimension = dimension,
            Name = AxisValue.DimensionName(dimension) + "." + AxisName(binding, key),
            Number = number,
            Word = number is null ? word : null,
            Unit = binding.Unit,
            Source = source,
            Path = path,
            Attribute = attribute,
            Multiplied = multiplied,
            Effect = node.Type,
            Parameter = binding.Parameter,
            Detail = Join(binding.WrittenDetail, detail),
            HidesNumber = binding.HidesNumber,
            Constant = constant,
            Writer = origin?.Writer,
            Origin = origin?.Path,
        };
    }

    /// <summary>
    /// A value the graph leaves out: the attribute the handler falls back to when the weave states it, else the
    /// rule it reads; for an implicit word, what the effect states by being in the graph.
    /// </summary>
    private static AxisValue? Unwritten(Siblings siblings, AxisBinding binding, Dimension dimension, string path)
    {
        var (reading, node) = (siblings.Reading, siblings.Node);
        double? number;
        AxisSource source;
        string? detail;
        var multiplied = false;
        if (binding.Implicit && AxisUnits.IsWord(binding.Unit) && binding.DefaultWord is { } word)
        {
            return new AxisValue
            {
                Dimension = dimension,
                Name = AxisValue.DimensionName(dimension) + "." + binding.Axis,
                Word = word,
                Unit = binding.Unit,
                Source = node.AddedAs ?? AxisSource.Literal,
                Path = path,
                Effect = node.Type,
                Parameter = binding.Parameter,
                Detail = node.AddedBy,
            };
        }
        // Only the weave's own fold is the weave's statement: a stack the handler reads elsewhere is the caster's, not the weave's.
        if (binding.Attribute is { } attribute && binding.Role != AttributeRole.None && binding.Fold == AttributeFold.Weave && reading.Mentions(attribute))
        {
            if (binding.Role == AttributeRole.Fallback)
            {
                // A sibling's word may scale the fallback too: a bar takes a quarter of the weave's area width.
                var factor = binding.Factor;
                if (binding.FactorOf is { } of)
                {
                    if (Property(of, siblings) is not { } share)
                    {
                        return null;
                    }
                    factor *= share;
                }
                number = reading.Fold(attribute) * factor;
                detail = factor == 1 ? "falls back to " + attribute : $"{AxisReport.Format(factor)} x {attribute}";
            }
            else if (binding.Default is { } @default && Reach(reading, binding, attribute, @default) is { } reached)
            {
                number = reached.Number;
                detail = reached.Detail;
                multiplied = true;
            }
            else
            {
                return null;
            }
            source = AxisSource.Attribute;
        }
        else if (binding.Rule is { } rule)
        {
            number = reading.Content.Rules.Number(rule, binding.Default ?? 0);
            source = AxisSource.Rule;
            detail = rule;
        }
        else
        {
            return null;
        }
        if (number is null)
        {
            return null;
        }
        if (node.AddedAs is { } added)
        {
            source = added;
            detail = Join(node.AddedBy, detail);
        }
        return new AxisValue
        {
            Dimension = dimension,
            Name = AxisValue.DimensionName(dimension) + "." + binding.Axis,
            Number = number,
            Unit = binding.Unit,
            Source = source,
            Path = path,
            Attribute = binding.Attribute,
            Multiplied = multiplied,
            Effect = node.Type,
            Parameter = binding.Parameter,
            Detail = detail,
        };
    }

    /// <summary>
    /// A number as the attribute of a multiplier or a channel reaches it: times the weave's fold of a multiplier,
    /// through the weave's modifiers on a channel; as written where the stack is another's (the caster's own, a
    /// bearer's), which the report's holder has none of.
    /// </summary>
    private static (double Number, string Detail)? Reach(AxisReading reading, AxisBinding binding, string attribute, double value)
    {
        var text = AxisReport.Format(value);
        if (binding.Fold != AttributeFold.Weave)
        {
            return (value, $"{text} x {attribute} on the {(binding.Fold == AttributeFold.Caster ? "caster's" : "bearer's")} own stack");
        }
        if (binding.Role == AttributeRole.Channel)
        {
            return reading.Channel(attribute, value) is { } through ? (through, $"{text} through {attribute}") : null;
        }
        return reading.Fold(attribute) is { } factor ? (value * factor, $"{text} x {attribute}") : null;
    }

    private static string? Join(string? a, string? b) => string.IsNullOrEmpty(a) ? b : string.IsNullOrEmpty(b) ? a : a + "; " + b;

    /// <summary>
    /// A binding's attribute with each sibling named in braces put in as the node writes it (<c>charge.{id}.max</c>
    /// is <c>charge.fury.max</c>); null where the node writes no such word.
    /// </summary>
    private static string? Expand(string? attribute, AxisNode node)
    {
        while (attribute is not null && attribute.IndexOf('{') is var open and >= 0)
        {
            var close = attribute.IndexOf('}', open);
            if (close < 0 || JsonNum.StringOf(node.Node[attribute[(open + 1)..close]]) is not { } word)
            {
                return null;
            }
            attribute = attribute[..open] + word + attribute[(close + 1)..];
        }
        return attribute;
    }

    /// <summary>
    /// Where a binding's parameter sits in a node: the parameter itself, a field inside a block
    /// (<c>place.range</c>), each key of a map (<c>amounts.*</c>, with the key) or each element of a list
    /// (<c>choices[].weight</c>). A plain parameter the node leaves out yields once with no value.
    /// </summary>
    private static IEnumerable<(JsonNode? Json, string Path, string? Key)> Occurrences(JsonObject node, string parameter, string path)
    {
        var steps = parameter.Split('.');
        var frontier = new List<(JsonNode? Json, string Path, string? Key)> { (node, path, null) };
        foreach (var step in steps)
        {
            var next = new List<(JsonNode?, string, string?)>();
            foreach (var (json, at, key) in frontier)
            {
                if (step == "*")
                {
                    if (json is JsonObject map)
                    {
                        foreach (var (name, value) in map)
                        {
                            next.Add((value, $"{at}.{name}", name));
                        }
                    }
                }
                else if (step.EndsWith("[]", StringComparison.Ordinal))
                {
                    var name = step[..^2];
                    if ((json as JsonObject)?[name] is JsonArray list)
                    {
                        for (var i = 0; i < list.Count; i++)
                        {
                            next.Add((list[i], $"{at}.{name}[{i}]", key));
                        }
                    }
                }
                else
                {
                    next.Add(((json as JsonObject)?[step], $"{at}.{step}", key));
                }
            }
            frontier = next;
        }
        return frontier;
    }

    /// <summary>
    /// Whether a binding's condition holds: every term joined by <c>&amp;</c> holds, each over a sibling parameter
    /// or the weave (see <see cref="AxisBinding.When"/>).
    /// </summary>
    private static bool Holds(string condition, Siblings siblings)
    {
        foreach (var raw in condition.Split('&'))
        {
            if (!HoldsTerm(raw.Trim(), siblings))
            {
                return false;
            }
        }
        return true;
    }

    private static bool HoldsTerm(string term, Siblings siblings)
    {
        if (term.StartsWith('#'))
        {
            return siblings.Reading.Weave.Tags.Has(term[1..]);
        }
        if (term.StartsWith('!'))
        {
            return siblings.Node.Node[term[1..]] is null;
        }
        if (term.Contains(':', StringComparison.Ordinal))
        {
            return Property(term, siblings) is { } value && value != 0;
        }
        var negate = term.Contains("!=", StringComparison.Ordinal);
        var cut = term.IndexOf(negate ? "!=" : "=", StringComparison.Ordinal);
        var parameter = cut < 0 ? term : term[..cut];
        var effective = Effective(parameter, siblings);
        if (cut < 0)
        {
            return effective is not null && effective.Length > 0 && effective is not ("0" or "false" or "none");
        }
        var words = term[(cut + (negate ? 2 : 1))..].Split('|');
        var hit = effective is not null && Array.IndexOf(words, effective) >= 0;
        return negate ? !hit : hit;
    }

    /// <summary>
    /// A property of a sibling's word, <c>shape:width_share</c>: the word the sibling states, looked up in the
    /// vocabulary its binding names; null where the vocabulary declares no such property or does not know the word.
    /// </summary>
    private static double? Property(string term, Siblings siblings)
    {
        var colon = term.IndexOf(':');
        var parameter = term[..colon];
        var registry = siblings.Bindings.FirstOrDefault(b => b.Parameter == parameter && b.Registry is not null)?.Registry;
        return registry is not null && Effective(parameter, siblings) is { } word ? AxisBindings.PropertyOf(registry, term[(colon + 1)..], word) : null;
    }

    /// <summary>A sibling's value as a word: as the graph writes it, else as the report placed it, else its binding's default.</summary>
    private static string? Effective(string parameter, Siblings siblings)
    {
        var (node, stated, bindings) = (siblings.Node, siblings.Stated, siblings.Bindings);
        switch (node.Node[parameter])
        {
            case JsonValue value when value.TryGetValue<bool>(out var flag):
                return flag ? "true" : "false";
            case JsonValue value when JsonNum.TryDouble(value, out var n):
                return AxisReport.Format(n);
            case JsonValue value when JsonNum.StringOf(value) is { } text:
                return text;
            case JsonNode other:
                return other.ToJsonString();
        }
        if (stated.TryGetValue(parameter, out var placed))
        {
            return placed.Number is { } number ? AxisReport.Format(number) : placed.Word;
        }
        foreach (var binding in bindings)
        {
            if (binding.Parameter == parameter)
            {
                return binding.DefaultWord ?? (binding.Default is { } d ? AxisReport.Format(d) : null);
            }
        }
        return null;
    }
}
