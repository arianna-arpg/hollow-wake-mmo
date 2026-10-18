using System.Text.Json;
using HollowWake.Client.World.Logic;
using HollowWake.Core.Content;
using Xunit;

namespace HollowWake.Client.Tests;

public class KinAppearanceTests
{
    [Fact]
    public void NativeAndBrowserBuildTheSameReviewedPlansIncludingGear()
    {
        var root = Path.GetFullPath(Path.Combine(ThemeContentTests.FindContentDir(), "../.."));
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "tests/fixtures/gnoll-beastkin-appearance.json")));
        var catalog = new AppearanceCatalog(ContentRegistry.Load(new[] { ThemeContentTests.FindContentDir() }, out _));
        foreach (var row in fixture.RootElement.EnumerateArray())
        {
            var id = row.GetProperty("id").GetString()!;
            var key = row.GetProperty("key").GetInt64();
            var def = catalog.ById(id)!;
            var p = BodyFamilies.Build(def, key, row.GetProperty("height").GetSingle(), row.GetProperty("radius").GetSingle());
            foreach (var (slot, item) in catalog.DefaultGear(def, id))
            {
                var gear = catalog.GearFor(slot, item.Class, item.Tags);
                Assert.NotNull(gear);
                var before = p.Parts.Count;
                GearFamilies.Wear(p, gear!, slot, key);
                Assert.True(p.Parts.Count > before, id + ":" + slot + " did not attach");
            }
            var actual = JsonSerializer.SerializeToElement(new {
                family = p.Family, rig = p.Rig, height = p.Height, width = p.Width, hover = p.Hover, swings = p.Swings,
                bones = p.Bones.Select(b => new { name = b.Name, parent = b.Parent, pivot = V(b.Pivot), rest = V(b.Rest) }),
                parts = p.Parts.Select(a => new { bone = a.Bone, shape = a.Shape.ToString().ToLowerInvariant(), size = V(a.Size), at = V(a.At),
                    turn = V(a.Turn), color = new[] { a.Color.R, a.Color.G, a.Color.B }, taper = a.Taper, glow = a.Glow, segments = a.Segments }),
                sockets = p.Sockets.ToDictionary(s => s.Key, s => new { bone = s.Value.Bone, at = V(s.Value.At), turn = V(s.Value.Turn), size = s.Value.Size })
            });
            Same(actual, row.GetProperty("plan"), id);
        }
    }

    private static float[] V(Vec3 p) => new[] { p.X, p.Y, p.Z };

    private static void Same(JsonElement actual, JsonElement expected, string path)
    {
        Assert.Equal(expected.ValueKind, actual.ValueKind);
        switch (expected.ValueKind)
        {
            case JsonValueKind.Number:
                Assert.True(Math.Abs(actual.GetDouble() - expected.GetDouble()) < 0.0001, path + " differs");
                break;
            case JsonValueKind.Object:
                Assert.Equal(expected.EnumerateObject().Select(p => p.Name), actual.EnumerateObject().Select(p => p.Name));
                foreach (var property in expected.EnumerateObject()) Same(actual.GetProperty(property.Name), property.Value, path + "." + property.Name);
                break;
            case JsonValueKind.Array:
                Assert.Equal(expected.GetArrayLength(), actual.GetArrayLength());
                for (var i = 0; i < expected.GetArrayLength(); i++) Same(actual[i], expected[i], path + "[" + i + "]");
                break;
            default:
                Assert.Equal(expected.ToString(), actual.ToString());
                break;
        }
    }
}
