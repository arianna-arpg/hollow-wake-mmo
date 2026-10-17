using System.Numerics;
using System.Text.Json;
using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Expressions;
using HollowWake.Core.Maths;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;
using HollowWake.Core.Weaves;
using Xunit;

namespace HollowWake.Core.Tests;

public class CastForecastTests
{
    private static CastForecast Observe(CastingArena a) => Assert.IsType<CastForecast>(a.Runtime.DescribeCast(a.Sim, a.Caster));
    private static ResolvedAreaPlan Plan(CastingArena a) => Assert.IsType<ResolvedAreaPlan>(Observe(a).Delivery.PrimaryRegion);

    [Fact]
    public void TimedCastsExposeIdentityAndAConditionalEmissionDeadlineWithPresentationDisabled()
    {
        var a = CastForecastContent.Arena();
        a.Sim.EmitCastPresentation = false;
        a.Begin(CastForecastContent.Slot(a, "forecast_timed"));
        var cast = a.Book.Casting!;
        var f = Observe(a);
        Assert.True(f.CastId > 0);
        Assert.Equal(cast.CastId, f.CastId);
        Assert.Equal(cast.StartedAt + cast.Duration, f.NextEmissionAt);
        Assert.Null(f.ReadyAt);
        Assert.Null(f.MaxHoldEndsAt);
        Assert.Equal("locked_on_start", f.DirectionPolicy);
        Assert.True(f.DirectionCommitted);
        Assert.Equal("primary", f.Coverage);
        Assert.Equal("live", f.GeometryPolicy);
        Assert.Equal(a.Sim.World.Get<Transform>(a.Caster)!.Position, f.Position);
        Assert.Equal(a.Sim.Clock.Tick, f.SampleTick);
        Assert.Equal(a.Now, f.SampleTime);
        Assert.Equal(0, f.EmissionOrdinal);
        Assert.Empty(a.Of<CastPresentationEvent>());
        a.Runtime.Interrupt(a.Sim, a.Caster);
        Assert.Null(a.Runtime.DescribeCast(a.Sim, a.Caster));
    }

    [Fact]
    public void AForecastRequiresALiveSourcePositionRatherThanFallingBackToAnAimPoint()
    {
        var a = CastForecastContent.Arena();
        a.Begin(CastForecastContent.Slot(a, "forecast_timed"));
        a.Sim.World.Remove<Transform>(a.Caster);
        Assert.Null(a.Runtime.DescribeCast(a.Sim, a.Caster));
    }

    [Fact]
    public void ChargeReadinessIsNotEmissionAndItsLiveSizeMatchesActualRelease()
    {
        var a = CastForecastContent.Arena(2.8f);
        var slot = CastForecastContent.Slot(a, "forecast_charge");
        a.Begin(slot);
        var initial = Observe(a);
        Assert.Null(initial.NextEmissionAt);
        Assert.Equal(a.Book.Casting!.StartedAt + a.Book.Casting.Duration, initial.ReadyAt);
        Assert.Equal(a.Book.Casting.StartedAt + a.Book.Casting.MaxHold, initial.MaxHoldEndsAt);
        Assert.Equal("aim_on_release", initial.DirectionPolicy);
        Assert.False(initial.DirectionCommitted);
        Assert.Equal(2, initial.Delivery.PrimaryRegion!.Value.Size.Radius);
        Assert.False(initial.Delivery.PrimaryRegion.Value.ContainsCenter(new(12.8f, 10, 0)));
        a.RunSeconds(0.6);
        var grown = Observe(a);
        Assert.True(grown.Delivery.PrimaryRegion!.Value.Size.Radius > 3);
        Assert.True(grown.Delivery.PrimaryRegion.Value.ContainsCenter(new(12.8f, 10, 0)));
        Assert.Equal(2, initial.Delivery.PrimaryRegion.Value.Size.Radius);
        Assert.True(a.Runtime.Release(a.Sim, a.Caster, a.SlotIndex(slot)));
        var hit = Assert.Single(a.Custom("forecast_hit"));
        Assert.Equal(grown.Delivery.PrimaryRegion.Value.Size.Radius, Convert.ToDouble(hit.Data!["radius"]), 5);
        Assert.Null(a.Runtime.DescribeCast(a.Sim, a.Caster));
    }

    [Fact]
    public void ChannelTimingUsesOnlyTheAlreadyChosenBeatAndRampsShareTheExecutionFold()
    {
        var a = CastForecastContent.Arena(2.2f);
        a.Begin(CastForecastContent.Slot(a, "forecast_channel"));
        var initial = Observe(a);
        Assert.Equal(a.Book.Casting!.NextChannelAt, initial.NextEmissionAt);
        Assert.Equal("track_while_held", initial.DirectionPolicy);
        Assert.False(initial.DirectionCommitted);
        Assert.Null(initial.ReadyAt);
        a.RunSeconds(0.25);
        var f = Observe(a);
        Assert.Equal(1, f.EmissionOrdinal);
        Assert.Equal(a.Book.Casting.NextChannelAt, f.NextEmissionAt);
        Assert.True(f.NextEmissionAt > a.Now);
        Assert.Equal(2.25, f.Delivery.PrimaryRegion!.Value.Size.Radius, 5);
        Assert.Equal(2.25, Convert.ToDouble(Assert.Single(a.Custom("forecast_hit")).Data!["radius"]), 5);
        var next = a.Book.Casting.NextChannelAt;
        for (var i = 0; i < 20; i++) Assert.Equal(next, Observe(a).NextEmissionAt);
    }

    [Fact]
    public void OverchargeReadinessIsKnownOnlyAfterTheActualLastStage()
    {
        var a = CastForecastContent.Arena(2);
        a.Begin(CastForecastContent.Slot(a, "forecast_over"));
        Assert.Null(Observe(a).ReadyAt);
        Assert.Null(Observe(a).NextEmissionAt);
        a.RunSeconds(0.5);
        Assert.Equal(3, Plan(a).Size.Radius, 5);
        Assert.Null(Observe(a).ReadyAt);
        a.RunSeconds(1);
        var f = Observe(a);
        Assert.Equal(a.Book.Casting!.FullAt, f.ReadyAt);
        Assert.Equal(5, f.Delivery.PrimaryRegion!.Value.Size.Radius, 5);
        Assert.Null(f.NextEmissionAt);
        Assert.NotNull(f.MaxHoldEndsAt);
    }

    [Fact]
    public void GuardBashCoverageDoesNotInventAReleaseDeadline()
    {
        var a = CastForecastContent.Arena();
        a.Begin(CastForecastContent.Slot(a, "forecast_guard"));
        var f = Observe(a);
        Assert.Equal("primary", f.Coverage);
        Assert.Null(f.NextEmissionAt);
        Assert.Null(f.ReadyAt);
        Assert.NotNull(f.MaxHoldEndsAt);
    }

    [Theory]
    [InlineData("forecast_random", "random_or_opaque_expression")]
    [InlineData("forecast_stamped", "pending_cast_state")]
    [InlineData("forecast_charging", "pending_cast_state")]
    [InlineData("forecast_caster_ramp", "pending_caster_modifiers")]
    public void ProspectiveOrOpaqueGeometryDeclinesInsteadOfPublishingAMisleadingRegion(string form, string reason)
    {
        var a = CastForecastContent.Arena();
        a.Begin(CastForecastContent.Slot(a, form));
        var f = Observe(a);
        Assert.Equal("unavailable", f.Coverage);
        Assert.Null(f.Delivery.PrimaryRegion);
        Assert.Equal(reason, f.Delivery.UnavailableReason);
    }

    [Fact]
    public void AChannelCannotClaimPostPaymentResourceGeometryFromItsPrePaymentState()
    {
        var a = CastForecastContent.Arena();
        a.Begin(CastForecastContent.Slot(a, "forecast_resource"));
        Assert.Equal("primary", Observe(a).Coverage); // First beat was paid on press.
        a.RunSeconds(0.25);
        var f = Observe(a);
        Assert.Equal("pending_resource_payment", f.Delivery.UnavailableReason);
        Assert.Null(f.Delivery.PrimaryRegion);
        Assert.NotNull(f.NextEmissionAt);
    }

    [Fact]
    public void OpaqueOrInvalidCastCurvesCannotEscapeTheObservationBoundary()
    {
        var a = CastForecastContent.Arena();
        a.Begin(CastForecastContent.Slot(a, "forecast_opaque_curve"));
        var calls = CastForecastContent.OpaqueCurveCalls;
        Assert.Equal("opaque_cast_curve", Observe(a).Delivery.UnavailableReason);
        Assert.Equal(calls, CastForecastContent.OpaqueCurveCalls);
        a.Runtime.Interrupt(a.Sim, a.Caster);
        a.Begin(CastForecastContent.Slot(a, "forecast_invalid_curve"));
        Assert.Equal("invalid_cast_curve", Observe(a).Delivery.UnavailableReason);
    }

    [Theory]
    [InlineData("forecast_timed")]
    [InlineData("forecast_channel")]
    [InlineData("forecast_charge")]
    [InlineData("forecast_over")]
    [InlineData("forecast_guard")]
    [InlineData("forecast_random")]
    [InlineData("forecast_caster_ramp")]
    public void InterleavedObservationLeavesFullEventTracesAndGameplayStateUnchanged(string form)
    {
        static (string[] Trace, string State) Run(string form, bool observe)
        {
            var a = CastForecastContent.Arena(1.6f);
            a.Begin(CastForecastContent.Slot(a, form));
            for (var tick = 0; tick < 70; tick++)
            {
                if (observe && a.Book.Casting is { } cast)
                {
                    var before = State(a);
                    var count = a.Events.Count;
                    var pending = a.Sim.Scheduler.Pending;
                    var local = cast.HasModifiers ? cast.Modifiers.ToArray() : Array.Empty<Modifier>();
                    for (var n = 0; n < 3; n++) _ = a.Runtime.DescribeCast(a.Sim, a.Caster);
                    Assert.Same(cast, a.Book.Casting);
                    Assert.Equal(before, State(a));
                    Assert.Equal(count, a.Events.Count);
                    Assert.Equal(pending, a.Sim.Scheduler.Pending);
                    Assert.Equal(local, cast.HasModifiers ? cast.Modifiers : Array.Empty<Modifier>());
                }
                a.Sim.Run(1);
            }
            return (a.Events.Select(e => JsonSerializer.Serialize(e, e.GetType(), ContentJson.Options)).ToArray(), State(a));
        }
        var plain = Run(form, false);
        var observed = Run(form, true);
        Assert.Equal(plain.Trace, observed.Trace);
        Assert.Equal(plain.State, observed.State);
    }

    private static string State(CastingArena a) => JsonSerializer.Serialize(new
    {
        a.Stats.Resources, a.Stats.Gauges, a.Stats.Flags, a.Stats.Recent,
        Modifiers = a.Stats.Attributes.AllModifiers.ToArray(),
        a.Book.ReadyAt, a.Book.GlobalReadyAt,
        Slots = a.Book.Slots.Select(s => new { s.Key, s.State }),
        EnemyResources = a.EnemyStats.Resources,
        Time = a.Now, a.Sim.Scheduler.Pending,
    }, ContentJson.Options);

    [Fact]
    public void EveryShippedPresetAndEverySupportOnAnAreaHasPrimaryOrExplicitUnavailableCoverage()
    {
        var a = CastForecastContent.Arena();
        var compiler = a.Sim.Service<WeaveCompiler>();
        var specs = CoreContent.Registry.All<WeaveDef>().Keys.OrderBy(x => x, StringComparer.Ordinal)
            .Select(id => new WeaveSpec { Preset = id }).Concat(
                CoreContent.Registry.All<GlyphDef>().Keys.OrderBy(x => x, StringComparer.Ordinal)
                .Select(id => new WeaveSpec { Form = "cleave", Glyphs = { GlyphRef.Parse(id) } })).ToArray();
        var primary = 0;
        var unavailable = 0;
        long id = 0;
        foreach (var spec in specs)
        {
            var weave = compiler.Compile(spec, GlyphFit.Dormant);
            var slot = new WeaveSlot { Spec = spec, Compiled = weave };
            // Observing an already-compiled active graph is independent of whether its costs/gates
            // would permit this test body to begin it. No compile or gameplay work belongs in observation.
            a.Book.Casting = new CastState { Slot = slot, Weave = weave, CastId = ++id, Mode = weave.CastMode,
                Target = a.Enemy, Point = new(13, 10, 0), Direction = Vector2.UnitX, Duration = 1, MaxHold = 3 };
            var before = State(a);
            var events = a.Events.Count;
            var f = Observe(a);
            Assert.Equal(id, f.CastId);
            Assert.Equal(before, State(a));
            Assert.Equal(events, a.Events.Count);
            if (f.Delivery.PrimaryRegion is { } plan)
            {
                primary++;
                Assert.True(plan.IsFinite, spec.ToString());
                Assert.Null(f.Delivery.UnavailableReason);
            }
            else
            {
                unavailable++;
                Assert.False(string.IsNullOrWhiteSpace(f.Delivery.UnavailableReason), spec.ToString());
            }
        }
        Assert.True(primary > 0);
        Assert.True(unavailable > 0); // Unknown coverage is explicit, never a generic circle.
    }
}

