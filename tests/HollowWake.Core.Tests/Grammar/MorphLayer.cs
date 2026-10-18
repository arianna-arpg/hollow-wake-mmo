using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using HollowWake.Core.Content;
using HollowWake.Core.Maths;

namespace HollowWake.Core.Tests.Grammar;

/// <summary>One parameter's sensitivity points (one per value compared, set on the node) and, for an axis with an attribute behind it, its door points (the same values set on the weave's attribute).</summary>
internal sealed record Sense(Parameter Parameter, IReadOnlyList<Point> Points, IReadOnlyList<Point> Doors);

/// <summary>
/// The morph test's points and the scratch content layer they compile from. Every family's sampled points and its
/// sensitivity points are written as content (one weave on the family's shell and one caster pattern wearing it per
/// point, plus the crowd's dummy and two hostile factions) into a temporary layer and loaded beside core once, so
/// validation, compilation and the arena all see them as shipped content would be seen.
/// </summary>
internal sealed class MorphLayer
{
    /// <summary>
    /// Points sampled per family: sixteen keeps the whole class under about twenty seconds. A deeper sweep sets the
    /// environment variable <c>HOLLOWWAKE_MORPH_POINTS</c>; points draw one after another from the family's stream, so
    /// the first sixteen of a deeper sweep are these.
    /// </summary>
    public static int PointsPerFamily =>
        int.TryParse(Environment.GetEnvironmentVariable("HOLLOWWAKE_MORPH_POINTS"), out var n) && n > 0 ? n : 16;

    /// <summary>The share of points that set a parameter with an attribute fallback on the weave's attribute rather than the node.</summary>
    public const double ViaAttribute = 0.3;

    private static readonly Lazy<MorphLayer> Loaded = new(Build);

    public static MorphLayer Shared => Loaded.Value;

    public required ContentRegistry Content { get; init; }
    public required ValidationReport Report { get; init; }

    /// <summary>Each family's sampled points.</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<Point>> Morph { get; init; }

    /// <summary>Each family's sensitivity points, per parameter.</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<Sense>> Sensitivity { get; init; }

    /// <summary>Every point of a family: sampled, sensitivity and door points.</summary>
    public IEnumerable<Point> All(string family) =>
        Morph[family].Concat(Sensitivity[family].SelectMany(s => s.Points.Concat(s.Doors)));

    /// <summary>The issues validation raised about a point's weave or its caster.</summary>
    public IEnumerable<ValidationIssue> IssuesOf(Point p) => Report.Issues.Where(i => i.Id == p.Id || i.Id == p.Caster);

    // ------------------------------------------------------------ sampling

    /// <summary>
    /// A family's morph points: each parameter drawn on a stream seeded from the family's name, left out by the
    /// parameter's share (so the handler's default is sampled too), and set on the attribute rather than the node by
    /// <see cref="ViaAttribute"/> where an attribute stands behind it; each list filled by its share.
    /// </summary>
    public static List<Point> Sample(Family family, int count)
    {
        var rng = new Rng(Rng.Hash64("morph:" + family.Name));
        var points = new List<Point>(count);
        for (var i = 0; i < count; i++)
        {
            var p = new Point
            {
                Family = family,
                Id = $"morph_{family.Name}_{i:00}",
                Label = $"#{i}",
                Seed = Rng.Hash64($"morph:{family.Name}:{i}"),
            };
            foreach (var parameter in family.Parameters)
            {
                var absent = rng.NextDouble() < parameter.Absent;
                var value = parameter.Draw(rng);
                var viaAttribute = parameter.Attribute is not null && !parameter.AttributeOnly && rng.NextDouble() < ViaAttribute;
                if (absent)
                {
                    continue;
                }
                p.Values[parameter.Name] = value;
                if (viaAttribute)
                {
                    p.ViaAttribute.Add(parameter.Name);
                }
            }
            foreach (var list in family.Lists)
            {
                if (rng.NextDouble() < list.Chance)
                {
                    p.Lists.Add(list.Name);
                }
            }
            points.Add(p);
        }
        return points;
    }

    /// <summary>
    /// A family's sensitivity points: for each parameter, one point per value it compares (an axis's range ends, or
    /// every word of a choice), each the family's default sample with the parameter's live overrides, every list filled,
    /// all on one seed, so the points of one parameter differ on it alone. An axis with an attribute behind it also gets
    /// its door points: the same values set on the weave's attribute instead of the node.
    /// </summary>
    public static List<Sense> Senses(Family family)
    {
        var seed = Rng.Hash64($"morph:{family.Name}:sensitivity");
        var k = 0;
        var result = new List<Sense>();
        foreach (var parameter in family.Parameters)
        {
            Point Make(object value, bool viaAttribute)
            {
                var p = new Point { Family = family, Id = $"morph_{family.Name}_s{k++:000}", Label = $"{parameter.Name}{(viaAttribute ? "@attr" : "")}={Point.Format(value)}", Seed = seed };
                foreach (var (name, v) in family.Defaults)
                {
                    p.Values[name] = v;
                }
                foreach (var (name, v) in parameter.Live)
                {
                    p.Values[name] = v;
                }
                p.Values[parameter.Name] = value;
                if (viaAttribute)
                {
                    p.ViaAttribute.Add(parameter.Name);
                }
                foreach (var list in family.Lists)
                {
                    p.Lists.Add(list.Name);
                }
                return p;
            }
            var values = parameter.Probes();
            var points = values.Select(v => Make(v, false)).ToList();
            var doors = parameter.Attribute is not null && !parameter.AttributeOnly && parameter.Kind == ParameterKind.Axis
                ? values.Select(v => Make(v, true)).ToList()
                : new List<Point>();
            result.Add(new Sense(parameter, points, doors));
        }
        return result;
    }

    // ------------------------------------------------------------ the layer

    private static MorphLayer Build()
    {
        var morph = new Dictionary<string, IReadOnlyList<Point>>(StringComparer.Ordinal);
        var sensitivity = new Dictionary<string, IReadOnlyList<Sense>>(StringComparer.Ordinal);
        foreach (var family in Families.All)
        {
            morph[family.Name] = Sample(family, PointsPerFamily);
            sensitivity[family.Name] = Senses(family);
        }
        var points = morph.Values.SelectMany(p => p).Concat(sensitivity.Values.SelectMany(s => s).SelectMany(s => s.Points.Concat(s.Doors))).ToList();
        var dir = Path.Combine(Path.GetTempPath(), "hollowwake-morph-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Write(Path.Combine(dir, "factions.yaml"), "factions", new[]
            {
                Json($"{{\"id\": \"{MorphArena.Red}\", \"name\": \"Morph Casters\", \"relations\": {{\"{MorphArena.Blue}\": \"hostile\"}}}}"),
                Json($"{{\"id\": \"{MorphArena.Blue}\", \"name\": \"Morph Crowd\", \"relations\": {{\"{MorphArena.Red}\": \"hostile\"}}}}"),
            });
            Write(Path.Combine(dir, "patterns.yaml"), "patterns", points.Select(CasterOf).Prepend(Dummy()));
            Write(Path.Combine(dir, "weaves.yaml"), "weaves", points.Select(WeaveOf));
            var content = ContentRegistry.Load(new[] { CoreContent.FindContentDir(), dir }, out var report);
            return new MorphLayer { Content = content, Report = report, Morph = morph, Sensitivity = sensitivity };
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static JsonObject Json(string text) => (JsonObject)JsonNode.Parse(text)!;

    /// <summary>One YAML document of a kind, an entry per line as a flow mapping.</summary>
    private static void Write(string path, string kind, IEnumerable<JsonObject> entries)
    {
        var text = new StringBuilder();
        text.Append("# The morph test's scratch layer (tests/HollowWake.Core.Tests/Grammar), written for one run.\n");
        text.Append("kind: ").Append(kind).Append('\n');
        text.Append("entries:\n");
        foreach (var entry in entries)
        {
            text.Append("  - ").Append(entry.ToJsonString()).Append('\n');
        }
        File.WriteAllText(path, text.ToString());
    }

    /// <summary>The crowd's body: much life, nothing to cast, nowhere to go, and no evasion, so every hit that reaches it lands.</summary>
    private static JsonObject Dummy() => new()
    {
        ["id"] = MorphArena.Dummy,
        ["name"] = "Morph Dummy",
        ["extends"] = "creature",
        ["radius"] = 0.5,
        ["components"] = new JsonObject
        {
            ["stats"] = new JsonObject { ["modifiers"] = new JsonArray(Modifier("life.max", "flat", 100000), Modifier("evasion", "override", 0)) },
        },
    };

    /// <summary>The caster of a point: a creature wearing the point's weave, with life to spare and the resources to pay.</summary>
    private static JsonObject CasterOf(Point p) => new()
    {
        ["id"] = p.Caster,
        ["name"] = "Morph Caster",
        ["extends"] = "creature",
        ["radius"] = 0.5,
        ["components"] = new JsonObject
        {
            ["stats"] = new JsonObject
            {
                ["attributes"] = new JsonObject { ["mana.max"] = 400, ["stamina.max"] = 400 },
                ["modifiers"] = new JsonArray(Modifier("life.max", "flat", 100000)),
            },
            ["weaves"] = new JsonObject { ["list"] = new JsonArray(p.Id) },
        },
    };

    private static JsonObject Modifier(string attribute, string form, double value) => new() { ["attribute"] = attribute, ["form"] = form, ["value"] = value };

    /// <summary>A point's weave: the family's shell with its preset attributes, the axes the point sets on attributes, and the delivery in its carrier.</summary>
    public static JsonObject WeaveOf(Point p)
    {
        var attributes = new JsonObject();
        foreach (var (name, value) in p.Family.Attributes)
        {
            attributes[name] = value;
        }
        foreach (var parameter in p.Family.Parameters)
        {
            if (parameter.Attribute is { } attribute && (parameter.AttributeOnly || p.ViaAttribute.Contains(parameter.Name)) && p.Values.TryGetValue(parameter.Name, out var v))
            {
                attributes[attribute] = Convert.ToDouble(v, CultureInfo.InvariantCulture);
            }
        }
        return new JsonObject
        {
            ["id"] = p.Id,
            ["name"] = $"Morph {p.Family.Name} {p.Label}",
            ["form"] = p.Family.Shell,
            ["threads"] = new JsonArray("ember"),
            ["attributes"] = attributes,
            ["effects"] = p.Family.Carrier(p, Node(p)),
        };
    }

    /// <summary>The delivery node of a point: its effect, the values it sets on the node, and the lists it fills.</summary>
    public static JsonObject Node(Point p)
    {
        var node = new JsonObject { ["type"] = p.Family.Effect };
        foreach (var parameter in p.Family.Parameters)
        {
            if (parameter.AttributeOnly || p.ViaAttribute.Contains(parameter.Name) || !p.Values.TryGetValue(parameter.Name, out var v))
            {
                continue;
            }
            node[parameter.Name] = v is string word ? JsonValue.Create(word) : JsonValue.Create(Convert.ToDouble(v, CultureInfo.InvariantCulture));
        }
        foreach (var list in p.Family.Lists)
        {
            if (p.Lists.Contains(list.Name))
            {
                node[list.Name] = Riders.Of(list);
            }
        }
        return node;
    }
}
