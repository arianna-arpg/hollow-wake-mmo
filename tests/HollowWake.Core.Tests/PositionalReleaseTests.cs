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
using HollowWake.Core.Weaves;
using HollowWake.Core.World;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>Independent definitions exercise mechanics, never dispatch by a shipped skill's name.</summary>
internal static class CastForecastContent
{
    public static int OpaqueCurveCalls;
    private static readonly Lazy<ContentRegistry> Loaded = new(() =>
    {
        var r = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out var report);
        Assert.False(report.HasErrors, report.ToString());
        foreach (var form in CastingTestContent.Registry.All<FormDef>().Values) r.With(form);
        foreach (var glyph in CastingTestContent.Registry.All<GlyphDef>().Values) r.With(glyph);
        foreach (var weave in CastingTestContent.Registry.All<WeaveDef>().Values) r.With(weave);
        r.With(new FactionDef { Id = "red", Relations = { ["blue"] = Stance.Hostile } });
        r.With(new FactionDef { Id = "blue", Relations = { ["red"] = Stance.Hostile } });
        var direct = r.Get<FormDef>("t_shot")! with
        {
            Id = "forecast_direct", Cooldown = Expr.Parse("2"),
            Attributes = new() { ["range"] = 4 }, ReleaseCheck = "full",
        };
        r.With(direct);
        r.With(direct with { Id = "forecast_none", ReleaseCheck = "none" });
        r.With(direct with { Id = "forecast_range", ReleaseCheck = "range" });
        r.With(direct with { Id = "forecast_heal", Targeting = Targeting.Ally,
            Effects = new() { Spec("""{"type":"heal","amount":20}""") } });
        r.With(direct with { Id = "forecast_channel_target", CastMode = CastMode.Channel,
            Channel = new() { Interval = Expr.Parse("0.5"), Windup = Expr.Parse("0.25"), MaxHold = Expr.Parse("3") } });
        r.With(direct with { Id = "forecast_charge_target", CastMode = CastMode.Charge,
            Charge = new() { MaxTime = Expr.Parse("1"), FullHold = Expr.Parse("1") } });
        r.With(direct with { Id = "forecast_over_target", CastMode = CastMode.Overcharge,
            Overcharge = new() { Stages = 2, Time = Expr.Parse("0.5"), FullHold = Expr.Parse("1") } });
        r.With(direct with { Id = "forecast_instant_target", CastMode = CastMode.Instant });
        r.With(new WeaveDef { Id = "forecast_target_preset", Form = "forecast_instant_target" });
        r.With(direct with { Id = "forecast_follow", CastMode = CastMode.Instant,
            FollowUp = new() { Weave = "forecast_target_preset", Delay = Expr.Parse("0.3") } });
        foreach (var (name, basis) in new[] { ("timed", "t_shot"), ("channel", "t_channel"), ("charge", "t_charge"), ("over", "t_over"), ("guard", "t_guard") })
        {
            var form = r.Get<FormDef>(basis)! with
            {
                Id = "forecast_" + name, Targeting = Targeting.Direction,
                Attributes = new() { ["range"] = 30, ["area.radius"] = 2 }, Effects = new() { Area() },
            };
            if (name == "channel") form = form with { Channel = form.Channel! with { Jitter = Expr.Parse("0.1"), TrackAim = true } };
            if (name == "guard") form = form with { Guard = form.Guard! with { Bash = form.Guard.Bash! with { Effects = new() { Area() } } } };
            r.With(form);
        }
        var timed = r.Get<FormDef>("forecast_timed")!;
        r.With(timed with { Id = "forecast_live_ai", Effects = new() { Area("weave.area.radius + time + target.mana / 100") } });
        r.With(timed with { Id = "forecast_random", Effects = new() { Area("rand() + 2") } });
        r.With(timed with { Id = "forecast_stamped", Effects = new() { Area("has('recently_cast') ? 4 : 2") } });
        r.With(r.Get<FormDef>("forecast_charge")! with { Id = "forecast_charging", Effects = new() { Area("has('charging') ? 4 : 2") } });
        r.With(r.Get<FormDef>("forecast_channel")! with { Id = "forecast_resource", Effects = new() { Area("source.mana / 100") } });
        r.With(r.Get<FormDef>("forecast_channel")! with { Id = "forecast_caster_ramp",
            Channel = new() { Interval = Expr.Parse("0.5"), Windup = Expr.Parse("0.25"), MaxHold = Expr.Parse("2"),
                Ramps = new() { new() { Attribute = "area.radius", On = "caster", Per = 0.5 } } } });
        Curves.Register("forecast_opaque_curve", t => { OpaqueCurveCalls++; return t; });
        Curves.Register("forecast_invalid_curve", _ => throw new ArithmeticException("test curve"), previewSafe: true);
        foreach (var kind in new[] { "opaque", "invalid" })
            r.With(r.Get<FormDef>("forecast_channel")! with { Id = "forecast_" + kind + "_curve",
                Channel = new() { Interval = Expr.Parse("0.5"), Windup = Expr.Parse("0.25"), MaxHold = Expr.Parse("2"),
                    Ramps = new() { new() { Attribute = "area.radius", Per = 0.5, Curve = "forecast_" + kind + "_curve" } } } });
        return r;
    });

    public static ContentRegistry Registry => Loaded.Value;
    public static TypedSpec Spec(string json) => JsonSerializer.Deserialize<TypedSpec>(json, ContentJson.Options)!;
    public static TypedSpec Area(string radius = "weave.area.radius") => Spec(
        """{"type":"area","shape":"sector","center":"source","radius":""" + JsonSerializer.Serialize(radius) +
        ""","angle":90,"effects":[{"type":"event","name":"forecast_hit","data":{"radius":"weave.area.radius"}}]}""");
    public static CastingArena Arena(float distance = 3) => CastingArena.Create(distance: distance, registry: Registry);
    public static WeaveSlot Slot(CastingArena arena, string form) => arena.Slot(new WeaveSpec { Form = form });
    public static void Move(CastingArena arena, Entity body, Vector3 position)
    {
        arena.Sim.World.Get<Transform>(body)!.Position = position;
        arena.Sim.World.Get<Motion>(body)?.Stop();
        arena.Sim.Spatial.Update(body, position);
    }
    public static void Wall(CastingArena arena)
    {
        for (var y = 8; y <= 12; y++)
        {
            var (chunk, index) = arena.Sim.Terrain.Locate(new CellCoord(12, y));
            chunk.Flags[index] = CellFlags.Structure | CellFlags.Blocked;
        }
    }
}

public class PositionalReleaseTests
{
    [Theory]
    [InlineData("forecast_direct", true)]
    [InlineData("forecast_none", false)]
    [InlineData("forecast_range", true)]
    public void StoredTargetsAreRecheckedAtApplicationAndAMissKeepsCostsAndCooldown(string form, bool misses)
    {
        var a = CastForecastContent.Arena();
        var slot = CastForecastContent.Slot(a, form);
        var mana = a.Stats.Resource("mana");
        a.Begin(slot);
        var id = a.Book.Casting!.CastId;
        CastForecastContent.Move(a, a.Enemy, new(22, 10, 0));
        a.RunSeconds(1);
        Assert.Equal(mana - 3, a.Stats.Resource("mana"), 5);
        Assert.True(a.Book.ReadyAt[slot.Key] > a.Now);
        Assert.Null(a.Book.Casting);
        Assert.Equal(misses ? 0 : 1, a.Custom("t_shot").Count);
        if (misses)
        {
            var miss = Assert.Single(a.Of<WeaveMissed>());
            Assert.Equal("out_of_reach", miss.Reason);
            Assert.Equal(id, miss.CastId);
            Assert.Contains(a.Of<CastPresentationEvent>(), e => e.CastId == id && e.Phase == "miss");
            Assert.Contains(a.Of<CastPresentationEvent>(), e => e.CastId == id && e.Phase == "end");
        }
        else Assert.Empty(a.Of<WeaveMissed>());
    }

    [Theory]
    [InlineData("forecast_direct", true)]
    [InlineData("forecast_range", false)]
    [InlineData("forecast_none", false)]
    public void FullReleaseChecksSeeNewWallsWhileExplicitPoliciesRemainAuthorable(string form, bool misses)
    {
        var a = CastForecastContent.Arena();
        a.Begin(CastForecastContent.Slot(a, form));
        CastForecastContent.Wall(a);
        a.RunSeconds(1);
        Assert.Equal(misses ? 0 : 1, a.Custom("t_shot").Count);
        if (misses) Assert.Equal("line_of_sight", Assert.Single(a.Of<WeaveMissed>()).Reason);
    }

    [Fact]
    public void AllyHealCannotFollowTheAllyOutOfReach()
    {
        var a = CastForecastContent.Arena();
        var ally = a.Spawn("hob", new(13, 10, 0), "red");
        var stats = a.Sim.World.Get<Stats>(ally)!;
        stats.Resources["life"] -= 100;
        var life = stats.Resource("life");
        Assert.True(a.Runtime.TryBegin(a.Sim, a.Caster, CastForecastContent.Slot(a, "forecast_heal"),
            ally, new(13, 10, 0), Vector2.UnitX, out var reason), reason);
        CastForecastContent.Move(a, ally, new(24, 10, 0));
        a.RunSeconds(1);
        Assert.Equal(life, stats.Resource("life"));
        Assert.Equal("out_of_reach", Assert.Single(a.Of<WeaveMissed>()).Reason);
    }

    [Theory]
    [InlineData("forecast_charge_target")]
    [InlineData("forecast_over_target")]
    public void HeldReleasesUseTheSameTargetGate(string form)
    {
        var a = CastForecastContent.Arena();
        var slot = CastForecastContent.Slot(a, form);
        a.Begin(slot);
        a.RunSeconds(0.5);
        CastForecastContent.Move(a, a.Enemy, new(22, 10, 0));
        Assert.True(a.Runtime.Release(a.Sim, a.Caster, a.SlotIndex(slot)));
        Assert.Empty(a.Custom("t_shot"));
        Assert.Equal("out_of_reach", Assert.Single(a.Of<WeaveMissed>()).Reason);
        Assert.False(Assert.Single(a.Of<WeaveReleased>()).Fired);
    }

    [Fact]
    public void MissingAChannelBeatDoesNotEndTheChannelAndTheNextBeatRechecks()
    {
        var a = CastForecastContent.Arena();
        a.Begin(CastForecastContent.Slot(a, "forecast_channel_target"));
        var id = a.Book.Casting!.CastId;
        CastForecastContent.Move(a, a.Enemy, new(22, 10, 0));
        a.RunSeconds(0.3);
        Assert.Equal("out_of_reach", Assert.Single(a.Of<WeaveMissed>()).Reason);
        Assert.Empty(a.Custom("t_shot"));
        Assert.Equal(id, a.Runtime.DescribeCast(a.Sim, a.Caster)!.CastId);
        CastForecastContent.Move(a, a.Enemy, new(13, 10, 0));
        a.RunSeconds(0.5);
        Assert.Single(a.Custom("t_shot"));
        Assert.NotNull(a.Book.Casting);
        Assert.DoesNotContain(a.Of<CastPresentationEvent>(), e => e.Phase == "end");
    }

    [Fact]
    public void ADeadStoredTargetProducesAnExplicitMiss()
    {
        var a = CastForecastContent.Arena();
        a.Begin(CastForecastContent.Slot(a, "forecast_direct"));
        a.Sim.World.Destroy(a.Enemy);
        a.RunSeconds(1);
        Assert.Empty(a.Custom("t_shot"));
        Assert.Equal("dead", Assert.Single(a.Of<WeaveMissed>()).Reason);
    }

    [Fact]
    public void NestedPresetApplicationsCannotBypassTheReleaseGate()
    {
        var a = CastForecastContent.Arena();
        var registry = a.Sim.Service<EffectRegistry>();
        var effect = registry.Compile(CastForecastContent.Spec("""{"type":"cast","weave":"forecast_target_preset","at":"target"}"""),
            new EffectCompileContext { Content = a.Sim.Content, Registry = registry, Owner = "release-test" });
        CastForecastContent.Move(a, a.Enemy, new(22, 10, 0));
        effect.Execute(new EffectContext { Sim = a.Sim, Source = a.Caster, Target = a.Enemy,
            Point = new(22, 10, 0), Rng = new Rng(5) });
        Assert.Empty(a.Custom("t_shot"));
        Assert.Equal("out_of_reach", Assert.Single(a.Of<WeaveMissed>()).Reason);
    }

    [Fact]
    public void DelayedFollowUpsRecheckTheirStoredTarget()
    {
        var a = CastForecastContent.Arena();
        a.Begin(CastForecastContent.Slot(a, "forecast_follow"));
        Assert.Single(a.Custom("t_shot"));
        CastForecastContent.Move(a, a.Enemy, new(22, 10, 0));
        a.RunSeconds(0.5);
        Assert.Single(a.Custom("t_shot"));
        Assert.Equal("out_of_reach", Assert.Single(a.Of<WeaveMissed>()).Reason);
    }

    [Fact]
    public void RepeatsRecheckAfterTheFirstApplication()
    {
        var a = CastForecastContent.Arena();
        a.Begin(a.Slot(new WeaveSpec { Form = "forecast_direct", Glyphs = { "t_repeat" } }));
        for (var tick = 0; tick < 20 && a.Custom("t_shot").Count == 0; tick++) a.Sim.Run(1);
        Assert.Single(a.Custom("t_shot"));
        CastForecastContent.Move(a, a.Enemy, new(22, 10, 0));
        a.RunSeconds(1);
        Assert.Single(a.Custom("t_shot"));
        Assert.Equal("out_of_reach", Assert.Single(a.Of<WeaveMissed>()).Reason);
    }

    [Fact]
    public void TriggeredTargetedSpellsCannotHitAnOutOfRangeBody()
    {
        var a = CastForecastContent.Arena();
        a.Slot(new WeaveSpec { Form = "forecast_instant_target", Glyphs = { "t_on_hit" } });
        a.Sim.Run(1); // Arm the trigger through the ordinary slot pipeline.
        CastForecastContent.Move(a, a.Enemy, new(22, 10, 0));
        a.Hit(a.Caster, a.Enemy, 5); // A distant projectile contact can trigger a short-range spell.
        Assert.Single(a.Of<WeaveTriggered>());
        Assert.Empty(a.Custom("t_shot"));
        Assert.Equal("out_of_reach", Assert.Single(a.Of<WeaveMissed>()).Reason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AReleasedBoltMissesADodgeUnlessItsSupportMakesItSeek(bool seeking)
    {
        var a = CastForecastContent.Arena(8);
        var spec = new WeaveSpec { Form = "bolt", Threads = { "ember" } };
        if (seeking) spec.Glyphs.Add("seeking");
        a.Begin(a.Slot(spec));
        for (var tick = 0; tick < 40 && !a.Of<WeaveCast>().Any(); tick++) a.Sim.Run(1);
        Assert.Single(a.Of<WeaveCast>());
        Assert.Empty(a.Of<DamageDealt>());
        CastForecastContent.Move(a, a.Enemy, new(18, 13, 0));
        a.RunSeconds(3);
        Assert.Equal(seeking, a.Of<DamageDealt>().Any(e => e.Target == a.Enemy));
    }

    [Theory]
    [InlineData("strike")]
    [InlineData("measured_strike")]
    [InlineData("bash")]
    public void MeleeUsesACommittedSectorAndCanHitTheNearestBystander(string form)
    {
        var a = CastForecastContent.Arena(1.7f);
        var near = a.Spawn("hob", new(11, 10.1f, 0));
        var far = a.Spawn("hob", new(11.5f, 10.1f, 0));
        a.Stats.Attributes.Add(new Modifier { Attribute = "accuracy", Form = ModifierForm.Override, Value = 100000 });
        a.Begin(CastForecastContent.Slot(a, form));
        var before = a.Runtime.DescribeCast(a.Sim, a.Caster)!;
        Assert.Equal("sector", before.Delivery.PrimaryRegion!.Value.Shape.Name);
        Assert.True(before.DirectionCommitted);
        CastForecastContent.Move(a, a.Enemy, new(8.3f, 10, 0));
        a.Sim.World.Get<Transform>(a.Caster)!.Yaw = MathF.PI;
        var sample = a.Runtime.DescribeCast(a.Sim, a.Caster)!;
        Assert.Equal(Vector2.UnitX, sample.Delivery.PrimaryRegion!.Value.Direction);
        a.RunSeconds(3);
        Assert.Contains(a.Of<DamageDealt>(), e => e.Target == near);
        Assert.DoesNotContain(a.Of<DamageDealt>(), e => e.Target == a.Enemy || e.Target == far);
        Assert.Empty(a.Of<WeaveMissed>());
    }

    [Theory]
    [InlineData(20f, "out_of_reach")]
    [InlineData(8.3f, "outside_area")]
    public void MeleeWhiffsWhenNoBodyRemainsInTheCommittedRegion(float x, string reason)
    {
        var a = CastForecastContent.Arena(1.7f);
        a.Begin(a.Slot("strike"));
        CastForecastContent.Move(a, a.Enemy, new(x, 10, 0));
        a.RunSeconds(2);
        Assert.Empty(a.Of<DamageDealt>());
        Assert.Equal(reason, Assert.Single(a.Of<WeaveMissed>()).Reason);
    }

    [Fact]
    public void MeleePrefersItsEligibleAimEvenWhenAnotherBodyIsCloser()
    {
        var a = CastForecastContent.Arena(1.7f);
        var near = a.Spawn("hob", new(11, 10.1f, 0));
        a.Stats.Attributes.Add(new Modifier { Attribute = "accuracy", Form = ModifierForm.Override, Value = 100000 });
        a.Begin(a.Slot("strike"));
        a.RunSeconds(2);
        var hit = Assert.Single(a.Of<DamageDealt>());
        Assert.Equal(a.Enemy, hit.Target);
        Assert.Equal(0, hit.Depth); // Authored selection preserves the original direct payload's proc budget.
        Assert.DoesNotContain(a.Of<DamageDealt>(), e => e.Target == near);
    }

    [Fact]
    public void AiDoesNotStopOutsideItsActualPrimaryRegionToSwingForever()
    {
        var a = CastForecastContent.Arena(2.8f);
        a.Book.Slots.Clear();
        a.Slot("strike");
        Assert.Null(a.Runtime.ChooseForAi(a.Sim, a.Caster, a.Enemy));
        CastForecastContent.Move(a, a.Enemy, new(11.7f, 10, 0));
        Assert.NotNull(a.Runtime.ChooseForAi(a.Sim, a.Caster, a.Enemy));
    }

    [Fact]
    public void RepeatedAiPlanQueriesKeepTimeTargetResourcesGeometryAndEligibilityLive()
    {
        var a = CastForecastContent.Arena(3);
        a.Book.Slots.Clear();
        CastForecastContent.Slot(a, "forecast_live_ai");
        a.EnemyStats.Resources["mana"] = 0;
        Assert.Null(a.Runtime.ChooseForAi(a.Sim, a.Caster, a.Enemy));
        for (var i = 0; i < 20; i++) a.Sim.Clock.Advance();
        Assert.NotNull(a.Runtime.ChooseForAi(a.Sim, a.Caster, a.Enemy));
        CastForecastContent.Move(a, a.Enemy, new(14, 10, 0));
        Assert.Null(a.Runtime.ChooseForAi(a.Sim, a.Caster, a.Enemy));
        a.EnemyStats.Resources["mana"] = 100;
        Assert.NotNull(a.Runtime.ChooseForAi(a.Sim, a.Caster, a.Enemy));
        a.EnemyStats.Flags.Add("untargetable");
        Assert.Null(a.Runtime.ChooseForAi(a.Sim, a.Caster, a.Enemy));
        a.EnemyStats.Flags.Remove("untargetable");
        CastForecastContent.Wall(a);
        Assert.Null(a.Runtime.ChooseForAi(a.Sim, a.Caster, a.Enemy));
        a.Stats.Attributes.Add(new Modifier { Attribute = "phasing", Value = 1 });
        Assert.NotNull(a.Runtime.ChooseForAi(a.Sim, a.Caster, a.Enemy));
    }

    [Fact]
    public void AiObservationStorageDoesNotRetainAnotherArenaOrADeclinedSample()
    {
        var first = CastForecastContent.Arena(3);
        first.Book.Slots.Clear();
        CastForecastContent.Slot(first, "forecast_live_ai");
        first.EnemyStats.Resources["mana"] = 0;
        for (var i = 0; i < 30; i++) first.Sim.Clock.Advance();
        Assert.NotNull(first.Runtime.ChooseForAi(first.Sim, first.Caster, first.Enemy));

        var second = CastForecastContent.Arena(3);
        second.Book.Slots.Clear();
        var plain = CastForecastContent.Slot(second, "forecast_live_ai");
        second.EnemyStats.Resources["mana"] = 0;
        Assert.Null(second.Runtime.ChooseForAi(second.Sim, second.Caster, second.Enemy));
        second.Book.Slots.Clear();
        CastForecastContent.Slot(second, "forecast_random");
        Assert.NotNull(second.Runtime.ChooseForAi(second.Sim, second.Caster, second.Enemy)); // Unknown preserves AI fallback.
        second.Book.Slots.Clear();
        second.Book.Slots.Add(plain);
        Assert.Null(second.Runtime.ChooseForAi(second.Sim, second.Caster, second.Enemy));
        Assert.NotNull(first.Runtime.ChooseForAi(first.Sim, first.Caster, first.Enemy));
    }

    [Fact]
    public void RepeatedPrimaryAreaAiQueriesAllocateOnlyTheExistingChoiceRngAfterWarmup()
    {
        var a = CastForecastContent.Arena(1.5f);
        a.Book.Slots.Clear();
        a.Slot("strike");
        for (var i = 0; i < 512; i++) a.Runtime.ChooseForAi(a.Sim, a.Caster, a.Enemy);
        const int count = 2500;
        var before = GC.GetAllocatedBytesForCurrentThread();
        var selected = 0;
        for (var i = 0; i < count; i++)
            if (a.Runtime.ChooseForAi(a.Sim, a.Caster, a.Enemy) is not null) selected++;
        var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(count, selected);
        // ChooseForAi already creates a 56-byte seeded choice stream per call. Observation adds none.
        Assert.True(bytes < count * 80, $"AI primary-area queries allocated {(double)bytes / count:0.###} bytes/call.");
    }

    [Fact]
    public void NestedAiObservationCannotRebindItsOuterSamplesScope()
    {
        var registry = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out _);
        registry.With(new FactionDef { Id = "red", Relations = { ["blue"] = Stance.Hostile } });
        registry.With(new FactionDef { Id = "blue", Relations = { ["red"] = Stance.Hostile } });
        const string function = "t_ai_nested_observation";
        var inner = CastForecastContent.Arena(3);
        inner.Book.Slots.Clear();
        CastForecastContent.Slot(inner, "forecast_live_ai");
        inner.EnemyStats.Resources["mana"] = 0;
        var calls = 0;
        // Deliberate reentrancy injection to exercise storage ownership, not a production pure function.
        ExprFunctions.Register(function, 1, 1, values =>
        {
            calls++;
            Assert.Null(inner.Runtime.ChooseForAi(inner.Sim, inner.Caster, inner.Enemy));
            return values[0];
        }, previewSafe: true);
        try
        {
            registry.With(CastForecastContent.Registry.Get<FormDef>("forecast_timed")! with
            {
                Id = "nested_observation", Effects = new() { CastForecastContent.Area("2 + " + function + "(0) + time + target.mana / 100") },
            });
            var outer = CastingArena.Create(distance: 5, registry: registry);
            outer.Book.Slots.Clear();
            outer.Slot(new WeaveSpec { Form = "nested_observation" });
            outer.EnemyStats.Resources["mana"] = 200;
            for (var i = 0; i < 40; i++) outer.Sim.Clock.Advance();
            for (var i = 0; i < 12; i++)
                Assert.NotNull(outer.Runtime.ChooseForAi(outer.Sim, outer.Caster, outer.Enemy));
            Assert.Equal(12, calls);
        }
        finally { ExprFunctions.Register(function, 1, 1, values => values[0], previewSafe: true); }
    }

    [Fact]
    public void PublicProviderCanRetainItsContextAcrossLaterAiObservations()
    {
        var registry = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out _);
        registry.With(CastForecastContent.Registry.Get<FormDef>("forecast_timed")! with
        {
            Id = "retained_observation", Effects = new() { CastForecastContent.Spec("""{"type":"t_retained_observation"}""") },
        });
        var a = CastingArena.Create(distance: 3, registry: registry);
        var provider = new RetainingObservation();
        a.Sim.Service<EffectRegistry>().Register(provider);
        a.Book.Slots.Clear();
        a.Slot(new WeaveSpec { Form = "retained_observation" });
        Assert.NotNull(a.Runtime.ChooseForAi(a.Sim, a.Caster, a.Enemy));
        var first = Assert.Single(provider.Contexts);
        Assert.True(first.Scope.TryResolve("time", out var firstTime));
        var firstPoint = first.Point;
        for (var i = 0; i < 20; i++) a.Sim.Clock.Advance();
        CastForecastContent.Move(a, a.Enemy, new(14, 10, 0));
        Assert.NotNull(a.Runtime.ChooseForAi(a.Sim, a.Caster, a.Enemy));
        Assert.NotSame(first, provider.Contexts[1]);
        var other = CastForecastContent.Arena(1.5f);
        other.Book.Slots.Clear();
        other.Slot("strike");
        Assert.NotNull(other.Runtime.ChooseForAi(other.Sim, other.Caster, other.Enemy));
        Assert.Same(a.Sim, first.Sim);
        Assert.Equal(a.Caster, first.Source);
        Assert.Equal(firstPoint, first.Point);
        Assert.True(first.Scope.TryResolve("time", out var retainedTime));
        Assert.Equal(firstTime, retainedTime);
        Assert.True(provider.Contexts[1].Scope.TryResolve("time", out var laterTime));
        Assert.True(laterTime > firstTime);
    }

    private sealed class RetainingObservation : IEffectHandler
    {
        public string Type => "t_retained_observation";
        public List<EffectContext> Contexts { get; } = new();
        public CompiledEffect Compile(System.Text.Json.Nodes.JsonObject parameters, EffectCompileContext context)
            => new Effect(Contexts) { Type = Type };

        private sealed class Effect(List<EffectContext> contexts) : CompiledEffect, IDeliveryPlanSource
        {
            public override void Execute(EffectContext context) => throw new InvalidOperationException("Observation executed an effect.");
            public DeliveryForecast DescribeDelivery(EffectContext context)
            {
                contexts.Add(context);
                return DeliveryForecast.Unavailable("test_provider");
            }
        }
    }
}
