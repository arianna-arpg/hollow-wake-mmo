using System.Numerics;
using System.Text.Json;
using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Effects;
using HollowWake.Core.Expressions;
using HollowWake.Core.Maths;
using HollowWake.Core.Tags;
using Xunit;

namespace HollowWake.Core.Tests;

public class ForecastDependencyObservationTests
{
    private static CompiledEffect Area(DeliveryArena arena, string radius) =>
        arena.Effect("{\"type\":\"area\",\"occlusion\":\"free\",\"radius\":" + JsonSerializer.Serialize(radius) + "}");

    [Theory]
    [InlineData("value")]
    [InlineData("condition")]
    [InlineData("gauge")]
    [InlineData("link")]
    [InlineData("cycle")]
    public void APreviouslySafeLeafStillChecksNewDependenciesInTheSameTick(string route)
    {
        var arena = DeliveryArena.Create();
        var context = arena.Context(Vector3.Zero);
        var stats = context.SourceStats!;
        stats.UseRng(new Rng(991));
        stats.Attributes.SetBase("t_leaf", 2);
        var effect = Area(arena, "t_leaf");
        var tick = arena.Sim.Clock.Tick;
        for (var i = 0; i < 3; i++) Assert.Equal(2, DeliveryForecasts.Describe(effect, context).PrimaryRegion!.Value.Size.Radius);

        stats.Attributes.Add(new Modifier { Attribute = "t_unsafe", ValueExpr = Expr.Parse("rand()") });
        stats.Attributes.Add(new Modifier
        {
            Attribute = "t_leaf", Source = "new_dependency", Value = 1,
            ValueExpr = route == "value" ? Expr.Parse("t_unsafe") : route == "cycle" ? Expr.Parse("t_leaf") : null,
            Condition = route == "condition" ? Expr.Parse("t_unsafe > 0") : null,
            Gauge = route == "gauge" ? "t_unsafe" : null,
            Form = route == "link" ? ModifierForm.Link : ModifierForm.Flat,
            From = route == "link" ? "t_unsafe" : null,
        });
        var declined = DeliveryForecasts.Describe(effect, context);
        Assert.Null(declined.PrimaryRegion);
        Assert.Equal(route == "cycle" ? "cyclic_attribute_dependency" : "random_or_opaque_expression", declined.UnavailableReason);
        Assert.Equal(new Rng(991).NextDouble(), stats.NextRandom());
        Assert.Equal(new Rng(77).NextDouble(), context.Rng.NextDouble());
        Assert.Equal(tick, arena.Sim.Clock.Tick);

        stats.Attributes.RemoveSource("new_dependency");
        stats.Attributes.SetBase("t_leaf", 4);
        Assert.Equal(4, DeliveryForecasts.Describe(effect, context).PrimaryRegion!.Value.Size.Radius);
    }

    [Fact]
    public void LeafPurityIsRecheckedWhenACurveRegistrationChanges()
    {
        const string curve = "t_leaf_observation_curve";
        var calls = 0;
        Curves.Register(curve, x => x, previewSafe: true);
        var arena = DeliveryArena.Create();
        var context = arena.Context(Vector3.Zero);
        context.SourceStats!.Attributes.Add(new Modifier
        {
            Attribute = "t_curve_leaf", ValueExpr = Expr.Parse("curve('" + curve + "', 2)"),
        });
        var effect = Area(arena, "t_curve_leaf");
        Assert.Equal(2, DeliveryForecasts.Describe(effect, context).PrimaryRegion!.Value.Size.Radius);
        Curves.Register(curve, x => { calls++; return x; });
        Assert.Equal("random_or_opaque_expression", DeliveryForecasts.Describe(effect, context).UnavailableReason);
        Assert.Equal(0, calls);
        Curves.Register(curve, x => x, previewSafe: true);
        Assert.Equal(2, DeliveryForecasts.Describe(effect, context).PrimaryRegion!.Value.Size.Radius);
    }

    [Theory]
    [InlineData(31, true)]
    [InlineData(32, false)]
    public void DependencyDepthBoundaryStillIncludesLiteralLeafAttributes(int links, bool allowed)
    {
        var arena = DeliveryArena.Create();
        var context = arena.Context(Vector3.Zero);
        var stats = context.SourceStats!;
        for (var i = 0; i < links; i++)
            stats.Attributes.Add(new Modifier { Attribute = "t_depth_" + i, ValueExpr = Expr.Parse("t_depth_" + (i + 1)) });
        stats.Attributes.SetBase("t_depth_" + links, 2);
        var forecast = DeliveryForecasts.Describe(Area(arena, "t_depth_0"), context);
        if (allowed) Assert.Equal(2, forecast.PrimaryRegion!.Value.Size.Radius);
        else
        {
            Assert.Null(forecast.PrimaryRegion);
            Assert.Equal("dependency_depth", forecast.UnavailableReason);
        }
        // A declined deep walk must release its path state before the next observation.
        Assert.Equal(2, DeliveryForecasts.Describe(Area(arena, "2"), context).PrimaryRegion!.Value.Size.Radius);
    }

    [Fact]
    public void ScopedDependenciesAreCheckedOnlyForTheCurrentContext()
    {
        var arena = DeliveryArena.Create();
        var target = arena.Body(12, 10);
        var stats = arena.Sim.World.Get<Stats>(target)!;
        stats.Attributes.SetBase("t_target_leaf", 2);
        var context = arena.Context(Vector3.Zero, target: target);
        var effect = Area(arena, "target.t_target_leaf");
        Assert.Equal(2, DeliveryForecasts.Describe(effect, context).PrimaryRegion!.Value.Size.Radius);
        stats.Attributes.Add(new Modifier
        {
            Attribute = "t_target_leaf", Source = "out_of_scope",
            Scope = TagSet.Of("melee"), ValueExpr = Expr.Parse("rand()"),
        });
        Assert.Equal(2, DeliveryForecasts.Describe(effect, context).PrimaryRegion!.Value.Size.Radius);
        stats.Attributes.Add(new Modifier
        {
            Attribute = "t_target_leaf", Source = "in_scope", ValueExpr = Expr.Parse("rand()"),
        });
        Assert.Equal("random_or_opaque_expression", DeliveryForecasts.Describe(effect, context).UnavailableReason);
        stats.Attributes.RemoveSource("in_scope");
        Assert.Equal(2, DeliveryForecasts.Describe(effect, context).PrimaryRegion!.Value.Size.Radius);
    }

    [Fact]
    public void ResourceFlagDependenciesCannotHideBehindAnEarlierLiteralFlag()
    {
        var arena = DeliveryArena.Create();
        var context = arena.Context(Vector3.Zero);
        var stats = context.SourceStats!;
        stats.Flags.Add("low_life");
        stats.Attributes.Add(new Modifier { Attribute = "life.max", ValueExpr = Expr.Parse("rand()") });
        stats.Attributes.Add(new Modifier
        {
            Attribute = "t_flag_leaf", ValueExpr = Expr.Parse("has('low_life') ? 2 : 3"),
        });
        var effect = Area(arena, "t_flag_leaf");
        Assert.Equal(2, DeliveryForecasts.Describe(effect, context).PrimaryRegion!.Value.Size.Radius);
        stats.Flags.Remove("low_life");
        Assert.Equal("random_or_opaque_expression", DeliveryForecasts.Describe(effect, context).UnavailableReason);
    }

    [Fact]
    public void LinkReadsRetainTheirSingleHopSemantics()
    {
        var arena = DeliveryArena.Create();
        var context = arena.Context(Vector3.Zero);
        var stats = context.SourceStats!;
        stats.Attributes.SetBase("t_link_a", 2);
        stats.Attributes.SetBase("t_link_b", 4);
        stats.Attributes.Add(new Modifier { Attribute = "t_link_a", Form = ModifierForm.Link, From = "t_link_b", Value = .5 });
        stats.Attributes.Add(new Modifier { Attribute = "t_link_b", Form = ModifierForm.Link, From = "t_link_a", Value = 1 });
        Assert.Equal(4, DeliveryForecasts.Describe(Area(arena, "t_link_a"), context).PrimaryRegion!.Value.Size.Radius);
    }
}
