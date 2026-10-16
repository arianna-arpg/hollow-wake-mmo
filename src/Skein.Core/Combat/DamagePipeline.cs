using Skein.Core.Components;
using Skein.Core.Definitions;
using Skein.Core.Ecs;
using Skein.Core.Effects;
using Skein.Core.Expressions;
using Skein.Core.Sim;
using Skein.Core.StatusEffects;
using Skein.Core.Tags;

namespace Skein.Core.Combat;

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
/// The one path every hit takes. Stages, in order: base amount from the weave, the attacker's damage
/// stack scoped by the hit's tags (glyph modifiers join the same fold), conversion and extra-as,
/// crit, hit or evade, block, per-thread mitigation (armor rule or capped resistance less
/// penetration), the target's damage-taken stack, then the pools in rule order, then culling. All of
/// the formulas live in rules; the stage order lives here. There is no unmitigated damage.
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
        if (ctx.TargetStats is { } ts)
        {
            // Victim scope: vs.burning, vs.rare, vs.undead ...
            var vs = new List<string>();
            foreach (var tag in ts.Tags)
            {
                vs.Add("vs." + tag);
            }
            foreach (var flag in ts.Flags)
            {
                vs.Add("vs." + flag);
            }
            if (ts.ResourceFraction("life") <= 0.35)
            {
                vs.Add("vs.low_life");
            }
            if (ts.ResourceFraction("life") >= 0.95)
            {
                vs.Add("vs.full_life");
            }
            tags = tags.Union(TagSet.From(vs));
        }
        packet.Tags = tags;
        var share = threads.Count == 0 ? amount : amount / threads.Count;
        var extra = ctx.Weave?.Modifiers;
        foreach (var t in threads)
        {
            var threadTags = tags.With(t);
            var scaled = source is null ? share : source.Attributes.Apply("damage", share, source, threadTags, extra);
            packet.Amounts[t] = packet.Amounts.TryGetValue(t, out var existing) ? existing + scaled : scaled;
        }
        if (source is not null)
        {
            ApplyConversion(ctx, source, packet, extra);
            if (canCrit)
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
        return packet;
    }

    /// <summary>convert.{from}.{to} moves a fraction of one thread into another; extra.{to} adds a share of everything as another thread.</summary>
    private static void ApplyConversion(EffectContext ctx, Stats source, DamagePacket packet, IReadOnlyList<Attributes.Modifier>? extra)
    {
        var threads = ctx.Sim.Content.Each<ThreadDef>().Select(t => t.Id).ToList();
        var original = new Dictionary<string, double>(packet.Amounts, StringComparer.Ordinal);
        foreach (var (from, amount) in original)
        {
            if (amount <= 0)
            {
                continue;
            }
            double converted = 0;
            foreach (var to in threads)
            {
                if (to == from)
                {
                    continue;
                }
                var attr = $"convert.{from}.{to}";
                if (!source.Attributes.HasModifiers(attr) && (extra is null || !extra.Any(m => m.Attribute == attr)))
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
        foreach (var to in threads)
        {
            var attr = $"extra.{to}";
            if (!source.Attributes.HasModifiers(attr) && (extra is null || !extra.Any(m => m.Attribute == attr)))
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

    public static HitResult Apply(EffectContext ctx, DamagePacket packet)
    {
        var sim = ctx.Sim;
        var result = new HitResult { Crit = packet.Crit };
        var target = ctx.Target;
        if (!sim.World.TryGet<Stats>(target, out var ts) || sim.World.Has<Dead>(target))
        {
            return result;
        }
        var source = ctx.SourceStats;
        var rules = sim.Content.Rules;
        var tags = packet.Tags;
        var extra = ctx.Weave?.Modifiers;

        if (ts.HasFlag("immune") || ts.HasFlag("invulnerable"))
        {
            result.Immune = true;
            return result;
        }

        // Hit or evade: only hits carrying an evadable tag roll.
        var evadable = rules.Tags("combat.evadable_tags");
        if (evadable.IsEmpty)
        {
            evadable = TagSet.Of("attack");
        }
        if (source is not null && tags.HasAny(evadable))
        {
            var accuracy = source.Attributes.Get("accuracy", source, tags, extra);
            var evasion = ts.Get("evasion");
            var scope = new DictScope(ctx.Rng).Set("accuracy", accuracy).Set("evasion", evasion).Set("level", source.Level);
            var chance = Math.Clamp(rules.Eval("combat.hit_chance", "clamp(accuracy / max(1, accuracy + evasion * 0.25), 0.05, 1)", scope), 0, 1);
            if (!ctx.Rng.Chance(chance))
            {
                result.Evaded = true;
                ts.Stamp("evaded", sim.Clock.Time);
                Publish(sim, ctx, result, packet);
                return result;
            }
        }

        // Block: a fraction of the hit is refused.
        var blockChance = Math.Clamp(ts.Get("block.chance", tags), 0, 1);
        var blockAmount = 1.0;
        if (blockChance > 0 && ctx.Rng.Chance(blockChance))
        {
            result.Blocked = true;
            blockAmount = 1 - Math.Clamp(ts.Get("block.amount"), 0, 1);
            ts.Stamp("blocked", sim.Clock.Time);
        }

        // Mitigation per thread.
        var resistCap = rules.Number("combat.resist_cap", 0.75);
        var resistFloor = rules.Number("combat.resist_floor", -2.0);
        double total = 0;
        foreach (var (thread, raw) in packet.Amounts)
        {
            if (raw <= 0)
            {
                continue;
            }
            var amount = raw * blockAmount;
            var def = sim.Content.Get<ThreadDef>(thread);
            var mitigation = def?.Mitigation ?? "resist";
            if (mitigation == "armor")
            {
                var armor = ts.Get("armor");
                var scope = new DictScope(ctx.Rng).Set("armor", armor).Set("raw", amount).Set("level", ts.Level);
                var reduction = Math.Clamp(rules.Eval("combat.armor_reduction", "armor / (armor + 10 * raw)", scope), 0, 0.9);
                amount *= 1 - reduction;
            }
            else if (mitigation == "resist")
            {
                var resistAttr = def?.Resist ?? $"resist.{thread}";
                var capAttr = resistAttr + ".max";
                var cap = Math.Min(resistCap, ts.Attributes.Catalog.Contains(capAttr) || ts.Attributes.HasModifiers(capAttr) ? ts.Get(capAttr) : resistCap);
                var res = Math.Min(ts.Get(resistAttr), cap);
                if (source is not null)
                {
                    var penAttr = def?.Penetration ?? $"pen.{thread}";
                    var pen = source.Attributes.Get(penAttr, source, tags, extra);
                    if (pen > 0)
                    {
                        res = Math.Max(resistFloor, res - pen);
                    }
                }
                amount *= 1 - res;
            }
            // Damage taken stack on the target, scoped by the hit's tags plus the thread.
            amount = ts.Attributes.Apply("damage.taken", amount, ts, tags.With(thread));
            if (amount > 0)
            {
                result.Received[thread] = amount;
                total += amount;
            }
        }

        // Pools in rule order; life is always last.
        var pools = rules.Strings("combat.pools");
        if (pools.Count == 0)
        {
            pools = new List<string> { "ward", "life" };
        }
        var remaining = total;
        foreach (var pool in pools)
        {
            if (remaining <= 0)
            {
                break;
            }
            if (pool != "life" && !ts.Resources.ContainsKey(pool))
            {
                continue;
            }
            var cur = ts.Resource(pool);
            var take = Math.Min(cur, remaining);
            if (take > 0)
            {
                ts.AddResource(pool, -take);
                remaining -= take;
            }
            if (pool == "life")
            {
                break;
            }
        }
        if (remaining > 0 && !pools.Contains("life"))
        {
            ts.AddResource("life", -remaining);
        }
        result.Total = total;
        ts.Stamp("hurt", sim.Clock.Time);
        if (source is not null)
        {
            source.Stamp("hit", sim.Clock.Time);
            if (packet.Crit)
            {
                source.Stamp("crit", sim.Clock.Time);
            }
        }

        // Culling.
        if (source is not null && ts.Resource("life") > 0)
        {
            var cull = Math.Clamp(source.Attributes.Get("cull.threshold", source, tags, extra), 0, 1);
            if (cull > 0 && ts.ResourceFraction("life") <= cull)
            {
                ts.SetResource("life", 0);
            }
        }

        Publish(sim, ctx, result, packet);
        ApplyAilments(ctx, packet, result);

        if (ts.Resource("life") <= 0)
        {
            result.Killed = true;
            sim.Service<DeathService>().Kill(sim, target, ctx.Origin);
        }
        return result;
    }

    private static void Publish(Simulation sim, EffectContext ctx, HitResult result, DamagePacket packet)
    {
        sim.Events.Publish(new DamageDealt(ctx.Source, ctx.Target, result.Total, result.Crit, packet.Tags, result.Received, result.Evaded, result.Blocked) { Tick = sim.Clock.Tick });
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
            });
        }
    }
}
