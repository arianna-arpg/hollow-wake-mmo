using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using HollowWake.Core.Content;
using HollowWake.Core.Maths;

namespace HollowWake.Core.Tests.Grammar;

/// <summary>
/// One parameter's sensitivity points in one row (one per value compared, set on the node) and, for an axis with an
/// attribute behind it, its door points (the same values set on the weave's attribute) and, for each door whose value
/// the attribute catalog's bounds move, a twin: the node set to the value the door holds. <see cref="Row"/> names a
/// further row; null is the parameter's own.
/// </summary>
internal sealed record Sense(Parameter Parameter, IReadOnlyList<Point> Points, IReadOnlyList<Point> Doors, string? Row = null)
{
    public IReadOnlyList<Point?> Twins { get; init; } = Array.Empty<Point?>();
}

/// <summary>
/// The morph test's points and the scratch content layer they compile from. Every family's sampled points and its
/// sensitivity points are written as content (one weave on the family's shell and one caster pattern wearing it per
/// point, plus the crowd's dummy and two hostile factions) into a temporary layer and loaded beside core once, so
/// validation, compilation and the arena all see them as shipped content would be seen.
/// </summary>
internal sealed class MorphLayer
{
    /// <summary>
    /// Points sampled per family: a few by default, to keep the default run short. A deeper sweep sets the environment
    /// variable <c>HOLLOWWAKE_MORPH_POINTS</c>; points draw one after another from the family's stream, so a deeper
    /// sweep begins with the default's points.
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
    public IEnumerable<Point> All(string family) => Morph[family].Concat(Sensitivity[family].SelectMany(PointsOf));

    /// <summary>A sense's points: its node points, its doors and the twins of the doors the catalog moves.</summary>
    private static IEnumerable<Point> PointsOf(Sense s) => s.Points.Concat(s.Doors).Concat(s.Twins.OfType<Point>());

    /// <summary>
    /// The issues validation raised that fail a point: those about its weave or its caster, and every issue about no
    /// point's entry (the crowd's dummy, the factions, the riders' entries, or a core entry the layer's content makes
    /// fail). Core validates clean on its own, so every issue in the report is the scratch layer's.
    /// </summary>
    public IEnumerable<ValidationIssue> IssuesOf(Point p) => Report.Issues.Where(i => i.Id == p.Id || i.Id == p.Caster || !Owned.Contains(i.Id));

    /// <summary>The ids of every point's weave and caster.</summary>
    public required IReadOnlySet<string> Owned { get; init; }

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
            var drawn = family.Lists.Where(list => rng.NextDouble() < list.Chance).ToList();
            // A rider that stands alone takes its list for itself.
            foreach (var alone in drawn.Where(l => l.Alone).ToList())
            {
                drawn.RemoveAll(l => l.Name == alone.Name && !ReferenceEquals(l, alone));
            }
            foreach (var list in drawn)
            {
                p.Lists.Add(list.Key);
            }
            if (family.Relay > 0 && rng.NextDouble() < family.Relay)
            {
                p.Lists.Add(Relay);
            }
            points.Add(p);
        }
        return points;
    }

    /// <summary>What a point's lists hold when its caster carries the relay seat and the cast proc.</summary>
    public const string Relay = "relay";

    /// <summary>The scratch entries the causal riders name: the status that ticks, the weaves a cast and a follow-up run, the relay seat, and the cast proc.</summary>
    public const string Mark = "morph_mark";
    public const string Echo = "morph_echo";
    public const string Follow = "morph_follow";
    public const string RelaySeat = "morph_relay";
    public const string CastProc = "morph_cast_echo";

    /// <summary>Seconds the marking status lasts, and between its ticks.</summary>
    public const double MarkSeconds = 1.0;
    public const double MarkInterval = 0.25;

    /// <summary>Seconds after the relay's use its follow-up runs.</summary>
    public const double FollowDelay = 0.25;

    /// <summary>
    /// A family's sensitivity points: for each parameter, one point per value it compares (an axis's range ends, or
    /// every word of a choice), each the family's default sample with the parameter's live overrides, every list filled,
    /// all on one seed, so the points of one parameter differ on it alone; then the same for each of the family's further
    /// rows under that row's overrides. An axis with an attribute behind it also gets its door points: the same values set
    /// on the weave's attribute instead of the node.
    /// </summary>
    public static List<Sense> Senses(Family family)
    {
        var seed = Rng.Hash64($"morph:{family.Name}:sensitivity");
        var k = 0;
        var result = new List<Sense>();
        var rows = family.Parameters.Select(p => (Parameter: p, p.Live, Row: (string?)null))
            .Concat(family.Rows.Select(r => (Parameter: family.Parameter(r.Parameter), r.Live, Row: (string?)r.Label)));
        foreach (var (parameter, live, row) in rows)
        {
            Point Make(object value, bool viaAttribute, string? note = null)
            {
                var label = $"{parameter.Name}{(viaAttribute ? "@attr" : "")}={Point.Format(value)}{(row is null ? "" : $" ({row})")}{(note is null ? "" : $" ({note})")}";
                var p = new Point { Family = family, Id = $"morph_{family.Name}_s{k++:000}", Label = label, Seed = seed };
                foreach (var (name, v) in family.Defaults)
                {
                    p.Values[name] = v;
                }
                foreach (var (name, v) in live)
                {
                    p.Values[name] = v;
                }
                p.Values[parameter.Name] = value;
                if (viaAttribute)
                {
                    p.ViaAttribute.Add(parameter.Name);
                }
                foreach (var list in family.Lists.Where(l => !l.Sampled))
                {
                    p.Lists.Add(list.Key);
                }
                return p;
            }
            var values = parameter.Probes();
            var points = values.Select(v => Make(v, false)).ToList();
            var doors = parameter.Attribute is not null && !parameter.AttributeOnly && parameter.Kind == ParameterKind.Axis
                ? values.Select(v => Make(v, true)).ToList()
                : new List<Point>();
            // A door whose value the catalog's bounds move is compared with the node set to the value it holds.
            var twins = doors.Select(door =>
            {
                var value = Convert.ToDouble(door.Values[parameter.Name], CultureInfo.InvariantCulture);
                var held = View.Fold(CoreContent.Registry, parameter.Attribute!, value);
                return held == value ? null : Make(held, false, $"as {door.Label} holds it");
            }).ToList();
            result.Add(new Sense(parameter, points, doors, row) { Twins = twins });
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
        var points = morph.Values.SelectMany(p => p).Concat(sensitivity.Values.SelectMany(s => s).SelectMany(PointsOf)).ToList();
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
            Write(Path.Combine(dir, "weaves.yaml"), "weaves", points.Select(WeaveOf).Concat(Causes()));
            Write(Path.Combine(dir, "statuses.yaml"), "statuses", new[] { MarkStatus() });
            Write(Path.Combine(dir, "procs.yaml"), "procs", new[] { Proc() });
            var content = ContentRegistry.Load(new[] { CoreContent.FindContentDir(), dir }, out var report);
            var owned = points.SelectMany(p => new[] { p.Id, p.Caster }).ToHashSet(StringComparer.Ordinal);
            return new MorphLayer { Content = content, Report = report, Morph = morph, Sensitivity = sensitivity, Owned = owned };
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

    /// <summary>
    /// The caster of a point: a creature wearing the point's weave, with life to spare and the resources to pay. With
    /// the relay it also wears the relay seat and the chance of the cast proc.
    /// </summary>
    private static JsonObject CasterOf(Point p)
    {
        var relay = p.Lists.Contains(Relay);
        var modifiers = new JsonArray(Modifier("life.max", "flat", 100000));
        var weaves = new JsonArray(p.Id);
        if (relay)
        {
            modifiers.Add(Modifier("proc." + CastProc, "flat", 1));
            weaves.Add(RelaySeat);
        }
        return new JsonObject
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
                    ["modifiers"] = modifiers,
                },
                ["weaves"] = new JsonObject { ["list"] = weaves },
            },
        };
    }

    /// <summary>
    /// The weaves the causal riders run, each a marker of the depth it runs at: what a cast rider casts, what the relay's
    /// follow-up casts, and the relay seat itself, a self-aimed preset that casts itself once when its caster casts,
    /// costs nothing and follows up.
    /// </summary>
    private static IEnumerable<JsonObject> Causes()
    {
        yield return new JsonObject
        {
            ["id"] = Echo,
            ["name"] = "Morph Echo",
            ["form"] = "slam_quick",
            ["threads"] = new JsonArray("ember"),
            ["effects"] = new JsonArray(Riders.Marker("cast_run")),
        };
        yield return new JsonObject
        {
            ["id"] = Follow,
            ["name"] = "Morph Follow",
            ["form"] = "slam_quick",
            ["threads"] = new JsonArray("ember"),
            ["effects"] = new JsonArray(Riders.Marker("follow_up")),
        };
        yield return new JsonObject
        {
            ["id"] = RelaySeat,
            ["name"] = "Morph Relay",
            ["form"] = "burst",
            ["threads"] = new JsonArray("ember"),
            ["attributes"] = new JsonObject { ["cast.time"] = 0, ["cost.base.mana"] = 0, ["cooldown.base"] = 0 },
            ["trigger"] = new JsonObject { ["on"] = "cast", ["icd"] = 600 },
            ["follow_up"] = new JsonObject { ["weave"] = Follow, ["delay"] = FollowDelay },
            ["effects"] = new JsonArray(Riders.Marker("relay")),
        };
    }

    /// <summary>The status the status rider applies: no modifiers and no effects of its own, a tick for the applier's riders to ride, hidden from the interface.</summary>
    private static JsonObject MarkStatus() => new()
    {
        ["id"] = Mark,
        ["name"] = "Morph Mark",
        ["duration"] = MarkSeconds,
        ["tick"] = new JsonObject { ["interval"] = MarkInterval, ["effects"] = new JsonArray() },
        ["meta"] = new JsonObject { ["hidden"] = true },
    };

    /// <summary>The proc on the caster's casts: it marks the depth it runs at, for every cast, the relay's among them.</summary>
    private static JsonObject Proc() => new()
    {
        ["id"] = CastProc,
        ["name"] = "Morph Cast Echo",
        ["on"] = "cast",
        ["target"] = "owner",
        ["effects"] = new JsonArray(Riders.Marker("proc")),
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
            if (!p.Lists.Contains(list.Key))
            {
                continue;
            }
            if (node[list.Name] is not JsonArray riders)
            {
                riders = new JsonArray();
                node[list.Name] = riders;
            }
            foreach (var rider in Riders.Of(list))
            {
                riders.Add(rider!.DeepClone());
            }
        }
        return node;
    }
}
