using System.Text.Json.Nodes;
using HollowWake.Client.UI.Logic;
using HollowWake.Core.Content;
using Xunit;

namespace HollowWake.Client.Tests;

/// <summary>The shipped interface content, read the way the client reads it: raw entries into the client's model.</summary>
public class ThemeContentTests
{
    private static readonly Lazy<IReadOnlyDictionary<string, Dictionary<string, RawDefinition>>> Raw = new(() =>
    {
        var report = new ValidationReport();
        return ContentLoader.LoadRaw(new[] { FindContentDir() }, report);
    });

    private static IEnumerable<JsonObject> RawOf(string kind) =>
        Raw.Value.TryGetValue(kind, out var entries) ? entries.Values.Select(r => r.Object) : Enumerable.Empty<JsonObject>();

    [Fact]
    public void Ember_display_name_and_modifier_scope_keep_the_persisted_key()
    {
        var names = ContentNames.Load(RawOf);
        Assert.Equal("Ember", names.ThreadName("ember"));
        var modifier = JsonNode.Parse("{\"attribute\":\"damage\",\"form\":\"increased\",\"value\":0.1,\"scope\":[\"ember\"]}")!.AsObject();
        Assert.Contains("(Ember)", ModifierText.Line(modifier, names));
    }

    public static string FindContentDir()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, "content", "core");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }
        throw new DirectoryNotFoundException("content/core not found above " + AppContext.BaseDirectory);
    }

    [Fact]
    public void TheShippedContentValidatesClean()
    {
        ContentRegistry.Load(new[] { FindContentDir() }, out var report);
        var ours = report.Issues.Where(i => i.Kind.StartsWith("ui_", StringComparison.Ordinal)).ToList();
        Assert.True(ours.Count == 0, string.Join("\n", ours));
    }

    [Fact]
    public void TheDefaultThemeLoadsWithTheLayoutPagesAndSheet()
    {
        var ui = UiContent.Load(RawOf);
        Assert.True(ui.Theme.Default);
        Assert.Equal("etched_stone", ui.Theme.Id);
        Assert.Equal(2, ui.Layout.Orbs.Count);
        Assert.Equal("inventory", ui.CorePage?.Id);
        Assert.Contains(ui.Pages, p => p.Id == "weaves" && p.OnRibbon);
        Assert.NotEmpty(ui.Sheet);
        Assert.True(ui.Layout.RankOf("vendor") > ui.Layout.RankOf("page"));
        Assert.True(ui.Layout.RankOf("choice") > ui.Layout.RankOf("vendor"));
    }

    [Fact]
    public void EveryThemeColoursEveryPoolTheClusterDraws()
    {
        var ui = UiContent.Load(RawOf);
        var pools = ui.Layout.Orbs.SelectMany(o => new[] { o.Fill }.Concat(o.Overlays).Concat(o.Rim).Concat(o.Rings)).ToList();
        foreach (var theme in ui.Themes)
        {
            foreach (var pool in pools)
            {
                Assert.True(theme.Resources.ContainsKey(pool), $"{theme.Id} has no swatch for {pool}");
            }
            foreach (var surface in new[] { "pane", "edge", "edge_dark", "ink", "ink_muted", "label", "accent", "well", "inset" })
            {
                Assert.True(theme.Surface.ContainsKey(surface), $"{theme.Id} has no {surface} surface colour");
            }
        }
    }

    [Fact]
    public void AThemeCanBePickedById()
    {
        var ui = UiContent.Load(RawOf, "runic_brass");
        Assert.Equal("runic_brass", ui.Theme.Id);
        Assert.Equal("brass", ui.Theme.Frame);
        Assert.Equal(2, ui.Themes.Count);
    }

    [Fact]
    public void EveryPageIconAndPoolGlyphIsDrawable()
    {
        var ui = UiContent.Load(RawOf);
        foreach (var page in ui.Pages)
        {
            Assert.True(ui.Icons.Icons.ContainsKey(page.Icon), $"page {page.Id} icon {page.Icon}");
        }
        foreach (var pool in new[] { "life", "mana", "stamina", "ward", "shield", "poise", "insight" })
        {
            Assert.NotEqual(ui.Icons.Fallback, ui.Icons.ForPool(pool));
        }
        var svg = ui.Icons.Svg("bag", 48);
        Assert.StartsWith("<svg", svg, StringComparison.Ordinal);
        Assert.Contains("stroke=\"#ffffff\"", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryStatusInContentHasAGlyph()
    {
        var ui = UiContent.Load(RawOf);
        var names = ContentNames.Load(RawOf);
        Assert.NotEmpty(names.Statuses);
        // A status the rows show wears a glyph: its meta names one, or the icon set maps it.
        foreach (var status in names.Statuses.Keys.Where(s => !names.LookOf(s).Hidden))
        {
            var look = names.LookOf(status);
            Assert.True((look.Icon is { } icon && ui.Icons.Icons.ContainsKey(icon)) || ui.Icons.Statuses.ContainsKey(status), $"status {status} has no glyph");
        }
    }

    [Fact]
    public void StatusesArePresentedFromTheirMetaBlock()
    {
        var names = ContentNames.Load(RawOf);
        // The pool states the orbs draw are hidden from the rows by their content.
        Assert.True(names.LookOf("poise_rearming").Hidden);
        Assert.False(names.LookOf("burning").Hidden);
        // A status's own colour stands in when its meta names none.
        Assert.True(names.LookOf("burning").Color is { } c && Math.Abs(c.R - 0xe8 / 255f) < 0.01f);
        Assert.Equal(StatusLook.Plain, names.LookOf("no_such_status"));
        // The frame follows the status's beneficial flag unless meta names a kind.
        Assert.Equal("debuff", names.LookOf("burning").Kind);
        Assert.Contains(names.StatusLooks.Values, l => l.Kind == "buff");
        Assert.Equal("neutral", StatusLook.From(new JsonObject { ["id"] = "y", ["beneficial"] = true, ["meta"] = new JsonObject { ["kind"] = "neutral" } }).Kind);
        var look = StatusLook.From(new JsonObject { ["id"] = "x", ["meta"] = new JsonObject { ["icon"] = "flame", ["color"] = "#112233", ["kind"] = "debuff", ["cue"] = "pulse" } });
        Assert.Equal("flame", look.Icon);
        Assert.Equal("debuff", look.Kind);
        Assert.Equal("pulse", look.Cue);
        Assert.True(look.Color is { } m && Math.Abs(m.B - 0x33 / 255f) < 0.01f);
    }

    [Fact]
    public void WeavesFindTheirGlyphByLookOrKey()
    {
        var ui = UiContent.Load(RawOf);
        Assert.Equal("missile", ui.Icons.ForWeave(null, "ember_bolt"));
        Assert.Equal("blade", ui.Icons.ForWeave("strike", null));
        Assert.Equal(ui.Icons.Fallback, ui.Icons.ForWeave("#e8622a", "unheard_of"));
    }

    [Fact]
    public void TheRestOfTheContentNamesWhatTheInterfaceLabels()
    {
        var names = ContentNames.Load(RawOf);
        Assert.Equal("Might", names.AttributeName("might"));
        Assert.Contains(names.Threads, t => t.Id == "ember");
        Assert.Contains(names.Slots, s => s.Id == "main_hand");
        Assert.NotEmpty(names.Passives);
        // A generated choice group deals one option per visible attribute of its category.
        var generated = names.Choices["attributes"].Options;
        Assert.Contains(generated, o => o.Id == "might" && o.Lines.Single() == "+4 Might");
        var notable = names.Passives.First(p => p.Kind == "notable" && p.Choice is null);
        Assert.NotEmpty(notable.Lines);
        Assert.Equal("Main Hand", ContentNames.Humanize("main_hand"));
    }

    [Fact]
    public void ColoursParseAsContentWritesThem()
    {
        Assert.True(Rgba.TryParse("#fff", out var white));
        Assert.Equal(1f, white.R);
        Assert.True(Rgba.TryParse("#c0262e", out var red));
        Assert.Equal(0xc0 / 255f, red.R, 3);
        Assert.True(Rgba.TryParse("#00000080", out var half));
        Assert.Equal(0x80 / 255f, half.A, 3);
        Assert.False(Rgba.TryParse("red", out _));
        Assert.Equal("#c0262e", red.Hex());
    }
}
