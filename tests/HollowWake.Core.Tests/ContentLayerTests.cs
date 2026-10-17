using System.Text.Json.Nodes;
using HollowWake.Core.Content;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>How files of one layer combine: a patch may come earlier in the layer than the entry it extends.</summary>
public class ContentLayerTests
{
    [Fact]
    public void A_patch_in_an_earlier_file_extends_an_entry_defined_later_in_the_same_layer()
    {
        var layer = Path.Combine(Path.GetTempPath(), "hollowwake-layer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(layer);
        try
        {
            File.WriteAllText(Path.Combine(layer, "a-package.yaml"), "kind: rules\nentries:\n  - id: triggers\n    patch: true\n    values:\n      chance_cap: 0.9\n");
            File.WriteAllText(Path.Combine(layer, "z-core.yaml"), "kind: rules\nentries:\n  - id: triggers\n    values:\n      death_near_radius: 8\n");
            var report = new ValidationReport();
            var raw = ContentLoader.LoadRaw(new[] { layer }, report);
            var values = (JsonObject)raw["rules"]["triggers"].Object["values"]!;
            Assert.Equal(8, JsonNum.IntOf(values["death_near_radius"], 0));
            Assert.Equal(0.9, JsonNum.Of(values["chance_cap"], 0));
            Assert.Empty(report.Warnings);
        }
        finally
        {
            Directory.Delete(layer, true);
        }
    }

    [Fact]
    public void A_patch_with_no_target_in_any_layer_becomes_a_new_entry_with_a_warning()
    {
        var layer = Path.Combine(Path.GetTempPath(), "hollowwake-layer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(layer);
        try
        {
            File.WriteAllText(Path.Combine(layer, "only.yaml"), "kind: rules\nentries:\n  - id: orphan\n    patch: true\n    values:\n      x: 1\n");
            var report = new ValidationReport();
            var raw = ContentLoader.LoadRaw(new[] { layer }, report);
            Assert.True(raw["rules"].ContainsKey("orphan"));
            Assert.Contains(report.Warnings, w => w.Id == "orphan");
        }
        finally
        {
            Directory.Delete(layer, true);
        }
    }
}
