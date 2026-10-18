using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using HollowWake.Core.Definitions;
using HollowWake.Core.Effects;
using HollowWake.Core.Expressions;

namespace HollowWake.Core.Content;

/// <summary>
/// One thing the composition lint noticed: the check that found it, <c>smell</c> or <c>note</c>, what it concerns and
/// one sentence. A content finding names a kind and an id with the file the definition came from; an effect
/// parameter is kind <c>effect</c> with id <c>type.parameter</c>, a copied formula kind <c>expression</c> with the
/// formula as its id. A code finding names a file under the source root and a line.
/// </summary>
public sealed record CompositionFinding(string Check, string Severity, string? Kind, string? Id, string? File, int? Line, string Message)
{
    /// <summary>Where the finding points: <c>file:line</c> for code, <c>kind/id</c> for content, as validation names definitions.</summary>
    public string Subject => Line is { } line ? $"{File}:{line}" : $"{Kind}/{Id}";

    /// <summary>The order within a check, set by the lint.</summary>
    internal string Order { get; init; } = "";
}

/// <summary>
/// One parameter a definition writes, as the composition lint's walk found it: the definition, the effect type and
/// parameter, and the path of the node (a patch's for a <c>set</c>, <c>hooks.name</c> for a glyph's hook list).
/// </summary>
public sealed record CompositionWrite(string Kind, string Id, string Type, string Parameter, string Path);

/// <summary>The findings of one check, in report order.</summary>
public sealed record CompositionGroup(string Check, IReadOnlyList<CompositionFinding> Findings)
{
    public int Smells => Findings.Count(f => f.Severity == CompositionLint.Smell);
    public int Notes => Findings.Count(f => f.Severity == CompositionLint.Note);
}

/// <summary>
/// The composition lint (<c>hollowwake lint</c>). The combat grammar makes every mechanic a value on an axis or a node
/// in a graph; this names what breaks it: forms one preset uses, effect parameters no second definition writes,
/// numbers frozen into graphs where a weave attribute would carry them, formulas copied across graphs instead of named
/// once, switches on strings in the systems, and runtime reads through a weave's form where the compiled weave should
/// answer. It reads loaded content and, given a source root, the C# under it.
/// It reports and never fails validation; docs/design/content-and-expressions.md says what each check means.
/// Deterministic: findings come in check order, smells before notes, then by what they concern (copied formulas by
/// how many definitions write them, most first).
/// </summary>
public sealed class CompositionLint
{
    public const string Smell = "smell";
    public const string Note = "note";

    public const string FormOneUse = "form.one_use";
    public const string FormUnused = "form.unused";
    public const string ParameterSingleUse = "parameter.single_use";
    public const string ParameterUnused = "parameter.unused";
    public const string AxisFrozen = "axis.frozen";
    public const string ExpressionCopied = "expression.copied";
    public const string CodeStringSwitch = "code.string_switch";
    public const string CodeFormRead = "code.form_read";

    /// <summary>Every check, in report order.</summary>
    public static readonly IReadOnlyList<string> Checks = new[]
    {
        FormOneUse, FormUnused, ParameterSingleUse, ParameterUnused, AxisFrozen, ExpressionCopied, CodeStringSwitch, CodeFormRead,
    };

    /// <summary>
    /// The source directories that hold systems, where a switch on strings or a compared content name is a smell.
    /// A path matches when one of these appears in it as whole directory names.
    /// </summary>
    public static readonly IReadOnlyList<string> SystemDirectories = new[]
    {
        "Systems/", "Effects/Handlers/", "Combat/", "Ai/", "StatusEffects/", "Weaves/",
    };

    /// <summary>The source directories of the cast runtime, where a read through a weave's form is a <c>code.form_read</c>.</summary>
    public static readonly IReadOnlyList<string> FormReadDirectories = new[]
    {
        "Weaves/", "Systems/", "Combat/",
    };

    private readonly ContentRegistry _content;
    private readonly string? _sourceRoot;
    private readonly EffectRegistry _effects;

    /// <summary>
    /// A lint over <paramref name="content"/>; the code scan runs over the C# under <paramref name="sourceRoot"/> when
    /// one is given. Effect types, parameters and hooks come from <paramref name="effects"/>, the shared registry
    /// validation reads by default.
    /// </summary>
    public CompositionLint(ContentRegistry content, string? sourceRoot = null, EffectRegistry? effects = null)
    {
        _content = content;
        _sourceRoot = sourceRoot;
        _effects = effects ?? EffectRegistry.Shared;
    }

    /// <summary>The fewest distinct definitions whose graphs must write one formula for <c>expression.copied</c> to name it.</summary>
    public int CopiedThreshold { get; init; } = 5;

    public IReadOnlyList<CompositionFinding> Run()
    {
        var findings = new List<CompositionFinding>();
        Forms(findings);
        var graph = Walk();
        Parameters(graph, findings);
        Axes(graph, findings);
        Copies(graph, findings);
        if (_sourceRoot is not null)
        {
            Code(_sourceRoot, findings);
        }
        return findings
            .OrderBy(f => IndexOfCheck(f.Check))
            .ThenBy(f => f.Severity == Smell ? 0 : 1)
            .ThenBy(f => f.Order, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Every parameter write the walk finds in the loaded content, in walk order.</summary>
    public IReadOnlyList<CompositionWrite> Writes() =>
        Walk().Writes.Select(w => new CompositionWrite(w.Owner.Kind, w.Owner.Id, w.Type, w.Parameter, w.Path)).ToList();

    /// <summary>Findings grouped by check: one group for every check in <see cref="Checks"/> order, empty ones included.</summary>
    public static IReadOnlyList<CompositionGroup> ByCheck(IEnumerable<CompositionFinding> findings)
    {
        var list = findings.ToList();
        var checks = Checks.Concat(list.Select(f => f.Check).Where(c => !Checks.Contains(c)).Distinct().OrderBy(c => c, StringComparer.Ordinal));
        return checks.Select(c => new CompositionGroup(c, list.Where(f => f.Check == c).ToList())).ToList();
    }

    private static int IndexOfCheck(string check)
    {
        for (var i = 0; i < Checks.Count; i++)
        {
            if (Checks[i] == check)
            {
                return i;
            }
        }
        return Checks.Count;
    }

    // ------------------------------------------------------------ the axis table

    /// <summary>
    /// The attribute an effect parameter falls back to when content leaves it out, keyed by (effect type, parameter).
    /// Read from the handlers under Effects/Handlers/: the ParamOrAttr and Rate calls, the
    /// "Field is null ? WeaveAttr(...)" defaults of scatter and cascade, the fuse's weave.Attr defaults and the
    /// area.width default SizeOf gives a missing width. The rows read outside the handlers are kept by hand: summon and
    /// deploy (SummonRecipe.Read in Minions/Summoning.cs reads them for both types), damage effectiveness
    /// (DamagePipeline.AddedEffectiveness takes the node's value over the weave's damage.effectiveness for added damage)
    /// and a charge gain's max (ChargeService.Gain caps at the holder's charge.{id}.max unless the node gives a max). An
    /// attribute with a {parameter} placeholder is the holder's rather than the weave's, named by that parameter of the
    /// same node. Multipliers are left out, because a literal there is still scaled by its attribute: area angle
    /// (area.angle), projectile spread (projectile.spread), aim spread (aim.spread) and a shove's distance (knockback).
    /// This table is replaced by AxisBindings (Weaves/AxisBindings.cs) when both land; nothing but
    /// <see cref="FallbackAttribute"/> reads it.
    /// </summary>
    private static readonly Dictionary<(string Type, string Parameter), string> AxisFallbacks = new()
    {
        [("area", "radius")] = "area.radius",
        [("area", "width")] = "area.width",
        [("projectile", "count")] = "projectile.count",
        [("projectile", "speed")] = "projectile.speed",
        [("projectile", "range")] = "range",
        [("projectile", "pierce")] = "projectile.pierce",
        [("projectile", "forks")] = "projectile.forks",
        [("projectile", "chains")] = "projectile.chain",
        [("projectile", "rehit")] = "projectile.rehit",
        [("projectile", "bounce")] = "projectile.bounce",
        [("projectile", "homing")] = "projectile.homing",
        [("projectile", "guide")] = "projectile.guide",
        [("projectile", "erratic")] = "projectile.erratic",
        [("projectile", "spiral")] = "projectile.spiral",
        [("projectile", "orbit")] = "projectile.orbit",
        [("projectile", "spin")] = "projectile.spin",
        [("projectile", "wave")] = "projectile.wave",
        [("projectile", "accel")] = "projectile.accel",
        [("projectile", "recurve")] = "projectile.recurve",
        [("projectile", "converge")] = "projectile.converge",
        [("projectile", "shape_width")] = "area.width",
        [("chain", "count")] = "chain.count",
        [("field", "radius")] = "area.radius",
        [("field", "duration")] = "duration",
        [("field", "width")] = "area.width",
        [("aura", "radius")] = "area.radius",
        [("aura", "duration")] = "duration",
        [("scatter", "count")] = "area.strikes",
        [("scatter", "radius")] = "area.radius",
        [("scatter", "telegraph_radius")] = "area.radius",
        [("cascade", "count")] = "area.cascade",
        [("cascade", "step")] = "area.radius",
        [("tether", "duration")] = "duration",
        [("fuse", "delay")] = "fuse.delay",
        [("fuse", "power")] = "fuse.power",
        // Kept by hand: SummonRecipe.Read (Minions/Summoning.cs) defaults these to weave.summon.count,
        // weave.summon.max and weave.duration for both summon and deploy.
        [("summon", "count")] = "summon.count",
        [("summon", "max_active")] = "summon.max",
        [("summon", "duration")] = "duration",
        [("deploy", "count")] = "summon.count",
        [("deploy", "max_active")] = "summon.max",
        [("deploy", "lifetime")] = "duration",
        // Kept by hand: DamagePipeline.AddedEffectiveness (Combat/DamagePipeline.cs) reads the node's effectiveness, else
        // weave.damage.effectiveness, for the attacker's added damage.
        [("damage", "effectiveness")] = "damage.effectiveness",
        // Kept by hand: ChargeService.Gain (Combat/ChargeService.cs) caps a gain at the holder's charge.{id}.max, which
        // the charge-max family gives every charge, unless the node gives its own max.
        [("charge", "max")] = "charge.{id}.max",
    };

    /// <summary>
    /// The attribute an effect parameter falls back to when content leaves it out; null when it has none. A
    /// <c>{parameter}</c> placeholder (<c>charge.{id}.max</c>) marks the holder's attribute, named by that parameter.
    /// </summary>
    public static string? FallbackAttribute(string type, string parameter) =>
        AxisFallbacks.TryGetValue((type, parameter), out var attribute) ? attribute : null;

    private static readonly Regex Placeholder = new(@"\{(\w+)\}", RegexOptions.CultureInvariant);

    /// <summary>An attribute with its placeholders filled from the node's parameters; a placeholder the node cannot fill stays.</summary>
    private static string Fill(string attribute, JsonObject? node) =>
        Placeholder.Replace(attribute, m => node is not null && JsonNum.StringOf(node[m.Groups[1].Value]) is { } value ? value : m.Value);

    // ------------------------------------------------------------ forms

    /// <summary>
    /// <c>form.unused</c> and <c>form.one_use</c>: the presets composing on each form. A shell (drop weight zero) is
    /// meant to serve every preset of its kind, so one preset or none is a smell; a form codices teach is also composed
    /// by players, so there it is a note.
    /// </summary>
    private void Forms(List<CompositionFinding> findings)
    {
        var kind = ContentKinds.NameOf<FormDef>();
        var presets = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var weave in _content.Each<WeaveDef>())
        {
            if (!presets.TryGetValue(weave.Form, out var list))
            {
                presets[weave.Form] = list = new List<string>();
            }
            list.Add(weave.Id);
        }
        foreach (var form in _content.Each<FormDef>().OrderBy(f => f.Id, StringComparer.Ordinal))
        {
            var users = presets.TryGetValue(form.Id, out var list) ? list.OrderBy(i => i, StringComparer.Ordinal).ToList() : new List<string>();
            var shell = form.DropWeight <= 0;
            var owner = new Owner(kind, form.Id);
            if (users.Count == 0)
            {
                findings.Add(About(FormUnused, shell ? Smell : Note, owner, form.Id, shell
                    ? "A shell (drop_weight 0) that no preset composes on, so nothing can cast it."
                    : "No preset composes on this form; only codices and players' own weaves reach it."));
            }
            else if (users.Count == 1)
            {
                findings.Add(About(FormOneUse, shell ? Smell : Note, owner, form.Id, shell
                    ? $"A shell (drop_weight 0) that only preset '{users[0]}' composes on, where a shell is meant to serve every preset of its kind."
                    : $"Only preset '{users[0]}' composes on this form."));
            }
        }
    }

    // ------------------------------------------------------------ the graph walk

    private sealed record Owner(string Kind, string Id);

    private static readonly Comparer<Owner> OwnerOrder = Comparer<Owner>.Create((a, b) =>
    {
        var byKind = string.CompareOrdinal(a.Kind, b.Kind);
        return byKind != 0 ? byKind : string.CompareOrdinal(a.Id, b.Id);
    });

    /// <summary>
    /// One parameter a definition writes: on an effect node, through a patch's <c>set</c>, on a patch's wrapper, or by a
    /// glyph's hook list. <see cref="Node"/> is the object holding the parameter, when there is one.
    /// </summary>
    private sealed record Write(Owner Owner, string Type, string Parameter, JsonNode? Value, string Path, JsonObject? Node);

    private sealed class Graph
    {
        public readonly List<Write> Writes = new();

        /// <summary>The definitions holding at least one node of each effect type.</summary>
        public readonly Dictionary<string, SortedSet<Owner>> Users = new(StringComparer.Ordinal);

        public void Use(string type, Owner owner)
        {
            if (!Users.TryGetValue(type, out var owners))
            {
                Users[type] = owners = new SortedSet<Owner>(OwnerOrder);
            }
            owners.Add(owner);
        }
    }

    /// <summary>
    /// Every effect node in loaded content, at every depth, as the weave compiler finds them: a list element whose
    /// <c>type</c> names a registered effect is a node, any other object is data that may hold lists. A list under
    /// <c>patches</c> holds patches, whose <c>set</c> writes parameters of the patched type and whose <c>wrap</c> is a
    /// node. A glyph's hook list writes every parameter a signature maps to that hook, since the compiler appends it
    /// there. Every kind is walked (forms, presets, glyphs, statuses, procs, behaviors and the rest); a definition's
    /// free-form <c>meta</c> is not.
    /// </summary>
    private Graph Walk()
    {
        var graph = new Graph();
        var glyphs = ContentKinds.NameOf<GlyphDef>();
        foreach (var kind in _content.KindNames.OrderBy(k => k, StringComparer.Ordinal))
        {
            foreach (var raw in _content.RawOfKind(kind).OrderBy(r => r.Id, StringComparer.Ordinal))
            {
                var owner = new Owner(kind, raw.Id);
                foreach (var (key, value) in raw.Object)
                {
                    if (key == "meta")
                    {
                        continue;
                    }
                    if (kind == glyphs && key == "hooks" && value is JsonObject hooks)
                    {
                        Hooks(hooks, owner, graph);
                    }
                    WalkValue(value, owner, key, key, graph);
                }
            }
        }
        return graph;
    }

    /// <summary>A glyph's hook lists, each a write of every parameter a type's signature maps to its hook.</summary>
    private void Hooks(JsonObject hooks, Owner owner, Graph graph)
    {
        foreach (var (hook, list) in hooks)
        {
            foreach (var type in _effects.Types)
            {
                if (_effects.TryGetSignature(type, out var signature) && signature.Hooks.TryGetValue(hook, out var parameter))
                {
                    graph.Writes.Add(new Write(owner, type, parameter, list, $"hooks.{hook}", null));
                }
            }
        }
    }

    private void WalkValue(JsonNode? value, Owner owner, string path, string key, Graph graph)
    {
        switch (value)
        {
            case JsonArray list:
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i] is not JsonObject element)
                    {
                        continue;
                    }
                    var at = $"{path}[{i}]";
                    if (key == "patches" && IsPatch(element))
                    {
                        Patch(element, owner, at, graph);
                    }
                    else if (EffectType(element) is { } type)
                    {
                        Node(type, element, owner, at, graph);
                    }
                    else
                    {
                        WalkData(element, owner, at, graph);
                    }
                }
                break;
            case JsonObject data:
                WalkData(data, owner, path, graph);
                break;
        }
    }

    private void WalkData(JsonObject data, Owner owner, string path, Graph graph)
    {
        foreach (var (key, value) in data)
        {
            WalkValue(value, owner, $"{path}.{key}", key, graph);
        }
    }

    private void Node(string type, JsonObject node, Owner owner, string path, Graph graph)
    {
        graph.Use(type, owner);
        foreach (var (key, value) in node)
        {
            if (key == "type")
            {
                continue;
            }
            graph.Writes.Add(new Write(owner, type, key, value, path, node));
            WalkValue(value, owner, $"{path}.{key}", key, graph);
        }
    }

    private void Patch(JsonObject patch, Owner owner, string path, Graph graph)
    {
        if (EffectType(patch) is { } type && patch["set"] is JsonObject set)
        {
            foreach (var (key, value) in set)
            {
                if (key == "type")
                {
                    continue;
                }
                graph.Writes.Add(new Write(owner, type, key, value, path, set));
                WalkValue(value, owner, $"{path}.set.{key}", key, graph);
            }
        }
        if (patch["wrap"] is JsonObject wrap && EffectType(wrap) is { } wrapper)
        {
            Node(wrapper, wrap, owner, $"{path}.wrap", graph);
        }
    }

    private static bool IsPatch(JsonObject element) => element.ContainsKey("type") && (element.ContainsKey("set") || element.ContainsKey("wrap"));

    private string? EffectType(JsonObject node) => JsonNum.StringOf(node["type"]) is { } type && _effects.Has(type) ? type : null;

    // ------------------------------------------------------------ parameters

    /// <summary>
    /// <c>parameter.single_use</c>: a parameter of an effect type that exactly one definition writes.
    /// <c>parameter.unused</c>: a parameter a signature declares that nothing loaded writes, or a whole type no
    /// content uses. Keys a closed signature does not accept are left to validation.
    /// </summary>
    private void Parameters(Graph graph, List<CompositionFinding> findings)
    {
        var writers = new Dictionary<(string Type, string Parameter), SortedSet<Owner>>();
        var users = graph.Users.ToDictionary(kv => kv.Key, kv => new SortedSet<Owner>(kv.Value, OwnerOrder), StringComparer.Ordinal);
        foreach (var write in graph.Writes)
        {
            if (!_effects.TryGetSignature(write.Type, out var signature) || !signature.Accepts(write.Parameter))
            {
                continue;
            }
            if (!writers.TryGetValue((write.Type, write.Parameter), out var owners))
            {
                writers[(write.Type, write.Parameter)] = owners = new SortedSet<Owner>(OwnerOrder);
            }
            owners.Add(write.Owner);
            if (users.TryGetValue(write.Type, out var typeUsers))
            {
                typeUsers.Add(write.Owner);
            }
        }
        foreach (var type in _effects.Types)
        {
            _effects.TryGetSignature(type, out var signature);
            var written = writers.Where(kv => kv.Key.Type == type).OrderBy(kv => kv.Key.Parameter, StringComparer.Ordinal).ToList();
            if (users.TryGetValue(type, out var typeUsers) && typeUsers.Count == 1)
            {
                // A type one definition alone uses: one finding for the type rather than one per parameter it writes.
                var only = typeUsers.Min!;
                var parameters = written.Count == 0 ? "" : $" ({string.Join(", ", written.Select(kv => kv.Key.Parameter))})";
                findings.Add(new CompositionFinding(ParameterSingleUse, Smell, "effect", type, _content.OriginOf(only.Kind, only.Id), null,
                    $"Only {only.Kind}/{only.Id} uses '{type}', so no second definition reuses its parameters{parameters}.")
                { Order = type });
            }
            else
            {
                foreach (var (key, owners) in written)
                {
                    if (owners.Count == 1)
                    {
                        var only = owners.Min!;
                        findings.Add(new CompositionFinding(ParameterSingleUse, Smell, "effect", $"{type}.{key.Parameter}", _content.OriginOf(only.Kind, only.Id), null,
                            $"Only {only.Kind}/{only.Id} writes '{key.Parameter}' of '{type}', so no second definition reuses it.")
                        { Order = $"{type}.{key.Parameter}" });
                    }
                }
            }
            if (signature.IsOpen)
            {
                continue;
            }
            if (!graph.Users.ContainsKey(type))
            {
                var declared = signature.Params.OrderBy(p => p, StringComparer.Ordinal).ToList();
                var message = declared.Count == 0
                    ? $"No loaded content has a '{type}' node."
                    : $"No loaded content has a '{type}' node, so none of its parameters ({string.Join(", ", declared)}) is exercised.";
                findings.Add(new CompositionFinding(ParameterUnused, Note, "effect", type, null, null, message) { Order = type });
                continue;
            }
            foreach (var parameter in signature.Params.Where(p => !writers.ContainsKey((type, p))).OrderBy(p => p, StringComparer.Ordinal))
            {
                // Where an attribute carries the parameter, leaving it unwritten is the intended state.
                var message = FallbackAttribute(type, parameter) is { } attribute
                    ? $"'{type}' declares '{parameter}', which no loaded content writes, as intended: attribute '{attribute}' carries it."
                    : $"'{type}' declares '{parameter}' but no loaded content writes it.";
                findings.Add(new CompositionFinding(ParameterUnused, Note, "effect", $"{type}.{parameter}", null, null, message) { Order = $"{type}.{parameter}" });
            }
        }
    }

    // ------------------------------------------------------------ frozen axes

    /// <summary>
    /// <c>axis.frozen</c>: a literal number, or an expression that reads nothing, written for a parameter whose handler
    /// would otherwise read an attribute. A weave attribute counts in the graphs the weave compiler reads (forms,
    /// presets, glyphs); statuses, procs and other graphs run without a weave, where the handler falls back to its own
    /// literal anyway. A holder's attribute (<c>charge.{id}.max</c>) counts in every graph, since the holder has it
    /// wherever the effect runs.
    /// </summary>
    private void Axes(Graph graph, List<CompositionFinding> findings)
    {
        var weaveKinds = new HashSet<string>(StringComparer.Ordinal)
        {
            ContentKinds.NameOf<FormDef>(), ContentKinds.NameOf<WeaveDef>(), ContentKinds.NameOf<GlyphDef>(),
        };
        foreach (var write in graph.Writes)
        {
            if (FallbackAttribute(write.Type, write.Parameter) is not { } attribute || Literal(write.Value) is not { } literal)
            {
                continue;
            }
            var holder = attribute.Contains('{', StringComparison.Ordinal);
            if (!holder && !weaveKinds.Contains(write.Owner.Kind))
            {
                continue;
            }
            var name = Fill(attribute, write.Node);
            var at = $"'{write.Parameter}' on the '{write.Type}' effect at {write.Path} is the literal {literal}";
            findings.Add(About(AxisFrozen, Smell, write.Owner, $"{write.Path}.{write.Parameter}", holder
                ? $"{at}, which shadows the holder's attribute '{name}'."
                : $"{at}, so nothing that moves weave attribute '{name}' reaches it."));
        }
    }

    /// <summary>The value as written when it is a number or an expression that reads nothing; null when it reads something.</summary>
    private static string? Literal(JsonNode? value)
    {
        if (value is not JsonValue v)
        {
            return null;
        }
        if (v.TryGetValue<string>(out var text))
        {
            return Expr.TryParse(text, out var expr, out _) && expr is not null && expr.Identifiers.Count == 0 && expr.Flags.Count == 0 ? $"\"{text}\"" : null;
        }
        return JsonNum.TryDouble(v, out var number) ? number.ToString("R", CultureInfo.InvariantCulture) : null;
    }

    // ------------------------------------------------------------ copied formulas

    /// <summary>
    /// <c>expression.copied</c>: one formula, the same text but for whitespace, written in the graphs of
    /// <see cref="CopiedThreshold"/> or more distinct definitions. Bare names and constants are not formulas.
    /// </summary>
    private void Copies(Graph graph, List<CompositionFinding> findings)
    {
        var carriers = new Dictionary<string, (string Text, SortedSet<Owner> Owners)>(StringComparer.Ordinal);
        foreach (var write in graph.Writes)
        {
            foreach (var text in Texts(write.Value, write.Parameter))
            {
                if (!IsFormula(text))
                {
                    continue;
                }
                var key = Whitespace.Replace(text, "");
                if (!carriers.TryGetValue(key, out var entry))
                {
                    carriers[key] = entry = (Whitespace.Replace(text.Trim(), " "), new SortedSet<Owner>(OwnerOrder));
                }
                entry.Owners.Add(write.Owner);
            }
        }
        foreach (var (key, (text, owners)) in carriers)
        {
            if (owners.Count < CopiedThreshold)
            {
                continue;
            }
            var shown = owners.Take(3).Select(o => $"{o.Kind}/{o.Id}");
            var examples = string.Join(", ", shown) + (owners.Count > 3 ? $" and {owners.Count - 3} more" : "");
            findings.Add(new CompositionFinding(ExpressionCopied, Smell, "expression", text, null, null,
                $"Written in the graphs of {owners.Count} definitions ({examples}); a named basis in rules.yaml or an attribute should carry it.")
            { Order = $"{1_000_000 - owners.Count:D7}{key}" });
        }
    }

    /// <summary>The strings a parameter's value holds, leaving out the nodes and patches nested in it, which are writes of their own.</summary>
    private IEnumerable<string> Texts(JsonNode? value, string key)
    {
        switch (value)
        {
            case JsonValue v when v.TryGetValue<string>(out var text):
                yield return text;
                break;
            case JsonArray list:
                foreach (var item in list)
                {
                    if (item is JsonObject o && (EffectType(o) is not null || key == "patches" && IsPatch(o)))
                    {
                        continue;
                    }
                    foreach (var text in Texts(item, key))
                    {
                        yield return text;
                    }
                }
                break;
            case JsonObject data:
                foreach (var (child, node) in data)
                {
                    foreach (var text in Texts(node, child))
                    {
                        yield return text;
                    }
                }
                break;
        }
    }

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.CultureInvariant);
    private static readonly Regex BareName = new(@"^[A-Za-z_$][\w.$]*$", RegexOptions.CultureInvariant);

    private static bool IsFormula(string text) =>
        Expr.TryParse(text, out var expr, out _) && expr is not null && !expr.IsConstant && !BareName.IsMatch(text.Trim());

    private CompositionFinding About(string check, string severity, Owner owner, string order, string message) =>
        new(check, severity, owner.Kind, owner.Id, _content.OriginOf(owner.Kind, owner.Id), null, message) { Order = $"{owner.Kind}\u0001{owner.Id}\u0001{order}" };

    // ------------------------------------------------------------ the code scan

    /// <summary>
    /// <c>code.string_switch</c>, a scan of lines, not a compiler. Every switch over string words under the source root
    /// is reported: a smell inside the systems (<see cref="SystemDirectories"/>) or when its words re-list two or more
    /// effect types, a note elsewhere and where a handler parses its own choice words (<c>ChoiceOf</c>) at compile
    /// time or switches on words the same file declares with <c>ChoiceOf(</c> or <c>OneOf(</c>. Inside the systems, a
    /// line is also a smell when it tests a flag, tag or mechanism by a literal name, compares with a literal naming
    /// what code must never name by id (a status, resource, thread, charge, proc or attribute), or compares one
    /// operand with two or more literals in the file: a switch written as ifs. Lines that register words
    /// (<c>Register(</c>) are not findings. <c>code.form_read</c> comes from the same lines, in
    /// <see cref="FormReadDirectories"/>.
    /// </summary>
    private void Code(string sourceRoot, List<CompositionFinding> findings)
    {
        var root = Path.GetFullPath(sourceRoot);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Source root '{sourceRoot}' does not exist.");
        }
        var names = ContentNames();
        var effectTypes = new HashSet<string>(_effects.Types, StringComparer.Ordinal);
        var files = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Select(full => (Full: full, Relative: Path.GetRelativePath(root, full).Replace('\\', '/')))
            .Where(f => !f.Relative.Split('/').Any(part => part is "bin" or "obj"))
            .OrderBy(f => f.Relative, StringComparer.Ordinal)
            .ToList();
        foreach (var (full, relative) in files)
        {
            var lines = Lex(File.ReadAllText(full));
            var inSystems = Within(relative, SystemDirectories);
            Switches(relative, lines, inSystems, effectTypes, findings);
            if (inSystems)
            {
                Comparisons(relative, lines, names, findings);
            }
            if (Within(relative, FormReadDirectories))
            {
                FormReads(relative, lines, findings);
            }
        }
    }

    private static bool Within(string relative, IReadOnlyList<string> directories) =>
        directories.Any(d => ("/" + relative).Contains("/" + d, StringComparison.Ordinal));

    /// <summary>
    /// The words code must never name by id: the ids of statuses, resources, threads, charges, procs and attributes.
    /// Tags, flags and mechanisms are open words content adds freely, so they stay out, and adding one can never grow
    /// the code findings; a flag or tag tested by name is caught by the call that tests it instead.
    /// </summary>
    private HashSet<string> ContentNames()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var kind in new[]
        {
            ContentKinds.NameOf<StatusDef>(), ContentKinds.NameOf<ResourceDef>(), ContentKinds.NameOf<ThreadDef>(),
            ContentKinds.NameOf<ChargeDef>(), ContentKinds.NameOf<ProcDef>(), ContentKinds.NameOf<AttributeDef>(),
        })
        {
            names.UnionWith(_content.IdsOfKind(kind));
        }
        return names;
    }

    /// <summary>One line of C# with comments removed and each string literal masked as <c>§n§</c>, its text in <see cref="Literals"/>.</summary>
    private sealed record CodeLine(string Code, IReadOnlyList<string> Literals);

    private const string Mark = "§";
    private const string AnyLiteral = @"§\d+§";
    private const string Operand = @"(?<op>[A-Za-z_]\w*(?:\s*\??\.\s*[A-Za-z_]\w*)*)";
    private static readonly Regex LiteralToken = new(@"§(\d+)§", RegexOptions.CultureInvariant);
    private static readonly Regex SwitchKeyword = new(@"\bswitch\b", RegexOptions.CultureInvariant);
    private static readonly Regex CaseLabel = new(@"\bcase\s+(?:not\s+)?\(?\s*" + AnyLiteral + @"(?:\s*\)?\s*or\s+\(?\s*" + AnyLiteral + ")*", RegexOptions.CultureInvariant);
    private static readonly Regex Arm = new(AnyLiteral + @"(?:\s+or\s+" + AnyLiteral + @")*\s*(?:when\b.*?)?=>", RegexOptions.CultureInvariant);
    private static readonly Regex Membership = new(@"\b(?:HasFlag|HasMechanism|Flags\s*\.\s*Contains|Tags\s*\.\s*Has(?:Any|All|Exact)?)\s*\(\s*§(?<lit>\d+)§\s*\)", RegexOptions.CultureInvariant);
    private static readonly Regex EqualsLiteral = new("(?:" + Operand + @")?[\s?!]*(?:==|!=)\s*§(?<lit>\d+)§", RegexOptions.CultureInvariant);
    private static readonly Regex LiteralEquals = new(@"§(?<lit>\d+)§\s*(?:==|!=)\s*(?:" + Operand + ")?", RegexOptions.CultureInvariant);
    private static readonly Regex IsPattern = new("(?:" + Operand + @"\s+)?\bis\s+(?:not\s+)?\(?\s*(?<lits>" + AnyLiteral + @"(?:\s*\)?\s*or\s+\(?\s*" + AnyLiteral + ")*)", RegexOptions.CultureInvariant);
    private static readonly Regex FormMember = new(@"(?<!\w)Form\??\.(?<member>[A-Za-z_]\w*)", RegexOptions.CultureInvariant);
    private static readonly Regex WholeForm = new(@"(?<=[=(,]\s*)(?<!\bForm\s*=\s*)(?<recv>[A-Za-z_]\w*(?:\??\.[A-Za-z_]\w*)*?)\??\.Form(?=\s*[;,)])", RegexOptions.CultureInvariant);
    private static readonly Regex MergedRead = new(@"\bPreset\??\.[A-Za-z_]|\b(?:Active)?Glyphs\b", RegexOptions.CultureInvariant);

    /// <summary>Switches over string words, each attributed to the innermost switch whose braces directly hold its labels.</summary>
    private static void Switches(string file, IReadOnlyList<CodeLine> lines, bool inSystems, HashSet<string> effectTypes, List<CompositionFinding> findings)
    {
        var words = new SortedDictionary<int, List<string>>();
        var open = new Stack<(int Line, int Depth)>();
        int? pending = null;
        var depth = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var code = line.Code;
            var labels = new List<(int At, List<string> Words)>();
            foreach (Match m in CaseLabel.Matches(code))
            {
                labels.Add((m.Index, WordsOf(line, m.Value)));
            }
            foreach (Match m in Arm.Matches(code))
            {
                var head = m.Value;
                var when = Regex.Match(head, @"\bwhen\b|=>");
                labels.Add((m.Index, WordsOf(line, when.Success ? head[..when.Index] : head)));
            }
            labels.Sort((a, b) => a.At.CompareTo(b.At));
            var keywords = new HashSet<int>(SwitchKeyword.Matches(code).Select(m => m.Index));
            var next = 0;
            for (var c = 0; c <= code.Length; c++)
            {
                while (next < labels.Count && labels[next].At == c)
                {
                    if (open.Count > 0 && open.Peek().Depth == depth)
                    {
                        var at = open.Peek().Line;
                        if (!words.TryGetValue(at, out var list))
                        {
                            words[at] = list = new List<string>();
                        }
                        list.AddRange(labels[next].Words);
                    }
                    next++;
                }
                if (c == code.Length)
                {
                    break;
                }
                if (keywords.Contains(c))
                {
                    pending = i;
                }
                if (code[c] == '{')
                {
                    depth++;
                    if (pending is { } started)
                    {
                        open.Push((started, depth));
                        pending = null;
                    }
                }
                else if (code[c] == '}')
                {
                    if (open.Count > 0 && open.Peek().Depth == depth)
                    {
                        open.Pop();
                    }
                    depth--;
                }
            }
        }
        // Words the file declares as the choices it accepts, so a switch over them reads its own vocabulary.
        var declared = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            if (line.Code.Contains("ChoiceOf(", StringComparison.Ordinal) || line.Code.Contains("OneOf(", StringComparison.Ordinal))
            {
                declared.UnionWith(line.Literals);
            }
        }
        foreach (var (index, list) in words)
        {
            var distinct = list.Distinct(StringComparer.Ordinal).ToList();
            var relisted = distinct.Where(effectTypes.Contains).ToList();
            var parses = lines[index].Code.Contains("ChoiceOf(", StringComparison.Ordinal);
            string severity;
            string message;
            var strings = Plural(distinct.Count, "the string", "the strings");
            if (relisted.Count >= 2)
            {
                severity = Smell;
                message = $"A switch re-lists the effect types {Names(relisted)}, where what it decides belongs with each handler and its signature.";
            }
            else if (parses)
            {
                severity = Note;
                message = $"A handler parses its own choice {Plural(distinct.Count, "word", "words")} {Names(distinct)} here at compile time; a second copy elsewhere would be the smell.";
            }
            else if (distinct.All(declared.Contains))
            {
                severity = Note;
                message = $"A switch on {strings} {Names(distinct)}, which this file declares with ChoiceOf or OneOf; a second copy elsewhere would be the smell.";
            }
            else if (inSystems)
            {
                severity = Smell;
                message = $"A switch on {strings} {Names(distinct)} inside a system, where a registry or an axis should carry {Plural(distinct.Count, "the word", "the words")}.";
            }
            else
            {
                severity = Note;
                message = $"A switch on {strings} {Names(distinct)} outside the systems, where parsing and validation may need one; a copy of a handler's words would be the smell.";
            }
            findings.Add(CodeFinding(file, index + 1, severity, message));
        }
    }

    /// <summary>Literal names tested or compared inside a system, one finding per line.</summary>
    private static void Comparisons(string file, IReadOnlyList<CodeLine> lines, HashSet<string> names, List<CompositionFinding> findings)
    {
        var hits = new List<(int Line, List<string> Tested, List<string> Compared, List<(string Op, string Word)> Operands, List<(string Op, List<string> Words)> Patterns)>();
        var byOperand = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var code = line.Code;
            if (code.Contains("Register(", StringComparison.Ordinal) || code.Contains("ChoiceOf(", StringComparison.Ordinal))
            {
                continue;
            }
            var tested = new List<string>();
            var compared = new List<string>();
            var operands = new List<(string Op, string Word)>();
            var patterns = new List<(string Op, List<string> Words)>();
            foreach (Match m in Membership.Matches(code))
            {
                tested.Add(LiteralOf(line, m.Groups["lit"].Value));
            }
            foreach (Match m in EqualsLiteral.Matches(code).Concat(LiteralEquals.Matches(code)))
            {
                var word = LiteralOf(line, m.Groups["lit"].Value);
                compared.Add(word);
                if (OperandOf(m) is { } op)
                {
                    operands.Add((op, word));
                }
            }
            foreach (Match m in IsPattern.Matches(code))
            {
                var words = WordsOf(line, m.Groups["lits"].Value);
                compared.AddRange(words);
                var op = OperandOf(m);
                if (op is { } known)
                {
                    operands.AddRange(words.Select(w => (known, w)));
                }
                if (words.Distinct(StringComparer.Ordinal).Count() >= 2)
                {
                    patterns.Add((op ?? "a value", words));
                }
            }
            if (tested.Count + compared.Count == 0)
            {
                continue;
            }
            hits.Add((i, tested, compared, operands, patterns));
            foreach (var (op, word) in operands)
            {
                if (!byOperand.TryGetValue(op, out var set))
                {
                    byOperand[op] = set = new SortedSet<string>(StringComparer.Ordinal);
                }
                set.Add(word);
            }
        }
        foreach (var (index, tested, compared, operands, patterns) in hits)
        {
            var parts = new List<string>();
            var named = new HashSet<string>(StringComparer.Ordinal);
            if (tested.Count > 0)
            {
                named.UnionWith(tested);
                parts.Add($"tests {Plural(tested.Distinct(StringComparer.Ordinal).Count(), "the flag or tag", "the flags or tags")} {Names(tested)} by name");
            }
            var content = compared.Where(names.Contains).ToList();
            if (content.Count > 0)
            {
                named.UnionWith(content);
                parts.Add($"compares with {Plural(content.Distinct(StringComparer.Ordinal).Count(), "the content name", "the content names")} {Names(content)}");
            }
            var chained = new HashSet<string>(StringComparer.Ordinal);
            foreach (var op in operands.Select(o => o.Op).Distinct(StringComparer.Ordinal))
            {
                if (byOperand[op].Count >= 2)
                {
                    chained.Add(op);
                    named.UnionWith(byOperand[op]);
                    parts.Add($"compares '{op}' with {Names(byOperand[op])} in this file, a switch on strings written as ifs");
                }
            }
            foreach (var (op, words) in patterns)
            {
                if (!chained.Contains(op))
                {
                    named.UnionWith(words);
                    parts.Add($"matches {(op == "a value" ? op : $"'{op}'")} against {Names(words)}, a switch on strings in one pattern");
                }
            }
            if (parts.Count == 0)
            {
                continue;
            }
            var sentence = string.Join(" and ", parts);
            var carry = Plural(named.Count, "this word", "these words");
            findings.Add(CodeFinding(file, index + 1, Smell, $"{char.ToUpperInvariant(sentence[0])}{sentence[1..]}; content or a registry should carry {carry}."));
        }
    }

    /// <summary>
    /// <c>code.form_read</c>: a line that reads a member through a <c>Form</c> (<c>weave.Form.Channel</c>, a compiled
    /// weave's own <c>Form.Targeting</c>) or takes a whole form into a local or a helper (<c>var form = weave.Form;</c>,
    /// <c>IsInstant(face.Form)</c>), where the runtime should read the compiled weave that presets and glyphs merge into.
    /// A line that also reads the preset's or the glyphs' block merges them and is left out, as are a weave spec's or
    /// preset's form id (<c>spec.Form</c>, <c>preset.Form</c>) and a modifier's <c>Form = x.Form</c>. One finding per line.
    /// </summary>
    private static void FormReads(string file, IReadOnlyList<CodeLine> lines, List<CompositionFinding> findings)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var code = lines[i].Code;
            if (MergedRead.IsMatch(code))
            {
                continue;
            }
            var members = FormMember.Matches(code).Select(m => m.Groups["member"].Value).Distinct(StringComparer.Ordinal).ToList();
            var whole = WholeForm.Matches(code).Any(m => !IsFormId(m.Groups["recv"].Value));
            if (members.Count == 0 && !whole)
            {
                continue;
            }
            var parts = new List<string>();
            if (members.Count > 0)
            {
                parts.Add($"reads the form's {Names(members)}");
            }
            if (whole)
            {
                parts.Add("takes the whole form");
            }
            var sentence = string.Join(" and ", parts);
            findings.Add(CodeFinding(CodeFormRead, file, i + 1, Smell,
                $"{char.ToUpperInvariant(sentence[0])}{sentence[1..]} where the runtime should read the compiled weave, into which a preset or glyph can merge any block."));
        }
    }

    /// <summary>A receiver whose <c>Form</c> is a form id rather than a form: a weave spec or a preset.</summary>
    private static bool IsFormId(string receiver)
    {
        var last = receiver[(receiver.LastIndexOf('.') + 1)..];
        return last.Equals("spec", StringComparison.OrdinalIgnoreCase) || last.Equals("preset", StringComparison.OrdinalIgnoreCase);
    }

    private static CompositionFinding CodeFinding(string file, int line, string severity, string message) =>
        CodeFinding(CodeStringSwitch, file, line, severity, message);

    private static CompositionFinding CodeFinding(string check, string file, int line, string severity, string message) =>
        new(check, severity, null, null, file, line, message) { Order = $"{file}\u0001{line:D6}" };

    private static string? OperandOf(Match m)
    {
        var group = m.Groups["op"];
        return group.Success && group.Length > 0 ? Whitespace.Replace(group.Value, "").Replace("?.", ".", StringComparison.Ordinal) : null;
    }

    private static string LiteralOf(CodeLine line, string index) =>
        int.TryParse(index, NumberStyles.None, CultureInfo.InvariantCulture, out var i) && i < line.Literals.Count ? line.Literals[i] : "";

    private static List<string> WordsOf(CodeLine line, string masked) =>
        LiteralToken.Matches(masked).Select(m => LiteralOf(line, m.Groups[1].Value)).ToList();

    private static string Plural(int count, string one, string many) => count == 1 ? one : many;

    /// <summary><c>'a', 'b' and 'c'</c>, the first eight of them.</summary>
    private static string Names(IEnumerable<string> words)
    {
        var list = words.Distinct(StringComparer.Ordinal).Select(w => $"'{w}'").ToList();
        if (list.Count == 0)
        {
            return "nothing";
        }
        if (list.Count > 8)
        {
            return string.Join(", ", list.Take(8)) + $" and {list.Count - 8} more";
        }
        return list.Count == 1 ? list[0] : string.Join(", ", list.Take(list.Count - 1)) + " and " + list[^1];
    }

    /// <summary>
    /// Splits C# into lines with comments removed, character literals blanked and string literals (regular, verbatim,
    /// interpolated and raw) masked, so braces, keywords and patterns are only ever read from code.
    /// </summary>
    private static List<CodeLine> Lex(string text)
    {
        var lines = new List<CodeLine>();
        var code = new StringBuilder();
        var literals = new List<string>();
        void End()
        {
            lines.Add(new CodeLine(code.ToString(), literals.ToArray()));
            code.Clear();
            literals.Clear();
        }
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            var next = i + 1 < text.Length ? text[i + 1] : '\0';
            if (c == '\n')
            {
                End();
                i++;
            }
            else if (c == '\r')
            {
                i++;
            }
            else if (c == '/' && next == '/')
            {
                while (i < text.Length && text[i] != '\n')
                {
                    i++;
                }
            }
            else if (c == '/' && next == '*')
            {
                i += 2;
                while (i < text.Length && !(text[i] == '*' && i + 1 < text.Length && text[i + 1] == '/'))
                {
                    if (text[i] == '\n')
                    {
                        End();
                    }
                    i++;
                }
                i += 2;
            }
            else if (c == '\'')
            {
                i++;
                while (i < text.Length && text[i] != '\'' && text[i] != '\n')
                {
                    i += text[i] == '\\' ? 2 : 1;
                }
                i++;
                code.Append("' '");
            }
            else if (c == '"' || ((c == '@' || c == '$') && (next == '"' || next == '@' || next == '$')))
            {
                var (value, end, newlines) = ReadLiteral(text, i);
                code.Append(Mark).Append(literals.Count.ToString(CultureInfo.InvariantCulture)).Append(Mark);
                literals.Add(value);
                for (var n = 0; n < newlines; n++)
                {
                    End();
                }
                i = end;
            }
            else
            {
                code.Append(c);
                i++;
            }
        }
        End();
        return lines;
    }

    /// <summary>One string literal from its prefix on: its text, the index after it and the line breaks inside it.</summary>
    private static (string Value, int End, int Newlines) ReadLiteral(string text, int i)
    {
        var verbatim = false;
        var interpolated = false;
        while (i < text.Length && (text[i] == '@' || text[i] == '$'))
        {
            verbatim |= text[i] == '@';
            interpolated |= text[i] == '$';
            i++;
        }
        var run = 0;
        while (i + run < text.Length && text[i + run] == '"')
        {
            run++;
        }
        if (run >= 3 && !verbatim)
        {
            var close = text.IndexOf(new string('"', run), i + run, StringComparison.Ordinal);
            var stop = close < 0 ? text.Length : close;
            var raw = text[(i + run)..stop];
            return (raw, Math.Min(text.Length, stop + run), raw.Count(ch => ch == '\n'));
        }
        var value = new StringBuilder();
        var newlines = 0;
        i++;
        while (i < text.Length)
        {
            var c = text[i];
            if (c == '\n' && !verbatim)
            {
                break;
            }
            if (c == '\n')
            {
                newlines++;
                value.Append(c);
                i++;
            }
            else if (verbatim && c == '"')
            {
                if (i + 1 < text.Length && text[i + 1] == '"')
                {
                    value.Append('"');
                    i += 2;
                    continue;
                }
                i++;
                break;
            }
            else if (!verbatim && c == '\\')
            {
                if (i + 1 < text.Length)
                {
                    value.Append(text[i + 1]);
                }
                i += 2;
            }
            else if (!verbatim && c == '"')
            {
                i++;
                break;
            }
            else if (interpolated && c == '{' && i + 1 < text.Length && text[i + 1] == '{')
            {
                value.Append('{');
                i += 2;
            }
            else if (interpolated && c == '{')
            {
                var depth = 1;
                i++;
                while (i < text.Length && depth > 0)
                {
                    if (text[i] == '{')
                    {
                        depth++;
                    }
                    else if (text[i] == '}')
                    {
                        depth--;
                    }
                    else if (text[i] == '"')
                    {
                        i++;
                        while (i < text.Length && text[i] != '"' && text[i] != '\n')
                        {
                            i += text[i] == '\\' ? 2 : 1;
                        }
                    }
                    else if (text[i] == '\n')
                    {
                        newlines++;
                    }
                    i++;
                }
                value.Append("{}");
            }
            else
            {
                value.Append(c);
                i++;
            }
        }
        return (value.ToString(), Math.Min(i, text.Length), newlines);
    }
}
