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
/// with no parameter of its own (an area's aftershocks, a fork's angle), under the same conditions.
/// </summary>
internal static class DeliveryAxes
{
    public static void Read(AxisReading reading)
    {
        foreach (var node in reading.Nodes)
        {
            var bindings = AxisBindings.For(node.Type);
            if (bindings.Count == 0)
            {
                continue;
            }
            var stated = new Dictionary<string, AxisValue>(StringComparer.Ordinal);
            // Bindings without a condition first, so a condition reads its sibling as the report placed it.
            for (var pass = 0; pass < 2; pass++)
            {
                foreach (var binding in bindings)
                {
                    if ((binding.When is null) != (pass == 0) || binding.Dimension is not { } dimension || binding.ReadBy is not null)
                    {
                        continue;
                    }
                    if (binding.Kind == ParameterKind.Node && binding.Node is not (NodeShape.Predicate or NodeShape.Reference))
                    {
                        continue;
                    }
                    if (stated.ContainsKey(binding.Parameter) || (binding.When is { } when && !Holds(when, node, stated, bindings)))
                    {
                        continue;
                    }
                    foreach (var value in Values(reading, node, binding, dimension))
                    {
                        reading.Add(value);
                        stated.TryAdd(binding.Parameter, value);
                    }
                }
            }
        }
    }

    private static IEnumerable<AxisValue> Values(AxisReading reading, AxisNode node, AxisBinding binding, Dimension dimension)
    {
        if (binding.Implicit)
        {
            if (Unwritten(reading, node, binding, dimension, $"{node.Path}.{binding.Parameter}") is { } value)
            {
                yield return value;
            }
            yield break;
        }
        foreach (var (json, path, key) in Occurrences(node.Node, binding.Parameter, node.Path))
        {
            if (json is null)
            {
                if (key is null && Unwritten(reading, node, binding, dimension, path) is { } fallback)
                {
                    yield return fallback;
                }
                continue;
            }
            if (Written(reading, node, binding, dimension, json, path, key) is { } written)
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
        string? attribute = binding.Role == AttributeRole.Fallback ? binding.Attribute : null;
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
        if (binding.Role == AttributeRole.Multiplier && binding.Attribute is { } scale)
        {
            attribute = scale;
            multiplied = true;
            if (number is { } written && reading.Fold(scale) is { } factor)
            {
                detail = Join(detail, $"{AxisReport.Format(written)} x {scale}");
                number = written * factor;
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
            Detail = detail,
            HidesNumber = binding.HidesNumber,
        };
    }

    /// <summary>
    /// A value the graph leaves out: the attribute the handler falls back to when the weave states it, else the
    /// rule it reads; for an implicit word, what the effect states by being in the graph.
    /// </summary>
    private static AxisValue? Unwritten(AxisReading reading, AxisNode node, AxisBinding binding, Dimension dimension, string path)
    {
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
        if (binding.Attribute is { } attribute && binding.Role != AttributeRole.None && reading.Mentions(attribute))
        {
            if (binding.Role == AttributeRole.Fallback)
            {
                number = reading.Fold(attribute) * binding.Factor;
                detail = binding.Factor == 1 ? "falls back to " + attribute : $"{AxisReport.Format(binding.Factor)} x {attribute}";
            }
            else if (binding.Default is { } @default && reading.Fold(attribute) is { } factor)
            {
                number = @default * factor;
                detail = $"{AxisReport.Format(@default)} x {attribute}";
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

    private static string? Join(string? a, string? b) => string.IsNullOrEmpty(a) ? b : string.IsNullOrEmpty(b) ? a : a + "; " + b;

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

    /// <summary>Whether a binding's condition over a sibling parameter holds.</summary>
    private static bool Holds(string when, AxisNode node, Dictionary<string, AxisValue> stated, IReadOnlyList<AxisBinding> bindings)
    {
        var negate = when.Contains("!=", StringComparison.Ordinal);
        var cut = when.IndexOf(negate ? "!=" : "=", StringComparison.Ordinal);
        var parameter = cut < 0 ? when : when[..cut];
        var effective = Effective(parameter, node, stated, bindings);
        if (cut < 0)
        {
            return effective is not null && effective.Length > 0 && effective is not ("0" or "false" or "none");
        }
        var words = when[(cut + (negate ? 2 : 1))..].Split('|');
        var hit = effective is not null && Array.IndexOf(words, effective) >= 0;
        return negate ? !hit : hit;
    }

    /// <summary>A sibling's value as a word: as the graph writes it, else as the report placed it, else its binding's default.</summary>
    private static string? Effective(string parameter, AxisNode node, Dictionary<string, AxisValue> stated, IReadOnlyList<AxisBinding> bindings)
    {
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
