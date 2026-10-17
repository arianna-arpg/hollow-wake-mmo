using HollowWake.Client.UI.Logic;
using Xunit;

namespace HollowWake.Client.Tests;

public class WorkspaceTests
{
    private static readonly UiPage[] Pages =
    {
        new("inventory", "Inventory", "I", "bag", 0, "core", ""),
        new("weaves", "Weaves", "K", "weave", 1, "ribbon", ""),
        new("passives", "Passives", "P", "star", 2, "ribbon", ""),
        new("character", "Character", "C", "figure", 4, "popout", ""),
    };

    private static Workspace Make()
    {
        var layout = new UiLayout
        {
            Ranks = new List<string> { "page", "station", "decision" },
            Panes = new Dictionary<string, string> { ["vendor"] = "station", ["dialogue"] = "station", ["choice"] = "decision" },
        };
        return new Workspace(id => Pages.FirstOrDefault(p => p.Id == id), layout.RankOf);
    }

    [Fact]
    public void RequestingAPageOpensTheCorePaneAndSelectsIt()
    {
        var w = Make();
        w.RequestPage("weaves", Via.Key);
        Assert.True(w.CoreOpen);
        Assert.Equal("weaves", w.SelectedPage);
        w.RequestPage("passives", Via.Menu);
        Assert.Equal("passives", w.SelectedPage);
        Assert.True(w.Book.Has("weaves"));
    }

    [Fact]
    public void RepeatingThePagesKeyClosesTheCorePane()
    {
        var w = Make();
        w.RequestPage("weaves", Via.Key);
        w.RequestPage("weaves", Via.Key);
        Assert.False(w.CoreOpen);
        Assert.Empty(w.Book.Leaves);
    }

    [Fact]
    public void TheSelectedPagesRibbonDismissesOnlyThatPage()
    {
        var w = Make();
        w.RequestPage("weaves", Via.Key);
        w.RequestPage("passives", Via.Key);
        w.RequestPage("passives", Via.Ribbon);
        Assert.True(w.CoreOpen);
        Assert.False(w.Book.Has("passives"));
        Assert.Equal("weaves", w.SelectedPage);
    }

    [Fact]
    public void ARibbonOnAPageBehindSelectsIt()
    {
        var w = Make();
        w.RequestPage("weaves", Via.Key);
        w.RequestPage("passives", Via.Key);
        w.RequestPage("weaves", Via.Ribbon);
        Assert.Equal("weaves", w.SelectedPage);
        Assert.True(w.Book.Has("passives"));
    }

    [Fact]
    public void TheCoreKeyTogglesTheCorePane()
    {
        var w = Make();
        w.RequestPage("inventory", Via.Key);
        Assert.True(w.CoreOpen);
        w.RequestPage("inventory", Via.Key);
        Assert.False(w.CoreOpen);
    }

    [Fact]
    public void ClosingTheCorePaneRemembersItsPagesAndSelection()
    {
        var w = Make();
        w.RequestPage("weaves", Via.Key);
        w.RequestPage("passives", Via.Key);
        w.RequestPage("weaves", Via.Ribbon);
        w.RequestPage("inventory", Via.Key);
        Assert.False(w.CoreOpen);
        w.RequestPage("inventory", Via.Key);
        Assert.True(w.CoreOpen);
        Assert.True(w.Book.Has("weaves"));
        Assert.True(w.Book.Has("passives"));
        Assert.Equal("weaves", w.SelectedPage);
    }

    [Fact]
    public void AWorldPaneOpensTheCorePaneBesideIt()
    {
        var w = Make();
        w.OpenWorld("vendor:5", "vendor", "Vendor", source: 5);
        Assert.True(w.CoreOpen);
        Assert.Equal("vendor:5", w.Book.Front(LeafGroup.World)?.Id);
    }

    [Fact]
    public void AStationFrontsOverADevelopmentPage()
    {
        var w = Make();
        w.RequestPage("weaves", Via.Key);
        w.OpenWorld("vendor:5", "vendor", "Vendor", source: 5);
        Assert.Equal("vendor:5", w.Book.Front(LeafGroup.Page)?.Id);
        // The page is one tab away and promotes back when the vendor closes.
        w.CloseWorld("vendor:5");
        Assert.Equal("weaves", w.Book.Front(LeafGroup.Page)?.Id);
    }

    [Fact]
    public void EscapeFoldsTheTrayThenClosesTheFrontPaneThenTheCorePane()
    {
        var w = Make();
        w.RequestPage("weaves", Via.Key);
        w.OpenWorld("dialogue:3", "dialogue", "Talk", source: 3);
        w.TrayOpen = true;
        Assert.Equal(EscapeStep.Tray, w.Escape());
        Assert.Equal(EscapeStep.Pane, w.Escape());
        Assert.False(w.HasWorld("dialogue:3"));
        Assert.Equal(EscapeStep.Pane, w.Escape());
        Assert.False(w.Book.Has("weaves"));
        Assert.Equal(EscapeStep.Core, w.Escape());
        Assert.False(w.CoreOpen);
        Assert.Equal(EscapeStep.Nothing, w.Escape());
    }

    [Fact]
    public void ClosingTheCorePaneClosesTheWorldsPanes()
    {
        var w = Make();
        var closed = new List<string>();
        w.WorldClosed += leaf => closed.Add(leaf.Id);
        w.OpenWorld("vendor:5", "vendor", "Vendor", source: 5);
        w.CloseCore();
        Assert.Equal(new[] { "vendor:5" }, closed);
        Assert.Empty(w.Book.Leaves);
    }

    [Fact]
    public void WalkingOutOfReachClosesAWorldPane()
    {
        var w = Make();
        var closed = new List<string>();
        w.WorldClosed += leaf => closed.Add(leaf.Id);
        w.OpenWorld("vendor:5", "vendor", "Vendor", source: 5, reach: 4);
        w.OpenWorld("dialogue:6", "dialogue", "Talk", source: 6);
        var distances = new Dictionary<long, float> { [5] = 3.5f, [6] = 7f };
        w.Depart(k => distances.TryGetValue(k, out var d) ? d : null, defaultReach: 6);
        Assert.Equal(new[] { "dialogue:6" }, closed);
        distances[5] = 4.5f;
        w.Depart(k => distances.TryGetValue(k, out var d) ? d : null, defaultReach: 6);
        Assert.Equal(new[] { "dialogue:6", "vendor:5" }, closed);
        Assert.True(w.CoreOpen);
    }

    [Fact]
    public void ASourceTheClientNoLongerKnowsHasDeparted()
    {
        var w = Make();
        w.OpenWorld("vendor:5", "vendor", "Vendor", source: 5);
        w.Depart(_ => null, 6);
        Assert.False(w.HasWorld("vendor:5"));
    }
}
