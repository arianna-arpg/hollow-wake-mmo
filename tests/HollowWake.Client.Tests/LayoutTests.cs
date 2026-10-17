using HollowWake.Client.UI.Logic;
using Xunit;

namespace HollowWake.Client.Tests;

public class LayoutTests
{
    private static UiLayout Layout() => new()
    {
        Orbs = new List<UiOrb>
        {
            new("left", "life", new[] { "shield" }, new[] { "ward" }, new[] { "poise", "insight" }),
            new("right", "mana", Array.Empty<string>(), Array.Empty<string>(), new[] { "stamina" }),
        },
        Metrics = new Dictionary<string, double>
        {
            ["orb"] = 124, ["rim"] = 7, ["ring"] = 6, ["ring_gap"] = 3, ["slot"] = 52, ["slot_gap"] = 6, ["slots"] = 8,
            ["bar_pad"] = 10, ["cluster_margin"] = 10, ["cast_bar"] = 14, ["status_icon"] = 28, ["menu_button"] = 46, ["edge"] = 14,
        },
    };

    [Fact]
    public void TheClusterStandsAtTheBottomCentre()
    {
        var c = ClusterLayout.Compute(Layout(), 1600, 900);
        Assert.Equal(1f, c.Scale);
        Assert.Equal(800f, c.Bar.CenterX, 2);
        Assert.Equal(890f, c.Bar.Bottom, 2);
        Assert.True(c.LeftOrb.Right <= c.Bar.X);
        Assert.True(c.RightOrb.X >= c.Bar.Right);
        Assert.True(c.Menu.X >= c.RightOrb.Right);
        Assert.Equal(c.LeftOrb.Bottom, c.RightOrb.Bottom, 2);
        Assert.True(c.Cast.Bottom <= c.Bar.Y);
        Assert.True(c.Status.Bottom <= c.Cast.Y);
        Assert.Equal(8, c.Slots.Count);
    }

    [Fact]
    public void ThePublishedRectangleHoldsEverythingTheClusterDraws()
    {
        var c = ClusterLayout.Compute(Layout(), 1600, 900);
        foreach (var part in new[] { c.LeftOrb, c.RightOrb, c.Bar, c.Cast, c.Status, c.Menu })
        {
            Assert.True(part.X >= c.Rect.X && part.Right <= c.Rect.Right + 0.01f && part.Y >= c.Rect.Y && part.Bottom <= c.Rect.Bottom + 0.01f);
        }
    }

    [Fact]
    public void RingSpaceIsReservedForEveryPoolTheLayoutNames()
    {
        var c = ClusterLayout.Compute(Layout(), 1600, 900);
        // orb radius 62, rim 7, two rings of gap 3 and width 6
        Assert.Equal(62f + 7 + 2 * 9, c.OuterRadius, 2);
        Assert.True(c.RingRadius(1) + c.RingWidth / 2 <= c.OuterRadius + 0.01f);
        Assert.True(c.RingRadius(0) - c.RingWidth / 2 >= c.OrbRadius + c.RimWidth);
    }

    [Fact]
    public void ANarrowScreenShrinksTheCluster()
    {
        var c = ClusterLayout.Compute(Layout(), 800, 600);
        Assert.True(c.Scale < 1f);
        Assert.True(c.Rect.X >= 0 && c.Rect.Right <= 800);
    }

    private static PaneLayout.Input Input(float viewW = 1600, bool pages = true, bool world = false, Seat? seat = null) => new(
        new Box(0, 0, viewW, 900),
        new Box(viewW / 2 - 380, 700, 760, 190),
        seat,
        CoreWidth: 430,
        CoreHeight: 560,
        PageWidth: 460,
        WorldWidth: 420,
        Edge: 14,
        HasPages: pages,
        HasWorld: world,
        Ribbons: 4,
        RibbonHeight: 52,
        BadgeHeight: 14,
        Chips: 3,
        ChipHeight: 40,
        RailTop: 40);

    [Fact]
    public void TheCorePaneSitsRightAboveTheClusterByDefault()
    {
        var r = PaneLayout.Compute(Input());
        Assert.Equal(1600 - 14, r.Core.Right, 2);
        Assert.Equal(700 - 14, r.Core.Bottom, 2);
        Assert.True(r.PagesLeft);
        Assert.True(r.PageBook.Right <= r.Core.X);
        Assert.Equal(r.Core.Y, r.PageBook.Y);
        Assert.Equal(r.Core.H, r.PageBook.H);
    }

    [Fact]
    public void PanesFitAboveTheCluster()
    {
        var input = Input() with { CoreHeight = 2000 };
        var r = PaneLayout.Compute(input);
        Assert.True(r.Core.Bottom <= 700 - 14 + 0.01f);
        Assert.True(r.Core.Y >= 14);
    }

    [Fact]
    public void ARememberedSeatIsKeptAndClamped()
    {
        var r = PaneLayout.Compute(Input(seat: new Seat(0.4f, 0.05f)));
        Assert.Equal(640, r.Core.X, 2);
        Assert.Equal(45, r.Core.Y, 2);
        var off = PaneLayout.Compute(Input(seat: new Seat(1.2f, 0.9f)));
        Assert.True(off.Core.Right <= 1600 - 14 + 0.01f);
        Assert.True(off.Core.Bottom <= 700 - 14 + 0.01f);
    }

    [Fact]
    public void WorldPanesStandAcrossTheCorePaneWhenThereIsRoom()
    {
        var r = PaneLayout.Compute(Input(pages: true, world: true, seat: new Seat(0.36f, 0.1f)));
        Assert.False(r.Merged);
        Assert.False(r.PageBook.Overlaps(r.WorldBook));
        Assert.False(r.Core.Overlaps(r.WorldBook));
    }

    [Fact]
    public void WithoutRoomTheSidesBindIntoOneBook()
    {
        var r = PaneLayout.Compute(Input(pages: true, world: true));
        Assert.True(r.Merged);
        Assert.Equal(r.PageBook, r.WorldBook);
        Assert.True(r.WorldBook.Right <= r.Core.X);
    }

    [Fact]
    public void AWorldPaneAloneTakesTheSideWithRoom()
    {
        var r = PaneLayout.Compute(Input(pages: false, world: true));
        Assert.False(r.Merged);
        Assert.True(r.WorldBook.Right <= r.Core.X);
    }

    [Fact]
    public void BooksClampInsideANarrowScreen()
    {
        var r = PaneLayout.Compute(Input(viewW: 700, pages: true));
        Assert.True(r.PageBook.X >= 14);
        Assert.True(r.Core.X >= 14);
    }

    [Fact]
    public void TheRailDropsBadgesBeforeChips()
    {
        var roomy = PaneLayout.Compute(Input());
        Assert.True(roomy.RailBadges);
        Assert.True(roomy.RailChips);
        // 4 ribbons of 52 + badges 56 + chips 120 = 384; rail room is the height less 40.
        var tight = PaneLayout.Compute(Input() with { CoreHeight = 400 });
        Assert.False(tight.RailBadges);
        Assert.True(tight.RailChips);
        var tighter = PaneLayout.Compute(Input() with { CoreHeight = 300 });
        Assert.False(tighter.RailBadges);
        Assert.False(tighter.RailChips);
    }

    [Fact]
    public void SeatsAreFractionsOfTheViewport()
    {
        var view = new Box(0, 0, 1000, 500);
        var seat = Seat.Of(new Box(250, 100, 10, 10), view);
        Assert.Equal((0.25f, 0.2f), (seat.FX, seat.FY));
        Assert.Equal((500f, 200f), seat.At(new Box(0, 0, 2000, 1000)));
    }
}
