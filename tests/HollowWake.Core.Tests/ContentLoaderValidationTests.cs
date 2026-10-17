using HollowWake.Core.Content;
using Xunit;

namespace HollowWake.Core.Tests;

public class ContentLoaderValidationTests
{
    [Theory]
    [InlineData("{\"kind\":42,\"entries\":[]}", "kind")]
    [InlineData("{\"kind\":{},\"entries\":[]}", "kind")]
    [InlineData("{\"kind\":\"rules\",\"entries\":[{\"id\":42}]}", "id")]
    [InlineData("{\"kind\":\"rules\",\"entries\":[{\"id\":[]}]}", "id")]
    [InlineData("{\"kind\":\"rules\",\"entries\":[{\"id\":\"bad\",\"patch\":\"true\"}]}", "patch")]
    [InlineData("{\"kind\":\"rules\",\"entries\":[{\"id\":\"bad\",\"remove\":1}]}", "remove")]
    [InlineData("{\"kind\":\"rules\",\"entries\":{\"bad\":{\"patch\":{}}}}", "patch")]
    [InlineData("{\"kind\":\"rules\",\"entries\":{\"bad\":{\"remove\":[]}}}", "remove")]
    [InlineData("kind: 42\nentries: []\n", "kind")]
    [InlineData("kind: rules\nentries:\n  - id: 42\n", "id")]
    [InlineData("kind: rules\nentries:\n  - id: bad\n    patch: invalid\n", "patch")]
    public void Malformed_metadata_reports_its_origin_and_loading_continues(string document, string field)
    {
        WithLayer(layer =>
        {
            var file = Path.Combine(layer, document.StartsWith('{') ? "a-bad.json" : "a-bad.yaml");
            File.WriteAllText(file, document);
            File.WriteAllText(Path.Combine(layer, "z-good.json"), """
                {"kind":"rules","entries":[{"id":"good","values":{"answer":42}}]}
                """);
            var report = new ValidationReport();

            var raw = ContentLoader.LoadRaw(new[] { layer }, report);

            var issue = Assert.Single(report.Errors);
            Assert.Equal(file, issue.Origin);
            Assert.Contains($"'{field}'", issue.Message);
            Assert.True(raw["rules"].ContainsKey("good"));
            Assert.False(raw["rules"].ContainsKey("bad"));
        });
    }

    [Fact]
    public void A_malformed_entry_does_not_discard_valid_siblings_or_an_existing_definition()
    {
        WithLayer(layer =>
        {
            File.WriteAllText(Path.Combine(layer, "content.json"), """
                {"kind":"rules","entries":[
                  {"id":"keep","values":{"answer":42}},
                  {"id":"keep","remove":"true"},
                  {"id":[],"values":{}},
                  {"id":"after","values":{}}
                ]}
                """);
            var report = new ValidationReport();

            var raw = ContentLoader.LoadRaw(new[] { layer }, report);

            Assert.Equal(2, report.ErrorCount);
            Assert.Equal(42, JsonNum.IntOf(raw["rules"]["keep"].Object["values"]!["answer"], 0));
            Assert.True(raw["rules"].ContainsKey("after"));
        });
    }

    [Fact]
    public void Valid_controls_keep_the_existing_patch_remove_and_default_behavior()
    {
        WithLayer(layer =>
        {
            File.WriteAllText(Path.Combine(layer, "content.json"), """
                {"kind":"rules","entries":[
                  {"id":"keep","values":{"answer":42,"retained":1}},
                  {"id":"gone","values":{}},
                  {"id":"keep","patch":true,"values":{"answer":43}},
                  {"id":"gone","remove":true},
                  {"id":"nulls","patch":null,"remove":null,"values":{}},
                  {"id":"false_flags","patch":false,"remove":false,"values":{}}
                ]}
                """);
            var report = new ValidationReport();

            var raw = ContentLoader.LoadRaw(new[] { layer }, report);

            Assert.Empty(report.Issues);
            Assert.Equal(3, raw["rules"].Count);
            Assert.Equal(43, JsonNum.IntOf(raw["rules"]["keep"].Object["values"]!["answer"], 0));
            Assert.Equal(1, JsonNum.IntOf(raw["rules"]["keep"].Object["values"]!["retained"], 0));
            Assert.False(raw["rules"].ContainsKey("gone"));
            foreach (var definition in raw["rules"].Values)
            {
                Assert.False(definition.Object.ContainsKey("patch"));
                Assert.False(definition.Object.ContainsKey("remove"));
            }
        });
    }

    [Fact]
    public void Strict_loading_rejects_malformed_metadata_through_the_validation_report()
    {
        WithLayer(layer =>
        {
            var file = Path.Combine(layer, "bad.json");
            File.WriteAllText(file, """{"kind":false,"entries":[]}""");

            var exception = Assert.Throws<ContentLoadException>(() =>
                ContentRegistry.Load(new[] { layer }, out _, strict: true));

            Assert.Contains(file, exception.Message);
            Assert.Contains("'kind'", exception.Message);
        });
    }

    private static void WithLayer(Action<string> action)
    {
        var layer = Path.Combine(Path.GetTempPath(), "hollowwake-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(layer);
        try
        {
            action(layer);
        }
        finally
        {
            Directory.Delete(layer, true);
        }
    }
}
