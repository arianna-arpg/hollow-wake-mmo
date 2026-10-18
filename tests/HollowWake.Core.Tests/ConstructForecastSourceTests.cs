using System.Numerics;
using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Expressions;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;
using Xunit;

namespace HollowWake.Core.Tests;

public class ConstructForecastSourceTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ForecastSamplesPayloadStatsAtTheDeviceButKeepsDeviceTiming(bool inherits, bool ownerWide)
    {
        var content = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out _);
        content.With(new FactionDef { Id = "red", Relations = { ["blue"] = Stance.Hostile } });
        content.With(new FactionDef { Id = "blue", Relations = { ["red"] = Stance.Hostile } });
        content.With(new FormDef
        {
            Id = "construct_forecast", CastMode = CastMode.Cast, CastTime = Expr.Constant(1),
            Targeting = Targeting.Enemy, MinThreads = 0, Attributes = { ["range"] = 20 },
            Effects = { CastingTestContent.Spec("""
                {"type":"area","shape":"circle","center":"source","radius":"source.area.radius","filter":"enemy",
                 "effects":[{"type":"event","name":"construct_forecast_hit","data":{"radius":"source.area.radius"}}]}
                """) },
        });
        var arena = CastingArena.Create(registry: content, distance: 4);
        var owner = arena.Spawn("loosed_kindler", new Vector3(40, 10, 0), "red");
        arena.Sim.World.Add(arena.Caster, new Minion { Owner = owner, Construct = true, InheritCaster = inherits });
        var ownerStats = arena.Sim.World.Get<Stats>(owner)!;
        void Radius(Stats stats, double value) => stats.Attributes.Add(new Modifier
            { Attribute = "area.radius", Form = ModifierForm.Override, Value = value, Source = "geometry-probe" });
        Radius(arena.Stats, ownerWide ? 2 : 6);
        Radius(ownerStats, ownerWide ? 6 : 2);
        arena.Stats.Attributes.Add(new Modifier { Attribute = "cast.speed", Form = ModifierForm.Override, Value = 2, Source = "timing-probe" });
        ownerStats.Attributes.Add(new Modifier { Attribute = "cast.speed", Form = ModifierForm.Override, Value = 0.25, Source = "timing-probe" });
        arena.Begin(arena.Slot(new WeaveSpec { Form = "construct_forecast" }));
        var count = arena.Events.Count;
        var forecast = Assert.IsType<CastForecast>(arena.Runtime.DescribeCast(arena.Sim, arena.Caster));
        var plan = Assert.IsType<ResolvedAreaPlan>(forecast.Delivery.PrimaryRegion);
        var source = inherits ? owner : arena.Caster;
        var radius = inherits == ownerWide ? 6 : 2;
        Assert.Equal(radius, plan.Size.Radius);
        Assert.Equal(source, forecast.Context.Source);
        Assert.Equal(source, forecast.Context.Origin);
        Assert.Equal(arena.Caster, forecast.Context.CastSource);
        Assert.Equal(arena.Caster, forecast.Caster);
        Assert.Equal(arena.Sim.World.Get<Transform>(arena.Caster)!.Position, plan.Anchor);
        Assert.Equal(0.5, forecast.NextEmissionAt!.Value - forecast.StartedAt, 6);
        var expectedHit = plan.IsCandidate(forecast.Context, arena.Enemy, arena.Sim.World.Get<Transform>(arena.Enemy)!.Position);
        Assert.Equal(radius == 6, expectedHit);
        Assert.Equal(count, arena.Events.Count);
        arena.RunSeconds(0.6);
        Assert.Equal(expectedHit ? 1 : 0, arena.Custom("construct_forecast_hit").Count);
        if (expectedHit)
        {
            var hit = Assert.Single(arena.Custom("construct_forecast_hit"));
            Assert.Equal(source, hit.Source);
            Assert.Equal(radius, hit.Data["radius"]);
        }
        Assert.Contains(arena.Of<WeaveCast>(), e => e.Caster == arena.Caster);
    }
}
