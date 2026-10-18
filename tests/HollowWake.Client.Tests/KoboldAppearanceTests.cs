using System.Text.Json;
using HollowWake.Client.World.Logic;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using Xunit;

namespace HollowWake.Client.Tests;

public class KoboldAppearanceTests
{
    [Fact]
    public void NativeAndBrowserBuildTheSameReviewedPlansIncludingGear()
    {
        var root = Path.GetFullPath(Path.Combine(ThemeContentTests.FindContentDir(), "../.."));
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "tests/fixtures/kobolds-appearance.json")));
        var content = ContentRegistry.Load(new[] { ThemeContentTests.FindContentDir() }, out _);
        var catalog = new AppearanceCatalog(content);
        Assert.Equal(content.Each<PatternDef>().Where(p => p.Extends == "kobold_body").Select(p => p.Id).Order(),
            fixture.RootElement.EnumerateArray().Select(p => p.GetProperty("id").GetString()!).Order());
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
                Assert.True(p.Parts.Count > before, id + ":" + slot + " did not attach equipment");
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

    [Theory]
    [InlineData("biped")]
    [InlineData("skeleton")]
    [InlineData("scaled_biped")]
    public void FieldEquipmentAttachesToOtherFamiliesAndFollowsTheCorrectBones(string family)
    {
        var content = ContentRegistry.Load(new[] { ThemeContentTests.FindContentDir() }, out _);
        foreach (var gear in content.Each<AppearanceDef>().Where(a => a.Body == "field_kit"))
        {
            var body = new AppearanceDef { Id = "gear-host", Body = family, Variation = new AppearanceVariation { Scale = 0 } };
            var plan = BodyFamilies.Build(body, 17, 1.8f, 0.4f);
            var before = plan.Parts.Count;
            GearFamilies.Wear(plan, gear, "chest", 17);
            var added = plan.Parts.Skip(before).ToArray();
            Assert.NotEmpty(added);
            Assert.All(added, part => Assert.Contains(part.Bone, new[] { "torso", "hips", "head" }));
            if (gear.Features.Any(f => f is "robe" or "vials" or "drum"))
                Assert.Contains(added, part => part.Bone == "hips");
            if (gear.Features.Contains("crown")) Assert.Contains(added, part => part.Bone == "head");
            var enlarged = BodyFamilies.Build(body, 17, 3.6f, 0.8f);
            var enlargedBefore = enlarged.Parts.Count;
            GearFamilies.Wear(enlarged, gear, "chest", 17);
            Assert.Equal(added.Length, enlarged.Parts.Count - enlargedBefore);
            for (var n = 0; n < added.Length; n++)
                Assert.True((enlarged.Parts[enlargedBefore + n].Size - added[n].Size * 2).Length < 0.0001f);
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
