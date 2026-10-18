using System.Text.Json.Nodes;
using HollowWake.Core.Attributes;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Expressions;
using static HollowWake.Core.Weaves.AxisUnits;
using static HollowWake.Core.Weaves.Dimension;

namespace HollowWake.Core.Weaves.Axes;

/// <summary>
/// The statuses family, and with it coupling: the state one cast writes on a body and another reads. Writes
/// are the statuses an effect applies (and the flags they carry), the charges it gains or drains, the
/// modifiers it lends, the marks it leaves and the statuses it strips; reads are the statuses it consumes,
/// ruptures, spreads or moves, the charges it spends (in its graph or as its cast's spend), the marks it
/// returns to, and every name of a status, a charge or a gauge its formulas, gates, requirements and its
/// modifiers' gauges read, with the <c>vs.*</c> scopes its modifiers ask of the struck body. Each value's word
/// is the body the key is written on or read from: <c>target</c>, <c>self</c>, <c>allies</c> ...
/// </summary>
internal static class StatusAxes
{
    public const string Family = "statuses";

    /// <summary>How each effect type that couples writes or reads its keys, registered per type.</summary>
    private static readonly Dictionary<string, Action<AxisReading, AxisNode, Sink>> Couplers = new(StringComparer.Ordinal);

    static StatusAxes()
    {
        Register("apply_status", (r, n, add) =>
        {
            var to = Word(n, "to") ?? (Flag(n, "self") ? "self" : "target");
            foreach (var status in Statuses(r, n))
            {
                Status(r, add, "writes", status, to, n, "status");
            }
        });
        Register("remove_status", (r, n, add) =>
        {
            foreach (var (key, parameter) in Selection(r, n))
            {
                add("writes.remove." + key, Flag(n, "self") ? "self" : "target", $"{n.Path}.{parameter}", n.Provenance(parameter));
            }
        });
        Register("consume_status", (r, n, add) => Reads(r, n, add, Flag(n, "self") ? "self" : "target"));
        Register("rupture", (r, n, add) => Reads(r, n, add, Flag(n, "self") ? "self" : "target"));
        Register("spread_status", (r, n, add) =>
        {
            Reads(r, n, add, "target");
            Writes(r, n, add, Word(n, "filter") ?? "allies");
        });
        Register("move_status", (r, n, add) =>
        {
            Reads(r, n, add, Word(n, "from") ?? "target");
            Writes(r, n, add, Word(n, "to") ?? "self");
        });
        Register("charge", (r, n, add) =>
        {
            if (Word(n, "id") is not { } id)
            {
                return;
            }
            var body = n.Node["self"] is JsonValue v && v.TryGetValue<bool>(out var self) && !self ? "target" : "self";
            if (n.Node["gain"] is not null || n.Node["drain"] is not null)
            {
                add("writes.charge." + id, body, n.Path + ".id", n.Provenance("id"));
            }
            if (n.Node["spend"] is not null || Flag(n, "all"))
            {
                add("reads.charge." + id, body, n.Path + ".id", n.Provenance("id"));
            }
        });
        Register("modify", (r, n, add) =>
        {
            if (Word(n, "attribute") is { } attribute)
            {
                add("writes.modifier." + attribute, Flag(n, "self") ? "self" : "target", n.Path + ".attribute", n.Provenance("attribute"));
            }
        });
        Register("mark", (r, n, add) => add("writes.mark." + (Word(n, "key") ?? "mark"), "self", n.Path + ".key", n.Provenance("key")));
        Register("move", (r, n, add) =>
        {
            var kind = Word(n, "kind") ?? "teleport";
            var mover = Word(n, "who") ?? (kind is "knockback" or "pull" or "buffet" ? "target" : "self");
            if (kind == "recall")
            {
                add("reads.mark." + (Word(n, "mark") ?? "mark"), "self", n.Path + ".mark", n.Provenance("mark"));
            }
            if (Word(n, "status") is { } status)
            {
                Status(r, add, "writes", status, mover, n, "status");
            }
        });
    }

    /// <summary>Registers how an effect type couples; a type registered again replaces its coupler.</summary>
    public static void Register(string type, Action<AxisReading, AxisNode, Sink> coupler) => Couplers[type] = coupler;

    /// <summary>Places one coupling key: its axis below <c>coupling.</c>, the body, the path, and who stated it.</summary>
    public delegate void Sink(string axis, string body, string path, (AxisSource Source, string By)? who);

    public static void Read(AxisReading r)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in r.Nodes)
        {
            Sink add = (axis, body, path, who) => Place(r, seen, axis, body, path, who ?? Added(node), node.Type);
            if (Couplers.TryGetValue(node.Type, out var coupler))
            {
                coupler(r, node, add);
            }
            // Every formula of the node: what it reads off a body by name.
            foreach (var (parameter, value) in node.Node)
            {
                if (parameter != "type" && JsonNum.StringOf(value) is { } text && !JsonNum.TryDouble(value, out _) && Expr.TryParse(text, out var expr, out _) && expr is not null)
                {
                    Scan(r, expr, $"{node.Path}.{parameter}", node.Provenance(parameter) ?? Added(node), add);
                }
            }
        }

        var weave = r.Weave;
        // What the cast spends.
        if (weave.Casting.Spend is { } spend)
        {
            var (source, at) = r.OwnerOf(spend, g => g.Spend, p => p.Spend, "spend");
            Place(r, seen, "reads.charge." + spend.Charge, "self", at + ".charge", (source, ""), null);
        }
        // The modifiers the weave carries: the victim scopes they ask for, the gauges they scale by, the conditions they read.
        foreach (var modifier in weave.Modifiers)
        {
            var (source, at, by) = OwnerOf(modifier);
            foreach (var tag in modifier.Scope)
            {
                if (tag.StartsWith("vs.", StringComparison.Ordinal))
                {
                    Place(r, seen, "reads." + tag, "target", at, (source, by), null);
                }
            }
            if (modifier.Gauge is { } gauge)
            {
                Place(r, seen, ReadOf(r, gauge) ?? "reads.gauge." + gauge, "self", at, (source, by), null);
            }
            if (modifier.Condition is { } condition)
            {
                Scan(r, condition, at, (source, by), (axis, body, path, who) => Place(r, seen, axis, body, path, who, null));
            }
        }
        // The gates, the target's requirement and a convert's condition.
        foreach (var (gate, source, path) in TargetingAxes.Gates(r))
        {
            Scan(r, gate, path, (source, ""), (axis, body, p, who) => Place(r, seen, axis, body, p, who, null));
        }
        if (weave.Form.Target?.Require is { } require)
        {
            Scan(r, require, "form.target.require", (AxisSource.Form, ""), (axis, body, p, who) => Place(r, seen, axis, body, p, who, null));
        }
        if (weave.Casting.Convert is { } convert)
        {
            var (source, at) = r.OwnerOf(convert, g => g.Convert, p => p.Convert, "convert");
            Scan(r, convert.When, at + ".when", (source, ""), (axis, body, p, who) => Place(r, seen, axis, body, p, who, null));
        }
    }

    private static void Place(AxisReading r, HashSet<string> seen, string axis, string body, string path, (AxisSource Source, string By)? who, string? effect)
    {
        if (!seen.Add(axis + "|" + path))
        {
            return;
        }
        r.Add(new AxisValue
        {
            Dimension = Coupling,
            Name = AxisValue.DimensionName(Coupling) + "." + axis,
            Word = body,
            Unit = AxisUnits.Word,
            Source = who?.Source ?? AxisSource.Literal,
            Path = path,
            Effect = effect,
            Detail = string.IsNullOrEmpty(who?.By) ? null : who.Value.By,
        });
    }

    private static (AxisSource Source, string By)? Added(AxisNode node) => node.AddedAs is { } added ? (added, node.AddedBy ?? "") : null;

    /// <summary>Where a modifier the weave carries came from, by its source: the preset, a glyph, or a tree node.</summary>
    private static (AxisSource Source, string Path, string By) OwnerOf(Modifier modifier)
    {
        var colon = modifier.Source.IndexOf(':');
        var owner = colon < 0 ? modifier.Source : modifier.Source[(colon + 1)..];
        return modifier.Source.StartsWith("preset:", StringComparison.Ordinal) ? (AxisSource.Preset, "preset.modifiers", "")
            : modifier.Source.StartsWith("tree:", StringComparison.Ordinal) ? (AxisSource.Glyph, $"tree.{owner}.modifiers", "tree " + owner)
            : (AxisSource.Glyph, $"glyph.{owner}.modifiers", "glyph " + owner);
    }

    /// <summary>The names of statuses, charges and gauges a formula reads, each on the body its prefix names.</summary>
    private static void Scan(AxisReading r, Expr expr, string path, (AxisSource Source, string By)? who, Sink add)
    {
        foreach (var name in expr.Identifiers)
        {
            var body = "self";
            var rest = name;
            if (name.StartsWith("target.", StringComparison.Ordinal))
            {
                body = "target";
                rest = name["target.".Length..];
            }
            else if (name.StartsWith("source.", StringComparison.Ordinal) || name.StartsWith("caster.", StringComparison.Ordinal))
            {
                rest = name[(name.IndexOf('.') + 1)..];
            }
            if (ReadOf(r, rest) is { } axis)
            {
                add(axis, body, path, who);
            }
        }
    }

    /// <summary>The coupling read a name is: <c>status.{id}...</c> of a status, <c>charge.{id}...</c> of a charge kind, <c>gauge.*</c>.</summary>
    private static string? ReadOf(AxisReading r, string name)
    {
        var parts = name.Split('.');
        if (parts.Length >= 2 && parts[0] == "status" && r.Content.Has<StatusDef>(parts[1]))
        {
            return "reads.status." + parts[1];
        }
        if (parts.Length >= 2 && parts[0] == "charge" && r.Content.Has<ChargeDef>(parts[1]))
        {
            return "reads.charge." + parts[1];
        }
        return parts.Length >= 2 && parts[0] == "gauge" ? "reads.gauge." + string.Join(".", parts.Skip(1)) : null;
    }

    private static void Status(AxisReading r, Sink add, string direction, string status, string body, AxisNode node, string parameter)
    {
        var who = node.Provenance(parameter);
        add($"{direction}.status.{status}", body, $"{node.Path}.{parameter}", who);
        if (direction == "writes" && r.Content.Get<StatusDef>(status) is { } def)
        {
            foreach (var flag in def.Flags)
            {
                add("writes.flag." + flag, body, $"{node.Path}.{parameter}", who);
            }
        }
    }

    private static void Reads(AxisReading r, AxisNode node, Sink add, string body)
    {
        foreach (var (key, parameter) in Selection(r, node))
        {
            add("reads.status." + key, body, $"{node.Path}.{parameter}", node.Provenance(parameter));
        }
    }

    private static void Writes(AxisReading r, AxisNode node, Sink add, string body)
    {
        foreach (var (key, parameter) in Selection(r, node))
        {
            add("writes.status." + key, body, $"{node.Path}.{parameter}", node.Provenance(parameter));
        }
    }

    /// <summary>
    /// The statuses a removal or a transfer selects, each with the parameter that names it: by id (<c>status</c>,
    /// <c>statuses</c>), else by tags (<c>tags.a+b</c>), else every status of its kind (<c>kind.harmful</c>).
    /// </summary>
    private static List<(string Key, string Parameter)> Selection(AxisReading r, AxisNode node)
    {
        var keys = new List<(string, string)>();
        if (Word(node, "status") is { } one)
        {
            keys.Add((one, "status"));
        }
        if (node.Node["statuses"] is JsonArray many)
        {
            keys.AddRange(many.Select(JsonNum.StringOf).OfType<string>().Select(id => (id, "statuses")));
        }
        if (keys.Count == 0 && Words(r, node, "tags") is { Length: > 0 } tags)
        {
            keys.Add(("tags." + tags.Replace(',', '+'), "tags"));
        }
        if (keys.Count == 0)
        {
            keys.Add(("kind." + (Word(node, "kind") ?? "harmful"), "kind"));
        }
        return keys;
    }

    /// <summary>The status an application names, with the Thread's own put in for <c>$thread_status</c>.</summary>
    private static IEnumerable<string> Statuses(AxisReading r, AxisNode node)
    {
        var status = Word(node, "status") ?? "$thread_status";
        if (status != "$thread_status")
        {
            yield return status;
            yield break;
        }
        var thread = Threads(r, node).FirstOrDefault();
        if (thread is not null && r.Content.Get<ThreadDef>(thread)?.Status is { } own)
        {
            yield return own;
        }
    }

    /// <summary>The Threads an effect names, as the compiler resolves them: the weave's for <c>$threads</c> or none named.</summary>
    private static IEnumerable<string> Threads(AxisReading r, AxisNode node)
    {
        var weave = r.Weave.Threads.Select(t => t.Id).ToList();
        switch (node.Node["threads"])
        {
            case null:
                return weave;
            case JsonArray list:
                return list.Select(JsonNum.StringOf).OfType<string>().SelectMany(t => t == "$threads" ? weave : t == "$thread" ? weave.Take(1) : new[] { t });
            case var single when JsonNum.StringOf(single) is { } word:
                return word == "$threads" ? weave : word == "$thread" ? weave.Take(1) : new[] { word };
            default:
                return weave;
        }
    }

    private static string? Word(AxisNode node, string parameter) => JsonNum.StringOf(node.Node[parameter]);

    private static string? Words(AxisReading r, AxisNode node, string parameter) => r.Words(node.Node[parameter]);

    private static bool Flag(AxisNode node, string parameter) => JsonNum.BoolOf(node.Node[parameter], false);
}
