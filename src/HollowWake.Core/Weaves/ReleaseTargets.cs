using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Effects.Handlers;
using HollowWake.Core.Expressions;
using HollowWake.Core.Sim;
using HollowWake.Core.World;

namespace HollowWake.Core.Weaves;

/// <summary>One release-time rule for every root application to a stored body. Never reacquires a target.</summary>
public static class ReleaseTargets
{
    public static bool Check(Simulation sim, Entity caster, Entity target, CompiledWeave weave, out string reason)
    {
        reason = "";
        var policy = weave.Form.ReleaseCheck ?? (sim.Content.Rules.Flag("casting.recheck_target_at_release", true) ? "full" : "none");
        // A targetless placement is an aim point, not a stored body. Its delivery owns positional selection.
        if (policy == "none" || target.IsNone || weave.Targeting is not (Targeting.Enemy or Targeting.Ally or Targeting.Any)) return true;
        if (!sim.World.IsAlive(target)) { reason = "dead"; return false; }
        if (weave.Form.Target is { } spec)
        {
            if (!TargetKinds.TryGet(spec.Kind, out var kind) || !kind.Accepts(sim, caster, target))
            { reason = "invalid_target"; return false; }
            if (policy == "full" && spec.Require is { } require && sim.World.Get<Stats>(caster) is { } source)
            {
                var scope = new ScopeOverlay(new CasterWeaveScope(weave, source));
                if (sim.World.Get<Stats>(target) is { } victim) scope.Mount("target", victim);
                if (!require.IsTrue(scope)) { reason = "target_condition"; return false; }
            }
        }
        else
        {
            if (sim.World.Has<Dead>(target)) { reason = "dead"; return false; }
            if (policy == "full" && weave.Targeting == Targeting.Enemy && !Relations.IsHostile(sim, caster, target))
            { reason = "not_hostile"; return false; }
            if (policy == "full" && weave.Targeting == Targeting.Ally && Relations.IsHostile(sim, caster, target))
            { reason = "not_ally"; return false; }
        }
        if (sim.World.Get<Stats>(target)?.Flags.Contains("untargetable") == true)
        { reason = "untargetable"; return false; }
        var from = sim.World.Get<Transform>(caster);
        var to = sim.World.Get<Transform>(target);
        var stats = sim.World.Get<Stats>(caster);
        if (from is null || to is null || stats is null) { reason = "missing_position"; return false; }
        var range = weave.Range(stats);
        if (range > 0 && from.GroundDistanceTo(to.Position) > range + from.Radius + to.Radius ||
            MathF.Abs(from.Position.Z - to.Position.Z) > SpatialGrid.DefaultHeightTolerance)
        { reason = "out_of_reach"; return false; }
        if (policy == "full" && weave.AttrOrZero(stats, "phasing") <= 0 &&
            !LineOfSight.Clear(sim.Terrain, from.Position + EffectHelpers.Eye, to.Position + EffectHelpers.Eye))
        { reason = "line_of_sight"; return false; }
        return true;
    }

    /// <summary>Nested presets, metas and commanded casts use the same gate as active casts.</summary>
    public static bool Execute(EffectContext context, IReadOnlyList<CompiledEffect> effects)
    {
        if (context.Weave is { } weave && !Check(context.Sim, context.Source, context.Target, weave, out var reason))
        {
            Miss(context, reason);
            return false;
        }
        CompiledEffect.ExecuteAll(effects, context);
        return true;
    }

    internal static void Miss(EffectContext context, string reason, bool emitted = false)
    {
        context.Sim.Events.Publish(new WeaveMissed(context.Source, context.Slot?.Key ?? "", context.Weave?.Key ?? "", context.Target, reason)
            { CastId = context.CastId, Tick = context.Sim.Clock.Tick, Depth = context.Depth });
        if (context.Sim.EmitCastPresentation && context.Weave is { } weave)
            context.Sim.Events.Publish(new CastPresentationEvent(context.CastId, "miss", context.Source, context.Target,
                weave.Key, weave.Name, CastPresentationEvent.ModeName(weave.CastMode), context.Tags,
                context.SourceTransform?.Position ?? context.Point, context.Point, context.Sim.Clock.Time, 0,
                context.Sim.Clock.Time, emitted, reason) { Tick = context.Sim.Clock.Tick, Mechanisms = weave.Mechanisms, Outcome = true });
    }
}
