using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using HollowWake.Core.Content;
using HollowWake.Core.Effects;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>
/// The composition lint names what it was built for: a scratch layer beside core plants one of each content smell
/// (ids start with <c>t_lint_</c>), a scratch source tree plants string switches and form reads, and a run over core
/// and the real source groups its report by check.
/// </summary>
public class CompositionLintTests
{
    private const string Layer = """
kind: forms
entries:
  - { id: t_lint_shell, targeting: enemy, cast_mode: instant, min_threads: 0, drop_weight: 0, mechanisms: [projectile], attributes: { range: 30 }, effects: [] }
  - { id: t_lint_orphan, targeting: enemy, cast_mode: instant, min_threads: 0, drop_weight: 0, mechanisms: [projectile], attributes: { range: 30 }, effects: [] }
  - { id: t_lint_codex, targeting: self, cast_mode: instant, min_threads: 0, effects: [ { type: heal, amount: 1 } ] }

---
kind: weaves
entries:
  - id: t_lint_frozen
    form: t_lint_shell
    effects:
      - type: projectile
        count: 3
        speed: "weave.projectile.speed * 2"
        on_hit: [ { type: damage, amount: 10, threads: [mind], can_crit: false } ]
  - { id: t_lint_codex_preset, form: t_lint_codex }

---
kind: statuses
entries:
  - { id: t_lint_copy_a, duration: 2, on_apply: [ { type: heal, amount: "level * 1.37 + 0.11" } ] }
  - { id: t_lint_copy_b, duration: 2, on_apply: [ { type: heal, amount: "level*1.37+0.11" } ] }
  - { id: t_lint_copy_c, duration: 2, on_apply: [ { type: heal, amount: "level * 1.37 + 0.11" } ] }
  - { id: t_lint_copy_d, duration: 2, on_apply: [ { type: heal, amount: "level  *  1.37 +  0.11" } ] }
  - { id: t_lint_copy_e, duration: 2, on_apply: [ { type: heal, amount: "level * 1.37 + 0.11" } ] }
  - { id: t_lint_charger, duration: 2, on_apply: [ { type: charge, id: fury, gain: 1, max: 3 } ] }

---
kind: glyphs
entries:
  - id: t_lint_patcher
    patches:
      - { type: projectile, set: { count: 4 } }
      - { type: damage, wrap: { type: fuse, delay: 0.5, power: 0.1 } }
  - id: t_lint_hooker
    hooks:
      on_kill: [ { type: heal, amount: 1 } ]

---
kind: world_events
entries:
  - { id: t_lint_event_a, on_start: [ { type: t_lint_probe, used: 1, shared: 1 }, { type: t_lint_solo, alpha: 1, beta: 2 } ] }
  - { id: t_lint_event_b, on_start: [ { type: t_lint_probe, shared: 2 } ] }
""";

    private static readonly Lazy<ContentRegistry> Loaded = new(() =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "hollowwake-lint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "lint-tests.yaml"), Layer.Replace("\r\n", "\n"));
            var registry = ContentRegistry.Load(new[] { CoreContent.FindContentDir(), dir }, out var report);
            Assert.False(report.HasErrors, report.ToString());
            return registry;
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    });

    // The shared effects plus test types only the layer's world events use (their lists are not validated against the
    // shared registry): t_lint_probe, which two events use, one writing a parameter the other does not and neither
    // writing a third; t_lint_solo, which one event alone uses; and t_lint_idle, which nothing uses.
    private static readonly Lazy<EffectRegistry> Effects = new(() =>
    {
        var effects = EffectRegistry.CreateDefault();
        effects.Register(new ProbeHandler("t_lint_probe", "used", "shared", "unused"));
        effects.Register(new ProbeHandler("t_lint_solo", "alpha", "beta"));
        effects.Register(new ProbeHandler("t_lint_idle", "gamma"));
        return effects;
    });

    private static readonly Lazy<IReadOnlyList<CompositionFinding>> Planted = new(() => new CompositionLint(Loaded.Value, null, Effects.Value).Run());

    private sealed class ProbeHandler : IEffectHandler
    {
        public ProbeHandler(string type, params string[] parameters)
        {
            Type = type;
            Signature = EffectSignature.Of(parameters);
        }

        public string Type { get; }

        public EffectSignature Signature { get; }

        public CompiledEffect Compile(JsonObject parameters, EffectCompileContext ctx) => new Nothing { Type = Type };

        private sealed class Nothing : CompiledEffect
        {
            public override void Execute(EffectContext ctx)
            {
            }
        }
    }

    private static CompositionFinding Single(string check, string kind, string id) =>
        Assert.Single(Planted.Value, f => f.Check == check && f.Kind == kind && f.Id == id);

    [Fact]
    public void A_shell_one_preset_composes_on_is_a_smell_and_a_codex_form_a_note()
    {
        var shell = Single(CompositionLint.FormOneUse, "forms", "t_lint_shell");
        Assert.Equal(CompositionLint.Smell, shell.Severity);
        Assert.Contains("t_lint_frozen", shell.Message);
        Assert.Equal(CompositionLint.Note, Single(CompositionLint.FormOneUse, "forms", "t_lint_codex").Severity);
    }

    [Fact]
    public void A_shell_no_preset_composes_on_is_unused()
    {
        Assert.Equal(CompositionLint.Smell, Single(CompositionLint.FormUnused, "forms", "t_lint_orphan").Severity);
        Assert.DoesNotContain(Planted.Value, f => f.Check == CompositionLint.FormUnused && f.Id == "t_lint_shell");
    }

    [Fact]
    public void A_parameter_one_definition_writes_is_single_use_and_one_none_writes_is_unused()
    {
        var single = Single(CompositionLint.ParameterSingleUse, "effect", "t_lint_probe.used");
        Assert.Contains("world_events/t_lint_event_a", single.Message);
        Assert.DoesNotContain(Planted.Value, f => f.Id == "t_lint_probe.shared");
        Assert.Equal(CompositionLint.Note, Single(CompositionLint.ParameterUnused, "effect", "t_lint_probe.unused").Severity);
    }

    [Fact]
    public void A_type_one_definition_alone_uses_is_one_finding_and_a_type_nothing_uses_is_unused_whole()
    {
        var solo = Single(CompositionLint.ParameterSingleUse, "effect", "t_lint_solo");
        Assert.Contains("world_events/t_lint_event_a", solo.Message);
        Assert.Contains("alpha, beta", solo.Message);
        Assert.DoesNotContain(Planted.Value, f => f.Id is "t_lint_solo.alpha" or "t_lint_solo.beta");
        var idle = Single(CompositionLint.ParameterUnused, "effect", "t_lint_idle");
        Assert.Contains("gamma", idle.Message);
        Assert.DoesNotContain(Planted.Value, f => f.Id == "t_lint_idle.gamma");
    }

    [Fact]
    public void Unused_parameters_an_attribute_carries_are_expected()
    {
        var unused = Planted.Value.Where(f => f.Check == CompositionLint.ParameterUnused && f.Id!.Contains('.')).ToList();
        Assert.NotEmpty(unused);
        foreach (var f in unused)
        {
            var dot = f.Id!.IndexOf('.');
            var carried = CompositionLint.FallbackAttribute(f.Id[..dot], f.Id[(dot + 1)..]) is not null;
            Assert.True(carried == f.Message.Contains("as intended", StringComparison.Ordinal), f.Message);
        }
    }

    [Fact]
    public void A_glyphs_patches_and_hooks_count_as_writes_and_their_literals_freeze()
    {
        var lint = new CompositionLint(Loaded.Value, null, Effects.Value);
        var writes = lint.Writes();
        Assert.Contains(new CompositionWrite("glyphs", "t_lint_patcher", "projectile", "count", "patches[0]"), writes);
        Assert.Contains(new CompositionWrite("glyphs", "t_lint_patcher", "fuse", "delay", "patches[1].wrap"), writes);
        Assert.Contains(new CompositionWrite("glyphs", "t_lint_patcher", "fuse", "power", "patches[1].wrap"), writes);
        // The compiler appends a hook list into every parameter a signature maps to the hook.
        Assert.Contains(new CompositionWrite("glyphs", "t_lint_hooker", "damage", "on_kill", "hooks.on_kill"), writes);
        Assert.Contains(writes, w => w.Id == "t_lint_hooker" && w.Type == "heal" && w.Path == "hooks.on_kill[0]");

        var frozen = Planted.Value.Where(f => f.Check == CompositionLint.AxisFrozen && f.Id == "t_lint_patcher").Select(f => f.Message).ToList();
        Assert.Equal(3, frozen.Count);
        Assert.Contains(frozen, m => m.Contains("'projectile.count'", StringComparison.Ordinal) && m.Contains("patches[0]", StringComparison.Ordinal));
        Assert.Contains(frozen, m => m.Contains("'fuse.delay'", StringComparison.Ordinal) && m.Contains("patches[1].wrap", StringComparison.Ordinal));
        Assert.Contains(frozen, m => m.Contains("'fuse.power'", StringComparison.Ordinal));
    }

    [Fact]
    public void A_literal_charge_max_shadows_the_holders_attribute_in_any_graph()
    {
        Assert.Equal("charge.{id}.max", CompositionLint.FallbackAttribute("charge", "max"));
        var max = Single(CompositionLint.AxisFrozen, "statuses", "t_lint_charger");
        Assert.Contains("'charge.fury.max'", max.Message);
        Assert.Contains("holder", max.Message);
    }

    [Fact]
    public void A_literal_where_the_handler_reads_a_weave_attribute_is_frozen()
    {
        var frozen = Planted.Value.Where(f => f.Check == CompositionLint.AxisFrozen && f.Kind == "weaves" && f.Id == "t_lint_frozen").ToList();
        var count = Assert.Single(frozen);
        Assert.Contains("'count'", count.Message);
        Assert.Contains("'projectile.count'", count.Message);
        Assert.Equal("projectile.count", CompositionLint.FallbackAttribute("projectile", "count"));
        // A multiplier is not a fallback: a literal angle is still scaled by area.angle.
        Assert.Null(CompositionLint.FallbackAttribute("area", "angle"));
    }

    [Fact]
    public void A_formula_five_definitions_write_is_copied()
    {
        var copied = Single(CompositionLint.ExpressionCopied, "expression", "level * 1.37 + 0.11");
        Assert.Contains("5 definitions", copied.Message);
        var stricter = new CompositionLint(Loaded.Value, null, Effects.Value) { CopiedThreshold = 6 }.Run();
        Assert.DoesNotContain(stricter, f => f.Check == CompositionLint.ExpressionCopied && f.Id == "level * 1.37 + 0.11");
    }

    private const string SystemSource = """
namespace Sample;

public static class LintSample
{
    public static void Wire(Registry r) => r.Register("leap", Leap);

    public static int Kind(string kind) => kind switch
    {
        "leap" => 1,
        "blink" => 2,
        _ => 0,
    };

    // a comment that names "stunned" and a switch is not code
    public static bool Stopped(Stats stats) => stats.HasFlag("stunned");

    public static bool Lethal(string pool) => pool == "life";

    public static string Label(int n) => n switch
    {
        1 => "one",
        _ => "other",
    };
}
""";

    private const string ParseSource = """
namespace Sample;

public static class LintParse
{
    public static bool Truth(string text)
    {
        switch (text)
        {
            case "true":
                return true;
            default:
                return false;
        }
    }

    public static int Weight(string type)
    {
        switch (type)
        {
            case "area":
            case "projectile":
                return 1;
        }
        return 0;
    }

    public static string Named(CompiledWeave weave) => weave.Form.Id;
}
""";

    private const string WeaveSource = """
namespace Sample;

public sealed class LintForms
{
    public FormDef Form { get; init; } = null!;

    public bool Channels(CompiledWeave weave) => weave.Form.Channel is not null || weave?.Form.Guard?.Bash is not null;

    public bool Targets => Form.Target is not null;

    public ModifierForm Kind(Modifier m) => m.Form is ModifierForm.Flat ? ModifierForm.More : m.Form;

    // weave.Form.Toggle in a comment is not a read
}
""";

    [Fact]
    public void The_code_scan_names_string_switches_compared_names_and_form_reads_but_not_registrations()
    {
        var root = Path.Combine(Path.GetTempPath(), "hollowwake-lint-src-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Systems"));
        Directory.CreateDirectory(Path.Combine(root, "Content"));
        Directory.CreateDirectory(Path.Combine(root, "Weaves"));
        try
        {
            File.WriteAllText(Path.Combine(root, "Systems", "LintSample.cs"), SystemSource.Replace("\r\n", "\n"));
            File.WriteAllText(Path.Combine(root, "Content", "LintParse.cs"), ParseSource.Replace("\r\n", "\n"));
            File.WriteAllText(Path.Combine(root, "Weaves", "LintForms.cs"), WeaveSource.Replace("\r\n", "\n"));
            var all = new CompositionLint(CoreContent.Registry, root).Run();
            var code = all.Where(f => f.Check == CompositionLint.CodeStringSwitch).ToList();
            CompositionFinding At(string file, string source, string marker) =>
                Assert.Single(code, f => f.File == file && f.Line == LineOf(source, marker));

            var system = "Systems/LintSample.cs";
            var kind = At(system, SystemSource, "kind switch");
            Assert.Equal(CompositionLint.Smell, kind.Severity);
            Assert.Contains("'leap'", kind.Message);
            Assert.Contains("'blink'", kind.Message);
            Assert.Contains("'stunned'", At(system, SystemSource, "HasFlag").Message);
            Assert.Contains("'life'", At(system, SystemSource, "pool ==").Message);
            Assert.DoesNotContain(code, f => f.File == system && f.Line == LineOf(SystemSource, "Register("));
            Assert.DoesNotContain(code, f => f.File == system && f.Line == LineOf(SystemSource, "a comment"));
            Assert.DoesNotContain(code, f => f.File == system && f.Line == LineOf(SystemSource, "n switch"));

            var parse = "Content/LintParse.cs";
            Assert.Equal(CompositionLint.Note, At(parse, ParseSource, "switch (text)").Severity);
            var relisted = At(parse, ParseSource, "switch (type)");
            Assert.Equal(CompositionLint.Smell, relisted.Severity);
            Assert.Contains("effect types", relisted.Message);

            // Form reads: only in the runtime's directories, only member accesses, never comments or other Form members.
            var reads = all.Where(f => f.Check == CompositionLint.CodeFormRead).ToList();
            var weaves = "Weaves/LintForms.cs";
            var channels = Assert.Single(reads, f => f.File == weaves && f.Line == LineOf(WeaveSource, "Channels("));
            Assert.Contains("'Channel'", channels.Message);
            Assert.Contains("'Guard'", channels.Message);
            Assert.Contains("'Target'", Assert.Single(reads, f => f.File == weaves && f.Line == LineOf(WeaveSource, "Targets =>")).Message);
            Assert.Equal(2, reads.Count(f => f.File == weaves));
            Assert.DoesNotContain(reads, f => f.File == parse);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static int LineOf(string source, string marker)
    {
        var lines = source.Replace("\r\n", "\n").Split('\n');
        return Array.FindIndex(lines, l => l.Contains(marker, StringComparison.Ordinal)) + 1;
    }

    [Fact]
    public void Runs_over_core_and_groups_its_report_by_check()
    {
        var findings = new CompositionLint(CoreContent.Registry, SourceRoot()).Run();
        Assert.All(findings, f => Assert.Contains(f.Severity, new[] { CompositionLint.Smell, CompositionLint.Note }));
        var groups = CompositionLint.ByCheck(findings);
        Assert.Equal(CompositionLint.Checks, groups.Select(g => g.Check));
        Assert.All(groups, g => Assert.All(g.Findings, f => Assert.Equal(g.Check, f.Check)));
        Assert.Equal(findings, groups.SelectMany(g => g.Findings));
        Assert.Contains(findings, f => f.Check == CompositionLint.CodeStringSwitch);
    }

    // How handlers read a parameter with a weave attribute behind it, as the lint's table was read from them.
    private static readonly Regex HandlerClass = new(@"class\s+\w+\s*:\s*IEffectHandler");
    private static readonly Regex HandlerType = new(@"Type\s*=>\s*""(\w+)""");
    private static readonly Regex FieldKey = new(@"(\w+)\s*=\s*ctx\.\w+\(\s*p\[""(\w+)""\]");
    private static readonly Regex[] Fallbacks =
    {
        new(@"\(ctx,\s*(?<field>\w+),\s*""(?<attr>[\w.]+)"""),
        new(@"ParameterOrAttribute\((?<field>\w+),\s*""(?<attr>[\w.]+)"""),
        new(@"(?<field>\w+)\s+is\s+null\s*\?\s*WeaveAttr\(ctx,\s*""(?<attr>[\w.]+)"""),
        new(@"(?<field>\w+)\s+is\s+not\s+null\s*\?[^;]*?\.Attr\(\w+,\s*""(?<attr>[\w.]+)"""),
    };
    private static readonly Regex SizeOfWidth = new(@"SizeOf\(ctx,[^,]+,[^,]+,[^,]+,\s*(?<field>\w+)");

    [Fact]
    public void The_axis_table_has_every_fallback_the_handlers_read()
    {
        var attributes = CoreContent.Registry.Attributes;
        var derived = new HashSet<(string Type, string Parameter, string Attribute)>();
        foreach (var file in Directory.GetFiles(Path.Combine(SourceRoot(), "Effects", "Handlers"), "*.cs"))
        {
            var text = File.ReadAllText(file);
            var starts = HandlerClass.Matches(text).Select(m => m.Index).Append(text.Length).ToList();
            for (var i = 0; i + 1 < starts.Count; i++)
            {
                var block = text[starts[i]..starts[i + 1]];
                var type = HandlerType.Match(block);
                if (!type.Success)
                {
                    continue;
                }
                var keys = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (Match m in FieldKey.Matches(block))
                {
                    keys.TryAdd(m.Groups[1].Value, m.Groups[2].Value);
                }
                var reads = Fallbacks.SelectMany(r => r.Matches(block)).Select(m => (Field: m.Groups["field"].Value, Attribute: m.Groups["attr"].Value))
                    .Concat(SizeOfWidth.Matches(block).Select(m => (Field: m.Groups["field"].Value, Attribute: "area.width")));
                foreach (var (field, attribute) in reads)
                {
                    if (keys.TryGetValue(field, out var parameter) && attributes.Contains(attribute))
                    {
                        derived.Add((type.Groups[1].Value, parameter, attribute));
                    }
                }
            }
        }
        // Anchors: if the handlers change how they read these, the patterns above need the same change.
        Assert.Contains(("area", "radius", "area.radius"), derived);
        Assert.Contains(("projectile", "homing", "projectile.homing"), derived);
        Assert.Contains(("scatter", "telegraph_radius", "area.radius"), derived);
        Assert.Contains(("fuse", "delay", "fuse.delay"), derived);
        Assert.Contains(("field", "width", "area.width"), derived);
        foreach (var (type, parameter, attribute) in derived)
        {
            var table = CompositionLint.FallbackAttribute(type, parameter);
            Assert.True(table == attribute, $"'{type}.{parameter}' falls back to '{attribute}' in its handler, but the lint's axis table has '{table ?? "nothing"}'.");
        }
    }

    private static string SourceRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, "src", "HollowWake.Core");
            if (File.Exists(Path.Combine(candidate, "HollowWake.Core.csproj")))
            {
                return candidate;
            }
            dir = Path.GetDirectoryName(dir);
        }
        throw new DirectoryNotFoundException("src/HollowWake.Core not found above " + AppContext.BaseDirectory);
    }
}
