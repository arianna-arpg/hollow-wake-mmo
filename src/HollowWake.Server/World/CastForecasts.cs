using System.Numerics;
using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;
using HollowWake.Protocol;
using HollowWake.Server.Config;
using HollowWake.Server.Sessions;

namespace HollowWake.Server.World;

public sealed class CastForecastBuilder
{
    private readonly CastForecastSettings _forecastSettings;
    private readonly Core.Sim.Simulation Sim;
    public string Epoch { get; } = Guid.NewGuid().ToString("N");
    public CastForecastBuilder(Core.Sim.Simulation sim, CastForecastSettings? settings = null)
    { Sim = sim; _forecastSettings = (settings ?? new()).Normalized(); }
    private long _forecastSequence;
    private long _forecastSampleTick = -1;
    private readonly Dictionary<Entity, CastForecast?> _forecastSamples = new();

    public CastForecastMsg Build(SnapshotView view, Entity viewerEntity, double snapshotInterval)
    {
        if (_forecastSampleTick != Sim.Clock.Tick) { _forecastSamples.Clear(); _forecastSampleTick = Sim.Clock.Tick; }
        var viewer = Sim.World.Get<Transform>(viewerEntity)?.Position ?? Vector3.Zero;
        var runtime = Sim.Service<WeaveRuntime>();
        var casts = new List<CastForecastInfo>();
        var retained = new List<CastForecastRef>();
        var active = new HashSet<long>();
        var sequence = ++_forecastSequence;
        var omitted = 0;
        // All casters use the same path: players, remote players, monsters and summons. Interest is
        // refreshed first. Distance then stable identity makes overload selection deterministic.
        foreach (var entity in view.Known.Append(viewerEntity.Key).Distinct().Select(Sim.World.Resolve)
            .Where(e => !e.IsNone && Sim.World.Get<WeaveBook>(e)?.Casting is not null)
            .OrderBy(e => Vector3.DistanceSquared(Sim.World.Get<Transform>(e)?.Position ?? viewer, viewer)).ThenBy(e => e.Key))
        {
            if (active.Count >= _forecastSettings.MaxCasts) { omitted++; continue; }
            if (!_forecastSamples.TryGetValue(entity, out var sample)) _forecastSamples[entity] = sample = runtime.DescribeCast(Sim, entity);
            if (sample is null) continue;
            var hiddenTarget = !sample.Target.IsNone && sample.Target != viewerEntity && !view.Known.Contains(sample.Target.Key);
            var region = hiddenTarget ? null : sample.Delivery.PrimaryRegion;
            CastForecastRegion? wireRegion = null;
            if (region is { } r)
                wireRegion = new()
                {
                    Shape = r.Shape.Name,
                    // No snapshot rounding or decorative clamps: these are the actual sampled dimensions.
                    Size = new() { Radius = r.Size.Radius, Angle = r.Size.Angle * MathF.PI / 180,
                        Width = r.Size.Width, Inner = r.Size.Inner * r.Size.Radius, Curvature = r.Shape.Curvature },
                    X = r.Anchor.X, Y = r.Anchor.Y, Z = r.Anchor.Z, Heading = MathF.Atan2(r.Direction.Y, r.Direction.X),
                    Anchor = r.AnchorMode, HeightTolerance = r.HeightTolerance, RequiresLineOfSight = r.RequiresLineOfSight,
                    HasRequirement = r.HasRequirement, HasTargetLimit = r.HasTargetLimit,
                    ViewerEligibility = r.IsCandidate(sample.Context, viewerEntity, viewer) ? "candidate" : "not_candidate_now",
                };
            var position = Sim.World.Get<Transform>(entity)?.Position ?? Vector3.Zero;
            var info = new CastForecastInfo
            {
                CastId = sample.CastId, Source = entity.Key, Name = sample.Name, Mode = CastPresentationEvent.ModeName(sample.Mode),
                StartedAt = sample.StartedAt, NextEmissionStartedAt = sample.NextEmissionStartedAt,
                NextEmissionAt = sample.NextEmissionAt, ReadyAt = sample.ReadyAt,
                MaxHoldEndsAt = sample.MaxHoldEndsAt, EmissionOrdinal = sample.EmissionOrdinal,
                DirectionPolicy = sample.DirectionPolicy, DirectionCommitted = sample.DirectionCommitted,
                X = position.X, Y = position.Y, Z = position.Z, Region = wireRegion,
                Coverage = wireRegion is null ? "unavailable" : "primary_candidate",
                UnavailableReason = hiddenTarget ? "hidden_target" : sample.Delivery.UnavailableReason,
            };
            active.Add(info.CastId);
            if (view.Forecasts.TryGetValue(info.CastId, out var previous) && previous.Info == info && Sim.Clock.Time < previous.RefreshAt)
                retained.Add(new(info.CastId, previous.Revision));
            else
            {
                casts.Add(info);
                view.Forecasts[info.CastId] = (info, sequence, Sim.Clock.Time + _forecastSettings.RefreshSeconds);
            }
        }
        foreach (var id in view.Forecasts.Keys.Where(id => !active.Contains(id)).ToArray()) view.Forecasts.Remove(id);
        return new CastForecastMsg(Epoch, sequence, Sim.Clock.Time,
            Sim.Clock.Time + Math.Max(_forecastSettings.ValidSeconds, snapshotInterval * 3), casts, omitted)
            { Retained = retained.Count == 0 ? null : retained };
    }
}
