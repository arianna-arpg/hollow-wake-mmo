using HollowWake.Client.UI.Logic;
using Xunit;

namespace HollowWake.Client.Tests;

public class BookTests
{
    private const int Page = 0;
    private const int Station = 1;
    private const int Decision = 2;

    private static Leaf World(BookRule book, string id, int rank = Station, Ask ask = Ask.Auto) =>
        book.Bind(id, LeafGroup.World, id, rank, id, ask);

    private static Leaf PageLeaf(BookRule book, string id, Ask ask = Ask.Auto) =>
        book.Bind(id, LeafGroup.Page, id, Page, id, ask);

    [Fact]
    public void TheFirstLeafOpenedHoldsTheFront()
    {
        var book = new BookRule();
        World(book, "dialogue");
        var vendor = World(book, "vendor");
        Assert.Equal("dialogue", book.Front(LeafGroup.World)?.Id);
        Assert.True(vendor.Fresh);
    }

    [Fact]
    public void AHigherRankTakesTheFront()
    {
        var book = new BookRule();
        PageLeaf(book, "weaves");
        World(book, "station");
        Assert.Equal("station", book.Front(LeafGroup.World)?.Id);
        var choice = World(book, "choice", Decision);
        Assert.Equal("choice", book.Front(LeafGroup.World)?.Id);
        Assert.False(choice.Fresh);
    }

    [Fact]
    public void ALowerRankArrivesBehind()
    {
        var book = new BookRule();
        World(book, "vendor");
        var page = PageLeaf(book, "passives");
        Assert.Equal("vendor", book.Front(LeafGroup.Page)?.Id);
        Assert.True(page.Fresh);
    }

    [Fact]
    public void AnExplicitAskIsAbsolute()
    {
        var book = new BookRule();
        World(book, "vendor");
        PageLeaf(book, "weaves", Ask.Front);
        Assert.Equal("weaves", book.Front(LeafGroup.World)?.Id);
        World(book, "choice", Decision, Ask.Behind);
        Assert.Equal("weaves", book.Front(LeafGroup.World)?.Id);
    }

    [Fact]
    public void ClosingTheFrontPromotesTheLeafLastLookedAt()
    {
        var book = new BookRule();
        World(book, "a");
        World(book, "b");
        World(book, "c");
        book.Select("c");
        book.Select("b");
        book.Close("b");
        Assert.Equal("c", book.Front(LeafGroup.World)?.Id);
    }

    [Fact]
    public void WithNothingLookedAtTheFirstTabIsPromoted()
    {
        var book = new BookRule();
        World(book, "a");
        World(book, "b");
        World(book, "c");
        book.Close("a");
        Assert.Equal("b", book.Front(LeafGroup.World)?.Id);
    }

    [Fact]
    public void TabOrderIsBindingOrderAndNeverReshuffles()
    {
        var book = new BookRule();
        World(book, "a");
        World(book, "b");
        World(book, "c");
        book.Select("c");
        book.Select("a");
        Assert.Equal(new[] { "a", "b", "c" }, book.Leaves.Select(l => l.Id));
    }

    [Fact]
    public void LookingAtAFreshTabRestsIt()
    {
        var book = new BookRule();
        World(book, "a");
        var b = World(book, "b");
        Assert.True(b.Fresh);
        book.Select("b");
        Assert.False(b.Fresh);
    }

    [Fact]
    public void SplitSidesEachKeepTheirOwnFront()
    {
        var book = new BookRule { Merged = false };
        PageLeaf(book, "weaves");
        World(book, "vendor");
        Assert.Equal("weaves", book.Front(LeafGroup.Page)?.Id);
        Assert.Equal("vendor", book.Front(LeafGroup.World)?.Id);
        Assert.Equal(2, book.Drawn().Count);
        // A page bound while split arrives on its own side and fronts there only.
        var passives = PageLeaf(book, "passives");
        Assert.True(passives.Fresh);
        Assert.Equal("weaves", book.Front(LeafGroup.Page)?.Id);
    }

    [Fact]
    public void MergingShowsOneFront()
    {
        var book = new BookRule { Merged = false };
        PageLeaf(book, "weaves");
        World(book, "vendor");
        book.Merged = true;
        var drawn = Assert.Single(book.Drawn());
        Assert.Equal("vendor", drawn.Id);
    }

    [Fact]
    public void TheWalkTurnsTheActiveBookInTabOrder()
    {
        var book = new BookRule();
        World(book, "a");
        World(book, "b");
        World(book, "c");
        Assert.True(book.CanWalk);
        Assert.Equal("b", book.Walk(1)?.Id);
        Assert.Equal("c", book.Walk(1)?.Id);
        Assert.Equal("a", book.Walk(1)?.Id);
        Assert.Equal("c", book.Walk(-1)?.Id);
    }

    [Fact]
    public void ABookOfOneDoesNotWalk()
    {
        var book = new BookRule();
        World(book, "a");
        Assert.False(book.CanWalk);
        Assert.Null(book.Walk(1));
    }

    [Fact]
    public void RebindingUpdatesWithoutMovingTheFront()
    {
        var book = new BookRule();
        World(book, "a");
        World(book, "b");
        var again = book.Bind("b", LeafGroup.World, "b", Station, "Renamed", Ask.Auto, source: 7, reach: 4);
        Assert.Equal("a", book.Front(LeafGroup.World)?.Id);
        Assert.Equal("Renamed", again.Title);
        Assert.Equal(7, again.Source);
        book.Bind("b", LeafGroup.World, "b", Station, "Renamed", Ask.Front);
        Assert.Equal("b", book.Front(LeafGroup.World)?.Id);
    }
}
