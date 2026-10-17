using System.Globalization;
using System.Text.Json.Nodes;

namespace HollowWake.Client.UI.Logic;

/// <summary>A named content entry the interface labels things with.</summary>
public sealed record NamedEntry(string Id, string Name, string Description, int Order);

/// <summary>How the sheet labels and formats an attribute: from its definition, never its value.</summary>
public sealed record AttributeInfo(string Id, string Name, string Category, string Description, bool Percent, int? Decimals, bool Hidden, int Index);

/// <summary>A passive node as the constellation draws it: place, links, kind, the lines of its modifiers and its choice group.</summary>
public sealed record PassiveNode(string Id, string Name, string Kind, double X, double Y, IReadOnlyList<string> Links, string Realm, int Cost, string? Calling, IReadOnlyList<string> Lines)
{
    /// <summary>The choice group a choice node deals its options from, or null for an ordinary node.</summary>
    public string? Choice { get; init; }
}

/// <summary>An option a choice node deals.</summary>
public sealed record ChoiceOption(string Id, string Name, IReadOnlyList<string> Lines);

/// <summary>A choice group: the options its nodes deal and how many a node picks.</summary>
public sealed record ChoiceGroup(string Id, string Name, int Pick, IReadOnlyList<ChoiceOption> Options);

/// <summary>
/// How a status is presented, read from its content (never from the simulation): its <c>meta</c> block's
/// <c>hidden</c> keeps it out of the status rows (the pool states the orbs already draw), <c>icon</c> names its
/// glyph in the icon set, <c>color</c> tints it (the status's own <c>color</c> stands in), <c>kind</c> (buff,
/// debuff or neutral) frames it (by default a buff when the status is <c>beneficial</c>, else a debuff), and
/// <c>cue</c> (pulse or none, by default none) says how it draws the eye.
/// </summary>
public sealed record StatusLook(bool Hidden, string? Icon, Rgba? Color, string Kind, string? Cue)
{
    public static readonly StatusLook Plain = new(false, null, null, "neutral", null);

    public static readonly IReadOnlyList<string> Kinds = new[] { "buff", "debuff", "neutral" };
    public static readonly IReadOnlyList<string> Cues = new[] { "pulse", "none" };

    public static StatusLook From(JsonObject status)
    {
        var meta = status["meta"] as JsonObject;
        var color = Rgba.TryParse(Json.StrOrNull(meta?["color"]), out var c) ? c : Rgba.TryParse(Json.StrOrNull(status["color"]), out var own) ? own : (Rgba?)null;
        return new StatusLook(
            Json.Bool(meta?["hidden"]),
            Json.StrOrNull(meta?["icon"]),
            color,
            Json.StrOrNull(meta?["kind"]) ?? (Json.Bool(status["beneficial"]) ? "buff" : "debuff"),
            Json.StrOrNull(meta?["cue"]));
    }
}

/// <summary>
/// The names, descriptions and shapes the interface reads from the rest of the content: statuses, Threads,
/// currencies, equipment slots, attributes and the passive constellation. Labels only; every value the
/// interface shows comes from the server.
/// </summary>
public sealed class ContentNames
{
    public Dictionary<string, NamedEntry> Statuses { get; init; } = new(StringComparer.Ordinal);
    public List<NamedEntry> Threads { get; init; } = new();
    public Dictionary<string, NamedEntry> Currencies { get; init; } = new(StringComparer.Ordinal);
    public List<NamedEntry> Slots { get; init; } = new();
    public Dictionary<string, NamedEntry> Resources { get; init; } = new(StringComparer.Ordinal);
    public List<AttributeInfo> Attributes { get; init; } = new();
    public Dictionary<string, AttributeInfo> AttributeById { get; init; } = new(StringComparer.Ordinal);
    public List<PassiveNode> Passives { get; init; } = new();

    /// <summary>The Thread whose ailment a status is, so the status wears the Thread's colour.</summary>
    public Dictionary<string, string> ThreadOfStatus { get; init; } = new(StringComparer.Ordinal);

    /// <summary>How each status is presented, from its content's meta block.</summary>
    public Dictionary<string, StatusLook> StatusLooks { get; init; } = new(StringComparer.Ordinal);

    public StatusLook LookOf(string status) => StatusLooks.TryGetValue(status, out var look) ? look : StatusLook.Plain;

    /// <summary>Choice groups by id (passive_choices content), for the choice popup.</summary>
    public Dictionary<string, ChoiceGroup> Choices { get; init; } = new(StringComparer.Ordinal);

    /// <summary>Realm names by id (passive_realms content), for the realm tabs.</summary>
    public Dictionary<string, string> Realms { get; init; } = new(StringComparer.Ordinal);

    public string RealmName(string id) => Realms.TryGetValue(id, out var n) ? n : Humanize(id);

    public static readonly ContentNames Empty = new();

    public string StatusName(string id) => Statuses.TryGetValue(id, out var e) ? e.Name : Humanize(id);
    public string ThreadName(string id) => Threads.FirstOrDefault(t => t.Id == id)?.Name ?? Humanize(id);
    public string CurrencyName(string id) => Currencies.TryGetValue(id, out var e) ? e.Name : Humanize(id);
    public string SlotName(string id) => Slots.FirstOrDefault(s => s.Id == id)?.Name ?? Humanize(id);
    public string ResourceName(string id) => Resources.TryGetValue(id, out var e) ? e.Name : Humanize(id);
    public string AttributeName(string id) => AttributeById.TryGetValue(id, out var a) ? a.Name : Humanize(id);

    /// <summary>A content id as words: <c>main_hand</c> reads Main Hand, <c>resist.ember</c> reads Ember.</summary>
    public static string Humanize(string id)
    {
        var last = id.Contains('.') ? id[(id.LastIndexOf('.') + 1)..] : id;
        var words = last.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words.Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
    }

    public static ContentNames Load(Func<string, IEnumerable<JsonObject>> rawOfKind)
    {
        static NamedEntry Named(JsonObject o, int index) => new(
            Json.Str(o["id"]),
            Json.Str(o["name"], Humanize(Json.Str(o["id"]))),
            Json.Str(o["description"]),
            o["order"] is null ? index : (int)Json.Num(o["order"]));

        var attributes = new List<AttributeInfo>();
        var index = 0;
        foreach (var a in rawOfKind("attributes"))
        {
            var id = Json.Str(a["id"]);
            attributes.Add(new AttributeInfo(
                id,
                Json.Str(a["name"], Humanize(id)),
                Json.Str(a["category"], "misc"),
                Json.Str(a["description"]),
                Json.Bool(a["percent"]),
                a["decimals"] is null ? null : (int)Json.Num(a["decimals"]),
                Json.Bool(a["hidden"]),
                index++));
        }
        var byId = new Dictionary<string, AttributeInfo>(StringComparer.Ordinal);
        foreach (var a in attributes)
        {
            byId[a.Id] = a;
        }
        var names = new ContentNames
        {
            Statuses = rawOfKind("statuses").Select(Named).ToDictionary(e => e.Id, StringComparer.Ordinal),
            StatusLooks = rawOfKind("statuses").ToDictionary(s => Json.Str(s["id"]), StatusLook.From, StringComparer.Ordinal),
            Threads = rawOfKind("threads").Select(Named).ToList(),
            Currencies = rawOfKind("currencies").Select(Named).ToDictionary(e => e.Id, StringComparer.Ordinal),
            Slots = rawOfKind("slots").Select(Named).OrderBy(s => s.Order).ToList(),
            Resources = rawOfKind("resources").Select(Named).ToDictionary(e => e.Id, StringComparer.Ordinal),
            ThreadOfStatus = rawOfKind("threads")
                .Where(t => Json.Str(t["status"]).Length > 0)
                .GroupBy(t => Json.Str(t["status"]))
                .ToDictionary(g => g.Key, g => Json.Str(g.First()["id"]), StringComparer.Ordinal),
            Attributes = attributes,
            AttributeById = byId,
        };
        foreach (var p in rawOfKind("passives"))
        {
            var lines = Json.Items(p["modifiers"]).Select(m => ModifierText.Line(m, names)).Where(l => l.Length > 0).ToList();
            var choice = p["choice"] is JsonObject c ? Json.StrOrNull(c["group"]) : Json.StrOrNull(p["choice"]);
            names.Passives.Add(new PassiveNode(
                Json.Str(p["id"]),
                Json.Str(p["name"], Humanize(Json.Str(p["id"]))),
                Json.Str(p["kind"], "small"),
                Json.Num(p["x"]),
                Json.Num(p["y"]),
                Json.Strings(p["links"]),
                Json.Str(p["realm"], "star"),
                (int)Json.Num(p["cost"], 1),
                Json.StrOrNull(p["calling"]),
                lines)
            { Choice = string.IsNullOrEmpty(choice) ? null : choice });
        }
        foreach (var g in rawOfKind("passive_choices"))
        {
            var options = Json.Items(g["options"]).Select(o => new ChoiceOption(
                Json.Str(o["id"]),
                Json.Str(o["name"], Humanize(Json.Str(o["id"]))),
                Json.Items(o["modifiers"]).Select(m => ModifierText.Line(m, names)).Where(l => l.Length > 0).ToList())).ToList();
            var id = Json.Str(g["id"]);
            names.Choices[id] = new ChoiceGroup(id, Json.Str(g["name"], Humanize(id)), (int)Json.Num(g["pick"], 1), options);
        }
        foreach (var r in rawOfKind("passive_realms"))
        {
            var id = Json.Str(r["id"]);
            names.Realms[id] = Json.Str(r["name"], Humanize(id));
        }
        return names;
    }
}

/// <summary>Writes a content modifier as a line a player reads: "+10% increased Armor (Ember)". Formatting only.</summary>
public static class ModifierText
{
    public static string Line(JsonObject m, ContentNames names)
    {
        var attribute = Json.Str(m["attribute"]);
        if (attribute.Length == 0)
        {
            return "";
        }
        var form = Json.Str(m["form"], "flat");
        var info = names.AttributeById.GetValueOrDefault(attribute);
        var name = info?.Name ?? ContentNames.Humanize(attribute);
        var valueNode = m["value"];
        var isNumber = valueNode is JsonValue v && (v.TryGetValue<double>(out _) || v.TryGetValue<long>(out _) || v.TryGetValue<int>(out _));
        var value = Json.Num(valueNode);
        var text = form switch
        {
            "flat" when !isNumber => $"{name} + {Json.Str(valueNode)}",
            "flat" when info?.Percent == true => $"{Signed(value * 100)}% {name}",
            "flat" => $"{Signed(value)} {name}",
            "increased" when !isNumber => $"{name} increased by {Json.Str(valueNode)}",
            "increased" => value >= 0 ? $"{Number(value * 100)}% increased {name}" : $"{Number(-value * 100)}% reduced {name}",
            "more" when !isNumber => $"{name} more by {Json.Str(valueNode)}",
            "more" => value >= 0 ? $"{Number(value * 100)}% more {name}" : $"{Number(-value * 100)}% less {name}",
            "override" => $"{name} is {(info?.Percent == true ? Number(value * 100) + "%" : Number(value))}",
            "link" => $"{name} from {ContentNames.Humanize(Json.Str(m["source"]))}",
            _ => $"{name} {form} {Json.Str(valueNode)}",
        };
        var scope = Json.Strings(m["scope"]);
        if (scope.Count > 0)
        {
            text += $" ({string.Join(", ", scope.Select(ContentNames.Humanize))})";
        }
        var condition = Json.StrOrNull(m["condition"]);
        if (!string.IsNullOrEmpty(condition))
        {
            text += $" when {Readable(condition)}";
        }
        return text;
    }

    private static string Readable(string condition)
    {
        // has("stationary") reads "stationary"; anything else is shown as written.
        const string prefix = "has(\"";
        if (condition.StartsWith(prefix, StringComparison.Ordinal) && condition.EndsWith("\")", StringComparison.Ordinal))
        {
            return condition[prefix.Length..^2].Replace('_', ' ');
        }
        return condition;
    }

    public static string Number(double v) => Math.Round(v, 2).ToString("0.##", CultureInfo.InvariantCulture);

    public static string Signed(double v) => (v >= 0 ? "+" : "") + Number(v);
}
