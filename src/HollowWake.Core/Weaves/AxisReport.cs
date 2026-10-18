using System.Globalization;
using System.Text.Json.Nodes;
using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Effects;
using HollowWake.Core.Expressions;
using HollowWake.Core.Weaves.Axes;

namespace HollowWake.Core.Weaves;

/// <summary>Who stated a value the axis report reads.</summary>
public enum AxisSource
{
    /// <summary>The weave attribute folded with the weave's own bases and modifiers.</summary>
    Attribute,
    /// <summary>
    /// A number or a word the form's or preset's graph writes as it stands. A number written as a constant is a
    /// frozen axis unless an attribute multiplies or channels it, as is a glyph's under a patch or a rider (see
    /// <see cref="AxisValue.Constant"/>).
    /// </summary>
    Literal,
    /// <summary>A formula the form's or preset's graph writes.</summary>
    Expression,
    /// <summary>A glyph's or a tree node's patch: a parameter it set, or a node it wrapped or appended.</summary>
    Patch,
    /// <summary>A field of the form's cast layer.</summary>
    Form,
    /// <summary>A field of the preset's cast layer.</summary>
    Preset,
    /// <summary>A cast-layer field a glyph grants, or a modifier a glyph or a tree node brings.</summary>
    Glyph,
    /// <summary>A rule the handler or the runtime reads where the graph gives nothing.</summary>
    Rule,
    /// <summary>A rider: an effect a glyph's hook, on_cast list or added effects put in the graph.</summary>
    Hook,
    /// <summary>A mechanism probe that holds on the compiled weave.</summary>
    Probe,
}

/// <summary>
/// One axis of a compiled weave: its dimension, its name (<c>geometry.radius</c>), a number or a word, its
/// unit, who stated it, and where: a node path in the compiled graph (<c>effects[0].on_hit[2].range</c>,
/// <c>release[0].radius</c>) or a field of the cast layer (<c>form.cast_time</c>, <c>preset.channel.interval</c>).
/// </summary>
public sealed record AxisValue
{
    public required Dimension Dimension { get; init; }
    public required string Name { get; init; }
    public double? Number { get; init; }
    public string? Word { get; init; }
    public required string Unit { get; init; }
    public required AxisSource Source { get; init; }
    public required string Path { get; init; }
    /// <summary>The attribute behind the value: folded into it, multiplying it, the channel it passes through, or (for a literal) the one the parameter falls back to.</summary>
    public string? Attribute { get; init; }
    /// <summary>
    /// An attribute multiplies the number or the number passes through an attribute's modifiers (a channel such
    /// as <c>damage</c>), so a modifier reaches it even where the graph writes a literal.
    /// </summary>
    public bool Multiplied { get; init; }
    /// <summary>The effect type the value is a parameter of, for values read from the graph.</summary>
    public string? Effect { get; init; }
    /// <summary>The parameter of <see cref="Effect"/> the value was read from, as <see cref="AxisBindings"/> declares it (<c>radius</c>, <c>place.range</c>).</summary>
    public string? Parameter { get; init; }
    /// <summary>The formula as written, the glyph that patched or hooked it, the rule read.</summary>
    public string? Detail { get; init; }
    /// <summary>A word that stands for a number: the grammar's target is an axis with a style word.</summary>
    public bool HidesNumber { get; init; }

    /// <summary>
    /// The number was written as a constant (a literal, a constant formula, a raw number of the cast layer) by
    /// whoever wrote it: the form, the preset, a glyph or a tree node.
    /// </summary>
    public bool Constant { get; init; }

    /// <summary>
    /// Who wrote the value in content, for what a graph or the cast layer writes: <c>form bolt</c>, <c>preset
    /// heavy_strike</c>, <c>glyph shedding</c> (a tree node is its form's). Null where nobody wrote it: a fallback,
    /// a rule, a probe.
    /// </summary>
    public string? Writer { get; init; }

    /// <summary>
    /// Where the value sits in its writer's own definition, not in the compiled graph: <c>effects[0].on_hit[1].chance</c>,
    /// <c>hooks.on_tick[0].radius</c>, <c>patches[0].wrap.count</c>, <c>guard.arc</c>. A form's literal keeps its
    /// place in every preset on the form and under any glyph's wrapper; a glyph's on whatever form it is socketed.
    /// </summary>
    public string? Origin { get; init; }

    /// <summary>A constant no attribute multiplies or channels: a frozen axis, no modifier reaches it, whoever wrote it.</summary>
    public bool Frozen => Constant && Number is not null && !Multiplied;

    /// <summary>The frozen axis as the literal baseline keys it: its writer and its place in the writer's definition.</summary>
    public string? Key => Writer is null || Origin is null ? null : $"{Writer} {Origin}";

    /// <summary>The value as text: the number to three places, else the word.</summary>
    public string Text => Number is { } n ? AxisReport.Format(n) : Word ?? "";

    /// <summary>The dimension's name as the report prints it: <c>geometry</c>, <c>kinematics</c> ...</summary>
    public static string DimensionName(Dimension dimension) => dimension.ToString().ToLowerInvariant();
}

/// <summary>
/// One effect node of a compiled weave's graph as the axis report walks it: its type, its path, the section
/// it sits in (<c>effects</c>, <c>release</c>, <c>bash</c>, <c>deactivate</c>, <c>cycle</c>) and how it came
/// into the graph when the form's or preset's own graph did not put it there.
/// </summary>
public sealed class AxisNode
{
    private readonly IReadOnlyDictionary<string, string>? _patched;
    private readonly IReadOnlyDictionary<string, (string Writer, string Path)>? _patchedAt;

    internal AxisNode(JsonObject node, string type, string path, string section, AxisSource? addedAs, string? addedBy, IReadOnlyDictionary<string, string>? patched,
        string? writer = null, string? origin = null, IReadOnlyDictionary<string, (string Writer, string Path)>? patchedAt = null)
    {
        Node = node;
        Type = type;
        Path = path;
        Section = section;
        AddedAs = addedAs;
        AddedBy = addedBy;
        _patched = patched;
        Writer = writer;
        Origin = origin;
        _patchedAt = patchedAt;
    }

    public JsonObject Node { get; }
    public string Type { get; }
    public string Path { get; }
    public string Section { get; }

    /// <summary>Who wrote the node in content: <c>form bolt</c>, <c>preset ember_bolt</c>, <c>glyph shedding</c> (a tree node is its form's).</summary>
    public string? Writer { get; }

    /// <summary>Where the node sits in its writer's own definition: <c>effects[0].on_hit[1]</c>, <c>hooks.on_tick[0]</c>, <c>patches[0].wrap</c>.</summary>
    public string? Origin { get; }

    /// <summary>
    /// Who wrote a value of the node and where in their own definition: the patch that set the parameter
    /// (<c>patches[1].set.forks</c>), else the node's writer at the value's place inside the node.
    /// <paramref name="valuePath"/> is the value's compiled path, under <see cref="Path"/>.
    /// </summary>
    public (string Writer, string Path)? OriginOf(string parameter, string valuePath)
    {
        var suffix = valuePath.StartsWith(Path, StringComparison.Ordinal) ? valuePath[Path.Length..] : "." + parameter;
        if (_patchedAt is not null && _patchedAt.TryGetValue(parameter, out var at))
        {
            var head = "." + parameter;
            return (at.Writer, at.Path + (suffix.StartsWith(head, StringComparison.Ordinal) ? suffix[head.Length..] : ""));
        }
        return Writer is null || Origin is null ? null : (Writer, Origin + suffix);
    }

    /// <summary>
    /// <see cref="AxisSource.Hook"/> for a rider (a glyph's hook list, on_cast list or added effects),
    /// <see cref="AxisSource.Patch"/> for a wrapper or an effect a patch appended, <see cref="AxisSource.Glyph"/> for
    /// a glyph's cycle; null for the form's and preset's own nodes.
    /// </summary>
    public AxisSource? AddedAs { get; }

    /// <summary>The glyph (and moment) that added the node: <c>glyph searing on_hit</c>.</summary>
    public string? AddedBy { get; }

    /// <summary>The glyph or tree node whose patch set a parameter of the node, or null.</summary>
    public string? PatchedBy(string parameter) => _patched is not null && _patched.TryGetValue(parameter, out var by) ? by : null;

    /// <summary>Who stated a parameter the graph writes, when it was not the form's or preset's own graph: the node's adder, else a patch.</summary>
    public (AxisSource Source, string By)? Provenance(string parameter)
    {
        if (AddedAs is { } added)
        {
            return (added, AddedBy ?? "");
        }
        return PatchedBy(parameter) is { } by ? (AxisSource.Patch, by) : null;
    }
}

/// <summary>Places one family of what a compiled weave states on the dimensions.</summary>
public delegate void AxisReader(AxisReading reading);

/// <summary>
/// What the family readers of <see cref="AxisReport"/> share for one weave: the compiled weave, its preset,
/// a holder with no stack of its own (so a fold reads the weave's own statement: its bases, its preset's,
/// glyphs' and tree's modifiers, under its tags), the graph's nodes with their provenance, and the values read so far.
/// </summary>
public sealed class AxisReading
{
    private readonly List<AxisValue> _values = new();

    internal AxisReading(CompiledWeave weave, ContentRegistry content)
    {
        Weave = weave.Root;
        Content = content;
        Preset = Weave.Spec.Preset is { } id ? content.Get<WeaveDef>(id) : null;
        Holder = new Stats(content);
        Nodes = AxisGraph.Walk(Weave, Preset);
    }

    public CompiledWeave Weave { get; }
    public ContentRegistry Content { get; }
    public WeaveDef? Preset { get; }

    /// <summary>A body with no modifiers, points or items of its own, at level one.</summary>
    public Stats Holder { get; }

    /// <summary>Every effect node of the compiled graph, sections included, parents before what they hold.</summary>
    public IReadOnlyList<AxisNode> Nodes { get; }

    public IReadOnlyList<AxisValue> Values => _values;

    public void Add(AxisValue value) => _values.Add(value);

    /// <summary>Adds a value with a number or a word; nothing when it has neither.</summary>
    public void Add(Dimension dimension, string axis, double? number, string? word, string unit, AxisSource source, string path,
        string? attribute = null, string? detail = null, string? effect = null, bool multiplied = false)
    {
        if (number is null && word is null)
        {
            return;
        }
        _values.Add(new AxisValue
        {
            Dimension = dimension,
            Name = AxisValue.DimensionName(dimension) + "." + axis,
            Number = number,
            Word = word,
            Unit = unit,
            Source = source,
            Path = path,
            Attribute = attribute,
            Detail = detail,
            Effect = effect,
            Multiplied = multiplied,
        });
    }

    /// <summary>
    /// A quantity the cast layer writes as an expression: its number where the weave alone states one, else its
    /// formula. A field that names a weave attribute and nothing else states the attribute, folded with its bases
    /// and modifiers: the field's owner wrote the name, the base's owner the number (a swing's cast time is the
    /// form's field and the preset's base), so it is placed as the attribute, naming the base and who wrote it.
    /// </summary>
    public void Quantity(Dimension dimension, string axis, Expr? expr, string unit, AxisSource source, string path, string? attribute = null)
    {
        if (expr is null)
        {
            return;
        }
        var number = Evaluate(expr);
        if (WeaveReference(expr) is { } reference)
        {
            Add(dimension, axis, number, number is null ? expr.Source : null, unit, AxisSource.Attribute, path, reference, Based(expr.Source, reference));
            return;
        }
        Add(dimension, axis, number, number is null ? expr.Source : null, unit, source, path, attribute,
            number is not null && !expr.IsConstant ? expr.Source : null);
    }

    /// <summary>
    /// Who wrote the base a weave attribute folds from: the preset, whose bases the compiler merges first, else
    /// the form; null where neither writes one and the attribute's own base stands.
    /// </summary>
    public string? BaseOwner(string attribute) =>
        Preset is { } preset && preset.Attributes.ContainsKey(attribute) ? "preset " + preset.Id
        : Weave.Form.Attributes.ContainsKey(attribute) ? "form " + Weave.Form.Id
        : null;

    /// <summary>The detail of a value that names a weave attribute: what was written, then the base the attribute folds from and who wrote it.</summary>
    public string Based(string written, string attribute) =>
        BaseOwner(attribute) is { } owner && Weave.Bases.TryGetValue(attribute, out var b)
            ? $"{written}; base {AxisReport.Format(b)} from {owner}"
            : $"{written}; the attribute's own base";

    /// <summary>Whether the weave's bases or its own modifiers name an attribute.</summary>
    public bool Mentions(string attribute) => Weave.Mentions(attribute);

    /// <summary>A weave attribute folded on the holder under the weave's tags; null when its formula cannot be read here.</summary>
    public double? Fold(string attribute, double? baseValue = null)
    {
        try
        {
            var value = baseValue is { } b ? Weave.Attr(Holder, attribute, b) : Weave.Attr(Holder, attribute);
            return double.IsFinite(value) ? value : null;
        }
        catch (Exception e) when (e is ExprEvalException or AttributeCycleException or ArithmeticException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// A number passed through an attribute's modifier stack on the holder under the weave's tags, the weave's
    /// modifiers joining, as an amount passes through the <c>damage</c> channel; null when it cannot be read here.
    /// </summary>
    public double? Channel(string attribute, double value)
    {
        try
        {
            var through = Holder.Attributes.Apply(attribute, value, Holder, Weave.Tags, Weave.Modifiers);
            return double.IsFinite(through) ? through : null;
        }
        catch (Exception e) when (e is ExprEvalException or AttributeCycleException or ArithmeticException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// The number an expression states by itself: a constant, or a formula over the weave's attributes alone
    /// (and its level) folded on the holder. Null for a formula that reads the caster, the target, the moment
    /// or a random draw: its value is the cast's, not the weave's.
    /// </summary>
    public double? Evaluate(Expr? expr)
    {
        if (expr is null)
        {
            return null;
        }
        if (expr.IsConstant)
        {
            return expr.ConstantValue;
        }
        if (!expr.IsPreviewSafe || expr.Flags.Count > 0 || expr.Identifiers.Count == 0)
        {
            return null;
        }
        foreach (var name in expr.Identifiers)
        {
            if (!name.StartsWith("weave.", StringComparison.Ordinal))
            {
                return null;
            }
        }
        try
        {
            var value = expr.Evaluate(Weave.ScopeFor(Holder));
            return double.IsFinite(value) ? value : null;
        }
        catch (Exception e) when (e is ExprEvalException or AttributeCycleException or ArithmeticException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>The attribute an expression is exactly a reference to (<c>weave.area.radius</c> is <c>area.radius</c>), or null.</summary>
    public static string? WeaveReference(Expr expr) =>
        expr.Identifiers.Count == 1 && expr.Flags.Count == 0 && expr.Source.Trim() == expr.Identifiers[0] && expr.Identifiers[0].StartsWith("weave.", StringComparison.Ordinal) && expr.Identifiers[0] != "weave.level"
            ? expr.Identifiers[0]["weave.".Length..]
            : null;

    /// <summary>
    /// Where a cast-layer block the weave holds came from: the first active glyph that grants it, else the
    /// preset, else the form, with the path of the field that holds it.
    /// </summary>
    public (AxisSource Source, string Path) OwnerOf<T>(T block, Func<GlyphDef, T?> ofGlyph, Func<WeaveDef, T?> ofPreset, string field) where T : class
    {
        foreach (var glyph in Weave.Glyphs)
        {
            if (ReferenceEquals(ofGlyph(glyph), block))
            {
                return (AxisSource.Glyph, $"glyph.{glyph.Id}.{field}");
            }
        }
        if (Preset is { } preset && ReferenceEquals(ofPreset(preset), block))
        {
            return (AxisSource.Preset, "preset." + field);
        }
        return (AxisSource.Form, "form." + field);
    }

    /// <summary>The words a word parameter names, with the weave's threads put in for <c>$threads</c>, <c>$thread</c> and <c>$thread_status</c>.</summary>
    public string? Words(JsonNode? node)
    {
        switch (node)
        {
            case null:
                return null;
            case JsonArray list:
                return string.Join(",", list.Select(Words).Where(w => !string.IsNullOrEmpty(w)));
            case JsonObject:
                return node.ToJsonString();
            case JsonValue value when value.TryGetValue<bool>(out var flag):
                return flag ? "true" : "false";
            case JsonValue value when JsonNum.StringOf(value) is { } text:
                return Resolve(text);
            default:
                return JsonNum.TryDouble(node, out var n) ? AxisReport.Format(n) : node.ToJsonString();
        }
    }

    private string Resolve(string word) => word switch
    {
        "$threads" => Weave.Threads.Count == 0 ? word : string.Join(",", Weave.Threads.Select(t => t.Id)),
        "$thread" => Weave.Threads.Count == 0 ? word : Weave.Threads[0].Id,
        "$thread_status" => Weave.Threads.FirstOrDefault()?.Status ?? word,
        _ => word,
    };
}

/// <summary>
/// The axis report of one weave the census compiled: a form compiled bare, a preset, or a glyph on the first form
/// it fits (its own values only); or why it was not placed.
/// </summary>
public sealed record WeaveAxes(string Id, string Kind, IReadOnlyList<AxisValue> Values, string? Error);

/// <summary>
/// Places a compiled weave on the nine dimensions of the combat grammar: every axis the weave states, each
/// with its unit, who stated it and where. It reads only what the compiled weave already states: its folded
/// attributes, its mechanism probes, its compiled graph at every depth (parameters, hooks and sections), its
/// cast-layer blocks through the compiled weave's accessors, its triggers, follow-ups and repeats. The
/// parameter readings come from <see cref="AxisBindings"/>. One reader per family of mechanics places its
/// part; a new family registers a reader with <see cref="Register"/> rather than editing one.
/// <c>hollowwake weave &lt;id&gt; --axes</c> prints it, and the axis report tests hold the content's frozen
/// literals to their baseline through it.
/// </summary>
public static class AxisReport
{
    private static readonly object Gate = new();
    private static (string Family, AxisReader Reader)[] _readers = Array.Empty<(string, AxisReader)>();

    static AxisReport()
    {
        Register("deliveries", DeliveryAxes.Read);
        Register("casting", CastingAxes.Read);
        Register("targeting", TargetingAxes.Read);
        Register("summons", SummonAxes.Read);
        Register("triggers", TriggerAxes.Read);
        Register("statuses", StatusAxes.Read);
        Register("mechanisms", MechanismAxes.Read);
    }

    /// <summary>Registers a family's reader; a family registered again replaces its reader in place.</summary>
    public static void Register(string family, AxisReader reader)
    {
        lock (Gate)
        {
            var list = _readers.ToList();
            var at = list.FindIndex(r => r.Family == family);
            if (at >= 0)
            {
                list[at] = (family, reader);
            }
            else
            {
                list.Add((family, reader));
            }
            _readers = list.ToArray();
        }
    }

    /// <summary>The families with a registered reader, in reading order.</summary>
    public static IReadOnlyList<string> Families => _readers.Select(r => r.Family).ToArray();

    /// <summary>Every axis the compiled weave states, family by family in reading order.</summary>
    public static IReadOnlyList<AxisValue> Of(CompiledWeave weave, ContentRegistry content)
    {
        var reading = new AxisReading(weave, content);
        foreach (var (_, reader) in _readers)
        {
            reader(reading);
        }
        return reading.Values;
    }

    /// <summary>
    /// The frozen axes among a report's values: constants a graph or the cast layer writes that no attribute
    /// multiplies or channels, whoever wrote them (the form, the preset, a glyph). Where the binding names an
    /// attribute (<see cref="AxisValue.Attribute"/>), content can write the attribute instead; where it names
    /// none (a flight's radius today), the engine owes one.
    /// </summary>
    public static IReadOnlyList<AxisValue> Literals(IReadOnlyList<AxisValue> values) => values.Where(v => v.Frozen).ToArray();

    /// <summary>
    /// The literal baseline: the key of every frozen axis among the reports, each once however many weaves carry
    /// it (a form's literal in every preset on the form, a glyph's in every weave that sockets it), in ordinal order.
    /// </summary>
    public static IReadOnlyList<string> LiteralKeys(IEnumerable<WeaveAxes> reports) =>
        reports.SelectMany(r => r.Values).Where(v => v.Frozen && v.Key is not null).Select(v => v.Key!)
            .Distinct(StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal).ToArray();

    /// <summary>The frozen axes of a compiled weave.</summary>
    public static IReadOnlyList<AxisValue> Literals(CompiledWeave weave, ContentRegistry content) => Literals(Of(weave, content));

    /// <summary>
    /// The report over every form (compiled bare, with the first threads by id when it needs some) and every
    /// preset of the content, in id order. A weave that does not compile carries its error and no values.
    /// </summary>
    public static IReadOnlyList<WeaveAxes> OfAll(ContentRegistry content)
    {
        var compiler = new WeaveCompiler(content, EffectRegistry.Shared);
        var threads = content.Each<ThreadDef>().Select(t => t.Id).OrderBy(t => t, StringComparer.Ordinal).ToList();
        var all = new List<WeaveAxes>();
        foreach (var form in content.Each<FormDef>().OrderBy(f => f.Id, StringComparer.Ordinal))
        {
            var spec = new WeaveSpec { Form = form.Id, Threads = threads.Take(Math.Max(0, form.MinThreads)).ToList() };
            all.Add(Census(compiler, content, "form", form.Id, spec));
        }
        foreach (var preset in content.Each<WeaveDef>().OrderBy(p => p.Id, StringComparer.Ordinal))
        {
            all.Add(Census(compiler, content, "preset", preset.Id, new WeaveSpec { Preset = preset.Id }));
        }
        return all;
    }

    private static WeaveAxes Census(WeaveCompiler compiler, ContentRegistry content, string kind, string id, WeaveSpec spec) =>
        compiler.TryCompile(spec, out var weave, out var error, GlyphFit.Dormant)
            ? new WeaveAxes(id, kind, Of(weave!, content), null)
            : new WeaveAxes(id, kind, Array.Empty<AxisValue>(), error);

    /// <summary>
    /// The glyphs' part of the census, so a glyph no preset sockets is counted too: each glyph, in id order, on
    /// the first form by id it fits, active (with the first threads by id the form takes), else on the first
    /// preset by id it fits beside the preset's own glyphs, with the values the glyph wrote (what its patches set
    /// and created, its riders, added effects and cycle) and the cast-layer blocks it grants. A glyph's values are
    /// keyed in its own definition, so where it lands moves none of their keys. A glyph that fits nothing carries
    /// why and no values.
    /// </summary>
    public static IReadOnlyList<WeaveAxes> OfGlyphs(ContentRegistry content)
    {
        var compiler = new WeaveCompiler(content, EffectRegistry.Shared);
        var threads = content.Each<ThreadDef>().Select(t => t.Id).OrderBy(t => t, StringComparer.Ordinal).ToList();
        var forms = content.Each<FormDef>().Where(f => f.MaxGlyphs > 0).OrderBy(f => f.Id, StringComparer.Ordinal).ToList();
        var presets = content.Each<WeaveDef>().OrderBy(p => p.Id, StringComparer.Ordinal).ToList();
        var all = new List<WeaveAxes>();
        foreach (var glyph in content.Each<GlyphDef>().OrderBy(g => g.Id, StringComparer.Ordinal))
        {
            var writer = "glyph " + glyph.Id;
            var granted = $"glyph.{glyph.Id}.";
            var specs = forms.Select(f => new WeaveSpec { Form = f.Id, Threads = threads.Take(Math.Max(1, f.MinThreads)).Take(f.MaxThreads).ToList(), Glyphs = { glyph.Id } })
                .Concat(presets.Select(p => new WeaveSpec { Preset = p.Id, Glyphs = { glyph.Id } }));
            WeaveAxes? placed = null;
            foreach (var spec in specs)
            {
                if (!compiler.TryCompile(spec, out var weave, out _, GlyphFit.Dormant) || !weave!.Glyphs.Any(g => g.Id == glyph.Id))
                {
                    continue;
                }
                var own = Of(weave, content).Where(v => v.Writer == writer || v.Path.StartsWith(granted, StringComparison.Ordinal)).ToArray();
                placed = new WeaveAxes(glyph.Id, "glyph", own, null);
                break;
            }
            all.Add(placed ?? new WeaveAxes(glyph.Id, "glyph", Array.Empty<AxisValue>(), "fits no form or preset"));
        }
        return all;
    }

    /// <summary>A number as the report prints it: up to three places, invariant.</summary>
    public static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}

/// <summary>
/// Walks a compiled weave's graph for the axis report: every effect node at every depth of the main list and
/// the sections, each with its path, its provenance and its origin (who wrote it in content, and where in their
/// own definition). Riders are the trailing entries of each hook list, which the compiler appends glyph by glyph
/// after every patch; added effects and on_cast lists are the trailing roots of the main list. What the compiled
/// weave does not keep (which nodes a patch created, which parameters it set, where a node a wrap moved came
/// from) is found by replaying the active glyphs' and tree nodes' patches one at a time, through the compiler's
/// own <see cref="EffectPatcher"/>, on the form's and preset's graph and its sections, and reading what each changed.
/// </summary>
internal static class AxisGraph
{
    private static readonly string[] SectionOrder = { WeaveSections.Release, WeaveSections.Bash, WeaveSections.Deactivate, WeaveSections.Cycle };

    /// <summary>A node of the replayed graph: who wrote it and where, the patch owner that created it, and who set which parameters where.</summary>
    private sealed class Mark
    {
        public required string Writer { get; init; }
        public required string Origin { get; init; }
        public string? Created { get; init; }
        public Dictionary<string, string>? Set { get; init; }
        public Dictionary<string, (string Writer, string Path)>? SetAt { get; init; }
    }

    /// <summary>A node a glyph adds: the label it is reported under, its glyph, and its place in the glyph's definition.</summary>
    private readonly record struct Rider(string Label, string Writer, string Origin);

    public static List<AxisNode> Walk(CompiledWeave weave, WeaveDef? preset)
    {
        var probe = weave.Probe;
        var effects = probe?.Graph.Effects ?? EffectRegistry.Shared;
        var marks = Replay(weave, preset, effects);
        var riders = RiderPlan(weave.Glyphs);
        var nodes = new List<AxisNode>();

        // The main list: the form's and preset's effects, then the glyphs' added effects, then their on_cast lists.
        var main = weave.Specs;
        var own = weave.Form.Effects.Count + (preset?.Effects.Count ?? 0);
        var tail = new List<(Rider Rider, bool Hookable)>();
        foreach (var glyph in weave.Glyphs)
        {
            for (var k = 0; k < glyph.AddsEffects.Count; k++)
            {
                tail.Add((new Rider($"glyph {glyph.Id} adds_effects", "glyph " + glyph.Id, $"adds_effects[{k}]"), true));
            }
        }
        foreach (var glyph in weave.Glyphs)
        {
            if (glyph.Hooks.TryGetValue(EffectHooks.OnCast, out var onCast))
            {
                for (var k = 0; k < onCast.Count; k++)
                {
                    tail.Add((new Rider($"glyph {glyph.Id} {EffectHooks.OnCast}", "glyph " + glyph.Id, $"hooks.{EffectHooks.OnCast}[{k}]"), false));
                }
            }
        }
        for (var i = 0; i < main.Count; i++)
        {
            var node = WeaveGraph.ToNode(main[i]);
            var path = $"effects[{i}]";
            if (i < own || i - own >= tail.Count)
            {
                Visit(node, path, "effects", null, null, true, null, null, marks, riders, effects, nodes);
            }
            else
            {
                var (rider, hookable) = tail[i - own];
                Visit(node, path, "effects", AxisSource.Hook, rider.Label, hookable, rider.Writer, rider.Origin, marks, riders, effects, nodes);
            }
        }

        // The sections: a channel's release, a guard's bash, a toggle's deactivation, a cycle's payload.
        if (probe is not null)
        {
            var glyphCycle = weave.Glyphs.FirstOrDefault(g => g.Cycle is not null);
            foreach (var section in SectionOrder)
            {
                if (!probe.Graph.HasSection(section))
                {
                    continue;
                }
                var specs = probe.Graph.ToSpecs(section);
                for (var i = 0; i < specs.Count; i++)
                {
                    var fromGlyph = section == WeaveSections.Cycle && glyphCycle is not null;
                    Visit(WeaveGraph.ToNode(specs[i]), $"{section}[{i}]", section,
                        fromGlyph ? AxisSource.Glyph : null, fromGlyph ? $"glyph {glyphCycle!.Id} cycle" : null, true, null, null, marks, riders, effects, nodes);
                }
            }
        }
        return nodes;
    }

    /// <summary>For each hook name, the glyphs whose lists the compiler appends at it, in the order it appends them.</summary>
    private static List<(string Hook, string Glyph, int Count)> RiderPlan(IReadOnlyList<GlyphDef> glyphs)
    {
        var plan = new List<(string, string, int)>();
        foreach (var glyph in glyphs)
        {
            foreach (var (hook, list) in glyph.Hooks)
            {
                if (hook != EffectHooks.OnCast && list.Count > 0)
                {
                    plan.Add((hook, glyph.Id, list.Count));
                }
            }
        }
        return plan;
    }

    /// <summary>
    /// Adds a node and what it holds. <paramref name="writer"/> and <paramref name="origin"/> are the node's as its
    /// holder sees it; the replay's mark, where it has one, says better (a node a wrap moved keeps its own place).
    /// </summary>
    private static void Visit(JsonObject node, string path, string section, AxisSource? addedAs, string? addedBy, bool hookable,
        string? writer, string? origin, Dictionary<string, Mark> marks, List<(string Hook, string Glyph, int Count)> riders, EffectRegistry effects, List<AxisNode> into)
    {
        var type = WeaveGraph.TypeOf(node);
        if (type is null || !effects.TryGetSignature(type, out var signature))
        {
            return;
        }
        // Riders joined after every patch, so the replay never saw them; everything else it did.
        Mark? mark = null;
        if (addedAs is not AxisSource.Hook && marks.TryGetValue(section + "|" + path, out var found))
        {
            mark = found;
            writer = found.Writer;
            origin = found.Origin;
            if (addedAs is null && found.Created is { } creator)
            {
                addedAs = AxisSource.Patch;
                addedBy = creator;
            }
        }
        into.Add(new AxisNode(node, type, path, section, addedAs, addedBy, mark?.Set, writer, origin, mark?.SetAt));

        // The riders at each hook parameter: the trailing entries, by glyph, in the order appended.
        Dictionary<string, List<Rider>>? tails = null;
        if (hookable && signature.Hooks.Count > 0)
        {
            foreach (var (hook, glyph, count) in riders)
            {
                if (!signature.Hooks.TryGetValue(hook, out var parameter))
                {
                    continue;
                }
                tails ??= new Dictionary<string, List<Rider>>(StringComparer.Ordinal);
                if (!tails.TryGetValue(parameter, out var list))
                {
                    list = new List<Rider>();
                    tails[parameter] = list;
                }
                for (var k = 0; k < count; k++)
                {
                    list.Add(new Rider($"glyph {glyph} {hook}", "glyph " + glyph, $"hooks.{hook}[{k}]"));
                }
            }
        }
        // What a rider or a glyph's own content holds is the rider's too; a patch's wrapper holds the node it wrapped,
        // which keeps its own provenance. Nothing under a rider or an on_cast list is hooked: they joined after the hooks.
        var inherited = addedAs is AxisSource.Hook or AxisSource.Glyph ? addedAs : null;
        var inheritedBy = inherited is null ? null : addedBy;
        var holder = new Holder(path, writer, origin);
        foreach (var (parameter, value) in node)
        {
            if (parameter == "type" || value is null)
            {
                continue;
            }
            var riderLabels = tails is not null && tails.TryGetValue(parameter, out var labels) ? labels : null;
            Children(value, $"{path}.{parameter}", section, inherited, inheritedBy, hookable, riderLabels, holder, marks, riders, effects, into);
        }
    }

    /// <summary>The effect node holding a value as the walk goes down: its path, and its writer and place, which what it holds extends.</summary>
    private readonly record struct Holder(string Path, string? Writer, string? Origin)
    {
        /// <summary>The origin of a node at <paramref name="path"/> beneath the holder: the holder's, with the node's place under it.</summary>
        public string? Beneath(string path) => Origin is null ? null : Origin + path[Path.Length..];
    }

    private static void Children(JsonNode value, string path, string section, AxisSource? inherited, string? inheritedBy, bool hookable,
        List<Rider>? riderLabels, Holder holder, Dictionary<string, Mark> marks, List<(string Hook, string Glyph, int Count)> riders, EffectRegistry effects, List<AxisNode> into)
    {
        switch (value)
        {
            case JsonArray list:
            {
                var firstRider = riderLabels is null ? int.MaxValue : list.Count - riderLabels.Count;
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i] is not JsonObject element)
                    {
                        continue;
                    }
                    var at = $"{path}[{i}]";
                    if (IsEffect(element, effects))
                    {
                        if (i >= firstRider && firstRider >= 0)
                        {
                            var rider = riderLabels![i - firstRider];
                            Visit(element, at, section, AxisSource.Hook, rider.Label, false, rider.Writer, rider.Origin, marks, riders, effects, into);
                        }
                        else
                        {
                            Visit(element, at, section, inherited, inheritedBy, hookable, holder.Writer, holder.Beneath(at), marks, riders, effects, into);
                        }
                    }
                    else
                    {
                        Data(element, at, section, inherited, inheritedBy, hookable, holder, marks, riders, effects, into);
                    }
                }
                break;
            }
            case JsonObject obj when !IsEffect(obj, effects):
                Data(obj, path, section, inherited, inheritedBy, hookable, holder, marks, riders, effects, into);
                break;
        }
    }

    private static void Data(JsonObject obj, string path, string section, AxisSource? inherited, string? inheritedBy, bool hookable,
        Holder holder, Dictionary<string, Mark> marks, List<(string Hook, string Glyph, int Count)> riders, EffectRegistry effects, List<AxisNode> into)
    {
        foreach (var (key, value) in obj)
        {
            if (value is not null)
            {
                Children(value, $"{path}.{key}", section, inherited, inheritedBy, hookable, null, holder, marks, riders, effects, into);
            }
        }
    }

    private static bool IsEffect(JsonObject obj, EffectRegistry effects) => WeaveGraph.TypeOf(obj) is { } type && effects.Has(type);

    // ------------------------------------------------------------ the replay: origins and patch provenance

    /// <summary>
    /// Builds the graph as the compiler does before any patch (the form's and preset's effects, then the sections)
    /// with each node's writer and place in the writer's own definition, replays the patches of the active glyphs
    /// (in socket order) and of the bought tree nodes (after them) one at a time, and keys what it found by
    /// section and compiled path: every node's writer and origin, the nodes a patch created and where in the patch
    /// they come from, and the parameters a patch set.
    /// </summary>
    private static Dictionary<string, Mark> Replay(CompiledWeave weave, WeaveDef? preset, EffectRegistry effects)
    {
        var form = weave.Form;
        var formWriter = "form " + form.Id;
        var presetWriter = preset is null ? formWriter : "preset " + preset.Id;
        string HeldWriter(object? block, object? presetBlock) => presetBlock is not null && ReferenceEquals(block, presetBlock) ? presetWriter : formWriter;

        var graph = new WeaveGraph(effects);
        var counts = new List<(string Section, int Count)>();
        var rootOrigins = new List<(string Writer, string Path)>();
        void AddAll(IEnumerable<(TypedSpec Spec, string Writer, string Path)> specs, string section, bool patchable)
        {
            var n = 0;
            foreach (var (spec, writer, path) in specs)
            {
                graph.Add(spec, patchable, section == "effects" ? null : section);
                rootOrigins.Add((writer, path));
                n++;
            }
            counts.Add((section, n));
        }
        static IEnumerable<(TypedSpec, string, string)> Owned(IEnumerable<TypedSpec>? specs, string writer, string field) =>
            (specs ?? Enumerable.Empty<TypedSpec>()).Select((spec, i) => (spec, writer, $"{field}[{i}]"));
        var glyphCycle = weave.Glyphs.FirstOrDefault(g => g.Cycle is not null);
        AddAll(Owned(form.Effects, formWriter, "effects").Concat(Owned(preset?.Effects, presetWriter, "effects")), "effects", true);
        AddAll(Owned(weave.Channel?.Release?.Effects, HeldWriter(weave.Channel, preset?.Channel), "channel.release.effects"), WeaveSections.Release, true);
        AddAll(Owned(weave.Guard?.Bash?.Effects, HeldWriter(weave.Guard, preset?.Guard), "guard.bash.effects"), WeaveSections.Bash, true);
        AddAll(Owned(form.Toggle?.OnDeactivate, formWriter, "toggle.on_deactivate"), WeaveSections.Deactivate, true);
        AddAll(Owned((glyphCycle?.Cycle ?? preset?.Cycle ?? form.Cycle)?.Effects,
            glyphCycle is not null ? "glyph " + glyphCycle.Id : preset?.Cycle is not null ? presetWriter : formWriter, "cycle.effects"), WeaveSections.Cycle, glyphCycle is null);

        // Every node's writer and place in the writer's definition, before a patch moves anything.
        var origins = new Dictionary<JsonObject, (string Writer, string Path)>(ReferenceEqualityComparer.Instance);
        var r = 0;
        foreach (var root in graph.RootNodes)
        {
            var (writer, path) = rootOrigins[r++];
            Each(root, path, null, "", effects, (node, at, _, _) => origins[node] = (writer, at));
        }

        var owners = new List<(string Owner, string Label, string Writer, string Prefix, IReadOnlyList<EffectPatch> Patches)>();
        foreach (var glyph in weave.Glyphs)
        {
            if (glyph.Patches.Count > 0)
            {
                owners.Add((glyph.Id, "glyph " + glyph.Id, "glyph " + glyph.Id, "", glyph.Patches));
            }
        }
        foreach (var (node, _) in weave.Tree)
        {
            if (node.Patches.Count > 0)
            {
                // A tree node is part of its form: its patches sit under the form's tree.
                var index = form.Tree?.Nodes.FindIndex(n => n.Id == node.Id) ?? -1;
                owners.Add(("tree:" + node.Id, "tree " + node.Id, formWriter, $"tree.nodes[{index}].", node.Patches));
            }
        }

        var patcher = new EffectPatcher(graph);
        var created = new Dictionary<JsonObject, string>(ReferenceEqualityComparer.Instance);
        var set = new Dictionary<JsonObject, Dictionary<string, string>>(ReferenceEqualityComparer.Instance);
        var setAt = new Dictionary<JsonObject, Dictionary<string, (string Writer, string Path)>>(ReferenceEqualityComparer.Instance);
        foreach (var (owner, label, writer, prefix, patches) in owners)
        {
            for (var p = 0; p < patches.Count; p++)
            {
                var patch = patches[p];
                var before = Snapshot(graph, effects);
                try
                {
                    patcher.Apply(patch, owner);
                }
                catch (InvalidOperationException)
                {
                    // The compiler applied the same patches without error; a replay that cannot is read as far as it went.
                }
                var fresh = new HashSet<JsonObject>(ReferenceEqualityComparer.Instance);
                var index = p;
                foreach (var root in graph.RootNodes)
                {
                    Each(root, "", null, "", effects, (node, _, parent, fragment) =>
                    {
                        if (!before.TryGetValue(node, out var old))
                        {
                            // Created by this patch: inside another node it created, at its place there; else its wrapper or an item it set.
                            created.TryAdd(node, label);
                            fresh.Add(node);
                            origins[node] = parent is not null && fresh.Contains(parent) && origins.TryGetValue(parent, out var outer)
                                ? (outer.Writer, outer.Path + fragment)
                                : (writer, prefix + FromPatch(patch, index, node, parent, fragment, before, effects));
                            return;
                        }
                        foreach (var (parameter, value) in node)
                        {
                            if (parameter == "type" || old.TryGetValue(parameter, out var was) && was == value?.ToJsonString())
                            {
                                continue;
                            }
                            if (!set.TryGetValue(node, out var by))
                            {
                                by = new Dictionary<string, string>(StringComparer.Ordinal);
                                set[node] = by;
                                setAt[node] = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
                            }
                            // The patcher decides whether a write wins. A default does not lock the parameter,
                            // so a later explicit write may replace it; attribute the observed change to that write.
                            by[parameter] = label;
                            setAt[node][parameter] = (writer, $"{prefix}patches[{index}].set.{parameter}");
                        }
                    });
                }
            }
        }

        // Key what was found by the paths the walk of the compiled graph will meet: hooks only append after these.
        var marks = new Dictionary<string, Mark>(StringComparer.Ordinal);
        var roots = graph.RootNodes.ToList();
        var at = 0;
        foreach (var (section, count) in counts)
        {
            for (var i = 0; i < count && at < roots.Count; i++, at++)
            {
                Each(roots[at], $"{section}[{i}]", null, "", effects, (node, path, _, _) =>
                {
                    if (origins.TryGetValue(node, out var origin))
                    {
                        marks[section + "|" + path] = new Mark
                        {
                            Writer = origin.Writer,
                            Origin = origin.Path,
                            Created = created.GetValueOrDefault(node),
                            Set = set.GetValueOrDefault(node),
                            SetAt = setAt.GetValueOrDefault(node),
                        };
                    }
                });
            }
        }
        return marks;
    }

    /// <summary>
    /// Where in a patch a node it created comes from: the wrapper it put around a node (<c>patches[0].wrap</c>), an
    /// item of a list it set, counted in the patch's own list (<c>patches[1].set.on_hit[0]</c>, past what the node
    /// held before when it appends), else the patch.
    /// </summary>
    private static string FromPatch(EffectPatch patch, int index, JsonObject node, JsonObject? parent, string fragment,
        Dictionary<JsonObject, Dictionary<string, string?>> before, EffectRegistry effects)
    {
        var at = $"patches[{index}]";
        if (patch.Wrap is not null && WeaveGraph.TypeOf(node) is { } type && effects.TryGetSignature(type, out var signature)
            && signature.Wraps is { } inner && node[inner] is JsonArray held && held.Any(e => e is JsonObject wrapped && before.ContainsKey(wrapped)))
        {
            return at + ".wrap";
        }
        if (parent is null || patch.Set is not { } values || fragment.Length < 2)
        {
            return at;
        }
        var end = fragment.IndexOfAny(Steps, 1);
        var name = end < 0 ? fragment[1..] : fragment[1..end];
        if (!values.ContainsKey(name))
        {
            return at;
        }
        if (end < 0 || fragment[end] != '[')
        {
            return $"{at}.set.{name}{(end < 0 ? "" : fragment[end..])}";
        }
        var close = fragment.IndexOf(']', end);
        var item = int.Parse(fragment[(end + 1)..close], CultureInfo.InvariantCulture);
        var offset = patch.EffectiveMode == PatchMode.Append && values[name] is JsonArray written && parent[name] is JsonArray now ? now.Count - written.Count : 0;
        return $"{at}.set.{name}[{item - offset}]{fragment[(close + 1)..]}";
    }

    private static readonly char[] Steps = { '.', '[' };

    /// <summary>Every effect node of the graph with its parameters as they stand, written out to compare after a patch.</summary>
    private static Dictionary<JsonObject, Dictionary<string, string?>> Snapshot(WeaveGraph graph, EffectRegistry effects)
    {
        var snapshot = new Dictionary<JsonObject, Dictionary<string, string?>>(ReferenceEqualityComparer.Instance);
        foreach (var root in graph.RootNodes)
        {
            Each(root, "", null, "", effects, (node, _, _, _) =>
            {
                var parameters = new Dictionary<string, string?>(StringComparer.Ordinal);
                foreach (var (parameter, value) in node)
                {
                    parameters[parameter] = value?.ToJsonString();
                }
                snapshot[node] = parameters;
            });
        }
        return snapshot;
    }

    /// <summary>
    /// Visits a node and the effect nodes it holds, parents first, each with its path (the walk of the compiled graph
    /// gives the same), the effect node holding it, and its place under that node (<c>.on_hit[2]</c>, <c>.choices[0].effects[1]</c>).
    /// </summary>
    private static void Each(JsonObject node, string path, JsonObject? parent, string fragment, EffectRegistry effects, Action<JsonObject, string, JsonObject?, string> visit)
    {
        visit(node, path, parent, fragment);
        foreach (var (parameter, value) in node)
        {
            if (parameter != "type" && value is not null)
            {
                Inside(value, node, $"{path}.{parameter}", "." + parameter, effects, visit);
            }
        }
    }

    /// <summary>The effect nodes a value holds: in a list, or in the data objects (a random choice) a list or a block holds.</summary>
    private static void Inside(JsonNode value, JsonObject holder, string path, string fragment, EffectRegistry effects, Action<JsonObject, string, JsonObject?, string> visit)
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
                    if (IsEffect(element, effects))
                    {
                        Each(element, $"{path}[{i}]", holder, $"{fragment}[{i}]", effects, visit);
                    }
                    else
                    {
                        foreach (var (key, inner) in element)
                        {
                            if (inner is not null)
                            {
                                Inside(inner, holder, $"{path}[{i}].{key}", $"{fragment}[{i}].{key}", effects, visit);
                            }
                        }
                    }
                }
                break;
            case JsonObject obj when !IsEffect(obj, effects):
                foreach (var (key, inner) in obj)
                {
                    if (inner is not null)
                    {
                        Inside(inner, holder, $"{path}.{key}", $"{fragment}.{key}", effects, visit);
                    }
                }
                break;
        }
    }
}
