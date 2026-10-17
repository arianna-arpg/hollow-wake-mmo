using System.Collections.Concurrent;
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

    /// <summary>The hit cannot take the target's last point of life.</summary>
    public bool CannotKill { get; set; }

    /// <summary>Life leech this hit carries on top of the attacker's own (a damage effect's <c>leech</c>).</summary>
    public double Leech { get; set; }

    /// <summary>
    /// Where the hit's amount fell within its range, from 0 to 1: var <c>roll</c>, drawn once by a damage effect whose
    /// amount reads it (<c>lerp(lo, hi, roll)</c>). Null when the amount rolled no position.
    /// </summary>
    public double? Roll { get; set; }

    /// <summary>The skip entries of rule <c>combat.stage_skips</c> the packet's tags matched when it was built; null until then.</summary>
    internal ulong? MatchedSkips { get; set; }
}

public sealed class HitResult
{
    public bool Evaded;
    public bool Blocked;
    public bool Immune;
    public bool Crit;
    public bool Killed;
    /// <summary>The target was culled at or below the attacker's threshold.</summary>
    public bool Culled;
    /// <summary>A per-hit cap clamped what the target received.</summary>
    public bool Clamped;
    /// <summary>The hit would have killed and the target cheated death.</summary>
    public bool CheatedDeath;
    public double Total;
    /// <summary>What the pools before life (status pools, ward, shield) absorbed.</summary>
    public double Absorbed;
    /// <summary>What was deferred into a stagger status instead of landing now.</summary>
    public double Staggered;
    /// <summary>Damage dealt to break bars (poise).</summary>
    public double Broken;
    /// <summary>The packet's roll within its range (<see cref="DamagePacket.Roll"/>), for high-roll effects; null when it rolled none.</summary>
    public double? Roll;
    public Dictionary<string, double> Received = new(StringComparer.Ordinal);
}

/// <summary>
/// The one path every hit takes. <see cref="Build"/> is the attacker's side: the base amount split over the
/// hit's Threads plus any per-Thread amounts, flat added damage per Thread from <c>added.{thread}</c> scaled by
/// its effectiveness, the attacker's damage fold scoped by the hit's tags (glyph modifiers joining) with the
/// victim tags in the context, conversion and extra-as, and the crit roll against the victim's crit avoidance.
/// <see cref="Apply"/> walks the defender's ladder: the stages registered by name in the order rule
/// <c>combat.stages</c> gives, skipping those rule <c>combat.stage_skips</c> names for the hit's tags; then
/// the hit is published, statuses roll and life at zero kills. Every formula lives in rules. There is no
/// unmitigated damage.
/// </summary>
public static class DamagePipeline
{
    public static DamagePacket Build(EffectContext ctx, double amount, IReadOnlyList<string> threads, TagSet extraTags, bool canCrit) =>
        Build(ctx, amount, threads, ReadOnlySpan<(string, double)>.Empty, extraTags, canCrit, null);

    /// <summary>
    /// Builds a packet from an amount split over <paramref name="threads"/> plus per-Thread
    /// <paramref name="shares"/>. <paramref name="effectiveness"/> scales the attacker's flat added damage;
    /// without one it is the weave's <c>damage.effectiveness</c> (one for hits without a weave).
    /// </summary>
    public static DamagePacket Build(EffectContext ctx, double amount, IReadOnlyList<string> threads, ReadOnlySpan<(string Thread, double Amount)> shares, TagSet extraTags, bool canCrit, double? effectiveness)
    {
        var source = ctx.SourceStats;
        var packet = new DamagePacket();
        var tags = ctx.Tags.Union(extraTags).With("hit", "damage");
        foreach (var t in threads)
        {
            tags = tags.With(t);
        }
        foreach (var (t, _) in shares)
        {
            tags = tags.With(t);
        }
        var hit = HitScope.Rent(ctx, packet);
        try
        {
            if (hit.Target is { } ts)
            {
                tags = tags.Union(VictimTagsOf(ctx, ts, hit));
            }
            packet.Tags = tags;
            var share = threads.Count == 0 ? amount : amount / threads.Count;
            var extra = ctx.Weave?.Modifiers;
            foreach (var t in threads)
            {
                AddScaled(packet, source, hit, t, share, tags, extra);
            }
            foreach (var (t, a) in shares)
            {
                AddScaled(packet, source, hit, t, a, tags, extra);
            }
            if (source is not null)
            {
                if (!hit.Skips("added"))
                {
                    AddFlat(ctx, source, packet, hit, extra, effectiveness);
                }
                ApplyConversion(hit.Names, source, packet, extra);
                RollCrit(ctx, source, packet, hit, canCrit, extra);
            }
        }
        finally
        {
            hit.Return();
        }
        return packet;
    }

    /// <summary>
    /// A packet of amounts already worked out (a rupture's burst, a pop): no attacker fold, added damage,
    /// conversion or crit, since what it carries was folded when it was banked; the victim tags still join it.
    /// </summary>
    public static DamagePacket BuildFixed(EffectContext ctx, ReadOnlySpan<(string Thread, double Amount)> amounts, TagSet extraTags)
    {
        var packet = new DamagePacket();
        var tags = ctx.Tags.Union(extraTags).With("hit", "damage");
        foreach (var (t, _) in amounts)
        {
            tags = tags.With(t);
        }
        var hit = HitScope.Rent(ctx, packet);
        try
        {
            if (hit.Target is { } ts)
            {
                tags = tags.Union(VictimTagsOf(ctx, ts, hit));
            }
            packet.Tags = tags;
            foreach (var (t, a) in amounts)
            {
                packet.Amounts[t] = packet.Amounts.TryGetValue(t, out var existing) ? existing + a : a;
            }
        }
        finally
        {
            hit.Return();
        }
        return packet;
    }

    /// <summary>A Thread's share folded through the attacker's <c>damage</c> in the hit scope, added to the packet.</summary>
    private static void AddScaled(DamagePacket packet, Components.Stats? source, HitScope hit, string thread, double share, TagSet tags, IReadOnlyList<Attributes.Modifier>? extra)
    {
        var threadTags = tags.With(thread);
        var scaled = source is null ? share : source.Attributes.Apply("damage", share, hit.AttackerScope, threadTags, extra);
        packet.Amounts[thread] = packet.Amounts.TryGetValue(thread, out var existing) ? existing + scaled : scaled;
    }

    /// <summary>
    /// Flat damage the attacker adds per Thread (<c>added.{thread}</c>, scoped by the hit's tags), including
    /// Threads the hit lacked, scaled by the added effectiveness and folded like the rest of the hit.
    /// </summary>
    private static void AddFlat(EffectContext ctx, Components.Stats source, DamagePacket packet, HitScope hit, IReadOnlyList<Attributes.Modifier>? extra, double? effectiveness)
    {
        var names = hit.Names;
        if (!CombatNames.MayCarry(source.Attributes, extra, names.AddedFamily))
        {
            return;
        }
        var effective = double.NaN;
        for (var j = 0; j < names.Threads.Length; j++)
        {
            var attr = names.Added[j];
            if (!source.Attributes.Carries(attr) && !Mentions(extra, attr))
            {
                continue;
            }
            var flat = source.Attributes.Get(attr, source, packet.Tags, extra);
            if (flat <= 0)
            {
                continue;
            }
            if (double.IsNaN(effective))
            {
                effective = AddedEffectiveness(ctx, source, packet.Tags, extra, effectiveness);
            }
            if (effective <= 0)
            {
                return;
            }
            AddScaled(packet, source, hit, names.Threads[j], flat * effective, packet.Tags, extra);
        }
    }

    private static double AddedEffectiveness(EffectContext ctx, Components.Stats source, TagSet tags, IReadOnlyList<Attributes.Modifier>? extra, double? effectiveness)
    {
        var e = effectiveness ?? (ctx.Weave is { } weave ? weave.Attr(source, "damage.effectiveness", ctx.Tags) : 1);
        if (source.Attributes.Catalog.Contains("added.effectiveness") || source.Attributes.HasModifiers("added.effectiveness") || Mentions(extra, "added.effectiveness"))
        {
            e *= source.Attributes.Get("added.effectiveness", source, tags, extra);
        }
        return e;
    }

    /// <summary>
    /// The crit roll: <c>crit.chance</c> lowered by the victim's <c>crit.avoid</c>, unless the hit's tags skip the
    /// crit step; damage over time that skips it may still crit by the attacker's <c>crit.dot</c>.
    /// </summary>
    private static void RollCrit(EffectContext ctx, Components.Stats source, DamagePacket packet, HitScope hit, bool canCrit, IReadOnlyList<Attributes.Modifier>? extra)
    {
        var tags = packet.Tags;
        double chance;
        if (canCrit && !hit.Skips("crit"))
        {
            chance = Math.Clamp(source.Attributes.Get("crit.chance", source, tags, extra), 0, 1);
        }
        else if (tags.Has("dot") && (source.Attributes.Carries("crit.dot") || Mentions(extra, "crit.dot")))
        {
            chance = Math.Clamp(source.Attributes.Get("crit.dot", source, tags, extra), 0, 1);
        }
        else
        {
            return;
        }
        if (hit.Target is { } target && target.Attributes.Carries("crit.avoid"))
        {
            chance *= 1 - Math.Clamp(target.Attributes.Get("crit.avoid", target, tags), -1, 1);
            chance = Math.Clamp(chance, 0, 1);
        }
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

    /// <summary>
    /// Victim scope: <c>vs.</c> plus each of the struck body's tags, flags and active statuses, then <c>vs.</c>
    /// plus the key of each rule in <c>combat.victim_tags</c> whose expression holds in the hit scope.
    /// </summary>
    private static TagSet VictimTagsOf(EffectContext ctx, Components.Stats ts, HitScope hit)
    {
        var vs = hit.VictimTags;
        vs.Clear();
        foreach (var tag in ts.Tags)
        {
            vs.Add(Vs(tag));
        }
        foreach (var flag in ts.Flags)
        {
            // The status: and forbid: copies of flags say nothing a victim scope needs.
            if (flag.StartsWith("status:", StringComparison.Ordinal) || flag.StartsWith("forbid:", StringComparison.Ordinal))
            {
                continue;
            }
            vs.Add(Vs(flag));
        }
        if (ctx.Sim.World.TryGet<Statuses>(ctx.Target, out var statuses))
        {
            foreach (var s in statuses.Active)
            {
                vs.Add(Vs(s.Id));
            }
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

    private static readonly ConcurrentDictionary<string, string> VsNames = new(StringComparer.Ordinal);

    /// <summary><c>vs.{name}</c>, built once per name.</summary>
    private static string Vs(string name) => VsNames.GetOrAdd(name, static n => "vs." + n);

    /// <summary>convert.{from}.{to} moves a fraction of one thread into another; extra.{to} adds a share of everything as another thread.</summary>
    private static void ApplyConversion(CombatNames names, Components.Stats source, DamagePacket packet, IReadOnlyList<Attributes.Modifier>? extra)
    {
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
                if (!source.Attributes.Carries(attr) && !Mentions(extra, attr))
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
            if (!source.Attributes.Carries(attr) && !Mentions(extra, attr))
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

    internal static bool Mentions(IReadOnlyList<Attributes.Modifier>? extra, string attribute)
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
        var result = new HitResult { Crit = packet.Crit, Roll = packet.Roll };
        var target = ctx.Target;
        if (!sim.World.TryGet<Components.Stats>(target, out var ts) || sim.World.Has<Dead>(target))
        {
            return result;
        }
        var hit = HitScope.Rent(ctx, packet, result);
        bool skipAilments;
        CombatNames names;
        try
        {
            names = hit.Names;
            hit.LoadThreads();
            var plan = hit.Plan;
            var stages = plan.Stages;
            var skipped = plan.SkippedStages(hit.MatchedSkips);
            skipAilments = hit.Skips("ailments");
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
        if (!skipAilments)
        {
            ApplyAilments(ctx, packet, result, names);
        }

        if (ts.Resource("life") <= 0)
        {
            result.Killed = true;
            sim.Service<DeathService>().Kill(sim, target, ctx.Origin, ctx.Depth);
            Sustain.OnKill(ctx, packet);
        }
        return result;
    }

    internal static void Publish(Simulation sim, EffectContext ctx, HitResult result, DamagePacket packet)
    {
        sim.Events.Publish(new DamageDealt(ctx.Source, ctx.Target, result.Total, result.Crit, packet.Tags, result.Received, result.Evaded, result.Blocked)
        {
            Tick = sim.Clock.Tick,
            Depth = ctx.Depth,
            Culled = result.Culled,
            Clamped = result.Clamped,
            Roll = result.Roll,
        });
    }

    /// <summary>
    /// Statuses a landed hit inflicts. Each Thread that dealt damage may leave its ailment by
    /// <c>ailment.chance</c> scoped to the Thread (plus the attacker's chance for that very status and a crit
    /// bonus); then every other status the attacker has a <c>status.{id}.chance</c> for rolls once. Magnitudes
    /// come from rule <c>combat.ailment_magnitude.{status}</c> (or the status's baseline), potency from
    /// <c>ailment.potency</c>. A block stops them all when rule <c>combat.block_stops_ailments</c> says so, and
    /// hits whose tags skip the <c>ailments</c> step (damage over time) leave none.
    /// </summary>
    private static void ApplyAilments(EffectContext ctx, DamagePacket packet, HitResult result, CombatNames names)
    {
        var sim = ctx.Sim;
        if (ctx.SourceStats is not { } source || !sim.TryService<StatusService>(out var statuses))
        {
            return;
        }
        var plan = HitPlan.For(sim.Content.Rules);
        if (result.Blocked && plan.BlockStopsAilments)
        {
            return;
        }
        var extra = ctx.Weave?.Modifiers;
        var chances = CombatNames.MayCarry(source.Attributes, extra, names.ChanceFamily);
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
            var chance = source.Attributes.Get("ailment.chance", source, packet.Tags.With(thread), extra);
            if (def.StatusChance is not null && Expr.TryParse(def.StatusChance, out var ce, out _))
            {
                chance += ce!.Evaluate(ctx.Scope);
            }
            var own = chances ? names.StatusChance(def.Status) : null;
            if (own is not null && (source.Attributes.Carries(own) || Mentions(extra, own)))
            {
                chance += source.Attributes.Get(own, source, packet.Tags.With(thread), extra);
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
            var magnitude = (plan.AilmentMagnitude(def.Status) ?? DefaultMagnitude).Evaluate(scope);
            var potency = 1 + source.Attributes.Get("ailment.potency", source, packet.Tags.With(thread), extra);
            statuses.Apply(sim, ctx.Target, def.Status, ctx.Source, new StatusApplication
            {
                Magnitude = magnitude,
                Potency = potency,
                SourceKey = ctx.Weave?.Key ?? "hit",
                Tags = packet.Tags,
                Depth = ctx.Depth,
            });
        }
        if (!chances || result.Total <= 0 || sim.World.Has<Dead>(ctx.Target))
        {
            return;
        }
        // Every other status the attacker has a chance for, rolled once per hit in id order.
        var ids = names.Statuses;
        for (var i = 0; i < ids.Length; i++)
        {
            var attr = names.Chances[i];
            if (!source.Attributes.Carries(attr) && !Mentions(extra, attr))
            {
                continue;
            }
            if (ThreadAilmentRolled(sim, result, ids[i]))
            {
                continue;
            }
            var chance = source.Attributes.Get(attr, source, packet.Tags, extra);
            if (chance <= 0 || !ctx.Rng.Chance(Math.Min(1, chance)))
            {
                continue;
            }
            var status = ids[i];
            var magnitude = MagnitudeOf(sim, ctx, source, status, result.Total);
            var potency = 1 + source.Attributes.Get("ailment.potency", source, packet.Tags, extra);
            statuses.Apply(sim, ctx.Target, status, ctx.Source, new StatusApplication
            {
                Magnitude = magnitude,
                Potency = potency,
                SourceKey = ctx.Weave?.Key ?? "hit",
                Tags = packet.Tags,
                Depth = ctx.Depth,
            });
            if (sim.World.Has<Dead>(ctx.Target))
            {
                return;
            }
        }
    }

    /// <summary>A Thread ailment's magnitude when the rules name none for it.</summary>
    private static readonly Expr DefaultMagnitude = Expr.Parse("amount * 0.25");

    /// <summary>Whether a Thread that dealt damage this hit already rolled this status as its own ailment.</summary>
    private static bool ThreadAilmentRolled(Simulation sim, HitResult result, string status)
    {
        foreach (var (thread, dealt) in result.Received)
        {
            if (dealt > 0 && sim.Content.Get<ThreadDef>(thread)?.Status == status)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>A status's magnitude from a hit: its <c>combat.ailment_magnitude</c> rule over the damage, else its baseline, else one.</summary>
    internal static double MagnitudeOf(Simulation sim, EffectContext ctx, Components.Stats source, string status, double amount)
    {
        if (HitPlan.For(sim.Content.Rules).AilmentMagnitude(status) is { } rule)
        {
            var scope = new DictScope(ctx.Rng).Set("amount", amount).Set("level", source.Level);
            return rule.Evaluate(scope);
        }
        if (sim.Content.Get<StatusDef>(status)?.Baseline is { } baseline)
        {
            return baseline.Evaluate(source);
        }
        return 1;
    }
}

/// <summary>
/// The attribute names the pipeline reads per Thread, per resource and per status, built once per content set
/// instead of per hit and never changed afterwards, so zones on several threads share them. A registry extended
/// in code after its first hit keeps the names it had, which only tools and tests do.
/// </summary>
internal sealed class CombatNames
{
    private static readonly ConditionalWeakTable<ContentRegistry, CombatNames> Cache = new();

    private readonly Dictionary<string, string[]> _convert = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string[]> _takenAs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _statusChance = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _statusLeech = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _threadIndex = new(StringComparer.Ordinal);

    private CombatNames(ContentRegistry content)
    {
        Threads = content.Each<ThreadDef>().Select(t => t.Id).ToArray();
        Extra = Threads.Select(t => $"extra.{t}").ToArray();
        Added = Threads.Select(t => $"added.{t}").ToArray();
        var defs = content.Each<ThreadDef>().ToDictionary(t => t.Id, StringComparer.Ordinal);
        Resist = Threads.Select(t => defs[t].Resist ?? $"resist.{t}").ToArray();
        ResistMax = Resist.Select(r => r + ".max").ToArray();
        Pen = Threads.Select(t => defs[t].Penetration ?? $"pen.{t}").ToArray();
        for (var i = 0; i < Threads.Length; i++)
        {
            _threadIndex[Threads[i]] = i;
        }
        foreach (var from in Threads)
        {
            _convert[from] = Threads.Select(t => $"convert.{from}.{t}").ToArray();
            _takenAs[from] = Threads.Select(t => $"taken_as.{from}.{t}").ToArray();
        }
        Statuses = content.Each<StatusDef>().Select(s => s.Id).OrderBy(s => s, StringComparer.Ordinal).ToArray();
        Chances = Statuses.Select(s => $"status.{s}.chance").ToArray();
        foreach (var s in Statuses)
        {
            _statusChance[s] = $"status.{s}.chance";
            _statusLeech["status." + s] = $"status.{s}.leech";
        }
        Resources = content.Each<ResourceDef>().Select(r => r.Id).ToArray();
        Leech = Resources.Select(r => $"leech.{r}").ToArray();
        OnHit = Resources.Select(r => $"on_hit.{r}").ToArray();
        OnKill = Resources.Select(r => $"on_kill.{r}").ToArray();
        LifeIndex = Array.IndexOf(Resources, "life");
        var catalog = content.Attributes;
        string? FamilyOf(string[] members) => members.Length == 0 ? null : catalog.Get(members[0])?.Family;
        AddedFamily = FamilyOf(Added);
        ChanceFamily = FamilyOf(Chances);
        LeechFamily = FamilyOf(Leech);
        OnHitFamily = FamilyOf(OnHit);
        OnKillFamily = FamilyOf(OnKill);
        TakenAsFamily = Threads.Length == 0 ? null : catalog.Get(TakenAs(Threads[0], 0))?.Family;
        StatusLeechFamily = Statuses.Length == 0 ? null : catalog.Get($"status.{Statuses[0]}.leech")?.Family;
    }

    // The attribute families the members above belong to, when content declares them as families. A family lets
    // a hit skip all of its members with one lookup; without one, members are checked one by one.
    public string? AddedFamily { get; }
    public string? ChanceFamily { get; }
    public string? LeechFamily { get; }
    public string? OnHitFamily { get; }
    public string? OnKillFamily { get; }
    public string? TakenAsFamily { get; }
    public string? StatusLeechFamily { get; }

    /// <summary>
    /// Whether an owner (with a weave's modifiers joining) might carry any member of a family, so its members are
    /// worth checking one by one. True when there is no family to ask.
    /// </summary>
    public static bool MayCarry(Attributes.AttributeSet set, IReadOnlyList<Attributes.Modifier>? extra, string? family)
    {
        if (family is null || set.CarriesFamily(family))
        {
            return true;
        }
        if (extra is null)
        {
            return false;
        }
        for (var i = 0; i < extra.Count; i++)
        {
            if (set.Catalog.Get(extra[i].Attribute)?.Family == family)
            {
                return true;
            }
        }
        return false;
    }

    public string[] Threads { get; }
    public string[] Extra { get; }
    public string[] Added { get; }
    public string[] Resist { get; }
    public string[] ResistMax { get; }
    public string[] Pen { get; }
    /// <summary>Status ids in id order, and the matching <c>status.{id}.chance</c> attributes.</summary>
    public string[] Statuses { get; }
    public string[] Chances { get; }
    public string[] Resources { get; }
    public string[] Leech { get; }
    public string[] OnHit { get; }
    public string[] OnKill { get; }
    public int LifeIndex { get; }

    public static CombatNames For(ContentRegistry content) => Cache.GetValue(content, static c => new CombatNames(c));

    /// <summary>The index of a Thread, or -1.</summary>
    public int ThreadIndex(string thread) => _threadIndex.TryGetValue(thread, out var i) ? i : -1;

    /// <summary><c>convert.{from}.{to}</c> for the thread at index <paramref name="to"/>.</summary>
    public string Convert(string from, int to) =>
        _convert.TryGetValue(from, out var row) ? row[to] : $"convert.{from}.{Threads[to]}";

    /// <summary><c>taken_as.{from}.{to}</c> for the thread at index <paramref name="to"/>.</summary>
    public string TakenAs(string from, int to) =>
        _takenAs.TryGetValue(from, out var row) ? row[to] : $"taken_as.{from}.{Threads[to]}";

    /// <summary><c>status.{id}.chance</c>, or null for an unknown status.</summary>
    public string? StatusChance(string status) => _statusChance.TryGetValue(status, out var a) ? a : null;

    /// <summary>The leech attribute of a status a damage-over-time tick carries the <c>status.{id}</c> tag of.</summary>
    public bool StatusLeech(string statusTag, out string attribute) => _statusLeech.TryGetValue(statusTag, out attribute!);
}

/// <summary>
/// The rules every hit reads, resolved once per ruleset: the stage order (<c>combat.stages</c>), which
/// stages hits with a tag skip (<c>combat.stage_skips</c>, a map from tag to stage names; a name that is
/// not a stage, such as <c>crit</c>, is checked by the step it names), the victim tag rules
/// (<c>combat.victim_tags</c>, a map from tag to expression), the pools and what each does
/// (<c>combat.pools</c>, <c>combat.pool_rules</c>), and the modes and numbers the built-in stages use.
/// </summary>
public sealed class HitPlan
{
    private static readonly ConditionalWeakTable<Rules, HitPlan> Cache = new();

    /// <summary>A victim tag and the expression, read in the hit scope, that grants it.</summary>
    public readonly record struct VictimTagRule(string Tag, Expr When);

    private readonly (TagSet Tag, ulong Stages, HashSet<string> Names)[] _skips;

    // For each stage, step or pool name, the skip entries (bits by index into _skips) that name it.
    private readonly Dictionary<string, ulong> _entriesByName = new(StringComparer.Ordinal);

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
        if (_skips.Length > 64)
        {
            problems.Add("combat.stage_skips has more than 64 tags; the rest are never matched");
            Array.Resize(ref _skips, 64);
        }
        for (var e = 0; e < _skips.Length; e++)
        {
            var (_, stageMask, others) = _skips[e];
            for (var i = 0; i < StageNames.Length; i++)
            {
                if ((stageMask & (1UL << i)) != 0)
                {
                    _entriesByName[StageNames[i]] = _entriesByName.GetValueOrDefault(StageNames[i]) | (1UL << e);
                }
            }
            foreach (var name in others)
            {
                _entriesByName[name] = _entriesByName.GetValueOrDefault(name) | (1UL << e);
            }
        }

        var victimTags = new List<VictimTagRule>();
        if (rules.Node("combat.victim_tags") is JsonObject vt)
        {
            foreach (var (tag, _) in vt)
            {
                victimTags.Add(new VictimTagRule("vs." + tag, Parse(rules, "combat.victim_tags." + tag, "0", problems)));
            }
        }
        VictimTags = victimTags.ToArray();

        var evadable = rules.Tags("combat.evadable_tags");
        EvadableTags = evadable.IsEmpty ? TagSet.Of("attack") : evadable;
        var pools = rules.Strings("combat.pools");
        Pools = pools.Count == 0 ? new[] { "ward", "life" } : pools.ToArray();
        PoolsIncludeLife = Array.IndexOf(Pools, "life") >= 0;
        PoolRules = new PoolRule[Pools.Length];
        for (var i = 0; i < Pools.Length; i++)
        {
            PoolRules[i] = PoolRule.Read(rules, Pools[i], problems);
        }
        ResistCap = rules.Number("combat.resist_cap", 0.75);
        ResistFloor = rules.Number("combat.resist_floor", -2.0);
        EntropyEvasion = rules.Text("combat.evasion", "roll") == "entropy";
        GuardBlock = rules.Text("combat.block", "fraction") == "guard";
        BlockStopsAilments = rules.Flag("combat.block_stops_ailments", false);
        StaggerStatus = rules.Text("combat.stagger_status", "stagger");
        HitChance = Parse(rules, "combat.hit_chance", "clamp(accuracy / max(1, accuracy + evasion * 0.25), 0.05, 1)", problems);
        ArmorReduction = Parse(rules, "combat.armor_reduction", "clamp(armor * (1 - pen) / (armor * (1 - pen) + 10 * raw), 0, 0.9)", problems);
        BlockValue = Parse(rules, "combat.block_value", "10 + level * 2 + block.value", problems);
        if (rules.Node("combat.ailment_magnitude") is JsonObject magnitudes)
        {
            foreach (var (status, _) in magnitudes)
            {
                _magnitudes[status] = Parse(rules, "combat.ailment_magnitude." + status, "1", problems);
            }
        }
        CheatDeathStatus = rules.Text("combat.cheat_death_status", "death_cheated");
        Problems = problems;
    }

    public static HitPlan For(Rules rules) => Cache.GetValue(rules, static r => new HitPlan(r));

    public IHitStage[] Stages { get; }
    public string[] StageNames { get; }
    public VictimTagRule[] VictimTags { get; }
    public TagSet EvadableTags { get; }
    /// <summary>Pool ids in the order hits reach them; the interface lists the ones before life.</summary>
    public string[] Pools { get; }
    /// <summary>What each pool in <see cref="Pools"/> does, by position.</summary>
    public PoolRule[] PoolRules { get; }
    public bool PoolsIncludeLife { get; }
    public double ResistCap { get; }
    public double ResistFloor { get; }
    /// <summary>Rule <c>combat.evasion</c> is <c>entropy</c>: evasion accumulates instead of rolling.</summary>
    public bool EntropyEvasion { get; }
    /// <summary>Rule <c>combat.block</c> is <c>guard</c>: a block stops up to a value rather than a fraction.</summary>
    public bool GuardBlock { get; }
    /// <summary>Rule <c>combat.block_stops_ailments</c>: a blocked hit inflicts no statuses.</summary>
    public bool BlockStopsAilments { get; }
    public string StaggerStatus { get; }
    public string CheatDeathStatus { get; }
    /// <summary>Rule <c>combat.hit_chance</c> over <c>accuracy</c>, <c>evasion</c> and <c>level</c>.</summary>
    public Expr HitChance { get; }
    /// <summary>Rule <c>combat.armor_reduction</c> over <c>armor</c>, <c>raw</c>, <c>level</c> and <c>pen</c>.</summary>
    public Expr ArmorReduction { get; }
    /// <summary>Rule <c>combat.block_value</c> over the blocker's <c>level</c> and <c>block.value</c>.</summary>
    public Expr BlockValue { get; }

    private readonly Dictionary<string, Expr> _magnitudes = new(StringComparer.Ordinal);

    /// <summary>Rule <c>combat.ailment_magnitude.{status}</c> over <c>amount</c> and <c>level</c>, or null when the rules name none.</summary>
    public Expr? AilmentMagnitude(string status) => _magnitudes.TryGetValue(status, out var e) ? e : null;

    /// <summary>
    /// A rule formula parsed for the plan alone, never through the shared rules cache: plans are built once per
    /// ruleset and read by hits on every zone thread.
    /// </summary>
    private static Expr Parse(Rules rules, string path, string fallback, List<string> problems)
    {
        if (rules.Node(path) is JsonValue v)
        {
            if (JsonNum.TryDouble(v, out var d))
            {
                return Expr.Constant(d);
            }
            if (v.TryGetValue<bool>(out var b))
            {
                return Expr.Constant(b ? 1 : 0);
            }
            if (v.TryGetValue<string>(out var text))
            {
                if (Expr.TryParse(text, out var parsed, out var error))
                {
                    return parsed!;
                }
                problems.Add($"{path} does not parse: {error}");
            }
        }
        return Expr.Parse(fallback);
    }

    /// <summary>What is wrong with the rules, for validation.</summary>
    public IReadOnlyList<string> Problems { get; }

    /// <summary>The skip entries a hit with these tags matches, as bits by their order in <c>combat.stage_skips</c>.</summary>
    public ulong MatchSkips(TagSet hitTags)
    {
        var matched = 0UL;
        var items = hitTags.Items;
        for (var e = 0; e < _skips.Length; e++)
        {
            var key = _skips[e].Tag.Items;
            var all = true;
            for (var k = 0; k < key.Count; k++)
            {
                if (!HasTag(items, key[k]))
                {
                    all = false;
                    break;
                }
            }
            if (all)
            {
                matched |= 1UL << e;
            }
        }
        return matched;
    }

    /// <summary>
    /// <see cref="TagSet.Has"/> over a set's sorted items: the tag itself, or a descendant (<c>tag.x</c>), which sorts
    /// right after where the tag would stand.
    /// </summary>
    private static bool HasTag(IReadOnlyList<string> sorted, string tag)
    {
        int lo = 0, hi = sorted.Count - 1;
        while (lo <= hi)
        {
            var mid = (lo + hi) >> 1;
            var c = string.CompareOrdinal(sorted[mid], tag);
            if (c == 0)
            {
                return true;
            }
            if (c < 0)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }
        for (var i = lo; i < sorted.Count; i++)
        {
            var s = sorted[i];
            if (!s.StartsWith(tag, StringComparison.Ordinal))
            {
                return false;
            }
            if (s.Length > tag.Length && s[tag.Length] == '.')
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Whether a hit that matched these skip entries skips the named stage, step or pool.</summary>
    public bool Skips(ulong matched, string name) => matched != 0 && _entriesByName.TryGetValue(name, out var entries) && (entries & matched) != 0;

    /// <summary>The stages, as bits by their index in <see cref="Stages"/>, that a hit matching these skip entries skips.</summary>
    public ulong SkippedStages(ulong matched)
    {
        var mask = 0UL;
        for (var e = 0; matched != 0 && e < _skips.Length; e++)
        {
            if ((matched & (1UL << e)) != 0)
            {
                mask |= _skips[e].Stages;
            }
        }
        return mask;
    }

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

    /// <summary>The rule of a pool by id, or null when the pool is not in <c>combat.pools</c>.</summary>
    public PoolRule? PoolRuleOf(string pool)
    {
        for (var i = 0; i < Pools.Length; i++)
        {
            if (Pools[i] == pool)
            {
                return PoolRules[i];
            }
        }
        return null;
    }
}

/// <summary>
/// What a pool in rule <c>combat.pools</c> does, from <c>combat.pool_rules.{id}</c>. An absorbing pool takes
/// damage before life (a <c>fraction</c> of what reaches it at most); a break bar (<c>kind: break</c>) never
/// absorbs but loses <c>dealt</c> times <c>taken</c> of each landing hit and breaks when it empties. A landing
/// hit that reaches a pool the body holds applies <c>on_hurt</c> (the shield's recharge delay), and one that
/// empties it applies <c>on_break</c>. With <c>states</c> the status system keeps the pool's state flags
/// (<c>pool.{id}.broken</c>, <c>.rearming</c>, <c>.recharging</c>) for the interface: broken while empty or
/// while <c>on_break</c> lasts, re-arming while <c>on_hurt</c> or <c>rearm</c> lasts, recharging while it refills.
/// </summary>
public sealed class PoolRule
{
    public required string Resource { get; init; }
    public bool Break { get; init; }
    public double Fraction { get; init; } = 1;
    public string? Dealt { get; init; }
    public string? Taken { get; init; }
    public string? OnHurt { get; init; }
    public string? OnBreak { get; init; }
    public string? Rearm { get; init; }
    public bool States { get; init; }
    public string BrokenFlag { get; init; } = "";
    public string RearmingFlag { get; init; } = "";
    public string RechargingFlag { get; init; } = "";

    internal static PoolRule Read(Rules rules, string pool, List<string> problems)
    {
        var path = "combat.pool_rules." + pool;
        var kind = rules.Text(path + ".kind", "absorb");
        if (kind is not ("absorb" or "break"))
        {
            problems.Add($"{path}.kind must be absorb or break, not '{kind}'");
        }
        string? Text(string key) => rules.Node(path + "." + key) is JsonValue v && v.TryGetValue<string>(out var s) && s.Length > 0 ? s : null;
        var isBreak = kind == "break";
        return new PoolRule
        {
            Resource = pool,
            Break = isBreak,
            Fraction = Math.Clamp(rules.Number(path + ".fraction", 1), 0, 1),
            Dealt = Text("dealt") ?? (isBreak ? pool + ".damage" : null),
            Taken = Text("taken") ?? (isBreak ? pool + ".damage.taken" : null),
            OnHurt = Text("on_hurt"),
            OnBreak = Text("on_break"),
            Rearm = Text("rearm"),
            States = rules.Flag(path + ".states", false),
            BrokenFlag = $"pool.{pool}.broken",
            RearmingFlag = $"pool.{pool}.rearming",
            RechargingFlag = $"pool.{pool}.recharging",
        };
    }
}
