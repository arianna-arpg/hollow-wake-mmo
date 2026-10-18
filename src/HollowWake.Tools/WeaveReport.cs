using System.Numerics;
using System.Text.Json.Nodes;
using HollowWake.Core.Characters;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Effects;
using HollowWake.Core.Items;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;

namespace HollowWake.Tools;

/// <summary>
/// <c>hollowwake weave</c>: compiles a preset or a composed weave the way the server would and prints what
/// the compiler made of it: level, the held-mode blocks it carries and where each came from, tags, declared
/// and probed mechanisms, active and dormant glyphs with their reasons, modifiers, the patched effect graph,
/// and preview numbers for a sample caster. With <c>--axes</c> it prints the weave on the combat grammar's
/// nine dimensions instead (<see cref="AxisReport"/>), and with <c>--axes --all</c> the census of every form,
/// preset and glyph, per dimension, with <c>--json</c> writing every value and <c>--literals</c> the frozen
/// literal baseline the axis report tests hold the content to.
/// </summary>
public static class WeaveReport
{
    public static int Run(ContentRegistry content, string? id, Func<string, string, string> opt, Func<string, bool> flag)
    {
        if (flag("axes") && flag("all"))
        {
            return AxesOfAll(content, opt("json", ""), opt("literals", ""));
        }
        id ??= "ember_bolt";
        var threads = Split(opt("threads", ""));
        var glyphs = Split(opt("glyphs", "")).Select(GlyphRef.Parse).ToList();
        var level = int.Parse(opt("level", "1"));
        var spec = content.Has<WeaveDef>(id)
            ? new WeaveSpec { Preset = id, Threads = threads, Glyphs = glyphs, Level = level }
            : new WeaveSpec { Form = id, Threads = threads, Glyphs = glyphs, Level = level };

        var sim = ZoneBuilder.CreateArena(content, 1);
        var compiler = sim.Service<WeaveCompiler>();
        if (!compiler.TryCompile(spec, out var weave, out var error, GlyphFit.Dormant))
        {
            Console.WriteLine($"{spec}: does not compile: {error}");
            return 1;
        }
        var w = weave!;
        if (flag("axes"))
        {
            PrintAxes(w, content);
            return 0;
        }
        Console.WriteLine($"{w.Name} ({w.Key})  level {w.Level}{(w.LevelBonus != 0 ? $" ({spec.Level} + {w.LevelBonus} from glyphs)" : "")}");
        Console.WriteLine($"form        {w.Form.Id}: {w.CastMode.ToString().ToLowerInvariant()}, targeting {w.Targeting.ToString().ToLowerInvariant()}, scope {w.CastScope}");
        foreach (var (block, source) in w.HeldBlocks.Sources())
        {
            Console.WriteLine($"block       {block} from {source}");
        }
        Console.WriteLine($"threads     {(w.Threads.Count == 0 ? "-" : string.Join(", ", w.Threads.Select(t => t.Id)))}");
        Console.WriteLine($"tags        {w.Tags}");
        Console.WriteLine($"declared    {w.DeclaredMechanisms}");
        Console.WriteLine($"probed      [{string.Join(" ", w.Mechanisms.Where(m => !w.DeclaredMechanisms.HasExact(m)))}]");
        Console.WriteLine($"glyphs      {(w.GlyphRefs.Count == 0 ? "-" : string.Join(", ", w.GlyphRefs))}");
        foreach (var d in w.Dormant)
        {
            Console.WriteLine($"dormant     {d.Ref}: {d.Reason}");
        }
        foreach (var m in w.Modifiers)
        {
            Console.WriteLine($"modifier    {m}  ({m.Source})");
        }
        foreach (var (k, v) in w.Bases)
        {
            Console.WriteLine($"base        {k} = {v:0.###}");
        }
        Console.WriteLine("graph");
        foreach (var s in w.Specs)
        {
            PrintNode(WeaveGraph.ToNode(s), "  ", sim.Service<EffectRegistry>());
        }

        // Preview numbers for a sample caster: a fresh character of a calling, at a level.
        var calling = opt("calling", content.Each<CallingDef>().Select(c => c.Id).OrderBy(c => c, StringComparer.Ordinal).FirstOrDefault() ?? "");
        var casterLevel = int.Parse(opt("caster-level", "1"));
        if (!content.Has<CallingDef>(calling))
        {
            return 0;
        }
        var characters = sim.Service<CharacterFactory>();
        var sheet = characters.Create(sim, "preview", "preview", "Preview", calling, sim.Service<ItemGenerator>());
        sheet.Level = casterLevel;
        sheet.Position = new Vector3(8, 8, 0);
        var caster = characters.Materialize(sim, sheet);
        var stats = sim.World.Get<Stats>(caster)!;
        var runtime = sim.Service<WeaveRuntime>();
        Console.WriteLine($"preview     for a level {casterLevel} {calling}");
        Console.WriteLine($"  cast time {runtime.CastDuration(w, stats):0.###} s, cooldown {w.Cooldown(stats):0.###} s, range {w.Range(stats):0.###}");
        foreach (var (resource, cost) in runtime.Costs(w, stats))
        {
            Console.WriteLine($"  cost      {resource} {cost:0.###}");
        }
        foreach (var attribute in w.Bases.Keys)
        {
            Console.WriteLine($"  {attribute,-24} {w.Attr(stats, attribute):0.###}");
        }
        return 0;
    }

    private static List<string> Split(string text) => text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    // ------------------------------------------------------------ --axes

    /// <summary>The weave on the nine dimensions, one axis a line, then its frozen literals and the words that hide a number.</summary>
    private static void PrintAxes(CompiledWeave w, ContentRegistry content)
    {
        var values = AxisReport.Of(w, content);
        var dimensions = values.Select(v => v.Dimension).Distinct().Count();
        Console.WriteLine($"{w.Name} ({w.Key})  level {w.Level}: {values.Count} axes on {dimensions} of the nine dimensions");
        foreach (var dimension in Enum.GetValues<Dimension>())
        {
            var group = values.Where(v => v.Dimension == dimension).ToList();
            if (group.Count == 0)
            {
                continue;
            }
            Console.WriteLine(AxisValue.DimensionName(dimension));
            foreach (var v in group)
            {
                Console.WriteLine($"  {v.Name,-36} {v.Text,-12} {v.Unit,-10} {Lower(v.Source),-10} {v.Path}{(v.Detail is { } d ? $"  ({d})" : "")}");
            }
        }
        var literals = AxisReport.Literals(values);
        Console.WriteLine($"literals    {literals.Count} frozen: literal numbers no modifier reaches, each with who wrote it and where in their definition");
        foreach (var v in literals)
        {
            var door = v.Attribute is { } a ? "write weave." + a : "no attribute reaches it";
            var what = v.Effect is null ? "cast layer" : $"{v.Effect}.{v.Parameter}";
            Console.WriteLine($"  {v.Name,-36} {v.Text,-12} {v.Unit,-10} {v.Path}  ({what}; {door}; {v.Key})");
        }
        var hiding = values.Where(v => v.HidesNumber).ToList();
        if (hiding.Count > 0)
        {
            Console.WriteLine($"words       {hiding.Count} that stand for a number");
            foreach (var v in hiding)
            {
                Console.WriteLine($"  {v.Name,-36} {v.Text,-12} {v.Path}  ({v.Effect}.{v.Parameter})");
            }
        }
    }

    /// <summary>
    /// The census over every form and preset, and each glyph on the first form it fits: per dimension how many
    /// weaves state it, their axes, literals (the values, and the distinct ones by who wrote them) and rules.
    /// </summary>
    private static int AxesOfAll(ContentRegistry content, string json, string literalsFile)
    {
        var all = AxisReport.OfAll(content);
        var glyphs = AxisReport.OfGlyphs(content);
        var compiled = all.Where(a => a.Error is null).ToList();
        var placedGlyphs = glyphs.Where(g => g.Error is null).ToList();
        var census = compiled.Concat(placedGlyphs).ToList();
        Console.WriteLine($"{all.Count} weaves: {compiled.Count(a => a.Kind == "form")} forms and {compiled.Count(a => a.Kind == "preset")} presets placed, {all.Count - compiled.Count} did not compile; {placedGlyphs.Count} of {glyphs.Count} glyphs placed on the first form or preset each fits");
        Console.WriteLine($"  {"dimension",-12} {"weaves",7} {"axes",7} {"literals",9} {"distinct",9} {"rules",7}");
        foreach (var dimension in Enum.GetValues<Dimension>())
        {
            var stating = compiled.Count(a => a.Values.Any(v => v.Dimension == dimension));
            var axes = compiled.Sum(a => a.Values.Count(v => v.Dimension == dimension));
            var literals = compiled.Sum(a => a.Values.Count(v => v.Dimension == dimension && v.Frozen));
            var distinct = census.SelectMany(a => a.Values).Where(v => v.Dimension == dimension && v.Frozen && v.Key is not null).Select(v => v.Key).Distinct(StringComparer.Ordinal).Count();
            var fromRules = compiled.Sum(a => a.Values.Count(v => v.Dimension == dimension && v.Source == AxisSource.Rule));
            Console.WriteLine($"  {AxisValue.DimensionName(dimension),-12} {stating,7} {axes,7} {literals,9} {distinct,9} {fromRules,7}");
        }
        var frozen = compiled.SelectMany(a => a.Values.Where(v => v.Frozen)).ToList();
        var keys = AxisReport.LiteralKeys(census);
        var byWriter = string.Join(", ", keys.GroupBy(k => k[..k.IndexOf(' ')]).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Count()} by {g.Key}s"));
        Console.WriteLine($"literals    {frozen.Count} in {compiled.Count(a => a.Values.Any(v => v.Frozen))} weaves; {keys.Count} distinct by who wrote them ({byWriter}); most often");
        foreach (var group in frozen.GroupBy(v => v.Effect is null ? v.Name : $"{v.Effect}.{v.Parameter}").OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).Take(20))
        {
            var door = group.First().Attribute is { } a ? "weave." + a : "no attribute";
            Console.WriteLine($"  {group.Key,-32} {group.Count(),6}  {group.First().Name} ({door})");
        }
        var rules = compiled.SelectMany(a => a.Values.Where(v => v.Source == AxisSource.Rule)).GroupBy(v => v.Detail ?? "").OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).ToList();
        Console.WriteLine($"rules       {rules.Count} read where a graph or block gives nothing");
        foreach (var group in rules)
        {
            Console.WriteLine($"  {group.Key,-32} {group.Count(),6}  {group.First().Name}");
        }
        var hiding = compiled.SelectMany(a => a.Values.Where(v => v.HidesNumber)).GroupBy(v => $"{v.Effect}.{v.Parameter}").OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).ToList();
        Console.WriteLine($"words       {hiding.Sum(g => g.Count())} that stand for a number");
        foreach (var group in hiding)
        {
            Console.WriteLine($"  {group.Key,-32} {group.Count(),6}  {string.Join(", ", group.Select(v => v.Text).Distinct().OrderBy(t => t, StringComparer.Ordinal))}");
        }
        foreach (var failed in all.Concat(glyphs).Where(a => a.Error is not null))
        {
            Console.WriteLine($"not placed  {failed.Kind} {failed.Id}: {failed.Error}");
        }
        if (json.Length > 0)
        {
            var path = Path.GetFullPath(json);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, AxesJson(all.Concat(glyphs).ToList()).ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = Plain.Encoder }) + "\n");
            Console.WriteLine($"wrote {path}");
        }
        if (literalsFile.Length > 0)
        {
            var path = Path.GetFullPath(literalsFile);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, string.Join("\n", LiteralsHeader.Concat(keys)) + "\n");
            Console.WriteLine($"wrote {path}");
        }
        return 0;
    }

    /// <summary>The literal baseline's header: what the file holds and how it is written.</summary>
    private static readonly string[] LiteralsHeader =
    {
        "# The frozen literals of the content: numbers a graph or the cast layer writes as constants that no modifier reaches,",
        "# one a line, keyed by who wrote it (a form, a preset or a glyph) and where in their own definition. Written by",
        "# hollowwake weave --axes --all --literals tests/fixtures/grammar/frozen-literals.txt; the axis report tests hold",
        "# the core content to it, so a new literal fails them and a literal resolved is removed by writing the file again.",
    };

    private static JsonArray AxesJson(IReadOnlyList<WeaveAxes> all)
    {
        var weaves = new JsonArray();
        foreach (var weave in all)
        {
            var values = new JsonArray();
            foreach (var v in weave.Values)
            {
                values.Add(new JsonObject
                {
                    ["dimension"] = AxisValue.DimensionName(v.Dimension),
                    ["name"] = v.Name,
                    ["number"] = v.Number,
                    ["word"] = v.Word,
                    ["unit"] = v.Unit,
                    ["source"] = Lower(v.Source),
                    ["path"] = v.Path,
                    ["attribute"] = v.Attribute,
                    ["multiplied"] = v.Multiplied,
                    ["effect"] = v.Effect,
                    ["parameter"] = v.Parameter,
                    ["detail"] = v.Detail,
                    ["writer"] = v.Writer,
                    ["origin"] = v.Origin,
                    ["constant"] = v.Constant,
                    ["frozen"] = v.Frozen,
                    ["hides_number"] = v.HidesNumber,
                });
            }
            weaves.Add(new JsonObject { ["id"] = weave.Id, ["kind"] = weave.Kind, ["error"] = weave.Error, ["values"] = values });
        }
        return weaves;
    }

    private static string Lower(AxisSource source) => source.ToString().ToLowerInvariant();

    // Formulas print as written: no escaping of characters such as '+'.
    private static readonly System.Text.Json.JsonSerializerOptions Plain = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>One effect node and, indented under the parameter holding them, the effect lists inside it.</summary>
    private static void PrintNode(JsonObject node, string indent, EffectRegistry effects)
    {
        var type = WeaveGraph.TypeOf(node) ?? "?";
        var simple = node.Where(kv => kv.Key != "type" && !HoldsEffects(kv.Value, effects)).Select(kv => $"{kv.Key}={kv.Value?.ToJsonString(Plain)}");
        Console.WriteLine($"{indent}{type} {string.Join(" ", simple)}".TrimEnd());
        foreach (var (param, value) in node)
        {
            if (param == "type" || !HoldsEffects(value, effects))
            {
                continue;
            }
            Console.WriteLine($"{indent}  {param}:");
            foreach (var child in ((JsonArray)value!).OfType<JsonObject>())
            {
                PrintNode(child, indent + "    ", effects);
            }
        }
    }

    private static bool HoldsEffects(JsonNode? value, EffectRegistry effects) =>
        value is JsonArray list && list.Count > 0 && list.All(n => n is JsonObject o && WeaveGraph.TypeOf(o) is { } t && effects.Has(t));
}
