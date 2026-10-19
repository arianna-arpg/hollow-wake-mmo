using System.Numerics;
using HollowWake.Core.Attributes;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Effects;
using HollowWake.Core.Expressions;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;
using Xunit;

namespace HollowWake.Core.Tests;

public class AreaRadiusSupportTests
{
    [Theory]
    [InlineData("strike", "ring", false)]
    [InlineData("strike", "wide", false)]
    [InlineData("measured_strike", "ring", false)]
    [InlineData("measured_strike", "wide", false)]
    [InlineData("bash", "ring", false)]
    [InlineData("bash", "wide", false)]
    [InlineData("strike", "ring", true)]
    [InlineData("strike", "wide", true)]
    [InlineData("measured_strike", "ring", true)]
    [InlineData("measured_strike", "wide", true)]
    [InlineData("bash", "ring", true)]
    [InlineData("bash", "wide", true)]
    public void RadiusSupportsExpandBothForecastAndActualContactWithoutLosingRangeModifiers(string form, string support, bool increasedRange)
    {
        (CastingArena Arena, CastForecast Sample) Prepare(bool supported)
        {
            var a = CastForecastContent.Arena(1.5f);
            a.Stats.Attributes.Add(new Modifier { Attribute = "accuracy", Form = ModifierForm.Override, Value = 100000 });
            if (increasedRange) a.Stats.Attributes.Add(new Modifier { Attribute = "range", Form = ModifierForm.Increased, Value = .25 });
            a.Sim.World.Get<Transform>(a.Enemy)!.Height = 1;
            a.Sim.World.Add(a.Enemy, new HitBody { Definition = new HitBodyDef { Id = "small_contact",
                Volumes = new() { new() { Shape = "cylinder", Up = .5f, Radius = .01f, HalfHeight = .5f } } } });
            HitBodies.InvalidateBounds(a.Sim);
            var spec = new WeaveSpec { Form = form };
            if (supported) spec.Glyphs.Add(support == "ring" ? new GlyphRef { Id = "ring" } :
                new GlyphRef { Id = "weavers_chassis", Rows = new Dictionary<string, string> { ["edge"] = "keen", ["reach"] = "wide" } });
            var slot = a.Slot(spec); a.Begin(slot);
            Assert.Empty(slot.Dormant);
            return (a, a.Runtime.DescribeCast(a.Sim, a.Caster)!);
        }
        var plain = Prepare(false); var enhanced = Prepare(true);
        var original = plain.Sample.Delivery.PrimaryRegion!.Value;
        var expanded = enhanced.Sample.Delivery.PrimaryRegion!.Value;
        var padding = enhanced.Arena.Sim.World.Get<Transform>(enhanced.Arena.Caster)!.Radius +
            (float)enhanced.Sample.Context.Weave!.Attr(enhanced.Arena.Stats, "melee.reach");
        Assert.Equal((original.Size.Radius - padding) * (support == "ring" ? 1.5 : 1.3), expanded.Size.Radius - padding, 5);
        var between = original.Anchor + new Vector3((original.Size.Radius + expanded.Size.Radius) / 2, 0, 0);
        foreach (var entry in new[] { plain, enhanced }) CastForecastContent.Move(entry.Arena, entry.Arena.Enemy, between);
        Assert.False(original.IsCandidate(plain.Sample.Context, plain.Arena.Enemy, between));
        Assert.True(expanded.IsCandidate(enhanced.Sample.Context, enhanced.Arena.Enemy, between));
        plain.Arena.RunSeconds(3); enhanced.Arena.RunSeconds(3);
        Assert.DoesNotContain(plain.Arena.Of<DamageDealt>(), hit => hit.Target == plain.Arena.Enemy && hit.Total > 0);
        Assert.Contains(enhanced.Arena.Of<DamageDealt>(), hit => hit.Target == enhanced.Arena.Enemy && hit.Total > 0);
    }

    [Fact]
    public void ExternalRadiusBaseUsesTheSameFlatLinkIncreasedMoreAndOverrideLayers()
    {
        var a = DeliveryArena.Create(); var stats = a.Context(default).SourceStats!;
        stats.Attributes.SetBase("range", 4);
        foreach (var modifier in new[] {
            new Modifier { Attribute = "area.radius", Value = 1 },
            new Modifier { Attribute = "area.radius", Form = ModifierForm.Link, From = "range", Value = .5 },
            new Modifier { Attribute = "area.radius", Form = ModifierForm.Increased, Value = .5 },
            new Modifier { Attribute = "area.radius", Form = ModifierForm.More, Value = .2 } }) stats.Attributes.Add(modifier);
        var effect = a.Effect("""{"type":"area","radius_base":3,"effects":[]}""");
        float Radius() => DeliveryForecasts.Describe(effect, a.Context(default)).PrimaryRegion!.Value.Size.Radius;
        Assert.Equal(10.8, Radius(), 5);
        stats.Attributes.Add(new Modifier { Attribute = "area.radius", Form = ModifierForm.Override, Value = 2 });
        Assert.Equal(2, Radius());
    }

    [Fact]
    public void ResolvedRadiusIsNotFoldedAgainAndExternalBaseKeepsCatalogBounds()
    {
        var a = DeliveryArena.Create();
        var stats = a.Context(default).SourceStats!;
        stats.Attributes.Add(new Modifier { Attribute = "area.radius", Form = ModifierForm.More, Value = 1 });
        var context = a.Context(default, weave: a.Weave("blast"));
        var resolved = a.Effect("""{"type":"area","radius":"weave.area.radius"}""");
        Assert.Equal(context.Weave!.Attr(stats, "area.radius"),
            DeliveryForecasts.Describe(resolved, context).PrimaryRegion!.Value.Size.Radius, 5);
        stats.Attributes.Add(new Modifier { Attribute = "area.radius", Form = ModifierForm.Override, Value = -10 });
        var external = a.Effect("""{"type":"area","radius_base":3}""");
        Assert.Equal(.25f, DeliveryForecasts.Describe(external, context).PrimaryRegion!.Value.Size.Radius);
    }

    [Fact]
    public void AmbiguousRadiusAuthoringIsRejectedAndUnsafeDependenciesDeclineObservation()
    {
        var a = DeliveryArena.Create();
        Assert.Throws<InvalidOperationException>(() => a.Effect("""{"type":"area","radius":2,"radius_base":3}"""));
        var unsafeBase = a.Effect("""{"type":"area","radius_base":"rand() + 2","effects":[]}""");
        Assert.Null(DeliveryForecasts.Describe(unsafeBase, a.Context(default)).PrimaryRegion);
        a.Context(default).SourceStats!.Attributes.Add(new Modifier { Attribute = "area.radius", ValueExpr = Expr.Parse("rand()") });
        var unsafeModifier = a.Effect("""{"type":"area","radius_base":3,"effects":[]}""");
        Assert.Null(DeliveryForecasts.Describe(unsafeModifier, a.Context(default)).PrimaryRegion);
    }
}
