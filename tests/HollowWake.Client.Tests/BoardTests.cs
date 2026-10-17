using HollowWake.Client.UI.Logic;
using Xunit;

namespace HollowWake.Client.Tests;

public class BoardTests
{
    private static Board Sample() => new(6, 4, new[]
    {
        new BoardItem(1, 0, 0, 1, 1),
        new BoardItem(2, 2, 0, 2, 3),
        new BoardItem(3, 4, 3, 1, 1),
    });

    [Fact]
    public void ItemsCoverTheirFootprints()
    {
        var board = Sample();
        Assert.Equal(2, board.ItemAt(3, 2)?.Uid);
        Assert.Equal(2, board.ItemAt(2, 0)?.Uid);
        Assert.Null(board.ItemAt(4, 0));
        Assert.Null(board.ItemAt(2, 3));
    }

    [Fact]
    public void FootprintsCentreUnderThePointer()
    {
        var board = Sample();
        // A two by two footprint with the pointer in cell (1.4, 2.6) anchors at (0, 2): it spans the pointer.
        var t = board.Target(99, 2, 2, 1.4f, 2.6f);
        Assert.Equal((0, 2), (t.X, t.Y));
        Assert.Equal(DropFit.Fits, t.Fit);
        // A single cell lands in the cell under the pointer.
        var one = board.Target(99, 1, 1, 5.2f, 1.9f);
        Assert.Equal((5, 1), (one.X, one.Y));
    }

    [Fact]
    public void FootprintsArePulledInsideTheBoard()
    {
        var board = Sample();
        var t = board.Target(99, 2, 2, 5.9f, 3.9f);
        Assert.Equal((4, 2), (t.X, t.Y));
    }

    [Fact]
    public void OneOccupantIsASwapTwoAreBlocked()
    {
        var board = Sample();
        var swap = board.Test(99, 1, 0, 2, 1);
        Assert.Equal(DropFit.Swap, swap.Fit);
        Assert.Equal(2, swap.SwapUid);
        var blocked = board.Test(99, 0, 0, 3, 1);
        Assert.Equal(DropFit.Blocked, blocked.Fit);
    }

    [Fact]
    public void TheCarriedItemNeverBlocksItsOwnMove()
    {
        var board = Sample();
        // Item 2 shifted one cell down overlaps only itself.
        Assert.Equal(DropFit.Fits, board.Test(2, 2, 1, 2, 3).Fit);
    }

    [Fact]
    public void MaskedCellsBlock()
    {
        var mask = new bool[6 * 4];
        mask[1 * 6 + 5] = true;
        var board = new Board(6, 4, Array.Empty<BoardItem>(), mask);
        Assert.Equal(DropFit.Blocked, board.Test(0, 4, 1, 2, 1).Fit);
        Assert.Equal(DropFit.Fits, board.Test(0, 3, 1, 2, 1).Fit);
    }

    [Fact]
    public void OutsideTheBoardIsOutside()
    {
        var board = Sample();
        Assert.Equal(DropFit.Outside, board.Target(99, 1, 1, -0.5f, 1f).Fit);
        Assert.Equal(DropFit.Outside, board.Target(99, 1, 1, 6.1f, 1f).Fit);
        Assert.Equal(DropFit.Outside, board.Target(99, 7, 1, 1f, 1f).Fit);
    }

    [Fact]
    public void FirstFitScansRowByRow()
    {
        var board = Sample();
        Assert.Equal((1, 0), board.FirstFit(1, 1));
        Assert.Equal((4, 0), board.FirstFit(2, 2));
        Assert.Null(board.FirstFit(5, 3));
    }

    [Fact]
    public void AListBagFillsRowsAndMasksBeyondCapacity()
    {
        var board = Board.FromList(new long[] { 10, 11, 12 }, capacity: 7, columns: 4);
        Assert.Equal(4, board.Width);
        Assert.Equal(2, board.Height);
        Assert.Equal(12, board.ItemAt(2, 0)?.Uid);
        Assert.False(board.Blocked(2, 1));
        Assert.True(board.Blocked(3, 1));
    }
}
