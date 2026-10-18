using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Effects;
using HollowWake.Core.Weaves;
using Xunit;

namespace HollowWake.Core.Tests.Grammar;

public class AxisProvenanceTests
{
    [Theory]
    [InlineData("default", 30, "t_axes_explicit_speed")]
    [InlineData("first", 20, "t_axes_default_speed")]
    public void ThePatchThatWinsOwnsTheFinalValue(string initialMode, double expectedSpeed, string expectedGlyph)
    {
        var directory = Path.Combine(Path.GetTempPath(), "hollowwake-axis-provenance-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "glyphs.yaml"), $$"""
kind: glyphs
entries:
  - id: t_axes_default_speed
    name: Default speed
    requires_mechanisms: [projectile]
    patches:
      - { type: projectile, mode: {{initialMode}}, set: { speed: 20 } }
  - id: t_axes_explicit_speed
    name: Explicit speed
    requires_mechanisms: [projectile]
    patches:
      - { type: projectile, mode: first, set: { speed: 30 } }
""");
            var content = ContentRegistry.Load(new[] { CoreContent.FindContentDir(), directory }, out var validation);
            Assert.False(validation.HasErrors, validation.ToString());
            var weave = new WeaveCompiler(content, EffectRegistry.Shared).Compile(new WeaveSpec
            {
                Form = "bolt",
                Threads = { "ember" },
                Glyphs = { "t_axes_default_speed", "t_axes_explicit_speed" },
            }, GlyphFit.Strict);
            var speed = Assert.Single(AxisReport.Of(weave, content), value => value.Path == "effects[0].speed");
            Assert.Equal(expectedSpeed, speed.Number);
            Assert.Equal(AxisSource.Patch, speed.Source);
            Assert.Equal("glyph " + expectedGlyph, speed.Writer);
            Assert.Equal("patches[0].set.speed", speed.Origin);
            Assert.Contains(expectedGlyph, speed.Detail);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
