using HollowWake.Core.Definitions;
using HollowWake.Core.Effects;
using HollowWake.Core.Weaves;
using Xunit;

namespace HollowWake.Core.Tests.Grammar;

public class UndeadAxisIntegrationTests
{
    [Fact]
    public void CorpseRaisedPatternIsVisibleBesideItsCorpseRequirement()
    {
        var content = CoreContent.Registry;
        var compiler = new WeaveCompiler(content, EffectRegistry.Shared);
        var raised = AxisReport.Of(compiler.Compile(new WeaveSpec { Preset = "raise_zombie" }, GlyphFit.Strict), content);
        var body = Assert.Single(raised, value => value.Effect == "summon" && value.Parameter == "raise_as");
        Assert.Equal(Dimension.Agency, body.Dimension);
        Assert.Equal("agency.summon", body.Name);
        Assert.Equal("zombie", body.Word);
        Assert.Equal("effects[0].raise_as", body.Path);
        Assert.Contains(raised, value => value.Name == "agency.from_corpse" && value.Word == "target");
        Assert.Contains(raised, value => value.Name == "economy.spends" && value.Word == "corpse");

        // Ordinary summons report their own pattern without inventing a corpse conversion.
        var called = AxisReport.Of(compiler.Compile(new WeaveSpec { Preset = "summon_skeleton" }, GlyphFit.Strict), content);
        Assert.DoesNotContain(called, value => value.Parameter == "raise_as");
        Assert.Contains(called, value => value.Name == "agency.summon" && value.Word == "skeleton_warrior");
    }
}
