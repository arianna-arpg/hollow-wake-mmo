using HollowWake.Core.Content;
using Xunit;

namespace HollowWake.Core.Tests;

public class CastVisualContentTests
{
    [Theory]
    [InlineData("\"max_effects\":0", "budget")]
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
