using System.Numerics;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects.Handlers;
using HollowWake.Core.Maths;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;
using HollowWake.Core.World;

namespace HollowWake.Core.Effects;

/// <summary>
/// A sampled primary area, shared by execution and observation. Core units: angles in degrees,
/// inner radius as a fraction, X east/Y north/Z up. This is a candidate region, never a damage promise.
/// Resolve again when the context changes; this value does not freeze a cast's future geometry.
/// </summary>
public readonly record struct ResolvedAreaPlan(
    Shape Shape, ShapeSize Size, Vector3 Anchor, Vector2 Direction, string AnchorMode,
    string Filter, Entity ExcludedTarget, bool RequiresLineOfSight,
    bool HasRequirement, bool HasTargetLimit)
{
    public float Reach => Shape.Reach(Size);
    public float HeightTolerance => SpatialGrid.DefaultHeightTolerance;

    /// <summary>Areas test body centers, with the same vertical tolerance as their spatial query.</summary>
    public bool ContainsCenter(Vector3 point) => MathF.Abs(point.Z - Anchor.Z) <= HeightTolerance &&
        Shape.Contains(Size, new Vector2(point.X - Anchor.X, point.Y - Anchor.Y), Direction, 0);

    /// <summary>
    /// Current candidate eligibility, before a delivery's per-target requirement and capped/random
    /// selection. Uses the same relation, untargetability and LOS checks as execution. No effects or rolls.
    /// </summary>
    public bool IsCandidate(EffectContext context, Entity entity, Vector3 position) =>
        ContainsCenter(position) && (ExcludedTarget.IsNone || entity != ExcludedTarget) &&
        EffectHelpers.Passes(context, entity, Filter) && (!RequiresLineOfSight ||
            LineOfSight.Clear(context.Sim.Terrain, Anchor + EffectHelpers.Eye, position + EffectHelpers.Eye));

    public bool IsFinite => float.IsFinite(Size.Radius) && float.IsFinite(Size.Angle) &&
        float.IsFinite(Size.Width) && float.IsFinite(Size.Inner) && float.IsFinite(Reach) &&
        float.IsFinite(Shape.Curvature) && float.IsFinite(Shape.CenterAllowanceSquared) && Shape.CenterAllowanceSquared >= 0 &&
        float.IsFinite(Anchor.X) && float.IsFinite(Anchor.Y) && float.IsFinite(Anchor.Z) &&
        float.IsFinite(Direction.X) && float.IsFinite(Direction.Y);
}

/// <summary>
/// An opt-in capability of a compiled effect supplied by its registered handler, after glyph patches.
/// Providers must not execute effects, consume random streams or infer future targets. New mechanisms
/// implement this capability; no central dispatch by skill/form/glyph/species identity is necessary.
/// </summary>
public interface IDeliveryPlanSource
{
    DeliveryForecast DescribeDelivery(EffectContext context);
}

/// <summary>
/// Internal fast path for a fresh observation-only context. It may initialize that context's scope;
/// callers must not pass an execution context or reuse it for a later sample. Providers still perform
/// the same live dependency checks and resolution. Third-party providers retain the public path.
/// </summary>
internal interface IDetachedDeliveryPlanSource : IDeliveryPlanSource
{
    DeliveryForecast DescribeDetachedDelivery(EffectContext context);
}

// Synchronous built-in capability: never retain the context or expose it to external providers.
// Unlike persistent DescribeDetachedDelivery, this borrows a freshly rebound context/scope per call.
// null means unknown, retaining the caller's existing behavior rather than claiming ineligibility.
internal interface IImmediateDeliveryCandidateSource : IDetachedDeliveryPlanSource
{
    bool? IsCandidate(EffectContext context, Entity target, Vector3 position);
}

/// <summary>
/// A primary-region observation only. It deliberately does not certify payload effects, secondary
/// scatter/pulses, later graph nodes, hit outcomes, cast commitment or application times.
/// A missing region means unknown coverage, not empty/safe space.
/// </summary>
public sealed record DeliveryForecast(ResolvedAreaPlan? PrimaryRegion, string? UnavailableReason)
{
    public static DeliveryForecast Unavailable(string reason) => new(null, reason);
}

public static class DeliveryForecasts
{
    internal static bool? CandidateForAi(CompiledEffect effect, Simulation sim, Entity caster,
        Entity target, Vector3 point, Vector2 direction, CompiledWeave weave)
    {
        if (effect is IImmediateDeliveryCandidateSource immediate)
        {
            var scratch = EffectContext.RentObservation(sim, caster, target, point, direction, weave);
            try { return immediate.IsCandidate(scratch, target, point); }
            finally { scratch.ReturnObservation(); }
        }
        // Preserve the public provider's detached lifetime and safety contract.
        var context = new EffectContext { Sim = sim, Source = caster, Target = target, Point = point,
            Direction = direction, Weave = weave, Tags = weave.Tags, Rng = new Rng(0) };
        return Describe(effect, context).PrimaryRegion is { } plan ? plan.IsCandidate(context, target, point) : null;
    }

    internal static DeliveryForecast DescribeDetached(IReadOnlyList<CompiledEffect> graph, EffectContext context) =>
        graph.Count == 1 && graph[0] is IDetachedDeliveryPlanSource provider
            ? provider.DescribeDetachedDelivery(context) : Describe(graph, context);

    public static DeliveryForecast Describe(CompiledEffect effect, EffectContext context) =>
        effect is IDeliveryPlanSource provider ? provider.DescribeDelivery(context) :
            DeliveryForecast.Unavailable("unsupported_delivery");

    /// <summary>
    /// A single compiled root can expose its primary region. Multiple roots may change the state read
    /// by later roots, so do not evaluate them all in the initial context and pretend that is execution.
    /// Wrappers need their own providers that preserve their real timing, placement and variables.
    /// </summary>
    public static DeliveryForecast Describe(IReadOnlyList<CompiledEffect> graph, EffectContext context) =>
        graph.Count == 1 ? Describe(graph[0], context) : DeliveryForecast.Unavailable("dependent_effect_graph");
}
