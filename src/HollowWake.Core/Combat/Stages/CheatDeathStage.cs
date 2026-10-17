using HollowWake.Core.Sim;
using HollowWake.Core.StatusEffects;

namespace HollowWake.Core.Combat.Stages;

/// <summary>
/// Cheating death: a hit that leaves the target without life rolls the target's <c>cheat_death.chance</c>;
/// on success the target keeps one point of life and gains the status rule <c>combat.cheat_death_status</c>
/// names, and while that status lasts it cannot cheat death again.
/// </summary>
internal sealed class CheatDeathStage : IHitStage
{
    public string Name => "cheat_death";

    public HitFlow Run(HitScope hit)
    {
        var ts = hit.Target!;
        if (ts.Resource("life") > 0 || !ts.Attributes.Carries("cheat_death.chance"))
        {
            return HitFlow.Continue;
        }
        var sim = hit.Sim;
        var ctx = hit.Context;
        var cooldown = hit.Plan.CheatDeathStatus;
        if (sim.World.TryGet<Components.Statuses>(ctx.Target, out var worn) && worn.Has(cooldown))
        {
            return HitFlow.Continue;
        }
        var chance = Math.Clamp(ts.Get("cheat_death.chance", hit.Tags), 0, 1);
        if (chance <= 0 || !ctx.Rng.Chance(chance))
        {
            return HitFlow.Continue;
        }
        ts.SetResource("life", 1);
        hit.Result.CheatedDeath = true;
        if (sim.TryService<StatusService>(out var statuses))
        {
            statuses.Apply(sim, ctx.Target, cooldown, ctx.Target, new StatusApplication { FixedDuration = true, SourceKey = "cheat_death", Depth = ctx.Depth });
        }
        sim.Events.Publish(new DeathCheated(ctx.Target, ctx.Source) { Tick = sim.Clock.Tick, Depth = ctx.Depth });
        CombatTriggers.Raise(sim, ctx.Target, CombatTriggers.CheatDeath, ctx.Source, ctx.Target, null, ctx.Depth);
        return HitFlow.Continue;
    }
}
