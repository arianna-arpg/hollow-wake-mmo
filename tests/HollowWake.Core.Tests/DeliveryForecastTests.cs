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
using HollowWake.Core.Tags;
using Xunit;

namespace HollowWake.Core.Tests;

public class DeliveryForecastTests
{
    private const string Hit = "\"effects\":[{\"type\":\"event\",\"name\":\"t_hit\"}]";

    private static CompiledEffect Area(DeliveryArena arena, string properties = "") =>
        arena.Effect("{\"type\":\"area\"," + (properties.Length > 0 ? properties + "," : "") + Hit + "}");

    private static ResolvedAreaPlan Plan(CompiledEffect effect, EffectContext context)
    {
        var forecast = DeliveryForecasts.Describe(effect, context);
        Assert.Null(forecast.UnavailableReason);
        return Assert.IsType<ResolvedAreaPlan>(forecast.PrimaryRegion);
    }

    public static IEnumerable<object[]> ShapesAndFacings => Shapes.Names.SelectMany(shape =>
        new[] { new object[] { shape, 0f }, new object[] { shape, 1.1f } });

    [Theory]
    [MemberData(nameof(ShapesAndFacings))]
    public void PrimaryCandidatesMatchActualExecutionAcrossRegisteredShapes(string shape, float rotation)
    {
        var arena = DeliveryArena.Create();
        var bodies = new List<Entity>();
        for (var x = 6; x <= 14; x++)
            for (var y = 6; y <= 14; y++)
                bodies.Add(arena.Body(x + 0.17f, y + 0.09f));
        var effect = Area(arena, $"\"shape\":\"{shape}\",\"radius\":3,\"angle\":125,\"width\":2,\"inner\":0.2,\"occlusion\":\"free\"");
        var context = arena.Context(new Vector3(10, 10, 0), new Vector2(MathF.Cos(rotation), MathF.Sin(rotation)));
        var plan = Plan(effect, context);
        var expected = bodies.Where(e => plan.IsCandidate(context, e, arena.Sim.World.Get<Transform>(e)!.Position)).ToHashSet();
        Assert.NotEmpty(expected);
        effect.Execute(context);
        Assert.True(expected.SetEquals(arena.Struck()));
    }

    [Theory]
    [InlineData("cleave", "", "cone")]
    [InlineData("cleave", "crescent_sigil", "crescent")]
    [InlineData("nova", "", "circle")]
    [InlineData("blast", "", "circle")]
    public void CapabilityComesFromThePatchedCompiledMechanism(string form, string glyph, string shape)
    {
        var arena = DeliveryArena.Create();
        var weave = glyph.Length == 0 ? arena.Weave(form) : arena.Weave(form, "", glyph);
        var context = arena.Context(new Vector3(17, 10, 0), Vector2.UnitY, weave: weave);
        var forecast = DeliveryForecasts.Describe(weave.Effects, context);
        Assert.Null(forecast.UnavailableReason);
        Assert.Equal(shape, forecast.PrimaryRegion!.Value.Shape.Name);
        Assert.Equal(Vector2.UnitY, forecast.PrimaryRegion.Value.Direction);
    }

    [Fact]
    public void LiveModifiersUseTheExistingFoldAndOldSamplesDoNotMove()
    {
        var arena = DeliveryArena.Create();
        var weave = arena.Weave("cleave", "", "t_wide");
        var context = arena.Context(new Vector3(40, 40, 0), Vector2.UnitX, weave: weave);
        var effect = Area(arena, "\"shape\":\"sector\",\"reach\":\"edge\"");
        var before = Plan(effect, context);
        var stats = context.SourceStats!;
        stats.Attributes.Add(new Modifier { Attribute = "area.radius", Value = 2, Scope = weave.Tags });
        stats.Attributes.Add(new Modifier { Attribute = "area.radius", Form = ModifierForm.More, Value = 0.25 });
        stats.Attributes.Add(new Modifier { Attribute = "area.angle", Form = ModifierForm.Increased, Value = 0.25 });
        stats.Attributes.Add(new Modifier { Attribute = "melee.reach", Value = 0.5 });
        context.SourceTransform!.Position = new Vector3(20, 20, 0);
        var after = Plan(effect, context);
        var expectedRadius = weave.Attr(stats, "area.radius", context.Tags) + weave.Attr(stats, "melee.reach", context.Tags) + context.SourceTransform.Radius;
        Assert.Equal(expectedRadius, after.Size.Radius, 5);
        Assert.Equal(90 * weave.Attr(stats, "area.angle", context.Tags), after.Size.Angle, 5);
        Assert.True(after.Size.Radius > before.Size.Radius);
        Assert.Equal(new Vector3(10, 10, 0), before.Anchor);
        Assert.Equal(new Vector3(20, 20, 0), after.Anchor);
    }

    [Fact]
    public void SourcePointFocusAndDeliveryOriginStayIndependentOfFacing()
    {
        var arena = DeliveryArena.Create();
        var target = arena.Body(19, 13);
        var context = new EffectContext
        {
            Sim = arena.Sim, Source = arena.Caster, Target = target, Point = new Vector3(30, 30, 0),
            From = new Vector3(2, 3, 1), Direction = new Vector2(0, 7), Rng = new Rng(9),
            Vars = new() { ["placement.scale"] = 0.5 },
        };
        var source = Plan(Area(arena, "\"shape\":\"line\",\"radius\":4"), context);
        Assert.Equal(context.From, source.Anchor);
        Assert.Equal(Vector2.UnitY, source.Direction);
        Assert.Equal(2, source.Size.Radius);
        var pointEffect = Area(arena, "\"center\":\"point\"");
        var focusEffect = Area(arena);
        var point = Plan(pointEffect, context);
        var focus = Plan(focusEffect, context);
        arena.Sim.World.Get<Transform>(target)!.Position += Vector3.UnitX;
        Assert.Equal(context.Point, Plan(pointEffect, context).Anchor);
        Assert.Equal(point.Anchor, Plan(pointEffect, context).Anchor);
        Assert.Equal(focus.Anchor + Vector3.UnitX, Plan(focusEffect, context).Anchor);
        context.SourceTransform!.Yaw = MathF.PI;
        Assert.True(Vector2.Distance(-Vector2.UnitX, Plan(Area(arena), arena.Context(Vector3.Zero)).Direction) < 0.00001f);
    }

    [Fact]
    public void CandidateContractIncludesHeightRelationsUntargetabilityAndExcludedTarget()
    {
        var arena = DeliveryArena.Create();
        var eligible = arena.Body(12, 10);
        var excluded = arena.Body(11, 10);
        var ally = arena.Body(12, 11, "red");
        var hidden = arena.Body(11, 11);
        var high = arena.Body(10, 11);
        var edge = arena.Body(13.01f, 10);
        arena.Sim.World.Get<Stats>(hidden)!.Flags.Add("untargetable");
        var height = arena.Sim.World.Get<Transform>(high)!;
        height.Position += Vector3.UnitZ * 4.01f;
        arena.Sim.Spatial.Update(high, height.Position);
        var effect = Area(arena, "\"center\":\"source\",\"radius\":3,\"include_center\":false,\"occlusion\":\"free\"");
        var context = arena.Context(Vector3.Zero, target: excluded);
        var plan = Plan(effect, context);
        Assert.Equal(excluded, plan.ExcludedTarget);
        Assert.False(plan.RequiresLineOfSight);
        Assert.True(plan.ContainsCenter(new Vector3(10, 10, 4)));
        Assert.True(plan.IsCandidate(context, eligible, arena.Sim.World.Get<Transform>(eligible)!.Position));
        foreach (var e in new[] { excluded, ally, hidden, high, edge })
            Assert.False(plan.IsCandidate(context, e, arena.Sim.World.Get<Transform>(e)!.Position));
        effect.Execute(context);
        Assert.Equal(new[] { eligible }, arena.Struck());
        Assert.True(Plan(Area(arena), context).RequiresLineOfSight);
    }

    [Fact]
    public void SelectionRequirementsAndEchoesAreNotPredictedOrExecuted()
    {
        var arena = DeliveryArena.Create();
        arena.Body(11, 10);
        arena.Body(12, 10);
        var weave = arena.Weave("blast", "ember", "t_echo");
        var context = arena.Context(new Vector3(10, 10, 0), weave: weave);
        var effect = Area(arena, "\"radius\":5,\"max_targets\":\"randi(1,1)\",\"require\":\"rand() >= 0\"");
        var count = arena.Sim.Spatial.Count;
        var resources = context.SourceStats!.Resources.ToArray();
        arena.Events.Clear();
        for (var i = 0; i < 20; i++)
        {
            var plan = Plan(effect, context);
            Assert.True(plan.HasRequirement);
            Assert.True(plan.HasTargetLimit);
        }
        Assert.Equal(new Rng(77).NextDouble(), context.Rng.NextDouble());
        Assert.Equal(resources, context.SourceStats.Resources.ToArray());
        Assert.Empty(arena.Events);
        Assert.Equal(count, arena.Sim.Spatial.Count);
        arena.Tick(40);
        Assert.Empty(arena.Struck()); // No preview-created scheduled scatter or pulses.
    }

    [Theory]
    [InlineData("radius", "rand()")]
    [InlineData("radius", "true ? 3 : randi(1, 5)")]
    [InlineData("angle", "rand()")]
    [InlineData("width", "randi(1, 5)")]
    [InlineData("inner", "rand()")]
    public void RandomGeometryIsDeclinedBeforeEitherStreamIsRead(string parameter, string expression)
    {
        var arena = DeliveryArena.Create();
        var context = arena.Context(Vector3.Zero);
        context.SourceStats!.UseRng(new Rng(99));
        var effect = Area(arena, "\"shape\":\"line\",\"" + parameter + "\":" + JsonSerializer.Serialize(expression));
        for (var i = 0; i < 3; i++)
        {
            var forecast = DeliveryForecasts.Describe(effect, context);
            Assert.Null(forecast.PrimaryRegion);
            Assert.Equal("random_or_opaque_expression", forecast.UnavailableReason);
        }
        Assert.Equal(new Rng(77).NextDouble(), context.Rng.NextDouble());
        Assert.Equal(new Rng(99).NextDouble(), context.SourceStats.NextRandom());
    }

    [Theory]
    [InlineData("value")]
    [InlineData("condition")]
    [InlineData("link")]
    [InlineData("gauge")]
    [InlineData("resource_fraction")]
    [InlineData("resource_flag")]
    [InlineData("reserved")]
    [InlineData("target")]
    [InlineData("weave")]
    [InlineData("phasing")]
    [InlineData("angle_scale")]
    [InlineData("melee_reach")]
    public void IndirectRandomDependenciesAreDeclinedWithoutPrimingAttributes(string route)
    {
        var arena = DeliveryArena.Create();
        var target = arena.Body(12, 10);
        var weave = arena.Weave("cleave", "");
        var context = arena.Context(Vector3.Zero, target: target, weave: weave);
        var stats = route == "target" ? context.TargetStats! : context.SourceStats!;
        stats.UseRng(new Rng(991));
        var attribute = route switch
        {
            "resource_fraction" or "resource_flag" => "life.max", "reserved" => "life.reserved",
            "phasing" => "phasing", "angle_scale" => "area.angle", "melee_reach" => "melee.reach", _ => "t_random",
        };
        stats.Attributes.Add(new Modifier { Attribute = attribute, ValueExpr = Expr.Parse("rand()") });
        var radius = route switch
        {
            "target" => "target.t_random", "weave" => "weave.t_random", "resource_fraction" or "reserved" => "life_pct",
            "resource_flag" => "has('low_life') ? 2 : 3", _ => "t_random",
        };
        if (route is "value" or "condition" or "link" or "gauge")
        {
            stats.Attributes.Add(new Modifier
            {
                Attribute = "t_indirect", Value = 1,
                ValueExpr = route == "value" ? Expr.Parse("t_random") : null,
                Condition = route == "condition" ? Expr.Parse("t_random > 0") : null,
                Form = route == "link" ? ModifierForm.Link : ModifierForm.Flat,
                From = route == "link" ? "t_random" : null, Gauge = route == "gauge" ? "t_random" : null,
            });
            radius = "t_indirect";
        }
        var properties = route is "phasing" or "angle_scale" or "melee_reach" ? "\"shape\":\"sector\"" : "\"radius\":" + JsonSerializer.Serialize(radius);
        var forecast = DeliveryForecasts.Describe(Area(arena, properties), context);
        Assert.Null(forecast.PrimaryRegion);
        Assert.Equal("random_or_opaque_expression", forecast.UnavailableReason);
        Assert.Equal(new Rng(991).NextDouble(), stats.NextRandom());
        Assert.Equal(new Rng(77).NextDouble(), context.Rng.NextDouble());
    }

    [Fact]
    public void AttributeFormulasCyclesAndBaseOverridesAreCheckedWithoutEvaluating()
    {
        var arena = DeliveryArena.Create();
        var content = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out _)
            .With(new AttributeDef { Id = "t_formula", Formula = Expr.Parse("rand()") });
        var stats = new Stats(content);
        stats.UseRng(new Rng(5));
        arena.Sim.World.Add(arena.Caster, stats);
        var effect = Area(arena, "\"radius\":\"t_formula\",\"occlusion\":\"free\"");
        var context = arena.Context(Vector3.Zero);
        Assert.Null(DeliveryForecasts.Describe(effect, context).PrimaryRegion);
        Assert.Equal(new Rng(5).NextDouble(), stats.NextRandom());
        stats.Attributes.SetBase("t_formula", 3);
        Assert.Equal(3, Plan(effect, context).Size.Radius);
        stats.Attributes.Add(new Modifier { Attribute = "t_cycle", ValueExpr = Expr.Parse("t_cycle") });
        Assert.Equal("cyclic_attribute_dependency", DeliveryForecasts.Describe(Area(arena, "\"radius\":\"t_cycle\""), context).UnavailableReason);
    }

    [Fact]
    public void UnknownScopesAreNotCalledButExplicitContextVariablesStillWork()
    {
        var arena = DeliveryArena.Create();
        var context = arena.Context(Vector3.Zero);
        var scope = new CountingScope();
        context.SourceStats!.Mounted = new ScopeOverlay(scope);
        var effect = Area(arena, "\"radius\":\"outside\"");
        Assert.Equal("opaque_mounted_scope", DeliveryForecasts.Describe(effect, context).UnavailableReason);
        Assert.Equal(0, scope.Reads);
        context.Vars["outside"] = 4;
        Assert.Equal(4, Plan(effect, context).Size.Radius);
        Assert.Equal(0, scope.Reads);
    }

    [Fact]
    public void ObservationDoesNotInitializeTheExecutionScopeOrFreezeTimeAndDistance()
    {
        var arena = DeliveryArena.Create();
        var target = arena.Body(12, 10);
        var context = arena.Context(Vector3.Zero, target: target);
        var effect = Area(arena, "\"radius\":\"distance + time\"");
        Plan(effect, context);
        arena.Sim.Clock.Advance();
        context.TargetTransform!.Position += Vector3.UnitX;
        Assert.Equal(arena.Sim.Clock.Time, context.Eval(Expr.Parse("time")));
        Assert.Equal(context.SourceTransform!.DistanceTo(context.TargetTransform), context.Eval(Expr.Parse("distance")));
    }

    [Fact]
    public void RepeatedObservationsDoNotChangeRandomSelectionOrScheduledPayloadTrace()
    {
        List<string> Run(bool observe)
        {
            var arena = DeliveryArena.Create();
            for (var x = 9; x <= 13; x++) arena.Body(x, 10);
            var weave = arena.Weave("blast", "ember", "t_echo");
            var context = arena.Context(new Vector3(10, 10, 0), weave: weave);
            var effect = Area(arena, "\"radius\":4,\"max_targets\":2,\"require\":\"rand() < 0.75\"");
            if (observe)
                for (var i = 0; i < 30; i++) Plan(effect, context);
            effect.Execute(context);
            arena.Tick(40);
            var trace = arena.Events.OfType<HollowWake.Core.Sim.CustomEvent>().Where(e => e.Name == "t_hit").Select(e => e.ToString()).ToList();
            Assert.NotEmpty(trace);
            trace.Add(context.Rng.NextDouble().ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            return trace;
        }
        Assert.Equal(Run(false), Run(true));
    }

    [Fact]
    public void UnsupportedGraphsAndInvalidGeometryReturnUnknownCoverage()
    {
        var arena = DeliveryArena.Create();
        var context = arena.Context(Vector3.Zero);
        foreach (var json in new[] { "{\"type\":\"projectile\"}", "{\"type\":\"delay\",\"seconds\":1,\"effects\":[{\"type\":\"area\"}]}" })
            Assert.Equal("unsupported_delivery", DeliveryForecasts.Describe(arena.Effect(json), context).UnavailableReason);
        Assert.Equal("dependent_effect_graph", DeliveryForecasts.Describe(new[] { Area(arena), Area(arena) }, context).UnavailableReason);
        Assert.Equal("dependent_effect_graph", DeliveryForecasts.Describe(Array.Empty<CompiledEffect>(), context).UnavailableReason);
        Assert.Equal("unresolved_expression", DeliveryForecasts.Describe(Area(arena, "\"radius\":\"unknown_value\""), context).UnavailableReason);
        Assert.Equal("non_finite_geometry", DeliveryForecasts.Describe(Area(arena, "\"radius\":\"exp(10000)\""), context).UnavailableReason);
        Assert.Equal("invalid_expression", DeliveryForecasts.Describe(Area(arena, "\"radius\":\"round(2,100)\""), context).UnavailableReason);
    }

    [Fact]
    public void CustomMechanismsCanOptInThroughTheirCompiledEffectCapability()
    {
        var arena = DeliveryArena.Create();
        var registry = new EffectRegistry();
        registry.Register(new ObservedHandler());
        var effect = registry.Compile(DeliveryArena.Spec("{\"type\":\"t_observed\"}"), new EffectCompileContext { Content = arena.Sim.Content, Registry = registry, Owner = "test" });
        Assert.Equal("custom_sample", DeliveryForecasts.Describe(effect, arena.Context(Vector3.Zero)).UnavailableReason);
    }

    private sealed class CountingScope : IExprScope
    {
        public int Reads;
        public bool TryResolve(string name, out double value) { Reads++; value = 3; return true; }
        public bool HasFlag(string flag) { Reads++; return true; }
        public double NextRandom() { Reads++; return 0.5; }
    }

    private sealed class ObservedHandler : IEffectHandler
    {
        public string Type => "t_observed";
        public EffectSignature Signature { get; } = EffectSignature.Of();
        public CompiledEffect Compile(System.Text.Json.Nodes.JsonObject p, EffectCompileContext context) => new ObservedEffect { Type = Type };
    }

    private sealed class ObservedEffect : CompiledEffect, IDeliveryPlanSource
    {
        public override void Execute(EffectContext context) => throw new InvalidOperationException("Observation executed the payload.");
        public DeliveryForecast DescribeDelivery(EffectContext context) => DeliveryForecast.Unavailable("custom_sample");
    }
}

public class ForecastExpressionTests
{
    [Theory]
    [InlineData("min(2, max(1, level))", true)]
    [InlineData("has('low_life') ? 1 : 2", true)]
    [InlineData("curve('smooth', level)", true)]
    [InlineData("true ? 1 : rand()", false)]
    [InlineData("false && randi(1, 2)", false)]
    [InlineData("-rand()", false)]
    [InlineData("min(3, rand())", false)]
    public void PreflightDoesNotTreatScopeDependenciesAsProofOfSafety(string expression, bool safeTree) =>
        Assert.Equal(safeTree, Expr.Parse(expression).IsPreviewSafe);

    [Fact]
    public void CustomFunctionsAndCurvesRequireAnExplicitPurityPromise()
    {
        var calls = 0;
        ExprFunctions.Register("t_forecast_opaque", 1, 1, a => { calls++; return a[0]; });
        ExprFunctions.Register("t_forecast_pure", 1, 1, a => a[0] * 2, previewSafe: true);
        Curves.Register("t_forecast_curve", t => t, previewSafe: true);
        var arena = DeliveryArena.Create();
        DeliveryForecast Describe(string radius) => DeliveryForecasts.Describe(arena.Effect("{\"type\":\"area\",\"radius\":" + JsonSerializer.Serialize(radius) + "}"), arena.Context(Vector3.Zero));
        Assert.Null(Describe("t_forecast_opaque(3)").PrimaryRegion);
        Assert.Equal(0, calls);
        Assert.Equal(6, Describe("t_forecast_pure(3)").PrimaryRegion!.Value.Size.Radius);
        var curve = Expr.Parse("curve('t_forecast_curve', 3)");
        Assert.True(curve.IsPreviewSafe);
        Assert.Equal(3, Describe(curve.Source).PrimaryRegion!.Value.Size.Radius);
        Curves.Register("t_forecast_curve", t => { calls++; return t; });
        Assert.False(curve.IsPreviewSafe);
        Assert.Null(Describe(curve.Source).PrimaryRegion);
        Assert.Equal(0, calls);
        Assert.Equal(3, Expr.Parse("t_forecast_opaque(3)").Evaluate(EmptyScope.Instance));
        Assert.Equal(1, calls); // Execution remains supported without preview opt-in.
    }
}
