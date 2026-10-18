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
/// and preview numbers for a sample caster.
/// </summary>
public static class WeaveReport
{
    public static int Run(ContentRegistry content, string? id, Func<string, string, string> opt)
    {
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
