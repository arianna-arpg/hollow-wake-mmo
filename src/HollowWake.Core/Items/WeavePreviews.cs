using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Expressions;
using HollowWake.Core.Maths;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;
using HollowWake.Core.Weaves;

namespace HollowWake.Core.Items;

/// <summary>One line of a weave's preview: a label and the server's text for it.</summary>
public readonly record struct PreviewLine(string Label, string Text);

/// <summary>
/// The numbers a weave's card shows (row J3 of the parity plan), computed on the server from the same fold a cast
/// uses and formatted for reading: damage per Thread from the weave's damage effects (lowest and highest roll,
/// through the caster's damage fold with the weave's modifiers), cast time and cooldown after speed and
/// recovery, costs, range, and the weave's own attributes named by their definitions. A client draws the lines
/// as given and never folds a number.
/// </summary>
public static class WeavePreviews
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static List<PreviewLine> Lines(Simulation sim, Entity caster, CompiledWeave weave)
    {
        var lines = new List<PreviewLine>();
        if (!sim.World.TryGet<Stats>(caster, out var stats))
        {
            return lines;
        }
        var runtime = sim.Service<WeaveRuntime>();
        foreach (var band in DamageBands(sim, caster, stats, weave))
        {
            lines.Add(new PreviewLine(Label(sim, "damage", "Damage"), band));
        }
        lines.Add(new PreviewLine(Label(sim, "cast_time", "Cast time"), Seconds(runtime.CastDuration(weave, stats))));
        var cooldown = weave.Cooldown(stats);
        if (cooldown > 0)
        {
            var recovery = Math.Max(0.1, stats.Attributes.Get("cooldown.recovery", stats, weave.Tags, weave.Modifiers));
            lines.Add(new PreviewLine(Label(sim, "cooldown", "Cooldown"), Seconds(cooldown / recovery)));
        }
        foreach (var (resource, cost) in runtime.Costs(weave, stats))
        {
            lines.Add(new PreviewLine(Label(sim, "cost", "Cost"), $"{Number(cost)} {sim.Content.Get<ResourceDef>(resource)?.DisplayName ?? Definition.Humanize(resource)}"));
        }
        var range = weave.Range(stats);
        if (range > 0)
        {
            lines.Add(new PreviewLine(Label(sim, "range", "Range"), Number(range)));
        }
        foreach (var (attribute, _) in weave.Bases)
        {
            if (attribute is "range" or "base_damage")
            {
                continue;
            }
            var def = sim.Content.Attributes.Get(attribute);
            if (def is { Hidden: true })
            {
                continue;
            }
            var value = weave.Attr(stats, attribute);
            var text = def is { Percent: true } ? (value * 100).ToString("0.#", Invariant) + "%" : value.ToString(def?.Decimals is { } d ? "0." + new string('#', Math.Max(1, d)) : "0.##", Invariant);
            lines.Add(new PreviewLine(def?.DisplayName ?? Definition.Humanize(attribute), text));
        }
        var crit = Math.Clamp(stats.Attributes.Get("crit.chance", stats, weave.Tags, weave.Modifiers), 0, 1);
        if (crit > 0 && lines.Count > 0 && lines[0].Label == Label(sim, "damage", "Damage"))
        {
            lines.Add(new PreviewLine(Label(sim, "crit", "Critical chance"), (crit * 100).ToString("0.#", Invariant) + "%"));
        }
        return lines;
    }

    /// <summary>A preview label from rule <c>inventory.preview_labels.{key}</c>, so wording is content.</summary>
    private static string Label(Simulation sim, string key, string fallback) => sim.Content.Rules.Text("inventory.preview_labels." + key, fallback);

    private static string Number(double v) => v.ToString("0.#", Invariant);

    private static string Seconds(double s) => s.ToString("0.##", Invariant) + " s";

    /// <summary>
    /// Every damage effect in the weave's graph as "12 to 18 Ember, 3 to 5 Storm": the amount at its lowest and
    /// highest roll, split over its Threads and folded through the caster's damage with the weave's modifiers.
    /// </summary>
    public static List<string> DamageBands(Simulation sim, Entity caster, Stats stats, CompiledWeave weave)
    {
        var bands = new List<string>();
        var nodes = new List<JsonObject>();
        foreach (var spec in weave.Specs)
        {
            Collect(WeaveGraph.ToNode(spec), nodes);
        }
        foreach (var node in nodes)
        {
            if (bands.Count >= 3)
            {
                break;
            }
            var amount = node["amount"] is { } a ? a.Deserialize<Expr>(ContentJson.Options) : null;
            amount ??= Expr.Constant(10);
            var threads = ThreadsOf(sim, node["threads"], weave);
            var tags = node["tags"] is { } t ? t.Deserialize<TagSet>(ContentJson.Options) ?? TagSet.Empty : TagSet.Empty;
            try
            {
                var low = Folded(sim, caster, weave, amount, threads, tags, 0);
                var high = Folded(sim, caster, weave, amount, threads, tags, 0.999999);
                var parts = new List<string>(low.Count);
                foreach (var (thread, min) in low)
                {
                    var max = high.TryGetValue(thread, out var h) ? h : min;
                    var name = sim.Content.Get<ThreadDef>(thread)?.DisplayName ?? Definition.Humanize(thread);
                    parts.Add(Math.Round(min) == Math.Round(max) ? $"{Number(Math.Round(min))} {name}" : $"{Number(Math.Round(min))} to {Number(Math.Round(max))} {name}");
                }
                var text = string.Join(", ", parts);
                if (text.Length > 0 && !bands.Contains(text))
                {
                    bands.Add(text);
                }
            }
            catch (ExprEvalException)
            {
                // A formula that reads what only a live cast has (how long it was held) has no number to preview.
            }
        }
        return bands;
    }

    private static Dictionary<string, double> Folded(Simulation sim, Entity caster, CompiledWeave weave, Expr amount, List<string> threads, TagSet tags, double roll)
    {
        var stats = sim.World.Get<Stats>(caster)!;
        var scope = new ScopeOverlay(new FixedRoll(stats, roll));
        scope.Mount("source", stats);
        scope.Mount("caster", stats);
        scope.Mount("weave", new WeaveScope(weave, stats, weave.Tags));
        scope.Set("depth", 0);
        scope.Set("time", sim.Clock.Time);
        var value = amount.Evaluate(scope);
        var ctx = new EffectContext { Sim = sim, Source = caster, Weave = weave, Tags = weave.Tags, Rng = new Rng(1) };
        var packet = DamagePipeline.Build(ctx, value, threads, tags, canCrit: false);
        return new Dictionary<string, double>(packet.Amounts, StringComparer.Ordinal);
    }

    /// <summary>The Threads a damage effect names: <c>$threads</c> for the weave's, <c>$thread</c> for its first, or a list.</summary>
    private static List<string> ThreadsOf(Simulation sim, JsonNode? node, CompiledWeave weave)
    {
        var own = new List<string>(weave.Threads.Count);
        foreach (var t in weave.Threads)
        {
            own.Add(t.Id);
        }
        var result = new List<string>();
        switch (node)
        {
            case null:
                result.AddRange(own);
                break;
            case JsonValue v when v.TryGetValue<string>(out var s):
                if (s == "$threads")
                {
                    result.AddRange(own);
                }
                else if (s == "$thread")
                {
                    result.AddRange(own.Take(1));
                }
                else
                {
                    result.Add(s);
                }
                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    if (item is JsonValue iv && iv.TryGetValue<string>(out var text))
                    {
                        if (text == "$threads")
                        {
                            result.AddRange(own);
                        }
                        else
                        {
                            result.Add(text);
                        }
                    }
                }
                break;
        }
        if (result.Count == 0)
        {
            result.Add(sim.Content.Rules.Text("combat.default_thread", "stone"));
        }
        return result;
    }

    /// <summary>Damage nodes anywhere in an effect graph, in graph order.</summary>
    private static void Collect(JsonObject node, List<JsonObject> into)
    {
        if (WeaveGraph.TypeOf(node) == "damage")
        {
            into.Add(node);
        }
        foreach (var (_, value) in node)
        {
            if (value is JsonArray list)
            {
                foreach (var child in list)
                {
                    if (child is JsonObject obj && WeaveGraph.TypeOf(obj) is not null)
                    {
                        Collect(obj, into);
                    }
                }
            }
            else if (value is JsonObject inner && inner["effects"] is JsonArray)
            {
                // A block holding a list (a projectile's on_tick with its interval).
                Collect(inner, into);
            }
        }
    }

    /// <summary>The caster's scope with every roll fixed, so a formula's lowest and highest outcomes can be read.</summary>
    private sealed class FixedRoll : IExprScope
    {
        private readonly IExprScope _inner;
        private readonly double _roll;

        public FixedRoll(IExprScope inner, double roll)
        {
            _inner = inner;
            _roll = roll;
        }

        public bool TryResolve(string name, out double value) => _inner.TryResolve(name, out value);
        public bool HasFlag(string flag) => _inner.HasFlag(flag);
        public double NextRandom() => _roll;
    }
}
