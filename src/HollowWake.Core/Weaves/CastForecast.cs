using System.Numerics;
using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Expressions;
using HollowWake.Core.Maths;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Weaves;

/// <summary>
/// A current active-cast observation, never an emission or a promise of future geometry or damage.
/// Deadlines are independent: a charge becoming ready is not a release. Context is server-local and
/// detached; use it with the sampled plan for current viewer eligibility, never serialize it.
/// </summary>
public sealed record CastForecast(
    long CastId, Entity Caster, Entity Target, string Weave, string Name, CastMode Mode,
    TagSet Tags, TagSet Mechanisms, Vector3 Position, long SampleTick, double SampleTime,
    double StartedAt, double? NextEmissionAt, double? ReadyAt, double? MaxHoldEndsAt,
    int EmissionOrdinal, string DirectionPolicy, bool DirectionCommitted,
    EffectContext Context, DeliveryForecast Delivery)
{
    /// <summary>The recorded start of the next emission's window, absent without a known emission.</summary>
    public double? NextEmissionStartedAt { get; init; }
    public string Coverage => Delivery.PrimaryRegion.HasValue ? "primary" : "unavailable";
    public string GeometryPolicy => "live";
}

public sealed partial class WeaveRuntime
{
    /// <summary>
    /// Observe on the simulation thread, only when someone needs it. No compilation, runtime scopes,
    /// RNG streams, costs, events or scheduled work are touched. An unsupported graph still has timing.
    /// </summary>
    public CastForecast? DescribeCast(Simulation sim, Entity caster)
    {
        if (!sim.World.IsAlive(caster) || sim.World.Has<Dead>(caster) ||
            sim.World.Get<WeaveBook>(caster) is not { Casting: { } cast } book ||
            sim.World.Get<Transform>(caster) is not { } transform ||
            sim.World.Get<Stats>(caster) is not { } stats ||
            (cast.Weave ?? cast.Slot.Compiled) is not { } weave || cast.CastId == 0) return null;

        var now = sim.Clock.Time;
        var held = Math.Max(0, now - cast.StartedAt);
        var pulse = cast.Mode == CastMode.Channel && (weave.Channel?.Release?.Pulses ?? true);
        var releaseAim = cast.Mode is CastMode.Charge or CastMode.Overcharge or CastMode.Guard || cast.Mode == CastMode.Channel && !pulse;
        var (point, direction) = releaseAim ? ReleaseAim(sim, caster, book, cast, weave) : (cast.Point, cast.Direction);
        var position = transform.Position;
        var target = cast.Target;
        if (!target.IsNone && (!sim.World.IsAlive(target) || sim.World.Has<Dead>(target))) target = Entity.None;
        if (!target.IsNone) point = sim.World.Get<Transform>(target)?.Position ?? point;

        var local = cast.HasModifiers ? new List<Modifier>(cast.Modifiers) : null;
        var vars = new CastVarValues();
        var changingAttributes = new HashSet<string>(StringComparer.Ordinal);
        var changingFlags = new HashSet<string>(StringComparer.Ordinal);
        // Run stamps these immediately before evaluating its graph. Do not describe a region using
        // the pre-release value of a condition that the application itself is about to change.
        changingFlags.Add("recently_cast");
        if (weave.Tags.Has("movement")) changingFlags.Add("recently_moved");
        string? unavailable = null;
        IReadOnlyList<CompiledEffect> effects = weave.Effects;
        switch (cast.Mode)
        {
            case CastMode.Channel:
                vars.Held = held;
                if (weave.Channel?.Brim is not null) vars.Fill = cast.Brim;
                if (weave.Channel is { } channel)
                {
                    if (channel.Ramps.Count > 0)
                    {
                        local = new List<Modifier>();
                        foreach (var ramp in channel.Ramps)
                        {
                            if (ramp.On == "caster") { changingAttributes.Add(ramp.Attribute); continue; }
                            if (!Curves.IsPreviewSafe(ramp.Curve)) { unavailable = "opaque_cast_curve"; break; }
                            try
                            {
                                local.Add(new Modifier { Attribute = ramp.Attribute, Form = ramp.Form, Value = RampValue(ramp, held), Scope = ramp.Scope, Source = "ramp" });
                            }
                            catch (ArithmeticException) { unavailable = "invalid_cast_curve"; break; }
                            catch (ArgumentException) { unavailable = "invalid_cast_curve"; break; }
                        }
                    }
                    if (!pulse)
                    {
                        effects = weave.Casting.Release.Count > 0 ? weave.Casting.Release : weave.Effects;
                        if (channel.Release is null) unavailable = "no_release_payload";
                        // Brim release scales can affect geometry indirectly through damage formulas.
                        if (channel.Brim is { } brim && (brim.Scale.AtEmpty != 1 || brim.Scale.AtFull != 1))
                            unavailable = "conditional_brim_release";
                    }
                }
                break;
            case CastMode.Charge:
                vars.Held = held;
                vars.Fill = cast.Duration <= 0 ? 1 : Math.Min(1, held / cast.Duration);
                local = new List<Modifier>();
                AddChargeModifiers(local, weave.Charge, vars.Fill.Value);
                changingFlags.Add("charging");
                break;
            case CastMode.Overcharge:
                vars.Held = held;
                vars.Stage = cast.Stage;
                vars.Fill = cast.Fill;
                local = new List<Modifier>();
                AddOverchargeModifiers(local, weave.Form.Overcharge, cast.Stage, now - cast.StageAt);
                changingFlags.Add("charging");
                break;
            case CastMode.Guard:
                effects = weave.Casting.Bash;
                vars.Held = held;
                vars.GuardAbsorbed = sim.World.Get<Guard>(caster)?.Absorbed ?? 0;
                if (effects.Count == 0) unavailable = "no_release_payload";
                break;
        }
        if (cast.Mode is CastMode.Cast or CastMode.Instant or CastMode.Charge or CastMode.Overcharge)
        {
            if (weave.Casting.Spend is not null) unavailable = "pending_charge_spend";
            if (weave.Casting.Uses is not null) vars.UsesSpent = cast.UsesSpent;
            local = UseModifiers(weave, cast, now, 0, local);
        }
        if (cast.Mode is CastMode.Charge or CastMode.Overcharge && cast.CasterSource is { } removedSource)
            foreach (var modifier in stats.Attributes.AllModifiers)
                if (modifier.Source == removedSource) changingAttributes.Add(modifier.Attribute);
        var view = local is { Count: > 0 } ? weave.Root.WithModifiers(Combined(weave.Root.Modifiers, local)) : weave;
        var context = new EffectContext
        {
            CastId = cast.CastId, Sim = sim, Source = caster, Target = target, Point = point,
            Direction = direction, Weave = view, Tags = view.Tags, Rng = new Rng(0), Slot = cast.Slot,
            ForecastRestrictions = new ForecastRestrictions(changingAttributes, changingFlags,
                pulse && cast.Pulses > 0),
        };
        vars.WriteTo(context.Vars);
        var delivery = unavailable is null ? DeliveryForecasts.DescribeDetached(effects, context) : DeliveryForecast.Unavailable(unavailable);

        var tracks = cast.Mode == CastMode.Channel && weave.Channel?.TrackAim == true ||
            cast.Mode == CastMode.Guard && weave.Guard?.TurnRate is not null;
        var canAimAtRelease = releaseAim && weave.Targeting is Targeting.Direction or Targeting.Ground;
        var directionPolicy = tracks ? "track_while_held" : canAimAtRelease ? "aim_on_release" : "locked_on_start";
        double? nextEmission = cast.Mode == CastMode.Cast ? Finite(cast.StartedAt + cast.Duration) : pulse ? Finite(cast.NextChannelAt) : null;
        double? maximumHold = cast.Mode != CastMode.Cast && cast.MaxHold > 0 ? Finite(cast.StartedAt + cast.MaxHold) : null;
        if (maximumHold is { } end && nextEmission > end) nextEmission = null;
        double? ready = cast.Mode == CastMode.Charge ? Finite(cast.StartedAt + cast.Duration) :
            cast.Mode == CastMode.Overcharge && cast.Stage >= cast.Stages && cast.FullAt >= 0 ? Finite(cast.FullAt) : null;
        return new CastForecast(cast.CastId, caster, cast.Target, weave.Key, weave.Name, cast.Mode, weave.Tags,
            weave.Mechanisms, position, sim.Clock.Tick, now, cast.StartedAt, nextEmission, ready, maximumHold,
            cast.PresentationReleases, directionPolicy, !tracks && !canAimAtRelease, context, delivery)
        {
            NextEmissionStartedAt = nextEmission is null ? null : cast.Mode == CastMode.Cast ? Finite(cast.StartedAt) :
                cast.NextChannelStartedAt is { } beatStart ? Finite(beatStart) : null,
        };
    }

    private static double? Finite(double value) => double.IsFinite(value) && value >= 0 ? value : null;
}
