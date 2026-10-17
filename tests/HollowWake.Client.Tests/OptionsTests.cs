using System.Text.Json.Nodes;
using HollowWake.Client.UI.Logic;
using HollowWake.Core.Content;
using Xunit;

namespace HollowWake.Client.Tests;

public class OptionsTests
{
    private static readonly Lazy<IReadOnlyDictionary<string, Dictionary<string, RawDefinition>>> Raw = new(() =>
        ContentLoader.LoadRaw(new[] { ThemeContentTests.FindContentDir() }, new ValidationReport()));

    private static IEnumerable<JsonObject> RawOf(string kind) =>
        Raw.Value.TryGetValue(kind, out var entries) ? entries.Values.Select(r => r.Object) : Enumerable.Empty<JsonObject>();

    private static OptionsCatalog Catalog() => OptionsCatalog.Load(RawOf);

    [Fact]
    public void TheCatalogListsPagesGroupsAndEntriesInOrder()
    {
        var catalog = Catalog();
        Assert.Equal("interface", catalog.Pages[0].Id);
        Assert.Contains(catalog.Pages, p => p.Id == "graphics");
        var theme = catalog.Entry("theme");
        Assert.NotNull(theme);
        Assert.Equal("choice", theme.Kind);
        // A choice drawn from a kind lists every entry of it: both themes are one choice away.
        Assert.Contains(theme.Choices, c => c.Id == "etched_stone");
        Assert.Contains(theme.Choices, c => c.Id == "runic_brass");
        Assert.Equal("etched_stone", theme.Default?.GetValue<string>());
    }

    [Fact]
    public void BindingGroupsBecomeAKeyEntryPerAction()
    {
        var catalog = Catalog();
        var move = catalog.Entry(OptionsCatalog.KeyEntryId("move_forward"));
        Assert.NotNull(move);
        Assert.Equal("key", move.Kind);
        Assert.Equal("move_forward", move.Action);
        foreach (var action in catalog.Actions)
        {
            Assert.True(catalog.Entry(OptionsCatalog.KeyEntryId(action.Id)) is not null, $"{action.Id} has no key entry");
        }
    }

    [Fact]
    public void SettingsFallBackToDefaultsAndRefuseMalformedValues()
    {
        var settings = new Settings(Catalog());
        Assert.Equal(1, settings.Number("ui_scale"));
        Assert.Equal("etched_stone", settings.Text("theme"));
        Assert.True(settings.Toggle("combat_text"));
        Assert.False(settings.Set("ui_scale", JsonValue.Create(9.0)));
        Assert.False(settings.Set("theme", JsonValue.Create("no_such_theme")));
        Assert.False(settings.Set("combat_text", JsonValue.Create("yes")));
        Assert.True(settings.Set("theme", JsonValue.Create("runic_brass")));
        Assert.Equal("runic_brass", settings.Text("theme"));
        Assert.False(settings.IsDefault("theme"));
    }

    [Fact]
    public void ChangesAreAnnouncedAndResetsReturnToTheDefault()
    {
        var settings = new Settings(Catalog());
        var changed = new List<string>();
        settings.Changed += e => changed.Add(e.Id);
        settings.Set("ui_scale", JsonValue.Create(1.25));
        settings.Set("combat_text", JsonValue.Create(false));
        Assert.Equal(new[] { "ui_scale", "combat_text" }, changed);
        settings.ResetGroup("interface", "look");
        Assert.Equal(1, settings.Number("ui_scale"));
        Assert.False(settings.Toggle("combat_text"));
        settings.ResetPage("interface");
        Assert.True(settings.Toggle("combat_text"));
        Assert.True(settings.IsDefault("combat_text"));
    }

    [Fact]
    public void TheFileRoundTripsAndABrokenOneIsDefaults()
    {
        var catalog = Catalog();
        var settings = new Settings(catalog);
        settings.Set("theme", JsonValue.Create("runic_brass"));
        settings.Rebind("jump", 0, "J", swap: false);
        var again = Settings.Load(catalog, settings.Serialize());
        Assert.Equal("runic_brass", again.Text("theme"));
        Assert.Equal("J", again.Bindings("jump").Primary);
        var broken = Settings.Load(catalog, "{ not json");
        Assert.Equal("etched_stone", broken.Text("theme"));
        var wrong = Settings.Load(catalog, "{\"version\":1,\"values\":{\"ui_scale\":\"huge\",\"theme\":\"runic_brass\"}}");
        Assert.Equal(1, wrong.Number("ui_scale"));
        Assert.Equal("runic_brass", wrong.Text("theme"));
    }

    [Fact]
    public void RebindingFindsConflictsInTheContextAndSwapsOrClears()
    {
        var settings = new Settings(Catalog());
        // E turns the camera right; binding it to interact conflicts within the world context.
        var conflicts = settings.Conflicts("interact", "E").Select(a => a.Id).ToList();
        Assert.Equal(new[] { "camera_rotate_right" }, conflicts);
        settings.Rebind("interact", 0, "E", swap: true);
        Assert.Equal("E", settings.Bindings("interact").Primary);
        Assert.Equal("F", settings.Bindings("camera_rotate_right").Primary);
        settings.Rebind("jump", 0, "F", swap: false);
        Assert.Equal("", settings.Bindings("camera_rotate_right").Primary);
        // Tab is the menu and the book walk, in different contexts: no conflict.
        Assert.Empty(settings.Conflicts("ui_book_next", "Tab"));
    }

    [Fact]
    public void GatesReadTheClientsFactsAndOtherOptions()
    {
        double? Facts(string name) => name switch { "client.desktop" => 1, "client.web" => 0, "option.shadows" => 1, _ => null };
        Assert.True(GateExpr.Holds("client.desktop", Facts));
        Assert.False(GateExpr.Holds("client.web", Facts));
        Assert.True(GateExpr.Holds("client.desktop && !client.web", Facts));
        Assert.True(GateExpr.Holds("option.shadows == 1 || client.web", Facts));
        Assert.True(GateExpr.Holds("has(\"x\")", Facts, f => f == "x"));
        Assert.True(GateExpr.Holds("", Facts));
        Assert.False(GateExpr.Holds("client.developer", Facts));
        Assert.Equal(3, GateExpr.Evaluate("1 + 2 * (3 - 2)", Facts));
        Assert.Equal(5, GateExpr.Evaluate("client.desktop ? 5 : 7", Facts));
        Assert.False(GateExpr.Holds("(1", Facts));
    }

    [Fact]
    public void KeyNamesCompareInOneSpelling()
    {
        Assert.Equal("Shift+Tab", KeyName.Canonical("shift+Tab"));
        Assert.Equal(KeyName.Canonical("Ctrl+Shift+k"), KeyName.Canonical("Shift+Ctrl+K"));
        Assert.Equal("Escape", KeyName.Canonical("Esc"));
        Assert.Equal("Ctrl+Shift+F1", KeyName.Compose("F1", shift: true, ctrl: true, alt: false));
    }
}
