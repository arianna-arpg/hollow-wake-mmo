using HollowWake.Core.Sim;
using HollowWake.Core.StatusEffects;

namespace HollowWake.Core.Combat.Stages;

/// <summary>
/// Stagger: the target's <c>stagger.fraction</c> of each landing hit is taken out of it and banked into the
/// status rule <c>combat.stagger_status</c> names, which deals it out over its duration (its ticks carry the
/// <c>deferred</c> tag, which rule <c>combat.stage_skips</c> sends past every stage that already ran).
/// </summary>
internal sealed class StaggerStage : IHitStage
{
    public string Name => "stagger";

    public HitFlow Run(HitScope hit)
    {
        var ts = hit.Target!;
        if (hit.Total <= 0 || !ts.Attributes.Carries("stagger.fraction"))
        {
            return HitFlow.Continue;
        }
        var fraction = Math.Clamp(ts.Get("stagger.fraction", hit.Tags), 0, 1);
        if (fraction <= 0)
        {
            return HitFlow.Continue;
        }
        var sim = hit.Sim;
        if (!sim.TryService<StatusService>(out var statuses))
        {
            return HitFlow.Continue;
        }
        var deferred = hit.Total * fraction;
        hit.ScaleReceived(1 - fraction);
        hit.Result.Staggered = deferred;
        var ctx = hit.Context;
        // The deferral keeps the status's own duration: the attacker's effect duration does not stretch it.
        statuses.Apply(sim, ctx.Target, hit.Plan.StaggerStatus, ctx.Source, new StatusApplication
        {
            BankAmount = deferred,
            Magnitude = deferred,
            FixedDuration = true,
            SourceKey = "stagger",
            Tags = hit.Tags,
            Depth = ctx.Depth,
        });
        sim.Events.Publish(new HitStaggered(ctx.Target, ctx.Source, deferred) { Tick = sim.Clock.Tick, Depth = ctx.Depth });
        return HitFlow.Continue;
    }
}
