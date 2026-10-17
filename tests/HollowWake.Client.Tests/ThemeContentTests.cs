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
        foreach (var status in names.Statuses.Keys)
        {
            Assert.True(ui.Icons.Statuses.ContainsKey(status), $"status {status} has no glyph");
        }
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
        var notable = names.Passives.First(p => p.Kind == "notable");
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
