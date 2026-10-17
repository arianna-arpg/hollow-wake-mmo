using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using HollowWake.Core.Combat.Stages;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Expressions;
using HollowWake.Core.Sim;
using HollowWake.Core.StatusEffects;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Combat;

/// <summary>Typed damage on its way to a target: per-thread amounts, the tags the hit carries, crit.</summary>
public sealed class DamagePacket
{
    public Dictionary<string, double> Amounts { get; } = new(StringComparer.Ordinal);
    public TagSet Tags { get; set; } = TagSet.Empty;
    public bool Crit { get; set; }
    public double Total => Amounts.Values.Sum();
}

public sealed class HitResult
{
    public bool Evaded;
    public bool Blocked;
    public bool Immune;
    public bool Crit;
    public bool Killed;
    public double Total;
    public Dictionary<string, double> Received = new(StringComparer.Ordinal);
}

/// <summary>
/// The one path every hit takes. <see cref="Build"/> is the attacker's side: the base amount split over
/// the hit's Threads, the attacker's damage fold scoped by the hit's tags (glyph modifiers joining) with
/// the victim tags in the context, conversion and extra-as, and the crit roll. <see cref="Apply"/> walks
/// the defender's ladder: the stages registered by name in the order rule <c>combat.stages</c> gives
/// (immunity, guard, evade, block, mitigate, taken, pools, cull by default), skipping those rule
/// <c>combat.stage_skips</c> names for the hit's tags; then the hit is published, ailments roll and life
/// at zero kills. Every formula lives in rules. There is no unmitigated damage.
/// </summary>
public static class DamagePipeline
{
    public static DamagePacket Build(EffectContext ctx, double amount, IReadOnlyList<string> threads, TagSet extraTags, bool canCrit)
    {
        var source = ctx.SourceStats;
        var packet = new DamagePacket();
        var tags = ctx.Tags.Union(extraTags).With("hit", "damage");
        foreach (var t in threads)
        {
            tags = tags.With(t);
        }
        var hit = HitScope.Rent(ctx, packet);
        try
        {
            if (hit.Target is { } ts)
            {
                tags = tags.Union(VictimTagsOf(ts, hit));
            }
            packet.Tags = tags;
            var share = threads.Count == 0 ? amount : amount / threads.Count;
            var extra = ctx.Weave?.Modifiers;
            foreach (var t in threads)
            {
                var threadTags = tags.With(t);
                var scaled = source is null ? share : source.Attributes.Apply("damage", share, hit.AttackerScope, threadTags, extra);
                packet.Amounts[t] = packet.Amounts.TryGetValue(t, out var existing) ? existing + scaled : scaled;
            }
            if (source is not null)
            {
                ApplyConversion(ctx, source, packet, extra);
                if (canCrit && !hit.Plan.Skips(tags, "crit"))
                {
                    var chance = Math.Clamp(source.Attributes.Get("crit.chance", source, tags, extra), 0, 1);
                    if (ctx.Rng.Chance(chance))
                    {
                        var multiplier = Math.Max(1, source.Attributes.Get("crit.multiplier", source, tags, extra));
                        foreach (var k in packet.Amounts.Keys.ToList())
                        {
                            packet.Amounts[k] *= multiplier;
                        }
                        packet.Crit = true;
                    }
                }
            }
        }
        finally
        {
            hit.Return();
        }
        return packet;
    }

    /// <summary>
    /// Victim scope: <c>vs.</c> plus each of the struck body's tags and flags, then <c>vs.</c> plus the key
    /// of each rule in <c>combat.victim_tags</c> whose expression holds in the hit scope.
    /// </summary>
    private static TagSet VictimTagsOf(Stats ts, HitScope hit)
    {
        var vs = hit.VictimTags;
        vs.Clear();
        foreach (var tag in ts.Tags)
        {
            vs.Add("vs." + tag);
        }
        foreach (var flag in ts.Flags)
        {
            vs.Add("vs." + flag);
        }
        foreach (var rule in hit.Plan.VictimTags)
        {
            if (rule.When.IsTrue(hit.AttackerScope))
            {
                vs.Add(rule.Tag);
            }
        }
        var result = TagSet.From(vs);
        vs.Clear();
        return result;
    }

    /// <summary>convert.{from}.{to} moves a fraction of one thread into another; extra.{to} adds a share of everything as another thread.</summary>
    private static void ApplyConversion(EffectContext ctx, Stats source, DamagePacket packet, IReadOnlyList<Attributes.Modifier>? extra)
    {
        var names = ConversionNames.For(ctx.Sim.Content);
        var threads = names.Threads;
        var original = new Dictionary<string, double>(packet.Amounts, StringComparer.Ordinal);
        foreach (var (from, amount) in original)
        {
            if (amount <= 0)
            {
                continue;
            }
            double converted = 0;
            for (var j = 0; j < threads.Length; j++)
            {
                var to = threads[j];
                if (to == from)
                {
                    continue;
                }
                var attr = names.Convert(from, j);
                if (!source.Attributes.HasModifiers(attr) && !Mentions(extra, attr))
                {
                    continue;
                }
                var fraction = Math.Clamp(source.Attributes.Get(attr, source, packet.Tags, extra), 0, 1 - converted);
                if (fraction <= 0)
                {
                    continue;
                }
                converted += fraction;
                packet.Amounts[to] = packet.Amounts.TryGetValue(to, out var e) ? e + amount * fraction : amount * fraction;
            }
            packet.Amounts[from] = amount * (1 - converted);
        }
        var total = original.Values.Sum();
        for (var j = 0; j < threads.Length; j++)
        {
            var to = threads[j];
            var attr = names.Extra[j];
            if (!source.Attributes.HasModifiers(attr) && !Mentions(extra, attr))
            {
                continue;
            }
            var fraction = Math.Max(0, source.Attributes.Get(attr, source, packet.Tags, extra));
            if (fraction > 0)
            {
                packet.Amounts[to] = packet.Amounts.TryGetValue(to, out var e) ? e + total * fraction : total * fraction;
            }
        }
    }

    private static bool Mentions(IReadOnlyList<Attributes.Modifier>? extra, string attribute)
    {
        if (extra is null)
        {
            return false;
        }
        for (var i = 0; i < extra.Count; i++)
        {
            if (extra[i].Attribute == attribute)
            {
                return true;
            }
        }
        return false;
    }

    public static HitResult Apply(EffectContext ctx, DamagePacket packet)
    {
        var sim = ctx.Sim;
        var result = new HitResult { Crit = packet.Crit };
        var target = ctx.Target;
        if (!sim.World.TryGet<Stats>(target, out var ts) || sim.World.Has<Dead>(target))
        {
            return result;
        }
        var hit = HitScope.Rent(ctx, packet, result);
        try
        {
            hit.LoadThreads();
            var plan = hit.Plan;
            var stages = plan.Stages;
            var skipped = plan.SkippedStages(packet.Tags);
            for (var i = 0; i < stages.Length; i++)
            {
                if ((skipped & (1UL << i)) != 0)
                {
                    continue;
                }
                if (stages[i].Run(hit) == HitFlow.Stop)
                {
                    return result;
                }
            }
        }
        finally
        {
            hit.Return();
        }

        Publish(sim, ctx, result, packet);
        ApplyAilments(ctx, packet, result);

        if (ts.Resource("life") <= 0)
        {
            result.Killed = true;
            sim.Service<DeathService>().Kill(sim, target, ctx.Origin, ctx.Depth);
        }
        return result;
    }

    internal static void Publish(Simulation sim, EffectContext ctx, HitResult result, DamagePacket packet)
    {
        sim.Events.Publish(new DamageDealt(ctx.Source, ctx.Target, result.Total, result.Crit, packet.Tags, result.Received, result.Evaded, result.Blocked) { Tick = sim.Clock.Tick, Depth = ctx.Depth });
    }

    /// <summary>Each thread may inflict its status; chance and magnitude come from attributes and rules.</summary>
    private static void ApplyAilments(EffectContext ctx, DamagePacket packet, HitResult result)
    {
        var sim = ctx.Sim;
        if (ctx.SourceStats is not { } source || !sim.TryService<StatusService>(out var statuses))
        {
            return;
        }
        foreach (var (thread, dealt) in result.Received)
        {
            if (dealt <= 0)
            {
                continue;
            }
            var def = sim.Content.Get<ThreadDef>(thread);
            if (def?.Status is null)
            {
                continue;
            }
            var chance = source.Attributes.Get("ailment.chance", source, packet.Tags.With(thread), ctx.Weave?.Modifiers);
            if (def.StatusChance is not null && Expr.TryParse(def.StatusChance, out var ce, out _))
            {
                chance += ce!.Evaluate(ctx.Scope);
            }
            if (packet.Crit)
            {
                chance += sim.Content.Rules.Number("combat.crit_ailment_bonus", 0.5);
            }
            if (chance <= 0 || !ctx.Rng.Chance(Math.Min(1, chance)))
            {
                continue;
            }
            var scope = new DictScope(ctx.Rng).Set("amount", dealt).Set("level", source.Level);
            var magnitude = sim.Content.Rules.Eval($"combat.ailment_magnitude.{def.Status}", "amount * 0.25", scope);
            var potency = 1 + source.Attributes.Get("ailment.potency", source, packet.Tags.With(thread), ctx.Weave?.Modifiers);
            statuses.Apply(sim, ctx.Target, def.Status, ctx.Source, new StatusApplication
            {
                Magnitude = magnitude,
                Potency = potency,
                SourceKey = ctx.Weave?.Key ?? "hit",
                Tags = packet.Tags,
                Depth = ctx.Depth,
            });
        }
    }

    /// <summary>
    /// The attribute names conversion reads, built once per content set instead of per hit, and never changed
    /// afterwards, so zones on several threads share them. Threads are fixed for a loaded content set; a
    /// registry extended in code after its first hit keeps the names it had, which only tools and tests do.
    /// </summary>
    private sealed class ConversionNames
    {
        private static readonly ConditionalWeakTable<ContentRegistry, ConversionNames> Cache = new();

        private readonly Dictionary<string, string[]> _convert = new(StringComparer.Ordinal);

        private ConversionNames(string[] threads)
        {
            Threads = threads;
            Extra = threads.Select(t => $"extra.{t}").ToArray();
            foreach (var from in threads)
            {
                _convert[from] = threads.Select(t => $"convert.{from}.{t}").ToArray();
            }
        }

        public string[] Threads { get; }
        public string[] Extra { get; }

        public static ConversionNames For(ContentRegistry content) =>
            Cache.GetValue(content, static c => new ConversionNames(c.Each<ThreadDef>().Select(t => t.Id).ToArray()));

        /// <summary><c>convert.{from}.{to}</c> for the thread at index <paramref name="to"/>.</summary>
        public string Convert(string from, int to) =>
            _convert.TryGetValue(from, out var row) ? row[to] : $"convert.{from}.{Threads[to]}";
    }
}

/// <summary>
/// The rules every hit reads, resolved once per ruleset: the stage order (<c>combat.stages</c>), which
/// stages hits with a tag skip (<c>combat.stage_skips</c>, a map from tag to stage names; a name that is
/// not a stage, such as <c>crit</c>, is checked by the step it names), the victim tag rules
/// (<c>combat.victim_tags</c>, a map from tag to expression), and the numbers the built-in stages use.
/// </summary>
public sealed class HitPlan
{
    private static readonly ConditionalWeakTable<Rules, HitPlan> Cache = new();

    /// <summary>A victim tag and the expression, read in the hit scope, that grants it.</summary>
    public readonly record struct VictimTagRule(string Tag, Expr When);

    private readonly (TagSet Tag, ulong Stages, HashSet<string> Names)[] _skips;

    private HitPlan(Rules rules)
    {
        var names = rules.Has("combat.stages") ? rules.Strings("combat.stages") : HitStages.DefaultOrder;
        var stages = new List<IHitStage>();
        var problems = new List<string>();
        foreach (var name in names)
        {
            if (HitStages.TryGet(name, out var stage))
            {
                stages.Add(stage);
            }
            else
            {
                problems.Add($"combat.stages names unknown stage '{name}'");
            }
        }
        if (stages.Count > 64)
        {
            problems.Add("combat.stages lists more than 64 stages; the rest never run");
            stages.RemoveRange(64, stages.Count - 64);
        }
        Stages = stages.ToArray();
        StageNames = Stages.Select(s => s.Name).ToArray();

        var skips = new List<(TagSet, ulong, HashSet<string>)>();
        if (rules.Node("combat.stage_skips") is JsonObject map)
        {
            foreach (var (tag, list) in map)
            {
                var mask = 0UL;
                var others = new HashSet<string>(StringComparer.Ordinal);
                if (list is JsonArray arr)
                {
                    foreach (var item in arr)
                    {
                        var name = JsonNum.StringOf(item) ?? "";
                        var index = Array.IndexOf(StageNames, name);
                        if (index >= 0)
                        {
                            mask |= 1UL << index;
                        }
                        else if (name.Length > 0)
                        {
                            others.Add(name);
                        }
                    }
                }
                else
                {
                    problems.Add($"combat.stage_skips.{tag} must be a list of stage names");
                }
                skips.Add((TagSet.Of(tag), mask, others));
            }
        }
        _skips = skips.ToArray();

        var victimTags = new List<VictimTagRule>();
        if (rules.Node("combat.victim_tags") is JsonObject vt)
        {
            foreach (var (tag, _) in vt)
            {
                victimTags.Add(new VictimTagRule("vs." + tag, rules.Expr("combat.victim_tags." + tag, "0")));
            }
        }
        VictimTags = victimTags.ToArray();

        var evadable = rules.Tags("combat.evadable_tags");
        EvadableTags = evadable.IsEmpty ? TagSet.Of("attack") : evadable;
        var pools = rules.Strings("combat.pools");
        Pools = pools.Count == 0 ? new[] { "ward", "life" } : pools.ToArray();
        PoolsIncludeLife = Array.IndexOf(Pools, "life") >= 0;
        ResistCap = rules.Number("combat.resist_cap", 0.75);
        ResistFloor = rules.Number("combat.resist_floor", -2.0);
        Problems = problems;
    }

    public static HitPlan For(Rules rules) => Cache.GetValue(rules, static r => new HitPlan(r));

    public IHitStage[] Stages { get; }
    public string[] StageNames { get; }
    public VictimTagRule[] VictimTags { get; }
    public TagSet EvadableTags { get; }
    public string[] Pools { get; }
    public bool PoolsIncludeLife { get; }
    public double ResistCap { get; }
    public double ResistFloor { get; }

    /// <summary>What is wrong with the rules, for validation.</summary>
    public IReadOnlyList<string> Problems { get; }

    /// <summary>The stages, as bits by their index in <see cref="Stages"/>, that a hit with these tags skips.</summary>
    public ulong SkippedStages(TagSet hitTags)
    {
        var mask = 0UL;
        foreach (var (tag, stages, _) in _skips)
        {
            if (stages != 0 && hitTags.HasAll(tag))
            {
                mask |= stages;
            }
        }
        return mask;
    }

    /// <summary>Whether a hit with these tags skips the named step, a stage or not.</summary>
    public bool Skips(TagSet hitTags, string name)
    {
        foreach (var (tag, stages, others) in _skips)
        {
            if (!hitTags.HasAll(tag))
            {
                continue;
            }
            if (others.Contains(name))
            {
                return true;
            }
            var index = Array.IndexOf(StageNames, name);
            if (index >= 0 && (stages & (1UL << index)) != 0)
            {
                return true;
            }
        }
        return false;
    }
}
