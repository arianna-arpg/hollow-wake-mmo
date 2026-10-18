using System.Text.Json.Nodes;
using HollowWake.Core.Definitions;
using HollowWake.Core.Effects;
using HollowWake.Core.Weaves;
using Xunit;

namespace HollowWake.Core.Tests.Grammar;

/// <summary>
/// The engine's handler signatures held at their baseline against the combat grammar (docs/design/combat-grammar.md):
/// the flags not yet resolved, the effect lists no signature declares as a hook, the parameter names that mean
/// different things on different handlers, and the axes reached through doors that differ. Each baseline shrinks with
/// the engine: an item resolved is removed from its list, and a new one fails its test. The declarations read are
/// <see cref="AxisBindings"/>, the interim form of what each <see cref="EffectSignature"/> will declare.
/// </summary>
public class SignatureGrammarTests
{
    /// <summary>
    /// Every parameter declared a flag: a bool, or a bespoke block, that the grammar resolves into an axis, a choice or
    /// a node. A flag resolved is removed from this list; a new flag fails the test.
    /// </summary>
    private static readonly string[] Flags =
    {
        "apply_status.fixed_duration",
        "apply_status.self",
        "area.include_center",
        "aura.toggle",
        "charge.all",
        "charge.self",
        "consume_minion.self",
        "consume_status.self",
        "damage.can_crit",
        "damage.cannot_kill",
        "deploy.inherit",
        "deploy.inherit_caster",
        "field.follow",
        "field.hit_once",
        "field.radius_curve",
        "field.rearm_on_exit",
        "field.segments",
        "guard.self",
        "heal.exclude_self",
        "heal.self",
        "modify.self",
        "move.behind",
        "move.phase",
        "move.telegraph",
        "projectile.gravity",
        "projectile.spread_by_aim",
        "remove_status.self",
        "resource.self",
        "rupture.self",
        "spawn.as_corpse",
        "summon.contract.toggle",
        "summon.inherit",
    };

    /// <summary>
    /// Effect lists at moments no signature declares as a hook, so no glyph can ride them (a wrapper's inner list is
    /// the node it wraps, not a moment, and is not counted). A list declared a hook is removed from this list; a new
    /// plain list fails the test.
    /// </summary>
    private static readonly string[] PlainLists =
    {
        "condition.else",
        "condition.then",
        "consume_minion.effects",
        "detonate.effects",
        "projectile.on_expire",
        "random.choices",
        "scatter.effects",
    };

    /// <summary>
    /// Parameter names bound on more than one handler that differ between them in kind, unit, the attribute behind
    /// them, vocabulary or default: one name for two mechanisms, or one mechanism behind two doors (rule 1 of the
    /// grammar: a name, a unit and a default). A name made to mean one thing is removed from this list; a new one
    /// fails the test.
    /// </summary>
    private static readonly string[] SplitNames =
    {
        "amount",
        "at",
        "count",
        "delay",
        "domain_filter",
        "duration",
        "filter",
        "fraction",
        "inherit",
        "interval",
        "key",
        "kind",
        "lifetime",
        "pick",
        "radius",
        "range",
        "resource",
        "scope",
        "seconds",
        "self",
        "speed",
        "spread",
        "telegraph",
        "to",
        "width",
    };

    /// <summary>
    /// Axes of a dimension, by name, that the bindings reach through more than one door (a parameter of a handler,
    /// or a number a handler reads with no parameter) where the doors differ in kind, unit, the attribute behind
    /// them and how it folds, rule, vocabulary or default: rule 1 of the grammar seen from the axis, so a chain
    /// count behind a flight's chains and a chain's count, or a chain range a rule and a default give, are one
    /// axis behind two doors. An axis made one door is removed from this list; a new one fails the test.
    /// </summary>
    private static readonly string[] SplitAxes =
    {
        "agency.bodies",
        "agency.inherit",
        "agency.lifetime",
        "geometry.chain_range",
        "geometry.radius",
        "geometry.spread",
        "geometry.width",
        "kinematics.chains",
        "kinematics.speed",
        "payload.amount",
        "payload.duration",
        "payload.resource",
        "targeting.at",
        "targeting.domain_filter",
        "targeting.filter",
        "targeting.pick",
        "targeting.radius",
        "targeting.self",
        "targeting.to",
        "timing.delay",
        "timing.duration",
        "timing.interval",
        "timing.pulses",
        "timing.telegraph",
    };

    [Fact]
    public void TheParametersBoundAsFlagsAreTheBaseline()
    {
        var flags = AxisBindings.All.Where(b => b.Kind == ParameterKind.Flag).Select(b => $"{b.Type}.{b.Parameter}");
        Baseline("flags", Flags, flags);
    }

    [Fact]
    public void TheEffectListsNoSignatureDeclaresAsAHookAreTheBaseline()
    {
        var effects = EffectRegistry.Shared;
        var lists = new List<string>();
        foreach (var binding in AxisBindings.All.Where(b => b.Kind == ParameterKind.Node && b.Node == NodeShape.Effects))
        {
            Assert.True(effects.TryGetSignature(binding.Type, out var signature), $"no handler '{binding.Type}'");
            if (!signature.Hooks.Values.Contains(binding.Parameter) && signature.Wraps != binding.Parameter)
            {
                lists.Add($"{binding.Type}.{binding.Parameter}");
            }
        }

        // The declarations agree with the content: every parameter a core graph fills with effects is declared an effect list.
        var content = CoreContent.Registry;
        var compiler = new WeaveCompiler(content, effects);
        var threads = content.Each<ThreadDef>().Select(t => t.Id).OrderBy(t => t, StringComparer.Ordinal).ToList();
        var specs = content.Each<WeaveDef>().Select(p => new WeaveSpec { Preset = p.Id })
            .Concat(content.Each<FormDef>().Select(f => new WeaveSpec { Form = f.Id, Threads = threads.Take(f.MinThreads).ToList() }));
        foreach (var spec in specs)
        {
            foreach (var root in compiler.Compile(spec, GlyphFit.Dormant).Specs)
            {
                CheckLists(WeaveGraph.ToNode(root), effects);
            }
        }
        Baseline("plain effect lists", PlainLists, lists);
    }

    [Fact]
    public void AParameterNameMeansOneThingAcrossHandlersBarTheBaseline()
    {
        var split = AxisBindings.All
            .Where(b => !b.Implicit)
            .GroupBy(b => b.Parameter)
            .Where(g => g.Select(b => b.Type).Distinct().Count() > 1 && g.Select(Meaning).Distinct().Count() > 1)
            .Select(g => g.Key);
        Baseline("parameter names split across handlers", SplitNames, split);
    }

    [Fact]
    public void AnAxisHasOneDoorBarTheBaseline()
    {
        // Every door onto one axis of a dimension (each parameter, or number read with no parameter, that the bindings
        // place under the axis's name), implicit doors included: two doors that differ in what they are is one
        // mechanism behind two doors, or two mechanisms under one name. A door with conditions on its kind
        // (a shove's distance against a dash's) is one door.
        var split = AxisBindings.All
            .Where(b => b.Dimension is not null)
            .GroupBy(b => $"{AxisValue.DimensionName(b.Dimension!.Value)}.{b.Axis}")
            .Where(g => g.GroupBy(b => $"{b.Type}.{b.Parameter}").Select(door => string.Join(" / ", door.Select(Meaning))).Distinct().Count() > 1)
            .Select(g => g.Key);
        Baseline("axes split across doors", SplitAxes, split);
    }

    /// <summary>Asserts a set equals its baseline, naming what is new and what was resolved.</summary>
    private static void Baseline(string what, string[] baseline, IEnumerable<string> actual)
    {
        var now = actual.Distinct().OrderBy(s => s, StringComparer.Ordinal).ToArray();
        var added = now.Except(baseline).ToArray();
        var gone = baseline.Except(now).ToArray();
        Assert.True(added.Length == 0 && gone.Length == 0,
            $"{what}: new, not in the baseline: [{string.Join(", ", added)}]; resolved, to remove from the baseline: [{string.Join(", ", gone)}]");
        Assert.Equal(baseline.OrderBy(s => s, StringComparer.Ordinal), baseline);
    }

    /// <summary>
    /// What a binding says a parameter is: its kind, unit, the attribute behind it, how it meets the parameter and
    /// on whose stack, its factors, the rule read in its place, its vocabulary and its default.
    /// </summary>
    private static string Meaning(AxisBinding b)
    {
        var words = AxisBindings.WordsOf(b) is { } list ? string.Join(",", list.OrderBy(w => w, StringComparer.Ordinal)) : "";
        return $"{b.Kind}|{b.Unit}|{b.Attribute}|{b.Role}|{b.Fold}|{b.Factor}|{b.FactorOf}|{b.Rule}|{words}|{b.Default}|{b.DefaultWord}";
    }

    /// <summary>Asserts that each parameter of a node holding effects is declared an effect list, at every depth.</summary>
    private static void CheckLists(JsonObject node, EffectRegistry effects)
    {
        var type = WeaveGraph.TypeOf(node)!;
        foreach (var (parameter, value) in node)
        {
            if (parameter == "type" || value is null)
            {
                continue;
            }
            var nested = Effects(value, effects).ToList();
            if (nested.Count == 0)
            {
                continue;
            }
            Assert.True(AxisBindings.Of(type, parameter).Any(b => b.Kind == ParameterKind.Node && b.Node == NodeShape.Effects),
                $"{type}.{parameter} holds effects but is not declared an effect list");
            foreach (var child in nested)
            {
                CheckLists(child, effects);
            }
        }
    }

    /// <summary>The effect nodes a value holds: in a list, or in the lists of the data objects a list holds.</summary>
    private static IEnumerable<JsonObject> Effects(JsonNode value, EffectRegistry effects)
    {
        if (value is not JsonArray list)
        {
            yield break;
        }
        foreach (var item in list.OfType<JsonObject>())
        {
            if (WeaveGraph.TypeOf(item) is { } type && effects.Has(type))
            {
                yield return item;
                continue;
            }
            foreach (var (_, inner) in item)
            {
                if (inner is not null)
                {
                    foreach (var effect in Effects(inner, effects))
                    {
                        yield return effect;
                    }
                }
            }
        }
    }
}
