using HollowWake.Client.UI.Logic;
using Xunit;

namespace HollowWake.Client.Tests;

public class MenuAndSheetTests
{
    private static readonly UiPage[] Pages =
    {
        new("inventory", "Inventory", "page_inventory", "bag", 0, "core", ""),
        new("weaves", "Weaves", "page_weaves", "weave", 1, "ribbon", ""),
        new("passives", "Passives", "page_passives", "star", 2, "ribbon", ""),
        new("reliquary", "Reliquary", "page_reliquary", "reliquary", 3, "ribbon", ""),
    };

    [Fact]
    public void TheTrayFoldsThePageState()
    {
        var served = new Dictionary<string, PageStatus>
        {
            ["inventory"] = new(true, null, 0),
            ["weaves"] = new(true, "Not while fighting", 2),
            ["passives"] = new(true, null, 3),
            ["reliquary"] = new(false, null, 0),
        };
        var rows = MenuFold.Fold(Pages, served, _ => 0, id => id == "passives");
        Assert.Equal(new[] { TrayState.Open, TrayState.Sealed, TrayState.Open, TrayState.Hidden }, rows.Select(r => r.State));
        Assert.Equal("Not while fighting", rows[1].Reason);
        Assert.True(rows[2].Showing);
        // The button wears only what can be opened.
        Assert.Equal(3, MenuFold.Badge(rows));
    }

    [Fact]
    public void BeforeThePageStateTheContentListDrivesTheTray()
    {
        var rows = MenuFold.Fold(Pages, null, id => id == "passives" ? 4 : 0, _ => false, p => p.Id == "weaves" ? "K" : "");
        Assert.All(rows, r => Assert.Equal(TrayState.Open, r.State));
        Assert.Equal(4, MenuFold.Badge(rows));
        Assert.Equal("K", rows[1].KeyLabel);
    }

    [Fact]
    public void AContainerPageListedOncePerContainerIsOpenWhenAnyIsInReach()
    {
        var near = MenuFold.Served(new[] { "inventory", "stash" }, new[]
        {
            ("inventory", false, (string?)null, 0),
            ("stash", true, (string?)"Stand at a stash chest.", 0),
            ("stash", false, (string?)null, 0),
        });
        Assert.True(near["stash"].Exists);
        Assert.Null(near["stash"].Sealed);
        var far = MenuFold.Served(new[] { "stash" }, new[]
        {
            ("stash", true, (string?)"Stand at a stash chest.", 0),
            ("stash", true, (string?)"Stand at the account chest.", 2),
        });
        Assert.Equal("Stand at a stash chest.", far["stash"].Sealed);
        Assert.Equal(2, far["stash"].Pips);
        var none = MenuFold.Served(new[] { "stash" }, Array.Empty<(string, bool, string?, int)>());
        Assert.False(none["stash"].Exists);
    }

    [Fact]
    public void BadgesCapAndSignaturesChangeWithTheRows()
    {
        Assert.Equal("", MenuFold.BadgeText(0));
        Assert.Equal("7", MenuFold.BadgeText(7));
        Assert.Equal("9+", MenuFold.BadgeText(12));
        var a = MenuFold.Signature(MenuFold.Fold(Pages, null, _ => 0, _ => false));
        var b = MenuFold.Signature(MenuFold.Fold(Pages, null, id => id == "passives" ? 1 : 0, _ => false));
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void KeyNamesParseAndMapToBrowserCodes()
    {
        var k = KeyName.Parse("Shift+Tab");
        Assert.True(k.Shift);
        Assert.Equal("Tab", k.Key);
        Assert.Equal("Shift+Tab", KeyName.Label("shift+Tab"));
        Assert.Equal("KeyI", KeyName.Parse("i").WebCode());
        Assert.Equal("Digit7", KeyName.Parse("7").WebCode());
        Assert.Equal("F1", KeyName.Parse("F1").WebCode());
        Assert.Equal("Escape", KeyName.Parse("Esc").WebCode());
        Assert.True(KeyName.Parse("").IsEmpty);
    }

    private static ContentNames Names() => new()
    {
        Attributes = new List<AttributeInfo>
        {
            new("might", "Might", "core", "Physical force.", false, null, false, 0),
            new("life.max", "Maximum Life", "resource", "", false, 0, false, 1),
            new("crit.chance", "Critical Chance", "offense", "", true, null, false, 2),
            new("pen.ember", "Ember Penetration", "offense", "", true, null, false, 3),
            new("resist.ember", "Ember Resistance", "defense", "", true, null, false, 4),
            new("resist.ember.max", "Maximum Ember Resistance", "defense", "", true, null, false, 5),
            new("secret", "Secret", "core", "", false, null, true, 6),
        },
        Threads = new List<NamedEntry> { new("ember", "Ember", "Fire.", 0), new("tide", "Tide", "Cold.", 1) },
    };

    [Fact]
    public void TheSheetListsSectionsFromContentWithServerValues()
    {
        var names = Names();
        foreach (var a in names.Attributes)
        {
            names.AttributeById[a.Id] = a;
        }
        var sections = new List<UiSheetSection>
        {
            new("attributes", "Attributes", "attributes", Array.Empty<string>(), new[] { "core" }, Array.Empty<string>(), null, null),
            new("offense", "Offense", "attributes", Array.Empty<string>(), new[] { "offense" }, new[] { "pen." }, null, null),
            new("resists", "Resistances", "threads", Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), "resist.{thread}", "resist.{thread}.max"),
            new("quests", "Quest Log", "quests", Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), null, null),
        };
        var values = new Dictionary<string, double>
        {
            ["might"] = 14, ["secret"] = 1, ["life.max"] = 412.4, ["crit.chance"] = 0.075, ["pen.ember"] = 0.1,
            ["resist.ember"] = 0.25, ["resist.ember.max"] = 0.75,
        };
        var quests = new List<QuestLine> { new("Clear the hedgerow", "active", new[] { "Hobs: 2/5" }) };
        var sheet = SheetModel.Build(sections, names, values, quests);
        Assert.Equal(new[] { "Might" }, sheet[0].Rows.Select(r => r.Label));
        Assert.Equal("14", sheet[0].Rows[0].Value);
        Assert.Equal("Physical force.", sheet[0].Rows[0].Description);
        Assert.Equal(new[] { "Critical Chance" }, sheet[1].Rows.Select(r => r.Label));
        Assert.Equal("7.5%", sheet[1].Rows[0].Value);
        var resist = Assert.Single(sheet[2].Rows);
        Assert.Equal("Ember", resist.Label);
        Assert.Equal("25% (max 75%)", resist.Value);
        Assert.Equal("ember", resist.Thread);
        Assert.Equal("Hobs: 2/5", sheet[3].Rows[0].Description);
    }

    [Fact]
    public void ValuesFormatByTheirDefinitionOnly()
    {
        Assert.Equal("412", SheetModel.Format(412.4, new AttributeInfo("life.max", "", "", "", false, 0, false, 0)));
        Assert.Equal("1.25", SheetModel.Format(1.25, null));
        Assert.Equal("150%", SheetModel.Format(1.5, new AttributeInfo("x", "", "", "", true, null, false, 0)));
    }
}
