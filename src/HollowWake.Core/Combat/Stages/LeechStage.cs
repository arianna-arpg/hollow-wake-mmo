using HollowWake.Core.Components;
using HollowWake.Core.Effects;
using HollowWake.Core.Sim;

namespace HollowWake.Core.Combat.Stages;

/// <summary>
/// Leech and on-hit sustain, after the pools: the attacker regains <c>leech.{resource}</c> (a fraction of the
/// damage the hit dealt, scoped by its tags and bounded like any attribute) plus <c>on_hit.{resource}</c> for
/// every resource, plus the hit's own leech on life, plus <c>status.{id}.leech</c> of the damage over time a
/// status it applied deals. A dead attacker regains nothing. Each gain is published as <see cref="LeechPaid"/>.
/// </summary>
internal sealed class LeechStage : IHitStage
{
    public string Name => "leech";

    public HitFlow Run(HitScope hit)
    {
        if (hit.Source is { } source && hit.Total > 0)
        {
            Sustain.OnHit(hit, source);
        }
        return HitFlow.Continue;
    }
}

/// <summary>What an attacker regains from its hits and kills.</summary>
internal static class Sustain
{
    public static void OnHit(HitScope hit, Stats source)
    {
        var ctx = hit.Context;
        var sim = ctx.Sim;
        if (sim.World.Has<Dead>(ctx.Source))
        {
            return;
        }
        var names = CombatNames.For(sim.Content);
        var extra = hit.Extra;
        var tags = hit.Tags;
        var dealt = hit.Total;
        var attributes = source.Attributes;
        var leech = CombatNames.MayCarry(attributes, extra, names.LeechFamily);
        var onHit = CombatNames.MayCarry(attributes, extra, names.OnHitFamily);
        // Damage over time leeches by its status's own family: the tick carries the status.{id} tag.
        var statusLeech = 0.0;
        if (tags.Has("dot") && CombatNames.MayCarry(attributes, extra, names.StatusLeechFamily))
        {
            foreach (var tag in tags.Items)
            {
                if (names.StatusLeech(tag, out var attr) && source.Attributes.Carries(attr))
                {
                    statusLeech += source.Attributes.Get(attr, source, tags, extra);
                }
            }
        }
        var own = hit.Packet.Leech + statusLeech;
        if (!leech && !onHit && own <= 0)
        {
            return;
        }
        for (var r = 0; r < names.Resources.Length; r++)
        {
            var leechAttr = names.Leech[r];
            var fraction = leech && (attributes.Carries(leechAttr) || DamagePipeline.Mentions(extra, leechAttr)) ? attributes.Get(leechAttr, source, tags, extra) : 0;
            if (r == names.LifeIndex)
            {
                fraction += own;
            }
            var onHitAttr = names.OnHit[r];
            var flat = onHit && (attributes.Carries(onHitAttr) || DamagePipeline.Mentions(extra, onHitAttr)) ? attributes.Get(onHitAttr, source, tags, extra) : 0;
            if (fraction > 0)
            {
                Pay(sim, ctx, source, names.Resources[r], dealt * fraction, "leech");
            }
            if (flat > 0)
            {
                Pay(sim, ctx, source, names.Resources[r], flat, "hit");
            }
        }
    }

    /// <summary>On a kill the attacker regains its <c>on_kill.{resource}</c>.</summary>
    public static void OnKill(EffectContext ctx, DamagePacket packet)
    {
        var sim = ctx.Sim;
        if (ctx.SourceStats is not { } source || sim.World.Has<Dead>(ctx.Source))
        {
            return;
        }
        var names = CombatNames.For(sim.Content);
        var extra = ctx.Weave?.Modifiers;
        if (!CombatNames.MayCarry(source.Attributes, extra, names.OnKillFamily))
        {
            return;
        }
        for (var r = 0; r < names.Resources.Length; r++)
        {
            var attr = names.OnKill[r];
            if (!source.Attributes.Carries(attr) && !DamagePipeline.Mentions(extra, attr))
            {
                continue;
            }
            var flat = source.Attributes.Get(attr, source, packet.Tags, extra);
            if (flat > 0)
            {
                Pay(sim, ctx, source, names.Resources[r], flat, "kill");
            }
        }
    }

    private static void Pay(Sim.Simulation sim, EffectContext ctx, Stats source, string resource, double amount, string kind)
    {
        var gained = source.AddResource(resource, amount);
        if (gained > 0)
        {
            sim.Events.Publish(new LeechPaid(ctx.Source, ctx.Target, resource, gained, kind) { Tick = sim.Clock.Tick, Depth = ctx.Depth });
        }
    }
}
