using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace HollowWake.Client.UI.Logic;

/// <summary>One option of a choice entry.</summary>
public sealed record OptionChoice(string Id, string Name);

/// <summary>An option entry as the Options pane draws it (docs/design/options.md).</summary>
public sealed record OptionEntry(
    string Id,
    string Name,
    string Description,
    string Kind,
    JsonNode? Default,
    string Scope,
    string Applies,
    IReadOnlyList<OptionChoice> Choices,
    double Min,
    double Max,
    double Step,
    string Unit,
    string? Action,
    int MaxLength,
    string? Gate,
    string? Requires,
    string RequiresReason,
    string PageId,
    string GroupId);

public sealed record OptionGroup(string Id, string Name, string Description, IReadOnlyList<OptionEntry> Entries);

public sealed record OptionPage(string Id, string Name, string Description, int Order, string Icon, string? Gate, IReadOnlyList<OptionGroup> Groups);

/// <summary>An input action and its default bindings (input_actions content).</summary>
public sealed record InputAction(string Id, string Name, string Group, string Context, int Order, IReadOnlyList<string> Keys, IReadOnlyList<string> Pad, bool Held);

/// <summary>An action's bindings: a primary and a secondary key and a controller input; empty means unbound.</summary>
public readonly record struct Binding(string Primary, string Secondary, string Pad)
{
    public IEnumerable<string> Keys
    {
        get
        {
            if (Primary.Length > 0)
            {
                yield return Primary;
            }
            if (Secondary.Length > 0)
            {
                yield return Secondary;
            }
        }
    }

    public string Slot(int index) => index switch { 0 => Primary, 1 => Secondary, _ => Pad };

    public Binding With(int index, string value) => index switch
    {
        0 => this with { Primary = value },
        1 => this with { Secondary = value },
        _ => this with { Pad = value },
    };

    public JsonArray ToJson() => new(Primary, Secondary, Pad);

    public static Binding Of(InputAction action) => new(action.Keys.ElementAtOrDefault(0) ?? "", action.Keys.ElementAtOrDefault(1) ?? "", action.Pad.ElementAtOrDefault(0) ?? "");
}

/// <summary>
/// Every option and input action the content declares, read from raw entries: pages in order, their groups
/// with entries (binding groups expanded into one key entry per action of the input action group), choices
/// drawn from other kinds resolved to ids and names. The validators in InterfaceKinds.cs keep the content sound.
/// </summary>
public sealed class OptionsCatalog
{
    public IReadOnlyList<OptionPage> Pages { get; init; } = Array.Empty<OptionPage>();
    public IReadOnlyList<InputAction> Actions { get; init; } = Array.Empty<InputAction>();
    public IReadOnlyDictionary<string, OptionEntry> Entries { get; init; } = new Dictionary<string, OptionEntry>();
    public IReadOnlyDictionary<string, InputAction> ActionById { get; init; } = new Dictionary<string, InputAction>();

    public OptionEntry? Entry(string id) => Entries.GetValueOrDefault(id);

    public static string KeyEntryId(string action) => "key." + action;

    public static OptionsCatalog Load(Func<string, IEnumerable<JsonObject>> rawOfKind)
    {
        var actions = rawOfKind("input_actions").Select(a => new InputAction(
                Json.Str(a["id"]),
                Json.Str(a["name"], ContentNames.Humanize(Json.Str(a["id"]))),
                Json.Str(a["group"], "general"),
                Json.Str(a["context"], "world"),
                (int)Json.Num(a["order"]),
                Json.Strings(a["keys"]),
                Json.Strings(a["pad"]),
                Json.Bool(a["held"])))
            .OrderBy(a => a.Order).ThenBy(a => a.Id, StringComparer.Ordinal).ToList();
        List<OptionChoice> ChoicesFrom(string kind) =>
            rawOfKind(kind).Select(o => new OptionChoice(Json.Str(o["id"]), Json.Str(o["name"], ContentNames.Humanize(Json.Str(o["id"]))))).OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
        var pages = new List<OptionPage>();
        var entries = new Dictionary<string, OptionEntry>(StringComparer.Ordinal);
        foreach (var p in rawOfKind("ui_options"))
        {
            var pageId = Json.Str(p["id"]);
            var groups = new List<OptionGroup>();
            foreach (var g in Json.Items(p["groups"]))
            {
                var groupId = Json.Str(g["id"]);
                var list = new List<OptionEntry>();
                foreach (var e in Json.Items(g["entries"]))
                {
                    var choices = Json.Items(e["choices"]).Select(c => new OptionChoice(Json.Str(c["id"]), Json.Str(c["name"], ContentNames.Humanize(Json.Str(c["id"]))))).ToList();
                    if (Json.StrOrNull(e["choices_from"]) is { } kind)
                    {
                        choices.AddRange(ChoicesFrom(kind));
                    }
                    list.Add(new OptionEntry(
                        Json.Str(e["id"]),
                        Json.Str(e["name"], ContentNames.Humanize(Json.Str(e["id"]))),
                        Json.Str(e["description"]),
                        Json.Str(e["kind"], "toggle"),
                        e["default"]?.DeepClone(),
                        Json.Str(e["scope"], "client"),
                        Json.Str(e["applies"], "live"),
                        choices,
                        Json.Num(e["min"]),
                        Json.Num(e["max"], 1),
                        Json.Num(e["step"], 0.1),
                        Json.Str(e["unit"]),
                        Json.StrOrNull(e["action"]),
                        (int)Json.Num(e["max_length"], 120),
                        Json.StrOrNull(e["gate"]),
                        Json.StrOrNull(e["requires"]),
                        Json.Str(e["requires_reason"]),
                        pageId,
                        groupId));
                }
                if (Json.StrOrNull(g["bindings"]) is { } bindings)
                {
                    foreach (var a in actions.Where(a => a.Group == bindings))
                    {
                        list.Add(new OptionEntry(KeyEntryId(a.Id), a.Name, "", "key", Binding.Of(a).ToJson(), "client", "live",
                            Array.Empty<OptionChoice>(), 0, 1, 1, "", a.Id, 0, null, null, "", pageId, groupId));
                    }
                }
                foreach (var entry in list)
                {
                    entries[entry.Id] = entry;
                }
                groups.Add(new OptionGroup(groupId, Json.Str(g["name"], ContentNames.Humanize(groupId)), Json.Str(g["description"]), list));
            }
            pages.Add(new OptionPage(pageId, Json.Str(p["name"], ContentNames.Humanize(pageId)), Json.Str(p["description"]), (int)Json.Num(p["order"]), Json.Str(p["icon"]), Json.StrOrNull(p["gate"]), groups));
        }
        return new OptionsCatalog
        {
            Pages = pages.OrderBy(p => p.Order).ThenBy(p => p.Id, StringComparer.Ordinal).ToList(),
            Actions = actions,
            Entries = entries,
            ActionById = actions.ToDictionary(a => a.Id, StringComparer.Ordinal),
        };
    }
}

/// <summary>
/// The player's settings for one profile: a value per option entry, defaults from the content for anything
/// missing or malformed, written as JSON. Every change raises <see cref="Changed"/>; the clients apply what
/// they understand (registries of appliers by entry id) and the rest is kept for later systems. Account and
/// character scoped entries live here too until the account store carries them.
/// </summary>
public sealed class Settings
{
    private readonly Dictionary<string, JsonNode?> _values = new(StringComparer.Ordinal);

    public OptionsCatalog Catalog { get; }

    /// <summary>Raised with the entry whose value changed.</summary>
    public event Action<OptionEntry>? Changed;

    public Settings(OptionsCatalog catalog)
    {
        Catalog = catalog;
    }

    /// <summary>The value of an entry, or its default.</summary>
    public JsonNode? Value(string id) => _values.TryGetValue(id, out var v) ? v : Catalog.Entry(id)?.Default;

    public bool Toggle(string id, bool fallback = false) => Value(id) is JsonValue v && v.TryGetValue<bool>(out var b) ? b : fallback;

    public double Number(string id, double fallback = 0) => Value(id) is { } n ? Json.Num(n, fallback) : fallback;

    public string Text(string id, string fallback = "") => Value(id) is JsonValue v && v.TryGetValue<string>(out var s) ? s : fallback;

    /// <summary>An action's effective bindings: the player's rebinds over the content's defaults.</summary>
    public Binding Bindings(string action)
    {
        var defaults = Catalog.ActionById.TryGetValue(action, out var a) ? Binding.Of(a) : new Binding("", "", "");
        if (_values.TryGetValue(OptionsCatalog.KeyEntryId(action), out var node) && node is JsonArray arr)
        {
            return new Binding(Json.Str(arr.ElementAtOrDefault(0)), Json.Str(arr.ElementAtOrDefault(1)), Json.Str(arr.ElementAtOrDefault(2)));
        }
        return defaults;
    }

    public bool IsDefault(string id) => !_values.ContainsKey(id) || JsonNode.DeepEquals(_values[id], Catalog.Entry(id)?.Default);

    /// <summary>Sets a value when it is valid for the entry's kind; returns whether it changed.</summary>
    public bool Set(string id, JsonNode? value)
    {
        if (Catalog.Entry(id) is not { } entry || !Valid(entry, value))
        {
            return false;
        }
        if (JsonNode.DeepEquals(Value(id), value))
        {
            return false;
        }
        if (JsonNode.DeepEquals(value, entry.Default))
        {
            _values.Remove(id);
        }
        else
        {
            _values[id] = value?.DeepClone();
        }
        Changed?.Invoke(entry);
        return true;
    }

    public bool SetBinding(string action, Binding binding) => Set(OptionsCatalog.KeyEntryId(action), binding.ToJson());

    public void Reset(string id) => Set(id, Catalog.Entry(id)?.Default?.DeepClone());

    public void ResetGroup(string pageId, string groupId)
    {
        foreach (var e in Catalog.Entries.Values.Where(e => e.PageId == pageId && e.GroupId == groupId).ToList())
        {
            Reset(e.Id);
        }
    }

    public void ResetPage(string pageId)
    {
        foreach (var e in Catalog.Entries.Values.Where(e => e.PageId == pageId).ToList())
        {
            Reset(e.Id);
        }
    }

    /// <summary>Whether a value fits the entry: the kind's type, a choice among its choices, a number in range, a colour.</summary>
    public static bool Valid(OptionEntry entry, JsonNode? value)
    {
        switch (entry.Kind)
        {
            case "toggle":
                return value is JsonValue t && t.TryGetValue<bool>(out _);
            case "slider":
                if (value is not JsonValue n || !IsNumber(n))
                {
                    return false;
                }
                var d = Json.Num(n);
                return d >= entry.Min - 1e-9 && d <= entry.Max + 1e-9;
            case "choice":
                return value is JsonValue c && c.TryGetValue<string>(out var id) && entry.Choices.Any(x => x.Id == id);
            case "key":
                return value is JsonArray a && a.Count == 3 && a.All(x => x is JsonValue v && v.TryGetValue<string>(out _));
            case "color":
                return value is JsonValue col && col.TryGetValue<string>(out var s) && Rgba.TryParse(s, out _);
            case "text":
                return value is JsonValue txt && txt.TryGetValue<string>(out var str) && str.Length <= entry.MaxLength;
            default:
                return false;
        }
    }

    private static bool IsNumber(JsonValue v) => v.TryGetValue<double>(out _) || v.TryGetValue<long>(out _) || v.TryGetValue<int>(out _) || v.TryGetValue<float>(out _);

    /// <summary>
    /// The actions sharing a context whose bindings include a key (other than the asking action): keys against
    /// the keyboard slots, a controller input (<paramref name="pad"/>) against the controller slot.
    /// </summary>
    public IEnumerable<InputAction> Conflicts(string action, string key, bool pad = false)
    {
        if (!Catalog.ActionById.TryGetValue(action, out var mine) || key.Length == 0)
        {
            yield break;
        }
        var wanted = KeyName.Canonical(key);
        foreach (var other in Catalog.Actions)
        {
            if (other.Id == action || other.Context != mine.Context)
            {
                continue;
            }
            var b = Bindings(other.Id);
            if (pad ? b.Pad == key : b.Keys.Any(k => KeyName.Canonical(k) == wanted))
            {
                yield return other;
            }
        }
    }

    /// <summary>Binds a key to an action's slot; with <paramref name="swap"/>, a conflicting action takes the key this slot held.</summary>
    public void Rebind(string action, int slot, string key, bool swap)
    {
        var pad = slot == 2;
        var mine = Bindings(action);
        var previous = mine.Slot(slot);
        foreach (var other in Conflicts(action, key, pad).ToList())
        {
            var b = Bindings(other.Id);
            for (var i = pad ? 2 : 0; i < (pad ? 3 : 2); i++)
            {
                var held = b.Slot(i);
                if (held.Length > 0 && (pad ? held == key : KeyName.Canonical(held) == KeyName.Canonical(key)))
                {
                    b = b.With(i, swap ? previous : "");
                }
            }
            SetBinding(other.Id, b);
        }
        SetBinding(action, mine.With(slot, key));
    }

    // ------------------------------------------------------------ the file

    public string Serialize()
    {
        var obj = new JsonObject();
        foreach (var (id, value) in _values.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            obj[id] = value?.DeepClone();
        }
        return new JsonObject { ["version"] = 1, ["values"] = obj }.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Reads a settings file; anything unknown or malformed takes the default.</summary>
    public static Settings Load(OptionsCatalog catalog, string? json)
    {
        var settings = new Settings(catalog);
        if (string.IsNullOrWhiteSpace(json))
        {
            return settings;
        }
        try
        {
            if (JsonNode.Parse(json) is JsonObject root && root["values"] is JsonObject values)
            {
                foreach (var (id, value) in values)
                {
                    if (catalog.Entry(id) is { } entry && Valid(entry, value))
                    {
                        settings._values[id] = value?.DeepClone();
                    }
                }
            }
        }
        catch (JsonException)
        {
            // A broken file is no settings at all: defaults.
        }
        return settings;
    }
}

/// <summary>
/// Evaluates the small expressions options gate and require with: numbers, identifiers with dots
/// (<c>client.web</c>, <c>option.shadows</c>), <c>has("flag")</c>, <c>! &amp;&amp; ||</c>, comparisons,
/// arithmetic, parentheses and <c>c ? a : b</c>; the same grammar the content's expressions use, so the
/// validator parses them too. An unknown identifier reads zero.
/// </summary>
public sealed class GateExpr
{
    private readonly string _text;
    private readonly Func<string, double?> _identifier;
    private readonly Func<string, bool> _has;
    private int _i;

    private GateExpr(string text, Func<string, double?> identifier, Func<string, bool> has)
    {
        _text = text;
        _identifier = identifier;
        _has = has;
    }

    /// <summary>The expression's value; a blank expression is true (one).</summary>
    public static double Evaluate(string? text, Func<string, double?> identifier, Func<string, bool>? has = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 1;
        }
        var e = new GateExpr(text, identifier, has ?? (_ => false));
        var v = e.Ternary();
        e.Ws();
        if (e._i < text.Length)
        {
            throw new FormatException($"unexpected '{text[e._i..]}' in '{text}'");
        }
        return v;
    }

    public static bool Holds(string? text, Func<string, double?> identifier, Func<string, bool>? has = null)
    {
        try
        {
            return Evaluate(text, identifier, has) != 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private void Ws()
    {
        while (_i < _text.Length && char.IsWhiteSpace(_text[_i]))
        {
            _i++;
        }
    }

    private bool Eat(string token)
    {
        Ws();
        if (string.CompareOrdinal(_text, _i, token, 0, token.Length) == 0)
        {
            _i += token.Length;
            return true;
        }
        return false;
    }

    private double Ternary()
    {
        var c = Or();
        if (Eat("?"))
        {
            var a = Ternary();
            if (!Eat(":"))
            {
                throw new FormatException("expected ':'");
            }
            var b = Ternary();
            return c != 0 ? a : b;
        }
        return c;
    }

    private double Or()
    {
        var v = And();
        while (Eat("||"))
        {
            var r = And();
            v = v != 0 || r != 0 ? 1 : 0;
        }
        return v;
    }

    private double And()
    {
        var v = Compare();
        while (Eat("&&"))
        {
            var r = Compare();
            v = v != 0 && r != 0 ? 1 : 0;
        }
        return v;
    }

    private double Compare()
    {
        var v = Sum();
        while (true)
        {
            if (Eat("=="))
            {
                v = Math.Abs(v - Sum()) < 1e-9 ? 1 : 0;
            }
            else if (Eat("!="))
            {
                v = Math.Abs(v - Sum()) >= 1e-9 ? 1 : 0;
            }
            else if (Eat("<="))
            {
                v = v <= Sum() ? 1 : 0;
            }
            else if (Eat(">="))
            {
                v = v >= Sum() ? 1 : 0;
            }
            else if (Eat("<"))
            {
                v = v < Sum() ? 1 : 0;
            }
            else if (Eat(">"))
            {
                v = v > Sum() ? 1 : 0;
            }
            else
            {
                return v;
            }
        }
    }

    private double Sum()
    {
        var v = Product();
        while (true)
        {
            if (Eat("+"))
            {
                v += Product();
            }
            else if (Eat("-"))
            {
                v -= Product();
            }
            else
            {
                return v;
            }
        }
    }

    private double Product()
    {
        var v = Unary();
        while (true)
        {
            if (Eat("*"))
            {
                v *= Unary();
            }
            else if (Eat("/"))
            {
                var d = Unary();
                v = d == 0 ? 0 : v / d;
            }
            else
            {
                return v;
            }
        }
    }

    private double Unary()
    {
        if (Eat("!"))
        {
            return Unary() == 0 ? 1 : 0;
        }
        if (Eat("-"))
        {
            return -Unary();
        }
        return Atom();
    }

    private double Atom()
    {
        Ws();
        if (Eat("("))
        {
            var v = Ternary();
            if (!Eat(")"))
            {
                throw new FormatException("expected ')'");
            }
            return v;
        }
        if (_i < _text.Length && (char.IsDigit(_text[_i]) || _text[_i] == '.'))
        {
            var start = _i;
            while (_i < _text.Length && (char.IsDigit(_text[_i]) || _text[_i] == '.'))
            {
                _i++;
            }
            var number = double.Parse(_text[start.._i], CultureInfo.InvariantCulture);
            if (_i < _text.Length && _text[_i] == '%')
            {
                _i++;
                number /= 100;
            }
            return number;
        }
        if (_i < _text.Length && (char.IsLetter(_text[_i]) || _text[_i] == '_'))
        {
            var start = _i;
            while (_i < _text.Length && (char.IsLetterOrDigit(_text[_i]) || _text[_i] is '_' or '.'))
            {
                _i++;
            }
            var name = _text[start.._i];
            if (name == "has" && Eat("("))
            {
                Ws();
                if (_i >= _text.Length || _text[_i] != '"')
                {
                    throw new FormatException("has() takes a quoted flag");
                }
                var end = _text.IndexOf('"', _i + 1);
                if (end < 0)
                {
                    throw new FormatException("unclosed string");
                }
                var flag = _text[(_i + 1)..end];
                _i = end + 1;
                if (!Eat(")"))
                {
                    throw new FormatException("expected ')'");
                }
                return _has(flag) ? 1 : 0;
            }
            return name switch
            {
                "true" => 1,
                "false" => 0,
                _ => _identifier(name) ?? 0,
            };
        }
        throw new FormatException($"unexpected '{(_i < _text.Length ? _text[_i..] : "end")}'");
    }
}
