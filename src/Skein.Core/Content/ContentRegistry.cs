using System.Text.Json;
using System.Text.Json.Nodes;
using Skein.Core.Attributes;
using Skein.Core.Definitions;
using Skein.Core.Expressions;
using Skein.Core.Tags;

namespace Skein.Core.Content;

/// <summary>
/// Everything a validator needs: the registry as bound so far (every kind is bound before any kind is
/// validated, so cross references resolve), the report, and helpers for the common checks.
/// </summary>
public sealed class ValidationContext
{
    public ContentRegistry Registry { get; }
    public ValidationReport Report { get; }
    public string Kind { get; }
    public string Id { get; }
    public string? Origin { get; }

    public ValidationContext(ContentRegistry registry, ValidationReport report, string kind, string id, string? origin)
    {
        Registry = registry;
        Report = report;
        Kind = kind;
        Id = id;
        Origin = origin;
    }

    public void Error(string message) => Report.Error(Kind, Id, message, Origin);
    public void Warn(string message) => Report.Warn(Kind, Id, message, Origin);

    /// <summary>Errors unless the referenced id exists in the kind.</summary>
    public bool Ref<T>(string? id, string field, bool required = true) where T : class, IDefinition
    {
        if (string.IsNullOrEmpty(id))
        {
            if (required)
            {
                Error($"'{field}' is required.");
            }
            return !required;
        }
        if (!Registry.Has<T>(id))
        {
            Error($"'{field}' references unknown {ContentKinds.NameOf<T>()} '{id}'.");
            return false;
        }
        return true;
    }

    public void Refs<T>(IEnumerable<string>? ids, string field) where T : class, IDefinition
    {
        if (ids is null)
        {
            return;
        }
        foreach (var id in ids)
        {
            Ref<T>(id, field);
        }
    }

    /// <summary>Warns when a modifier targets an attribute no definition declares. Channels are allowed but visible.</summary>
    public void Modifiers(IEnumerable<Modifier>? mods, string field)
    {
        if (mods is null)
        {
            return;
        }
        foreach (var m in mods)
        {
            if (!Registry.Attributes.Contains(m.Attribute))
            {
                Warn($"'{field}' modifies undeclared attribute '{m.Attribute}'.");
            }
            if (m.Form == ModifierForm.Link && string.IsNullOrEmpty(m.From))
            {
                Error($"'{field}' link modifier on '{m.Attribute}' has no 'from'.");
            }
            if (m.ValueExpr is not null)
            {
                Expression(m.ValueExpr, $"{field}.value_expr");
            }
            if (m.Condition is not null)
            {
                Expression(m.Condition, $"{field}.condition");
            }
        }
    }

    /// <summary>Warns about identifiers that are neither attributes nor well-known context names.</summary>
    public void Expression(Expr? expr, string field)
    {
        if (expr is null)
        {
            return;
        }
        foreach (var id in expr.Identifiers)
        {
            if (Registry.Attributes.Contains(id))
            {
                continue;
            }
            if (ScopeNames.IsKnown(id))
            {
                continue;
            }
            Warn($"'{field}' reads '{id}', which is not a declared attribute or a known scope name.");
        }
    }

    public void TypedSpecs(IEnumerable<TypedSpec>? specs, string field, Func<string, bool> knownType, string what)
    {
        if (specs is null)
        {
            return;
        }
        foreach (var s in specs)
        {
            if (!knownType(s.Type))
            {
                Error($"'{field}' uses unknown {what} type '{s.Type}'.");
            }
        }
    }
}

/// <summary>
/// Names expressions may read that are not attributes: entity context, mounted scopes, roll variables.
/// Extend with <see cref="Declare"/> when a system adds scope variables.
/// </summary>
public static class ScopeNames
{
    private static readonly HashSet<string> Exact = new(StringComparer.Ordinal)
    {
        "level", "life_pct", "mana_pct", "stamina_pct", "ward_pct", "distance", "score", "time", "tick",
        "stacks", "duration", "count", "depth", "quality", "raw", "amount", "value", "x", "y", "z",
        "item_level", "zone_level", "player_count", "rarity_rank", "influence", "size",
        // AI perception (see BehaviorSystem.BuildScope)
        "has_target", "target_distance", "target_life_pct", "nearest_enemy_distance", "home_distance", "state_time",
        "aggro_range", "leash_range", "preferred_range", "taunted",
        // crafting, holdings, loot, quests
        "quest_level", "holding_founts", "slot_budget", "tier", "base_roll", "accuracy", "evasion", "armor", "magnitude", "remaining",
    };

    private static readonly List<string> Prefixes = new()
    {
        "source.", "target.", "caster.", "owner.", "weave.", "item.", "zone.", "holding.", "quest.", "status.",
        "charge.", "gauge.", "input.", "roll.", "v", "ai.", "region.", "fount.", "recipe.", "calling.",
    };

    public static void Declare(string name) => Exact.Add(name);
    public static void DeclarePrefix(string prefix) => Prefixes.Add(prefix);

    public static bool IsKnown(string identifier)
    {
        if (Exact.Contains(identifier))
        {
            return true;
        }
        foreach (var p in Prefixes)
        {
            if (identifier.StartsWith(p, StringComparison.Ordinal))
            {
                if (p == "v")
                {
                    // v1, v2 ... roll variables
                    return identifier.Length > 1 && identifier.Skip(1).All(char.IsDigit);
                }
                return true;
            }
        }
        return false;
    }
}

/// <summary>
/// The bound, validated content of a game: every definition of every kind, plus the derived catalogs
/// the simulation reads constantly (attributes, rules). Immutable after load; hot reload builds a new one.
/// </summary>
public sealed class ContentRegistry
{
    private readonly Dictionary<string, Dictionary<string, IDefinition>> _byKind = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, RawDefinition>> _raw = new(StringComparer.Ordinal);

    public AttributeCatalog Attributes { get; private set; } = AttributeCatalog.Empty;
    public Rules Rules { get; private set; } = Rules.Empty;
    public IReadOnlyList<string> Layers { get; private set; } = Array.Empty<string>();

    private ContentRegistry()
    {
    }

    /// <summary>An empty registry for tests that build definitions in code.</summary>
    public static ContentRegistry Empty() => new();

    /// <summary>Loads content layers from disk. Throws in strict mode when the report has errors.</summary>
    public static ContentRegistry Load(IEnumerable<string> layerDirectories, out ValidationReport report, bool strict = false)
    {
        report = new ValidationReport();
        var dirs = layerDirectories.Select(Path.GetFullPath).ToList();
        var raw = ContentLoader.LoadRaw(dirs, report);
        var registry = FromRaw(raw, report);
        registry.Layers = dirs;
        if (strict && report.HasErrors)
        {
            throw new ContentLoadException(report);
        }
        return registry;
    }

    /// <summary>Builds a registry from already-parsed raw definitions (tests, tools, embedded content).</summary>
    public static ContentRegistry FromRaw(IReadOnlyDictionary<string, Dictionary<string, RawDefinition>> raw, ValidationReport report)
    {
        var registry = new ContentRegistry();
        foreach (var (kindName, entries) in raw)
        {
            registry._raw[kindName] = entries;
            if (!ContentKinds.TryGet(kindName, out var kind) || kind is null)
            {
                report.Warn(kindName, "", $"Unknown content kind '{kindName}'; entries kept as raw JSON only.");
                continue;
            }
            var bound = new Dictionary<string, IDefinition>(StringComparer.Ordinal);
            foreach (var (id, rawDef) in entries)
            {
                try
                {
                    foreach (var unknown in ContentJson.UnknownKeys(kind.DefinitionType, rawDef.Object))
                    {
                        if (unknown is "id" or "kind")
                        {
                            continue;
                        }
                        report.Warn(kindName, id, $"Unknown key '{unknown}' (ignored).", rawDef.Origin);
                    }
                    var def = kind.Bind(rawDef.Object);
                    if (!string.Equals(def.Id, id, StringComparison.Ordinal))
                    {
                        report.Error(kindName, id, $"Bound id '{def.Id}' does not match entry key.", rawDef.Origin);
                        continue;
                    }
                    bound[id] = def;
                }
                catch (JsonException ex)
                {
                    report.Error(kindName, id, $"Cannot bind: {ex.Message}", rawDef.Origin);
                }
                catch (Exception ex) when (ex is ExprParseException or InvalidOperationException or ArgumentException)
                {
                    report.Error(kindName, id, $"Cannot bind: {ex.Message}", rawDef.Origin);
                }
            }
            registry._byKind[kindName] = bound;
        }

        registry.Attributes = new AttributeCatalog(registry.All<AttributeDef>().Values);
        registry.Rules = Rules.FromDefinitions(registry.All<RulesDef>().Values);

        foreach (var (kindName, bound) in registry._byKind)
        {
            if (!ContentKinds.TryGet(kindName, out var kind) || kind?.Validate is null)
            {
                continue;
            }
            foreach (var (id, def) in bound)
            {
                var origin = registry._raw[kindName][id].Origin;
                var ctx = new ValidationContext(registry, report, kindName, id, origin);
                try
                {
                    kind.Validate(def, ctx);
                }
                catch (Exception ex)
                {
                    report.Error(kindName, id, $"Validator crashed: {ex.Message}", origin);
                }
            }
        }
        return registry;
    }

    /// <summary>Adds a definition in code. Intended for tests and tools; content files are the normal path.</summary>
    public ContentRegistry With<T>(T definition) where T : class, IDefinition
    {
        var name = ContentKinds.NameOf<T>();
        if (!_byKind.TryGetValue(name, out var dict))
        {
            dict = new Dictionary<string, IDefinition>(StringComparer.Ordinal);
            _byKind[name] = dict;
        }
        dict[definition.Id] = definition;
        if (definition is AttributeDef)
        {
            Attributes = new AttributeCatalog(All<AttributeDef>().Values);
        }
        if (definition is RulesDef)
        {
            Rules = Rules.FromDefinitions(All<RulesDef>().Values);
        }
        return this;
    }

    public IReadOnlyDictionary<string, T> All<T>() where T : class, IDefinition
    {
        var name = ContentKinds.NameOf<T>();
        if (!_byKind.TryGetValue(name, out var dict))
        {
            return new Dictionary<string, T>();
        }
        return dict.ToDictionary(kv => kv.Key, kv => (T)kv.Value, StringComparer.Ordinal);
    }

    public IEnumerable<T> Each<T>() where T : class, IDefinition
    {
        var name = ContentKinds.NameOf<T>();
        if (!_byKind.TryGetValue(name, out var dict))
        {
            yield break;
        }
        foreach (var v in dict.Values)
        {
            yield return (T)v;
        }
    }

    public bool TryGet<T>(string id, out T? definition) where T : class, IDefinition
    {
        var name = ContentKinds.NameOf<T>();
        if (_byKind.TryGetValue(name, out var dict) && dict.TryGetValue(id, out var d))
        {
            definition = (T)d;
            return true;
        }
        definition = null;
        return false;
    }

    public T? Get<T>(string id) where T : class, IDefinition => TryGet<T>(id, out var d) ? d : null;

    public T Require<T>(string id) where T : class, IDefinition =>
        TryGet<T>(id, out var d) && d is not null ? d : throw new KeyNotFoundException($"No {ContentKinds.NameOf<T>()} with id '{id}'.");

    public bool Has<T>(string id) where T : class, IDefinition => TryGet<T>(id, out _);

    public int Count<T>() where T : class, IDefinition => _byKind.TryGetValue(ContentKinds.NameOf<T>(), out var d) ? d.Count : 0;

    public IEnumerable<string> KindNames => _byKind.Keys;

    public int CountOfKind(string kindName) => _byKind.TryGetValue(kindName, out var d) ? d.Count : 0;

    public JsonObject? Raw(string kind, string id) => _raw.TryGetValue(kind, out var k) && k.TryGetValue(id, out var r) ? r.Object : null;

    public IEnumerable<RawDefinition> RawOfKind(string kind) => _raw.TryGetValue(kind, out var k) ? k.Values : Enumerable.Empty<RawDefinition>();
}

public sealed class ContentLoadException : Exception
{
    public ValidationReport Report { get; }

    public ContentLoadException(ValidationReport report) : base($"Content failed validation with {report.ErrorCount} error(s).\n{report}")
    {
        Report = report;
    }
}

/// <summary>
/// Game rules as named expressions and values: <c>combat.hit_chance</c>, <c>progression.xp_to_next</c>,
/// <c>items.rarity.weights</c>. Rules definitions are nested objects flattened with dots, so content can
/// group them freely and code reads them by path.
/// </summary>
public sealed class Rules
{
    public static readonly Rules Empty = new(new Dictionary<string, JsonNode?>(StringComparer.Ordinal));

    private readonly Dictionary<string, JsonNode?> _flat;
    private readonly Dictionary<string, Expr> _exprCache = new(StringComparer.Ordinal);

    private Rules(Dictionary<string, JsonNode?> flat)
    {
        _flat = flat;
    }

    public static Rules FromDefinitions(IEnumerable<RulesDef> defs)
    {
        var flat = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var def in defs)
        {
            if (def.Values is null)
            {
                continue;
            }
            Flatten(def.Id, def.Values, flat);
        }
        return new Rules(flat);
    }

    private static void Flatten(string prefix, JsonObject obj, Dictionary<string, JsonNode?> into)
    {
        foreach (var kv in obj)
        {
            var key = prefix.Length == 0 ? kv.Key : prefix + "." + kv.Key;
            if (kv.Value is JsonObject child)
            {
                into[key] = child;
                Flatten(key, child, into);
            }
            else
            {
                into[key] = kv.Value;
            }
        }
    }

    public bool Has(string path) => _flat.ContainsKey(path);

    public IEnumerable<string> Keys => _flat.Keys;

    public JsonNode? Node(string path) => _flat.TryGetValue(path, out var n) ? n : null;

    /// <summary>Reads a rule as an expression. Missing rules fall back to the supplied default expression.</summary>
    public Expr Expr(string path, string fallback)
    {
        if (_exprCache.TryGetValue(path, out var cached))
        {
            return cached;
        }
        Expr e;
        var node = Node(path);
        if (node is JsonValue v)
        {
            if (JsonNum.TryDouble(v, out var d))
            {
                e = Expressions.Expr.Constant(d);
            }
            else if (v.TryGetValue<bool>(out var b))
            {
                e = Expressions.Expr.Constant(b ? 1 : 0);
            }
            else if (v.TryGetValue<string>(out var s))
            {
                e = Expressions.Expr.Parse(s);
            }
            else
            {
                e = Expressions.Expr.Parse(fallback);
            }
        }
        else
        {
            e = Expressions.Expr.Parse(fallback);
        }
        _exprCache[path] = e;
        return e;
    }

    public double Number(string path, double fallback)
    {
        var node = Node(path);
        if (node is JsonValue v)
        {
            if (JsonNum.TryDouble(v, out var d))
            {
                return d;
            }
            if (v.TryGetValue<string>(out var s) && Expressions.Expr.TryParse(s, out var e, out _) && e!.IsConstant)
            {
                return e.ConstantValue;
            }
        }
        return fallback;
    }

    public double Eval(string path, string fallback, IExprScope scope) => Expr(path, fallback).Evaluate(scope);

    public string Text(string path, string fallback) => Node(path) is JsonValue v && v.TryGetValue<string>(out var s) ? s : fallback;

    public bool Flag(string path, bool fallback) => Node(path) is JsonValue v && v.TryGetValue<bool>(out var b) ? b : fallback;

    public IReadOnlyList<string> Strings(string path)
    {
        if (Node(path) is JsonArray arr)
        {
            return arr.Select(n => n?.ToString() ?? "").ToList();
        }
        return Array.Empty<string>();
    }

    public T? Bind<T>(string path) where T : class
    {
        var node = Node(path);
        return node is null ? null : node.Deserialize<T>(ContentJson.Options);
    }

    public TagSet Tags(string path) => TagSet.From(Strings(path));
}
