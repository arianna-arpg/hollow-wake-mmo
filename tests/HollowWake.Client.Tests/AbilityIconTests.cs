using System.Text.Json.Nodes;
using HollowWake.Client.UI.Logic;
using HollowWake.Core.Content;
using HollowWake.Protocol;
using Xunit;

namespace HollowWake.Client.Tests;

public sealed class AbilityIconTests
{
    private static AbilityIcons Catalog(params (string Kind, string Json)[] extra)
    {
        var raw = ContentLoader.LoadRaw(new[] { ThemeContentTests.FindContentDir() }, new ValidationReport());
        return AbilityIcons.Load(kind => (raw.TryGetValue(kind, out var entries) ? entries.Values.Select(r => r.Object) : Enumerable.Empty<JsonObject>())
            .Concat(extra.Where(e => e.Kind == kind).Select(e => JsonNode.Parse(e.Json)!.AsObject())));
    }

    [Fact]
    public void Imported_catalog_resolves_every_face_to_nonempty_vector_art()
    {
        var icons = Catalog();
        foreach (var (id, _) in icons.Art.Where(e => e.Key.StartsWith("weaves.") || e.Key.StartsWith("glyphs.")))
        {
            var split = id.IndexOf('.');
            var face = icons.Resolve(id[..split], id[(split + 1)..]);
            Assert.NotEmpty(icons.Art[face.Art].Layers);
            Assert.DoesNotContain("<text", icons.Svg(face));
            Assert.True(Rgba.TryParse(face.Color, out _));
        }
    }

    [Fact]
    public void Memory_and_learned_seat_share_identity_including_preset_and_plain_form_collision()
    {
        var icons = Catalog(("forms", "{\"id\":\"ember_bolt\",\"meta\":{\"ability_icon\":\"guard\"}}"));
        var memory = new ItemInfo(1, "memory", "Renamed memory", "common", 1, 1, null, new(), new())
            { Form = new ItemFormInfo("ember_bolt", "Renamed", 1, 2) { Weave = "ember_bolt" } };
        Assert.Equal(icons.Item(memory), icons.Seat("ember_bolt", "bolt", "ember_bolt", null));
        Assert.Equal("skill:firebolt", icons.Item(memory)!.Art);
        var plain = memory with { Form = memory.Form! with { Weave = null } };
        Assert.Equal("guard", icons.Item(plain)!.Art);
        Assert.Equal(icons.Item(plain), icons.Seat("ember_bolt[]+", "ember_bolt", null, null));
    }

    [Fact]
    public void Converted_and_legacy_faces_never_inherit_the_original_picture()
    {
        var icons = Catalog();
        var converted = new FaceInfo("Arbitrary translated label") { Weave = "tide_bolt", Form = "bolt" };
        Assert.Equal(icons.Resolve("weaves", "tide_bolt"), icons.Seat("ember_bolt", "bolt", "ember_bolt", converted));
        Assert.Equal("recall", icons.Seat("ember_bolt", "bolt", null, new FaceInfo("Recall") { Icon = "recall" }).Art);
        Assert.Equal("arcane", icons.Seat("ember_bolt", "bolt", null, new FaceInfo("Unknown")).Art);
    }

    [Fact]
    public void Future_content_uses_meta_or_semantics_not_name_keywords_and_supports_keep_badges()
    {
        var icons = Catalog(("forms", "{\"id\":\"new_form\",\"name\":\"Not a picture key\",\"tags\":[\"melee\"]}"),
            ("weaves", "{\"id\":\"new_weave\",\"form\":\"new_form\",\"color\":\"#123456\"}"),
            ("glyphs", "{\"id\":\"new_support\",\"color\":\"#abcdef\",\"meta\":{\"ability_icon\":\"recall\"}}"));
        Assert.Equal(new AbilityFace("sweep", "#123456", null), icons.Resolve("weaves", "new_weave"));
        Assert.Equal(new AbilityFace("recall", "#abcdef", "badge.support"), icons.Resolve("glyphs", "new_support"));
        Assert.Equal("arcane", icons.Resolve("forms", "misleading_fireball_name").Art);
        var support = new ItemInfo(1, "stone", "Name", "common", 1, 1, null, new(), new()) { Glyph = new ItemGlyphInfo("new_support", "Name", 1) };
        Assert.Equal(icons.Resolve("glyphs", "new_support"), icons.Item(support));
    }

    [Fact]
    public void Malformed_art_and_reference_cycles_are_content_errors()
    {
        var raw = ContentLoader.LoadRaw(new[] { ThemeContentTests.FindContentDir() }, new ValidationReport());
        var entries = raw["ui_ability_icons"];
        foreach (var json in new[] { "{\"id\":\"cycle_a\",\"art\":\"cycle_b\"}", "{\"id\":\"cycle_b\",\"art\":\"cycle_a\"}",
            "{\"id\":\"bad_art\",\"color\":\"red\",\"layers\":[{\"path\":\"M0 0\",\"fill\":\"url\",\"width\":-1,\"transform\":[1,2]}]}" })
        {
            var node = JsonNode.Parse(json)!.AsObject();
            var id = node["id"]!.GetValue<string>();
            entries[id] = new RawDefinition("ui_ability_icons", id, node, "icon-test");
        }
        var report = new ValidationReport();
        ContentRegistry.FromRaw(raw, report);
        Assert.Contains(report.Errors, e => e.Id == "cycle_a" && e.Message.Contains("cycle"));
        Assert.Contains(report.Errors, e => e.Id == "bad_art" && e.Message.Contains("colour"));
        Assert.Contains(report.Errors, e => e.Id == "bad_art" && e.Message.Contains("paint"));
        Assert.Contains(report.Errors, e => e.Id == "bad_art" && e.Message.Contains("width"));
        Assert.Contains(report.Errors, e => e.Id == "bad_art" && e.Message.Contains("matrix"));
    }

    [Fact]
    public void Svg_preserves_layers_matrices_and_escapes_content()
    {
        var icons = Catalog(("ui_ability_icons", "{\"id\":\"unsafe\",\"layers\":[{\"path\":\"M0 0\\\"/><script>bad</script>\",\"stroke\":\"ink\",\"transform\":[1,0,0,1,2,3]}]}"));
        var svg = icons.Svg(new AbilityFace("unsafe", "#abc", null));
        Assert.Contains("matrix(1 0 0 1 2 3)", svg);
        Assert.DoesNotContain("<script>", svg);
        Assert.Contains("&lt;script&gt;", svg);
        Assert.Contains("stroke-width=\"2.15\"", svg);
    }
}
