using System.Collections.Concurrent;
using HollowWake.Core.Ai;
using HollowWake.Core.Effects;
using HollowWake.Core.Effects.Handlers;
using HollowWake.Core.Expressions;
using HollowWake.Core.Sim;
using static HollowWake.Core.Weaves.AxisUnits;
using static HollowWake.Core.Weaves.Dimension;

namespace HollowWake.Core.Weaves;

/// <summary>
/// The nine dimensions of the combat grammar (docs/design/combat-grammar.md), each a group of axes: where a
/// resolution applies, how deliveries and bodies move, what lands on a body, what a cast costs and banks, the
/// time from press to last resolution, what is selected, who acts after the press, what starts a resolution
/// and how far it cascades, and the state one cast writes on a body for another to read.
/// </summary>
public enum Dimension
{
    Geometry,
    Kinematics,
    Payload,
    Economy,
    Timing,
    Targeting,
    Agency,
    Causality,
    Coupling,
}

/// <summary>What a handler's parameter is in the grammar: a quantity, an effect list or reference, a word, or a flag.</summary>
public enum ParameterKind
{
    /// <summary>A quantity with a unit, read as a number, an attribute reference or an expression.</summary>
    Axis,
    /// <summary>A graph element: an effect list at a moment, a modifier list, a predicate, a reference to a definition, a block.</summary>
    Node,
    /// <summary>A word from a registry or a fixed vocabulary.</summary>
    Choice,
    /// <summary>A bool or a bespoke block that does one thing; the grammar resolves each into an axis, a choice or a node.</summary>
    Flag,
}

/// <summary>What a node parameter holds.</summary>
public enum NodeShape
{
    None,
    /// <summary>An effect list.</summary>
    Effects,
    /// <summary>
    /// An effect list that is one branch of a nesting operator (a condition's <c>then</c> and <c>else</c>, a random
    /// pick's choices): the graph it nests, as a wrapper's inner list is, not a moment content rides.
    /// </summary>
    Branch,
    /// <summary>A modifier list lent to bodies.</summary>
    Modifiers,
    /// <summary>A condition, an expression that holds or not.</summary>
    Predicate,
    /// <summary>The id of another definition: a status, a pattern, a weave, a behavior, an item.</summary>
    Reference,
    /// <summary>A block of fields, each declared under the block's name and a dot.</summary>
    Block,
}

/// <summary>How the weave attribute of a binding meets its parameter.</summary>
public enum AttributeRole
{
    /// <summary>No weave attribute reaches the parameter.</summary>
    None,
    /// <summary>The handler folds the attribute (times the binding's factor) when the graph writes no value.</summary>
    Fallback,
    /// <summary>The attribute multiplies the parameter, written or not, so a modifier reaches even a written number.</summary>
    Multiplier,
    /// <summary>
    /// The parameter passes through the attribute's modifier stack, written or not, the way an amount of damage
    /// passes through <c>damage</c> and a heal through <c>healing</c>: the attribute's base plays no part (both
    /// are zero), its flats, increases and mores do, so a modifier reaches even a written number.
    /// </summary>
    Channel,
}

/// <summary>Whose stack a binding's attribute is folded on when the handler reads it.</summary>
public enum AttributeFold
{
    /// <summary>The weave's fold: the caster's stack with the weave's bases and modifiers, under the weave's tags.</summary>
    Weave,
    /// <summary>
    /// The caster's own stack under the weave's tags, without the weave's bases and modifiers: a glyph's or a
    /// passive's modifier on the caster reaches it, a modifier the weave carries does not (an applier's
    /// <c>effect.duration</c>, read by the status service).
    /// </summary>
    Caster,
    /// <summary>
    /// The stack of the body that bears what the effect writes, without the weave's tags or modifiers: the most
    /// charges a body holds is its own <c>charge.{id}.max</c>, the caster's or a target's.
    /// </summary>
    Bearer,
}

/// <summary>
/// The units an axis is written in. Content writes world units for distances, degrees for angles and degrees a
/// second for turning, seconds for times, fractions for shares and counts for things counted; the rest name
/// what the grammar calls a resource, a word, a flag, a condition or a weight.
/// </summary>
public static class AxisUnits
{
    public const string Units = "units";
    public const string UnitsPerSecond = "units/s";
    public const string UnitsPerSecondSquared = "units/s2";
    public const string Degrees = "deg";
    public const string DegreesPerSecond = "deg/s";
    public const string CyclesPerSecond = "cycles/s";
    public const string Seconds = "s";
    public const string PerSecond = "1/s";
    public const string Count = "count";
    public const string CountPerSecond = "count/s";
    public const string Fraction = "fraction";
    public const string FractionPerSecond = "fraction/s";
    /// <summary>An amount of life, mana or another pool: damage, healing, costs, reservations, a guard's pool.</summary>
    public const string Resource = "resource";
    public const string ResourcePerSecond = "resource/s";
    /// <summary>A number in the unit of the attribute a modifier moves.</summary>
    public const string Value = "value";
    /// <summary>A number read as on above zero.</summary>
    public const string Switch = "switch";
    public const string Weight = "weight";
    public const string Word = "word";
    public const string Flag = "flag";
    public const string Condition = "condition";

    /// <summary>Whether a value in the unit is a word rather than a number.</summary>
    public static bool IsWord(string unit) => unit is Word or Flag or Condition;
}

/// <summary>
/// One declaration: what an effect type's parameter is (an axis, a node, a choice or a flag), the dimension it
/// places a value on, its unit, the attribute it falls back to, is multiplied by or passes through and whose
/// stack that is, the rule the handler reads when the graph gives nothing, and for a choice the vocabulary its
/// words come from. A parameter with a
/// dot is a field inside a block (<c>place.range</c>); <c>*</c> stands for each key of a map
/// (<c>amounts.*</c>) and <c>[]</c> for each element of a list (<c>choices[].weight</c>). An implicit binding
/// names a number the handler or its system reads with no parameter of its own (a fork's angle, an area's
/// aftershocks).
/// </summary>
public sealed record AxisBinding
{
    public required string Type { get; init; }
    public required string Parameter { get; init; }
    public required ParameterKind Kind { get; init; }
    /// <summary>The dimension the parameter's value is placed on; null for what the report does not place (effect lists, keys, presentation) or a family reader places.</summary>
    public Dimension? Dimension { get; init; }
    public string Unit { get; init; } = Word;
    /// <summary>The attribute behind the parameter; a sibling's word in braces stands for itself (<c>charge.{id}.max</c>).</summary>
    public string? Attribute { get; init; }
    public AttributeRole Role { get; init; }
    /// <summary>Whose stack the attribute is folded on: the weave's, unless the handler reads the caster's own.</summary>
    public AttributeFold Fold { get; init; }
    /// <summary>What a fallback attribute is multiplied by: a storm's disc is twice the weave's area radius.</summary>
    public double Factor { get; init; } = 1;
    /// <summary>
    /// A property of a sibling's word that multiplies the fallback as well, <c>shape:width_share</c>: a bar
    /// takes a quarter of the weave's area width, a line all of it.
    /// </summary>
    public string? FactorOf { get; init; }
    /// <summary>The number the handler uses when neither the graph nor the attribute or rule gives one.</summary>
    public double? Default { get; init; }
    /// <summary>The word or flag the handler uses when the graph writes none.</summary>
    public string? DefaultWord { get; init; }
    /// <summary>A rule the handler or its system reads in place of a parameter.</summary>
    public string? Rule { get; init; }
    /// <summary>
    /// When the handler reads the parameter at all, written or not: terms joined by <c>&amp;</c>, each over a
    /// sibling parameter or the weave: <c>wave</c> (stated and not zero), <c>!amounts</c> (the graph writes no
    /// <c>amounts</c>), <c>fire=salvo</c>, <c>kind!=knockback|buffet</c>, <c>shape:anchored</c> (the sibling's
    /// word has a property its vocabulary declares, see <see cref="AxisBindings.Property"/>) or <c>#melee</c> (the
    /// weave carries the tag). A sibling the graph does not write takes its own binding's default.
    /// </summary>
    public string? When { get; init; }
    /// <summary>
    /// When the handler falls back to the attribute, rule or default, in the same terms as <see cref="When"/>: a
    /// written value is read whatever this says. A flight's range falls back to the weave's <c>range</c> only
    /// where no duration is written; with one, an unwritten range leaves the flight unbounded.
    /// </summary>
    public string? FallbackWhen { get; init; }
    public bool Implicit { get; init; }
    /// <summary>Only place a written value; a separate implicit axis describes the unwritten case.</summary>
    public bool WrittenOnly { get; init; }
    /// <summary>Explains the scope of a written value's attribute override.</summary>
    public string? WrittenDetail { get; init; }
    /// <summary>The axis name within the dimension, when it is not the parameter's: one mechanism, one name.</summary>
    public string? Name { get; init; }
    public NodeShape Node { get; init; }
    /// <summary>
    /// For a choice, the registry its words come from (<c>shapes</c>, <c>filters</c>, <c>picks</c> ...); for a
    /// reference, the content kind it names (<c>statuses</c>, <c>patterns</c>, <c>weaves</c> ...), as the handler's
    /// signature declares it where it does.
    /// </summary>
    public string? Registry { get; init; }
    /// <summary>For a choice with a fixed vocabulary, its words.</summary>
    public IReadOnlyList<string>? Words { get; init; }
    /// <summary>The family reader of <see cref="AxisReport"/> that places this parameter, when the parameter walk does not.</summary>
    public string? ReadBy { get; init; }
    /// <summary>A word that stands for a number (a firing style that means a spacing): the grammar's target is an axis with a style word.</summary>
    public bool HidesNumber { get; init; }

    /// <summary>The axis name within the dimension.</summary>
    public string Axis => Name ?? Parameter;

    private static readonly char[] Separators = { '.', '[' };

    /// <summary>The parameter the graph writes this binding under: the block's name for a field inside a block.</summary>
    public string Top
    {
        get
        {
            var cut = Parameter.IndexOfAny(Separators);
            return cut < 0 ? Parameter : Parameter[..cut];
        }
    }
}

/// <summary>A weave attribute the runtime reads off the weave as a whole rather than through a parameter.</summary>
public sealed record WeaveAttributeBinding(string Attribute, Dimension Dimension, string Unit, string Axis, bool Reported);

/// <summary>
/// The open registry of what every effect parameter is in the combat grammar, read by <see cref="AxisReport"/>
/// (<c>hollowwake weave --axes</c>) and held to its baselines by the signature tests. Each parameter of every
/// handler signature is declared here as an axis (with its dimension, unit, the attribute it falls back to, is
/// multiplied by or passes through, and the rule or default the handler reads otherwise), a node, a choice
/// (with its vocabulary) or a flag. Beside them it declares the weave attributes the runtime reads off a weave
/// as a whole and the dimension each mechanism probe speaks to.
/// </summary>
/// <remarks>
/// These declarations belong on each handler's <see cref="EffectSignature"/>, beside the parameter names it
/// already declares, so a handler states what it reads where it reads it. This registry is their interim home
/// while the handler files are held by other work; when the signatures carry them, the registry reads them
/// from the signatures and the static constructor below goes. A mod registers its handlers' parameters here
/// as the built-ins are, through <see cref="Register(AxisBinding)"/>.
/// </remarks>
public static class AxisBindings
{
    private static readonly object Gate = new();
    private static readonly ConcurrentDictionary<string, AxisBinding[]> ByType = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, WeaveAttributeBinding> Attributes = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, Dimension> Mechanisms = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, Func<IEnumerable<string>>> Vocabularies = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, Func<string, double?>> Properties = new(StringComparer.Ordinal);
    private static WeaveAttributeBinding[] _attributeOrder = Array.Empty<WeaveAttributeBinding>();

    /// <summary>The relation words the status transfers take, their own rather than the filter registry's.</summary>
    private static readonly string[] Sides = { "allies", "enemies", "any" };
    private static readonly string[] StatusKinds = { "harmful", "beneficial", "any" };
    private static readonly string[] Occlusions = { "blocked", "free" };

    static AxisBindings()
    {
        Vocabulary("shapes", () => Shapes.Names);
        Vocabulary("picks", () => Picks.Names);
        Vocabulary("curves", () => Curves.Names);
        Vocabulary("target kinds", () => TargetKinds.All.Select(k => k.Name));
        Vocabulary("trigger kinds", () => TriggerKinds.All.Select(k => k.Name));
        Vocabulary("filters", () => Filters.Names);
        Vocabulary("commands", () => MinionCommands.Names);
        ShapeProperties();

        Deliveries();
        Placements();
        Movement();
        Payloads();
        Statuses();
        Summons();
        Minions();
        Flow();
        Casting();
        Utility();
        WeaveLevel();
        MechanismDimensions();
    }

    // ------------------------------------------------------------ the registry

    /// <summary>Declares an axis: a number the handler reads, falling back to the weave attribute when one is named.</summary>
    public static void Register(string type, string parameter, Dimension dimension, string? attribute, string unit) =>
        Register(new AxisBinding
        {
            Type = type,
            Parameter = parameter,
            Kind = ParameterKind.Axis,
            Dimension = dimension,
            Unit = unit,
            Attribute = attribute,
            Role = attribute is null ? AttributeRole.None : AttributeRole.Fallback,
        });

    /// <summary>Declares a binding of any kind. Bindings of a type keep their order; the first whose condition holds wins.</summary>
    public static void Register(AxisBinding binding)
    {
        lock (Gate)
        {
            var list = ByType.TryGetValue(binding.Type, out var existing) ? existing.ToList() : new List<AxisBinding>();
            list.Add(binding);
            ByType[binding.Type] = list.ToArray();
        }
    }

    /// <summary>Every binding of an effect type, in the order declared; empty for a type nothing declares.</summary>
    public static IReadOnlyList<AxisBinding> For(string type) => ByType.TryGetValue(type, out var list) ? list : Array.Empty<AxisBinding>();

    /// <summary>The bindings of one parameter of a type (more than one where a condition picks between them).</summary>
    public static IReadOnlyList<AxisBinding> Of(string type, string parameter) => For(type).Where(b => b.Parameter == parameter).ToArray();

    /// <summary>
    /// The binding through which a parameter of a type falls back to a weave attribute when the graph writes no value;
    /// null for channels, multipliers and parameters without one. The composition lint and the mechanism probes read
    /// a parameter's attribute door here.
    /// </summary>
    public static AxisBinding? FallbackOf(string type, string parameter) => Of(type, parameter).FirstOrDefault(b => b.Role == AttributeRole.Fallback);

    /// <summary>Whether a parameter of a type is declared, by itself or as the block holding declared fields.</summary>
    public static bool Declares(string type, string parameter) => For(type).Any(b => !b.Implicit && b.Top == parameter);

    public static IEnumerable<string> Types => ByType.Keys.OrderBy(t => t, StringComparer.Ordinal);

    public static IEnumerable<AxisBinding> All => Types.SelectMany(For);

    /// <summary>Declares a weave attribute the runtime reads off the weave itself; <paramref name="reported"/> false when a family reader folds it into another value.</summary>
    public static void RegisterAttribute(string attribute, Dimension dimension, string unit, string? axis = null, bool reported = true)
    {
        lock (Gate)
        {
            var binding = new WeaveAttributeBinding(attribute, dimension, unit, axis ?? attribute, reported);
            Attributes[attribute] = binding;
            _attributeOrder = _attributeOrder.Where(a => a.Attribute != attribute).Append(binding).ToArray();
        }
    }

    /// <summary>The weave attributes the runtime reads off a weave as a whole, in the order declared.</summary>
    public static IReadOnlyList<WeaveAttributeBinding> WeaveAttributes => _attributeOrder;

    /// <summary>Declares the dimension a mechanism probe speaks to.</summary>
    public static void RegisterMechanism(string mechanism, Dimension dimension) => Mechanisms[mechanism] = dimension;

    /// <summary>The dimension of a mechanism, by its name before any colon (<c>affliction:bleeding</c> is <c>affliction</c>).</summary>
    public static bool TryMechanism(string mechanism, out Dimension dimension)
    {
        var colon = mechanism.IndexOf(':');
        return Mechanisms.TryGetValue(colon < 0 ? mechanism : mechanism[..colon], out dimension);
    }

    /// <summary>
    /// Every dimension an attribute moves: the parameters that fall back to it or that it multiplies, and the
    /// weave-level reads. A glyph's or a passive's modifier on the attribute moves those dimensions.
    /// </summary>
    public static IReadOnlyList<Dimension> DimensionsOf(string attribute)
    {
        var found = new SortedSet<Dimension>();
        foreach (var b in All)
        {
            if (b.Attribute == attribute && b.Dimension is { } d)
            {
                found.Add(d);
            }
        }
        if (Attributes.TryGetValue(attribute, out var weave))
        {
            found.Add(weave.Dimension);
        }
        return found.ToArray();
    }

    /// <summary>Declares where a named vocabulary's words come from, read live so a registry a mod grows is read as it stands.</summary>
    public static void Vocabulary(string registry, Func<IEnumerable<string>> words) => Vocabularies[registry] = words;

    /// <summary>The words a choice takes: its registry's as they stand, else its fixed list; null for an open word (a key, a tag list, an id).</summary>
    public static IReadOnlyList<string>? WordsOf(AxisBinding binding)
    {
        if (binding.Registry is { } registry && Vocabularies.TryGetValue(registry, out var words))
        {
            return words().ToArray();
        }
        return binding.Words;
    }

    /// <summary>
    /// Declares a property of a vocabulary's words, read live from what the vocabulary's registry says of each
    /// word: a number, one or zero for a yes or no. A condition reads it as <c>shape:anchored</c> (it holds
    /// where the property is not zero), a fallback factor as <c>shape:width_share</c>.
    /// </summary>
    public static void Property(string registry, string property, Func<string, double?> value) => Properties[registry + ":" + property] = value;

    /// <summary>A property of a word of a vocabulary; null where the vocabulary declares no such property or does not know the word.</summary>
    public static double? PropertyOf(string registry, string property, string word) =>
        Properties.TryGetValue(registry + ":" + property, out var value) ? value(word) : null;

    // ------------------------------------------------------------ declaration helpers

    private static void Axis(string type, string parameter, Dimension dimension, string unit, string? attribute = null, double? @default = null, double factor = 1, string? rule = null, string? when = null, string? name = null, string? fallbackWhen = null, string? factorOf = null, AttributeFold fold = AttributeFold.Weave) =>
        Register(new AxisBinding
        {
            Type = type,
            Parameter = parameter,
            Kind = ParameterKind.Axis,
            Dimension = dimension,
            Unit = unit,
            Attribute = attribute,
            Role = attribute is null ? AttributeRole.None : AttributeRole.Fallback,
            Fold = fold,
            Factor = factor,
            FactorOf = factorOf,
            Default = @default,
            Rule = rule,
            When = when,
            FallbackWhen = fallbackWhen,
            Name = name,
        });

    private static void Scaled(string type, string parameter, Dimension dimension, string unit, string attribute, double? @default = null, string? when = null, string? name = null, AttributeFold fold = AttributeFold.Weave) =>
        Register(new AxisBinding
        {
            Type = type,
            Parameter = parameter,
            Kind = ParameterKind.Axis,
            Dimension = dimension,
            Unit = unit,
            Attribute = attribute,
            Role = AttributeRole.Multiplier,
            Fold = fold,
            Default = @default,
            When = when,
            Name = name,
        });

    /// <summary>An axis that passes through an attribute's modifier stack (<see cref="AttributeRole.Channel"/>): an amount of damage or healing.</summary>
    private static void Channel(string type, string parameter, Dimension dimension, string unit, string attribute, double? @default = null, string? name = null, string? fallbackWhen = null) =>
        Register(new AxisBinding
        {
            Type = type,
            Parameter = parameter,
            Kind = ParameterKind.Axis,
            Dimension = dimension,
            Unit = unit,
            Attribute = attribute,
            Role = AttributeRole.Channel,
            Default = @default,
            FallbackWhen = fallbackWhen,
            Name = name,
        });

    private static void Choice(string type, string parameter, Dimension? dimension, string? @default = null, string[]? words = null, string? registry = null, bool hidesNumber = false, string? readBy = null, string? name = null) =>
        Register(new AxisBinding
        {
            Type = type,
            Parameter = parameter,
            Kind = ParameterKind.Choice,
            Dimension = dimension,
            Unit = Word,
            DefaultWord = @default,
            Words = words,
            Registry = registry,
            HidesNumber = hidesNumber,
            ReadBy = readBy,
            Name = name,
        });

    /// <summary>Open words a family reader or presentation reads (keys, tag lists, ids of content, names): declared, placed by no binding.</summary>
    private static void Keys(string type, string? readBy, params string[] parameters)
    {
        foreach (var parameter in parameters)
        {
            Choice(type, parameter, null, readBy: readBy);
        }
    }

    private static void FlagOf(string type, string parameter, Dimension dimension, bool @default = false, string unit = Flag, string? name = null) =>
        Register(new AxisBinding
        {
            Type = type,
            Parameter = parameter,
            Kind = ParameterKind.Flag,
            Dimension = dimension,
            Unit = unit,
            DefaultWord = @default ? "true" : "false",
            Name = name,
        });

    private static void NodeOf(string type, string parameter, NodeShape shape, Dimension? dimension = null, string? registry = null, string? readBy = null, string? name = null) =>
        Register(new AxisBinding
        {
            Type = type,
            Parameter = parameter,
            Kind = ParameterKind.Node,
            Node = shape,
            Dimension = dimension,
            Unit = shape == NodeShape.Predicate ? Condition : Word,
            Registry = registry,
            ReadBy = readBy,
            Name = name,
        });

    /// <summary>
    /// Declares a parameter that names content (a status, a pattern, a weave): of the kind the handler's signature
    /// declares for it with <see cref="EffectSignature.Refers"/>, else of the kind given here, until the signature
    /// declares it too; a signature test holds those it does not at their baseline.
    /// </summary>
    private static void Reference(string type, string parameter, string? kind = null, Dimension? dimension = null, string? readBy = null, string? name = null) =>
        NodeOf(type, parameter, NodeShape.Reference, dimension, SignatureKind(type, parameter) ?? kind, readBy, name);

    /// <summary>The content kind a built-in handler's signature says a parameter names, or null.</summary>
    private static string? SignatureKind(string type, string parameter) =>
        EffectRegistry.Shared.TryGetSignature(type, out var signature) ? signature.References.FirstOrDefault(r => r.Parameter == parameter).Kind : null;

    private static void Effects(string type, params string[] parameters)
    {
        foreach (var parameter in parameters)
        {
            NodeOf(type, parameter, NodeShape.Effects);
        }
    }

    /// <summary>Effect lists that are the branches of a nesting operator, not moments (<see cref="NodeShape.Branch"/>).</summary>
    private static void Branches(string type, params string[] parameters)
    {
        foreach (var parameter in parameters)
        {
            NodeOf(type, parameter, NodeShape.Branch);
        }
    }

    /// <summary>A number the handler or its system reads with no parameter: an attribute the weave may state, or a rule.</summary>
    private static void Implicit(string type, string name, Dimension dimension, string unit, string? attribute = null, string? rule = null, double? @default = null, string? when = null) =>
        Register(new AxisBinding
        {
            Type = type,
            Parameter = name,
            Kind = ParameterKind.Axis,
            Dimension = dimension,
            Unit = unit,
            Attribute = attribute,
            Role = attribute is null ? AttributeRole.None : AttributeRole.Fallback,
            Rule = rule,
            Default = @default,
            When = when,
            Implicit = true,
        });

    private static void Phasing(string type) => Implicit(type, "phasing", Geometry, Switch, attribute: "phasing");

    /// <summary>What an effect spends by being in the graph, a word on economy: a corpse a summon raises or a weave consumes.</summary>
    private static void Spends(string type, string what, string? when = null) =>
        Register(new AxisBinding
        {
            Type = type,
            Parameter = "spends",
            Kind = ParameterKind.Choice,
            Dimension = Economy,
            Unit = Word,
            DefaultWord = what,
            When = when,
            Implicit = true,
        });

    // ------------------------------------------------------------ the shape registry's words

    /// <summary>
    /// What a delivery's sizes read of its shape, from the shape itself: whether it is anchored (it extends from
    /// the caster, so a melee weave's reach lengthens it), whether an angle or a width shapes it, and the share
    /// of the weave's <c>area.width</c> it takes where content writes no width (its default width at a unit area
    /// width less its default width at none: all of it for a line, a quarter for a bar, an arc or a wave, none
    /// for a band, whose width is its radius).
    /// </summary>
    private static void ShapeProperties()
    {
        static double? Of(string word, Func<Shape, double> property) => Shapes.TryGet(word, out var shape) ? property(shape) : null;
        Property("shapes", "anchored", w => Of(w, s => s.Anchored ? 1 : 0));
        Property("shapes", "angle", w => Of(w, s => s.HasAngle ? 1 : 0));
        Property("shapes", "width", w => Of(w, s => s.HasWidth ? 1 : 0));
        Property("shapes", "width_share", w => Of(w, s => s.HasWidth ? s.DefaultWidth(1, 1) - s.DefaultWidth(1, 0) : 0));
    }

    // ------------------------------------------------------------ DeliveryHandlers.cs

    private static void Deliveries()
    {
        // area: an angle shapes only the shapes it shapes, and a width only those with one, an unwritten width
        // taking the shape's share of the weave's area.width (EffectHelpers.SizeOf).
        Choice("area", "shape", Geometry, "circle", registry: "shapes");
        Axis("area", "radius", Geometry, Units, "area.radius", 2);
        Scaled("area", "angle", Geometry, Degrees, "area.angle", when: "shape:angle");
        Axis("area", "width", Geometry, Units, "area.width", when: "shape:width", fallbackWhen: "shape:width_share", factorOf: "shape:width_share");
        Axis("area", "inner", Geometry, Fraction);
        Choice("area", "filter", Targeting, "enemy", registry: "filters");
        Choice("area", "center", Geometry, "focus", new[] { "focus", "source", "point" });
        Axis("area", "max_targets", Targeting, Count);
        FlagOf("area", "include_center", Targeting, @default: true);
        Choice("area", "pick", Targeting, "random", registry: "picks");
        NodeOf("area", "require", NodeShape.Predicate, Targeting);
        Choice("area", "reach", Geometry, "center", new[] { "center", "edge" }, hidesNumber: true);
        Choice("area", "occlusion", Geometry, "blocked", Occlusions, hidesNumber: true);
        Effects("area", "effects");
        Implicit("area", "aftershocks", Geometry, Count, attribute: "area.scatter");
        Implicit("area", "aftershock_scale", Geometry, Fraction, rule: "delivery.aftershock_scale", @default: 0.5, when: "aftershocks");
        Implicit("area", "aftershock_delay", Timing, Seconds, rule: "delivery.aftershock_delay", @default: 0.15, when: "aftershocks");
        Implicit("area", "pulses", Timing, Count, attribute: "area.pulses");
        Implicit("area", "pulse_interval", Timing, Seconds, rule: "delivery.pulse_interval", @default: 0.4, when: "pulses");
        // A melee weave's reach lengthens only the shapes that extend from the caster.
        Implicit("area", "melee_reach", Geometry, Units, attribute: "melee.reach", when: "shape:anchored & #melee");
        Phasing("area");

        // projectile: a written duration bounds a flight, and an unwritten range then leaves it unbounded; an aimed
        // fan's spreads replace the spread.
        Axis("projectile", "count", Geometry, Count, "projectile.count", 1, name: "flights");
        Axis("projectile", "speed", Kinematics, UnitsPerSecond, "projectile.speed", 12);
        Axis("projectile", "range", Kinematics, Units, "range", 12, fallbackWhen: "!duration");
        Scaled("projectile", "spread", Geometry, Degrees, "projectile.spread", 15, when: "!spread_by_aim");
        Axis("projectile", "pierce", Kinematics, Count, "projectile.pierce", 0);
        Axis("projectile", "radius", Geometry, Units, @default: 0.5);
        Effects("projectile", "on_expire");
        Keys("projectile", null, "look");
        FlagOf("projectile", "gravity", Kinematics);
        Choice("projectile", "shape", Geometry, "circle", registry: "shapes");
        Axis("projectile", "shape_size", Geometry, Units);
        Axis("projectile", "shape_angle", Geometry, Degrees, when: "shape:angle");
        Axis("projectile", "shape_width", Geometry, Units, "area.width", when: "shape:width", fallbackWhen: "shape:width_share", factorOf: "shape:width_share");
        Choice("projectile", "fire", Geometry, "fan", new[] { "fan", "volley", "ring", "salvo" }, hidesNumber: true);
        Axis("projectile", "salvo_interval", Timing, Seconds, rule: "delivery.salvo_interval", @default: 0.15, when: "fire=salvo");
        Axis("projectile", "volley_spacing", Geometry, Units, @default: 0.6, when: "fire=volley");
        Axis("projectile", "ring_jitter", Geometry, Degrees, @default: 0, when: "fire=ring");
        FlagOf("projectile", "spread_by_aim", Geometry);
        // An aimed fan's near and far spreads replace the spread, and the weave's projectile.spread scales them as it does the spread.
        Scaled("projectile", "spread_by_aim.near", Geometry, Degrees, "projectile.spread", 15, when: "spread_by_aim");
        Scaled("projectile", "spread_by_aim.far", Geometry, Degrees, "projectile.spread", 15, when: "spread_by_aim");
        Axis("projectile", "spread_by_aim.range", Geometry, Units, @default: 10);
        Choice("projectile", "origin", Geometry, "caster", new[] { "caster", "point" }, hidesNumber: true);
        Axis("projectile", "origin_range", Geometry, Units, when: "origin=point");
        Axis("projectile", "duration", Timing, Seconds);
        Choice("projectile", "return", Kinematics, "none", new[] { "none", "origin", "caster" }, hidesNumber: true);
        Axis("projectile", "forks", Kinematics, Count, "projectile.forks", 0);
        Axis("projectile", "chains", Kinematics, Count, "projectile.chain", 0);
        // A rehit is the lockout before a flight may strike the same body again: targeting, as a field's strike-once is.
        Axis("projectile", "rehit", Targeting, Seconds, "projectile.rehit", 0);
        Axis("projectile", "bounce", Kinematics, Count, "projectile.bounce", 0);
        foreach (var turning in new[] { "homing", "guide", "erratic", "spiral", "orbit", "spin", "recurve", "converge" })
        {
            Axis("projectile", turning, Kinematics, DegreesPerSecond, "projectile." + turning, 0);
        }
        Axis("projectile", "wave", Kinematics, CyclesPerSecond, "projectile.wave", 0);
        Axis("projectile", "accel", Kinematics, UnitsPerSecondSquared, "projectile.accel", 0);
        Axis("projectile", "amplitude", Kinematics, Units, rule: "delivery.wave_amplitude", @default: 0.6, when: "wave");
        Axis("projectile", "orbit_radius", Kinematics, Units, rule: "delivery.orbit_radius", @default: 2.5, when: "orbit");
        Axis("projectile", "zigzag", Kinematics, Degrees);
        Axis("projectile", "zigzag_every", Kinematics, Units, @default: 2, when: "zigzag");
        Axis("projectile", "tick", Timing, Seconds, @default: 0.5);
        Axis("projectile", "travel", Kinematics, Units, @default: 2);
        Choice("projectile", "occlusion", Geometry, "blocked", Occlusions, hidesNumber: true);
        Effects("projectile", "on_hit", "on_first_hit", "on_end", "on_tick", "on_travel", "on_land", "on_return");
        // The flight system's rules: no glyph reaches them (rule 5 of the grammar names them as frozen axes).
        Implicit("projectile", "fork_children", Kinematics, Count, rule: "delivery.fork_children", @default: 2, when: "forks");
        Implicit("projectile", "fork_angle", Kinematics, Degrees, rule: "delivery.fork_angle", @default: 30, when: "forks");
        // How far a chain looks for its next body is a radius of acquisition, on targeting with the homing radius.
        Implicit("projectile", "chain_range", Targeting, Units, rule: "delivery.chain_range", @default: 8, when: "chains");
        Implicit("projectile", "homing_radius", Targeting, Units, rule: "delivery.homing_radius", @default: 10, when: "homing");
        Implicit("projectile", "homing_reacquire", Targeting, Seconds, rule: "delivery.homing_reacquire", @default: 0.25, when: "homing");
        Implicit("projectile", "bounce_restitution", Kinematics, Fraction, rule: "delivery.bounce_restitution", @default: 0.6, when: "bounce");
        Implicit("projectile", "return_arrive", Kinematics, Units, rule: "delivery.return_arrive", @default: 0.6, when: "return!=none");
        Implicit("projectile", "return_max_seconds", Timing, Seconds, rule: "delivery.return_max_seconds", @default: 6, when: "return!=none");
        Implicit("projectile", "min_speed", Kinematics, UnitsPerSecond, rule: "delivery.min_speed", @default: 0.5, when: "accel");
        Implicit("projectile", "gravity_pull", Kinematics, UnitsPerSecondSquared, rule: "move.gravity", @default: 24, when: "gravity");
        Phasing("projectile");

        // chain
        Axis("chain", "count", Kinematics, Count, "chain.count", 0, name: "chains");
        Axis("chain", "range", Targeting, Units, @default: 6, name: "chain_range");
        Choice("chain", "filter", Targeting, "enemy", registry: "filters");
        Choice("chain", "pick", Targeting, "nearest", registry: "picks");
        Effects("chain", "effects");

        // field
        Axis("field", "radius", Geometry, Units, "area.radius", 2);
        Axis("field", "duration", Timing, Seconds, "duration", 4);
        Axis("field", "interval", Timing, Seconds, @default: 0.5);
        Choice("field", "filter", Targeting, "enemy", registry: "filters");
        Keys("field", null, "look", "exclusive");
        FlagOf("field", "follow", Kinematics);
        Choice("field", "shape", Geometry, "circle", registry: "shapes");
        Scaled("field", "angle", Geometry, Degrees, "area.angle", when: "shape:angle");
        Axis("field", "width", Geometry, Units, "area.width", when: "shape:width", fallbackWhen: "shape:width_share", factorOf: "shape:width_share");
        Axis("field", "inner", Geometry, Fraction);
        Axis("field", "delay", Timing, Seconds);
        Axis("field", "exposure", Timing, Seconds);
        Axis("field", "pull", Kinematics, UnitsPerSecond);
        Axis("field", "drift", Kinematics, UnitsPerSecond);
        Axis("field", "seek", Kinematics, UnitsPerSecond);
        Axis("field", "rotate", Kinematics, DegreesPerSecond);
        FlagOf("field", "radius_curve", Geometry);
        Choice("field", "radius_curve.curve", Geometry, "linear", registry: "curves");
        Axis("field", "radius_curve.from", Geometry, Fraction, @default: 1);
        Axis("field", "radius_curve.to", Geometry, Fraction, @default: 1);
        FlagOf("field", "hit_once", Targeting);
        FlagOf("field", "rearm_on_exit", Targeting);
        NodeOf("field", "domain", NodeShape.Modifiers);
        Choice("field", "domain_filter", Targeting, null, registry: "filters");
        FlagOf("field", "segments", Geometry);
        Axis("field", "segments.count", Geometry, Count, @default: 1);
        Axis("field", "segments.spacing", Geometry, Units, @default: 2);
        Choice("field", "segments.axis", Geometry, "along", new[] { "along", "across" });
        Axis("field", "pulses", Timing, Count);
        Choice("field", "occlusion", Geometry, "blocked", Occlusions, hidesNumber: true);
        Effects("field", "pulse", "on_tick", "on_land", "on_enter", "on_exit", "on_end");
        Implicit("field", "seek_radius", Targeting, Units, rule: "delivery.seek_radius", @default: 8, when: "seek");
        Phasing("field");

        // aura
        Axis("aura", "radius", Geometry, Units, "area.radius", 2);
        // A toggled aura with no written duration lasts until it is turned off; a written one is read either way.
        Axis("aura", "duration", Timing, Seconds, "duration", 4, fallbackWhen: "toggle=false");
        Axis("aura", "interval", Timing, Seconds, @default: 1);
        Choice("aura", "filter", Targeting, "enemy", registry: "filters");
        NodeOf("aura", "domain", NodeShape.Modifiers);
        Choice("aura", "domain_filter", Targeting, "ally_or_self", registry: "filters");
        Keys("aura", null, "key", "look");
        FlagOf("aura", "toggle", Timing);
        Choice("aura", "occlusion", Geometry, "blocked", Occlusions, hidesNumber: true);
        Effects("aura", "pulse", "on_tick", "on_enter", "on_exit", "on_end");
        Phasing("aura");
    }

    // ------------------------------------------------------------ PlacementHandlers.cs

    private static void Placements()
    {
        // scatter
        Axis("scatter", "count", Geometry, Count, "area.strikes", 1, name: "strikes");
        Axis("scatter", "radius", Geometry, Units, "area.radius", 4, factor: 2);
        Axis("scatter", "interval", Timing, Seconds, @default: 0);
        Choice("scatter", "at", Targeting, "random", new[] { "random", "enemies" }, hidesNumber: true);
        Axis("scatter", "jitter", Geometry, Units, @default: 0);
        Axis("scatter", "telegraph", Timing, Seconds, @default: 0);
        Axis("scatter", "telegraph_radius", Geometry, Units, "area.radius", 2);
        Choice("scatter", "filter", Targeting, "enemy", registry: "filters");
        Effects("scatter", "effects");

        // cascade
        Axis("cascade", "count", Geometry, Count, "area.cascade", 0, name: "placements");
        Choice("cascade", "dir", Geometry, "forward", new[] { "forward", "backward", "axis", "out", "random" }, hidesNumber: true);
        Axis("cascade", "step", Geometry, Units, "area.radius", 3, factor: 1.5);
        Axis("cascade", "scale_step", Geometry, Fraction, @default: 0);
        Axis("cascade", "damage_step", Payload, Fraction, @default: 0);
        Axis("cascade", "interval", Timing, Seconds, @default: 0.15);
        Axis("cascade", "interval_step", Timing, Seconds, @default: 0);
        Effects("cascade", "effects");

        // tether
        Choice("tether", "link", Targeting, "target", new[] { "target", "point", "owner" });
        Axis("tether", "width", Geometry, Units, @default: 1);
        Axis("tether", "duration", Timing, Seconds, "duration", 4);
        Axis("tether", "interval", Timing, Seconds, @default: 0.5);
        Choice("tether", "filter", Targeting, "enemy", registry: "filters");
        Axis("tether", "max_length", Geometry, Units);
        Keys("tether", null, "look");
        Effects("tether", "pulse", "on_end");

        // aim
        Axis("aim", "offset", Geometry, Degrees, @default: 0);
        Scaled("aim", "spread", Geometry, Degrees, "aim.spread", 0);
        Axis("aim", "sequence", Geometry, Degrees);
        Effects("aim", "effects");

        // detonate
        Choice("detonate", "tags", Targeting);
        Choice("detonate", "pick", Targeting, "all", new[] { "all", "nearest" }, hidesNumber: true);
        Axis("detonate", "radius", Geometry, Units);
        Effects("detonate", "effects");
    }

    // ------------------------------------------------------------ MovementHandlers.cs

    private static void Movement()
    {
        var shoves = "kind=knockback|pull|buffet";
        Choice("move", "kind", Kinematics, "teleport", new[] { "teleport", "dash", "leap", "blink", "recall", "knockback", "pull", "buffet" }, hidesNumber: true);
        Scaled("move", "distance", Kinematics, Units, "knockback", 4, when: "kind=knockback|buffet");
        Axis("move", "distance", Kinematics, Units, @default: 4, when: "kind!=knockback|buffet");
        Choice("move", "who", Targeting, null, new[] { "source", "target" });
        Choice("move", "anchor", Geometry, "from", new[] { "from", "caster", "point" });
        Reference("move", "status", "statuses", readBy: "statuses");
        Axis("move", "speed", Kinematics, UnitsPerSecond);
        Axis("move", "width", Geometry, Units, @default: 1.5);
        Choice("move", "filter", Targeting, "enemy", registry: "filters");
        FlagOf("move", "phase", Kinematics);
        Axis("move", "air_time", Timing, Seconds, @default: 0.6, when: "kind=leap");
        Axis("move", "arc", Kinematics, Units, @default: 2, when: "kind=leap");
        Axis("move", "delay", Timing, Seconds, when: "kind=blink");
        FlagOf("move", "behind", Geometry);
        Choice("move", "mark", null, "mark", readBy: "statuses");
        Axis("move", "travel", Kinematics, Units, @default: 2);
        FlagOf("move", "telegraph", Timing);
        Effects("move", "on_collide", "on_contact", "on_travel", "on_land", "on_end");
        Implicit("move", "impulse_decay", Kinematics, PerSecond, rule: "delivery.impulse_decay", @default: 8, when: shoves);
        Implicit("move", "impulse_min", Kinematics, UnitsPerSecond, rule: "delivery.impulse_min", @default: 0.3, when: shoves);
        Implicit("move", "collision_speed", Kinematics, UnitsPerSecond, rule: "delivery.collision_speed", @default: 2, when: shoves);

        Choice("mark", "key", null, "mark", readBy: "statuses");
        Choice("mark", "at", Geometry, "caster", new[] { "caster", "point" });
    }

    // ------------------------------------------------------------ DamageHandlers.cs

    private static void Payloads()
    {
        // Every amount passes through the attacker's damage stack, the weave's modifiers joining, in the damage pipeline;
        // per-Thread amounts alone mean no split amount, so the default ten stands only where no amounts are written.
        Channel("damage", "amount", Payload, Resource, "damage", 10, fallbackWhen: "!amounts");
        Channel("damage", "amounts.*", Payload, Resource, "damage", name: "amount.*");
        Choice("damage", "threads", Payload, registry: "threads");
        Choice("damage", "tags", Payload);
        FlagOf("damage", "can_crit", Payload, @default: true);
        // A written effectiveness scales the amount and the attacker's added damage. Unwritten, the amount is as written
        // and only the added damage takes the weave's damage.effectiveness (times added.effectiveness, DamagePipeline.AddedEffectiveness).
        Register(new AxisBinding
        {
            Type = "damage", Parameter = "effectiveness", Kind = ParameterKind.Axis,
            Dimension = Payload, Unit = Fraction, Attribute = "damage.effectiveness", Role = AttributeRole.Fallback,
            WrittenOnly = true, WrittenDetail = "shadows damage.effectiveness for added damage; also scales the written amount",
        });
        Implicit("damage", "added_effectiveness", Payload, Fraction, attribute: "damage.effectiveness", when: "!effectiveness");
        FlagOf("damage", "cannot_kill", Payload);
        Axis("damage", "leech", Payload, Fraction);
        Effects("damage", "on_kill");

        Channel("heal", "amount", Payload, Resource, "healing", 10);
        Choice("heal", "resource", Payload, "life", registry: "resources");
        Choice("heal", "tags", Payload);
        FlagOf("heal", "self", Targeting);
        FlagOf("heal", "exclude_self", Targeting);

        Axis("resource", "amount", Payload, Resource, @default: 10);
        Choice("resource", "resource", Payload, "mana", registry: "resources");
        FlagOf("resource", "self", Targeting);
    }

    // ------------------------------------------------------------ StatusHandlers.cs, ChargeHandlers.cs, StatusTransferHandlers.cs

    private static void Statuses()
    {
        Reference("apply_status", "status", readBy: "statuses");
        // The status service scales a duration by the applier's effect.duration, read on the applier's own stack without
        // the weave's modifiers, and a harmful one by the bearer's status.resistance, a defense as resistances are to damage.
        // A fixed duration replaces the duration and nothing scales it: a second door onto the duration, its number frozen.
        Scaled("apply_status", "duration", Payload, Seconds, "effect.duration", when: "!fixed_duration", fold: AttributeFold.Caster);
        Axis("apply_status", "fixed_duration", Payload, Seconds, name: "duration");
        Axis("apply_status", "chance", Payload, Fraction, @default: 1);
        Axis("apply_status", "stacks", Payload, Count, @default: 1);
        Axis("apply_status", "magnitude", Payload, Resource);
        Choice("apply_status", "threads", Payload, registry: "threads");
        FlagOf("apply_status", "self", Targeting);
        Choice("apply_status", "to", Targeting, "target", new[] { "target", "self", "minions", "allies" });
        Axis("apply_status", "radius", Geometry, Units, @default: 8, when: "to=allies");
        Axis("apply_status", "max_targets", Targeting, Count);
        Effects("apply_status", StatusHandlers.OnStatusTick, StatusHandlers.OnStatusExpire);

        Reference("remove_status", "status", readBy: "statuses");
        Keys("remove_status", "statuses", "tags");
        Axis("remove_status", "count", Payload, Count, name: "removals");
        FlagOf("remove_status", "self", Targeting);
        Choice("remove_status", "order", Targeting, "newest", new[] { "newest", "oldest" });
        Choice("remove_status", "kind", Targeting, "harmful", StatusKinds);

        Choice("modify", "attribute", null, registry: "attributes", readBy: "statuses");
        Choice("modify", "form", Payload, "flat", new[] { "flat", "increased", "more", "override", "link" });
        Axis("modify", "value", Payload, Value, @default: 0);
        Axis("modify", "duration", Payload, Seconds, @default: 5);
        Choice("modify", "scope", Payload);
        FlagOf("modify", "self", Targeting);
        Keys("modify", null, "key");

        Reference("charge", "id", readBy: "statuses");
        Axis("charge", "gain", Economy, Count, name: "charge.gain");
        // A written max replaces the holder's own charge.{id}.max (ChargeService.Gain), read on the holder's stack.
        Axis("charge", "max", Economy, Count, "charge.{id}.max", name: "charge.max", fold: AttributeFold.Bearer);
        Axis("charge", "spend", Economy, Count, name: "charge.spend");
        FlagOf("charge", "all", Economy, name: "charge.all");
        Axis("charge", "drain", Economy, CountPerSecond, name: "charge.drain");
        FlagOf("charge", "self", Targeting, @default: true);

        foreach (var transfer in new[] { "consume_status", "spread_status", "move_status" })
        {
            Reference(transfer, "status", "statuses", readBy: "statuses");
            Reference(transfer, "statuses", "statuses", readBy: "statuses");
            Keys(transfer, "statuses", "tags");
            Choice(transfer, "kind", Targeting, "harmful", StatusKinds);
        }
        Axis("consume_status", "count", Economy, Count, name: "stacks_spent");
        Keys("consume_status", null, "as");
        FlagOf("consume_status", "self", Targeting);

        Axis("spread_status", "radius", Geometry, Units, @default: 4);
        Axis("spread_status", "strength", Payload, Fraction, @default: 1);
        Choice("spread_status", "duration", Payload, "remaining", new[] { "remaining", "refresh" });
        Axis("spread_status", "max_targets", Targeting, Count);
        Choice("spread_status", "filter", Targeting, "allies", Sides);

        Choice("move_status", "from", Targeting, "target", new[] { "target", "self", "area" });
        Choice("move_status", "to", Targeting, "self", new[] { "self", "target" });
        Axis("move_status", "radius", Geometry, Units, @default: 6);
        Choice("move_status", "filter", Targeting, "allies", Sides);
        Axis("move_status", "count", Payload, Count, name: "statuses_moved");
        Axis("move_status", "splash", Payload, Fraction);

        Reference("rupture", "status", readBy: "statuses");
        Axis("rupture", "fraction", Payload, Fraction, @default: 1);
        Axis("rupture", "scale", Payload, Fraction, @default: 1);
        FlagOf("rupture", "self", Targeting);
    }

    // ------------------------------------------------------------ SummonHandlers.cs

    private static void Summons()
    {
        foreach (var type in new[] { "summon", "deploy" })
        {
            Reference(type, "pattern", "patterns", Agency, "summons", type);
            Reference(type, "pool", "patterns", Agency, "summons", type);
            Keys(type, null, "group", "pool_group", "exclusive_group");
            Axis(type, "count", Agency, Count, "summon.count", name: "bodies");
            Axis(type, "max_active", Agency, Count, "summon.max");
            NodeOf(type, "place", NodeShape.Block);
            Choice(type, "place.at", Geometry, "focus", new[] { "focus", "caster", "point", "target" });
            Axis(type, "place.range", Geometry, Units);
            Choice(type, "place.shape", Geometry, "around", new[] { "around", "scatter", "line", "circle" });
            Axis(type, "place.radius", Geometry, Units);
            Axis(type, "place.scatter", Geometry, Units, name: "place.radius");
            Axis(type, "place.spacing", Geometry, Units);
            Reference(type, "behavior", "behaviors", Agency);
            FlagOf(type, "inherit", Agency, @default: type == "summon");
            NodeOf(type, "decay", NodeShape.Block);
            Axis(type, "decay.delay", Agency, Seconds, @default: 0);
            Axis(type, "decay.fraction", Agency, Fraction, @default: 0.05);
            Axis(type, "decay.growth", Agency, Fraction, @default: 1.1);
            NodeOf(type, "presence", NodeShape.Modifiers);
            Effects(type, SummonHandlers.OnBirth, SummonHandlers.OnDeath);
        }
        Axis("summon", "duration", Agency, Seconds, "duration", name: "lifetime");
        NodeOf("summon", "contract", NodeShape.Block);
        Axis("summon", "contract.reserve.*", Economy, Resource, name: "reserve.*");
        Axis("summon", "contract.respawn", Timing, Seconds, @default: 8, name: "respawn");
        FlagOf("summon", "contract.toggle", Economy, @default: true);
        NodeOf("summon", "waves", NodeShape.Block);
        Axis("summon", "waves.count", Agency, Count, @default: 1);
        Axis("summon", "waves.interval", Timing, Seconds, @default: 1);
        Axis("summon", "waves.cost_factor", Economy, Fraction);
        NodeOf("summon", "replenish", NodeShape.Block);
        Axis("summon", "replenish.interval", Agency, Seconds, @default: 4);
        Choice("summon", "from_corpse", Agency, null, new[] { "nearest", "target" });
        Axis("summon", "search_radius", Targeting, Units, rule: "minions.corpse_search", @default: 6, when: "from_corpse");
        Spends("summon", "corpse", when: "from_corpse");

        Axis("deploy", "lifetime", Agency, Seconds, "duration");
        FlagOf("deploy", "inherit_caster", Agency);
        NodeOf("deploy", "intercept", NodeShape.Block);
        Axis("deploy", "intercept.radius", Geometry, Units, @default: 3);
        Choice("deploy", "intercept.mode", Kinematics, "dissolve", new[] { "dissolve", "deflect", "slow" }, hidesNumber: true);
        Axis("deploy", "intercept.slow", Kinematics, Fraction, @default: 0.35);

        Reference("spawn", "pattern", dimension: Agency, readBy: "summons", name: "spawn");
        Axis("spawn", "count", Agency, Count, @default: 1, name: "bodies");
        Axis("spawn", "lifetime", Agency, Seconds);
        FlagOf("spawn", "as_corpse", Agency);

        Axis("gather_corpses", "radius", Geometry, Units, @default: 8);
        Axis("consume_corpse", "radius", Targeting, Units, rule: "minions.corpse_target_reach", @default: 0.5);
        Spends("consume_corpse", "corpse");
    }

    // ------------------------------------------------------------ MinionHandlers.cs, AiHandlers.cs

    private static void Minions()
    {
        Axis("recall_minions", "radius", Geometry, Units, rule: "minions.recall_radius", @default: 1.5);
        Keys("recall_minions", null, "group");

        Choice("command_minions", "command", Agency, "assault", registry: "commands");
        Axis("command_minions", "duration", Agency, Seconds);
        Choice("command_minions", "affects", Agency, "minions", new[] { "minions", "squad", "both" });
        Axis("command_minions", "radius", Targeting, Units, rule: "minions.earshot", @default: 21, when: "affects=squad|both");
        Axis("command_minions", "discipline", Agency, Fraction);
        Keys("command_minions", null, "group");

        Reference("minion_cast", "weave", "weaves", Agency, "summons", "minion_cast");
        Choice("minion_cast", "at", Targeting, "point", new[] { "point", "enemy" });
        Axis("minion_cast", "range", Targeting, Units, rule: "minions.cast_range", @default: 11);
        Keys("minion_cast", null, "group");

        Axis("consume_minion", "count", Economy, Count, @default: 1, name: "minions_spent");
        Choice("consume_minion", "pick", Targeting, "oldest", new[] { "oldest", "newest", "nearest", "target" });
        Keys("consume_minion", null, "group");
        Axis("consume_minion", "radius", Geometry, Units);
        FlagOf("consume_minion", "self", Targeting);
        Choice("consume_minion", "of", Agency, "minions", new[] { "minions", "constructs", "any" });
        Effects("consume_minion", "effects");

        Axis("taunt", "duration", Targeting, Seconds, @default: 4);
    }

    // ------------------------------------------------------------ FlowHandlers.cs

    private static void Flow()
    {
        NodeOf("condition", "if", NodeShape.Predicate, Causality, name: "condition");
        Branches("condition", "then", "else");
        Branches("random", "choices");
        Axis("random", "choices[].weight", Causality, Weight, @default: 1, name: "weight");
        Axis("repeat", "times", Causality, Count, @default: 3, name: "repeat");
        Axis("repeat", "interval", Timing, Seconds, @default: 0.3);
        Effects("repeat", "effects");
        Effects("sequence", "effects");
    }

    // ------------------------------------------------------------ CastHandlers.cs

    private static void Casting()
    {
        Reference("cast", "weave", "weaves", Causality, "triggers", "sequel");
        Choice("cast", "at", Targeting, "point", new[] { "point", "target", "caster" });
        Choice("cast", "aim", Geometry, "onward", new[] { "onward", "out", "random", "rotating" }, hidesNumber: true);
        Axis("cast", "turn", Geometry, Degrees, @default: 45, when: "aim=rotating");
        Axis("cast", "level", Causality, Count);
        Axis("cast", "damage_scale", Payload, Fraction, @default: 1);

        Axis("delay", "seconds", Timing, Seconds, @default: 1, name: "delay");
        Effects("delay", "effects");

        Axis("fuse", "delay", Timing, Seconds, "fuse.delay", 1);
        Axis("fuse", "power", Payload, Fraction, "fuse.power", 0);
        Effects("fuse", "effects");

        Axis("cooldown", "seconds", Economy, Seconds, name: "cooldown_cut");
        Axis("cooldown", "fraction", Economy, Fraction, name: "cooldown_cut_fraction");
        Choice("cooldown", "scope", Economy, name: "cooldown_cut_scope");

        Axis("restore_uses", "amount", Economy, Count, name: "restore_uses");
        Choice("restore_uses", "scope", Economy, "host", new[] { "host", "all", "still" }, name: "restore_uses_scope");

        Axis("guard", "amount", Economy, Resource, name: "guard_surge");
        FlagOf("guard", "self", Targeting);
    }

    // ------------------------------------------------------------ UtilityHandlers.cs

    private static void Utility()
    {
        Keys("event", null, "name", "data");
        Reference("grant_item", "item", "items", Payload);
        Reference("grant_item", "table", "loot", Payload);
        Choice("grant_item", "rarity", Payload, registry: "item_rarities");
        Axis("grant_item", "count", Payload, Count, @default: 1, name: "items");
        Axis("grant_xp", "amount", Payload, Count, @default: 10, name: "xp");
        Keys("log", null, "text");
    }

    // ------------------------------------------------------------ the weave as a whole

    /// <summary>
    /// Attributes the runtime reads off the weave itself, not through a parameter: the bases content formulas
    /// name, the cast-layer reads (mobility, fizzles, reflexes), the repeats of a use and the rate of a bank. The
    /// casting reader places those a weave states; the ones another value already folds are declared unreported.
    /// </summary>
    private static void WeaveLevel()
    {
        RegisterAttribute("base_damage", Payload, Resource);
        RegisterAttribute("base_heal", Payload, Resource);
        RegisterAttribute("cast.mobility", Timing, Fraction);
        RegisterAttribute("cast.fizzle_chance", Causality, Fraction);
        RegisterAttribute("reflex", Timing, Switch);
        RegisterAttribute("repeat.count", Causality, Count);
        RegisterAttribute("repeat.seals", Causality, Count);
        RegisterAttribute("repeat.seal_time", Timing, Seconds);
        RegisterAttribute("repeat.interval", Timing, Seconds);
        RegisterAttribute("repeat.scale", Causality, Fraction);
        RegisterAttribute("repeat.retarget", Targeting, Switch);
        RegisterAttribute("repeat.lock", Timing, Switch);
        RegisterAttribute("uses.rate", Economy, Fraction);
        RegisterAttribute("range", Targeting, Units, reported: false);
        RegisterAttribute("cooldown.added", Economy, Seconds, reported: false);
        RegisterAttribute("uses.max", Economy, Count, reported: false);
        RegisterAttribute("trigger.chance", Causality, Fraction, reported: false);
        RegisterAttribute("overcharge.stages", Timing, Count, reported: false);
        RegisterAttribute("guard.strength", Economy, Fraction, reported: false);
        RegisterAttribute("cost", Economy, Fraction, reported: false);
    }

    /// <summary>The dimension each mechanism probe speaks to; a probe a package registers declares its own here.</summary>
    private static void MechanismDimensions()
    {
        var map = new (Dimension Dimension, string[] Names)[]
        {
            (Geometry, new[] { "area", "surface", "shape", "storm", "cascade", "tether", "fire" }),
            (Kinematics, new[] { "projectile", "flight", "returning", "homing", "trajectory", "chain", "displaces", "shove", "leap", "dash", "landing" }),
            (Payload, new[] { "strikes", "affliction", "status", "leech" }),
            (Economy, new[] { "cooldown", "uses", "bankless", "spends" }),
            (Timing, new[] { "channel", "held", "guard", "overcharge", "timing", "release", "ramps", "toggle", "telegraph", "engagement" }),
            (Agency, new[] { "summon", "minion", "construct", "periodic_construct", "contract", "pool", "corpse", "orders", "unlife" }),
            (Causality, new[] { "triggered", "repeats", "convert", "meta" }),
            (Coupling, new[] { "charges", "rupture", "consumes", "contagion", "bank" }),
        };
        foreach (var (dimension, names) in map)
        {
            foreach (var name in names)
            {
                RegisterMechanism(name, dimension);
            }
        }
    }
}
