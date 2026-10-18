using System.Numerics;
using System.Text.Json.Nodes;
using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Effects;
using HollowWake.Core.Expressions;
using HollowWake.Core.Maths;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;
using Xunit;

namespace HollowWake.Core.Tests;

public class ReleasePolicyTests
{
    private static ContentRegistry Content()
    {
        var content = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out _);
        content.With(new FactionDef { Id = "red", Relations = { ["blue"] = Stance.Hostile } });
        content.With(new FactionDef { Id = "blue", Relations = { ["red"] = Stance.Hostile } });
        return content;
    }

    [Fact]
    public void ReleaseBlocksComposeThroughPresetAndFirstActiveGlyph()
    {
        var content = Content();
        content.With(CastForecastContent.Registry.Get<FormDef>("forecast_direct")! with
            { ReleaseCheck = new() { HeightTolerance = Expr.Parse("1") } });
        content.With(new WeaveDef { Id = "release_preset", Form = "forecast_direct",
            ReleaseCheck = new() { HeightTolerance = Expr.Parse("2") } });
        content.With(new GlyphDef { Id = "release_glyph", ReleaseCheck = new() { HeightTolerance = Expr.Parse("3") } });
        content.With(new GlyphDef { Id = "release_later", ReleaseCheck = new() { HeightTolerance = Expr.Parse("9") } });
        var compiler = new WeaveCompiler(content, EffectRegistry.CreateDefault());
        var form = compiler.Compile(new WeaveSpec { Form = "forecast_direct" });
        var preset = compiler.Compile(new WeaveSpec { Preset = "release_preset" });
        var glyph = compiler.Compile(new WeaveSpec { Preset = "release_preset", Glyphs = { "release_glyph", "release_later" } });
        Assert.Equal(1, form.Casting.ReleaseCheck.HeightTolerance!.Evaluate(EmptyScope.Instance));
        Assert.Equal(2, preset.Casting.ReleaseCheck.HeightTolerance!.Evaluate(EmptyScope.Instance));
        Assert.Equal(3, glyph.Casting.ReleaseCheck.HeightTolerance!.Evaluate(EmptyScope.Instance));
    }

    [Fact]
    public void DeliveryPolicyRejectsRootPayloadRidersAndUnknownPolicyWords()
    {
        var content = Content();
        content.With(new GlyphDef { Id = "escaped_payload", AddsEffects =
            { CastForecastContent.Spec("""{"type":"damage","amount":10,"thread":"steel"}""") } });
        content.With(new GlyphDef { Id = "unknown_policy", ReleaseCheck = new() { Policy = "typo" } });
        var compiler = new WeaveCompiler(content, EffectRegistry.CreateDefault());
        Assert.Contains("every root", Assert.Throws<WeaveCompileException>(() =>
            compiler.Compile(new WeaveSpec { Form = "strike", Glyphs = { "escaped_payload" } })).Message);
        Assert.Contains("Unknown release policy", Assert.Throws<WeaveCompileException>(() =>
            compiler.Compile(new WeaveSpec { Form = "strike", Glyphs = { "unknown_policy" } })).Message);
        var pattern = new PatternDef { Id = "invalid_rider_pattern", Components =
            { ["weaves"] = JsonNode.Parse("""{"list":[{"form":"strike","glyphs":["escaped_payload"]}]}""")!.AsObject() } };
        var report = new ValidationReport();
        Assert.True(ContentKinds.TryGet("patterns", out var kind));
        kind!.Validate!(pattern, new ValidationContext(content, report, "patterns", pattern.Id, null));
        Assert.Contains(report.Errors, error => error.Message.Contains("every root"));
    }

    [Fact]
    public void DeliveryPolicyCannotUseAnUnoccludedSelectorAsAWallBypass()
    {
        var content = Content();
        content.With(content.Get<FormDef>("strike")! with { Id = "unoccluded_selector", Effects =
            new() { CastForecastContent.Spec("""{"type":"area","radius":2,"occlusion":"free","effects":[]}""") } });
        var compiler = new WeaveCompiler(content, EffectRegistry.CreateDefault());
        Assert.Throws<WeaveCompileException>(() => compiler.Compile(new WeaveSpec { Form = "unoccluded_selector" }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PhasingIsTheBodyReleaseWallBypass(bool phases)
    {
        var a = CastForecastContent.Arena();
        var slot = CastForecastContent.Slot(a, "forecast_direct");
        if (phases) a.Stats.Attributes.Add(new Modifier { Attribute = "phasing", Form = ModifierForm.Flat, Value = 1 });
        a.Begin(slot);
        CastForecastContent.Wall(a);
        a.RunSeconds(1);
        Assert.Equal(phases ? 1 : 0, a.Custom("t_shot").Count);
    }

    [Fact]
    public void BodyReleaseUsesNestedOriginAndAuthoredHeightAndRejectsDeadLivingTargets()
    {
        var content = Content();
        content.With(CastForecastContent.Registry.Get<FormDef>("forecast_direct")! with
            { Target = new() { Kind = "enemy" }, ReleaseCheck = new() { HeightTolerance = Expr.Parse("0.5") } });
        var a = CastingArena.Create(distance: 3, registry: content);
        var weave = a.Sim.Service<WeaveCompiler>().Compile(new WeaveSpec { Form = "forecast_direct" });
        EffectContext Context(Vector3 origin) => new() { Sim = a.Sim, Source = a.Caster, Target = a.Enemy,
            From = origin, Point = new(13, 10, 0), Weave = weave, Tags = weave.Tags, Rng = new Rng(1) };
        Assert.True(ReleaseTargets.Check(Context(new(13, 10, 0)), out var valid), valid);
        Assert.False(ReleaseTargets.Check(Context(new(13, 10, 1)), out var height));
        Assert.Equal("out_of_reach", height);
        Assert.False(ReleaseTargets.Execute(Context(new(13, 10, 1)), weave.Effects));
        var miss = Assert.Single(a.Of<CastPresentationEvent>(), e => e.Phase == "miss");
        Assert.True(miss.Outcome);
        Assert.Equal(new Vector3(13, 10, 1), miss.Position);
        a.Sim.World.Add(a.Enemy, new Dead());
        Assert.False(ReleaseTargets.Check(Context(new(13, 10, 0)), out var dead));
        Assert.Equal("invalid_target", dead);
    }
}
