using HollowWake.Core.Content;
using Xunit;

namespace HollowWake.Core.Tests;

public class CastVisualContentTests
{
    [Theory]
    [InlineData("\"max_effects\":0", "budget")]
    [InlineData("\"max_layers_per_event\":0", "budget")]
    [InlineData("\"outcomes\":[\"impact\"]", "lifecycle")]
    [InlineData("\"thread_phases\":null", "Thread phases")]
    [InlineData("\"profiles\":[{\"id\":\"thread.custom\"}]", "reserved")]
    [InlineData("\"profiles\":[{\"id\":\"x\",\"phases\":{\"impact\":{\"primitive\":\"not_registered\"}}}]", "primitive")]
    [InlineData("\"thread_phases\":{\"start\":{\"primitive\":\"ring\",\"pieces\":500}}", "pieces")]
    [InlineData("\"profiles\":null", "Profiles")]
    [InlineData("\"profiles\":[null]", "profile")]
    [InlineData("\"profiles\":[{\"id\":\"x\",\"phases\":{\"impact\":{\"segments\":[null]}}}]", "segments")]
    [InlineData("\"profiles\":[{\"id\":\"x\",\"match\":[],\"phases\":{}}]", "fallback")]
    public void Invalid_recipes_report_errors_without_throwing(string fields, string expected)
    {
        var layer = Path.Combine(Path.GetTempPath(), "hollowwake-cast-visuals-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(layer);
        try
        {
            File.WriteAllText(Path.Combine(layer, "visuals.json"), "{\"kind\":\"ui_cast_visuals\",\"entries\":[{\"id\":\"default\"," + fields + "}]}");
            ContentRegistry.Load(new[] { CoreContent.FindContentDir(), layer }, out var report, strict: false);
            Assert.Contains(report.Errors, e => e.Message.Contains(expected, StringComparison.OrdinalIgnoreCase));
        }
        finally { Directory.Delete(layer, true); }
    }
}
