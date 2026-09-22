using Puissance4.Core;

namespace Puissance4.Core.Tests;

public class BoardTests
{
    [Fact]
    public void Drop_StacksTheDiscsFromTheBottomUp()
    {
        var board = new Board();

        int first = board.Drop(3, Player.Red);
        int second = board.Drop(3, Player.Yellow);

        Assert.Equal(Grid.Index(5, 3), first);
        Assert.Equal(Grid.Index(4, 3), second);
        Assert.Equal(2, board.HeightOf(3));
        Assert.Equal(2, board.TotalDiscs);
    }

    [Fact]
    public void Drop_RefusesAFullColumn()
    {
        var board = new Board();
        for (int i = 0; i < Grid.Rows; i++)
        {
            board.Drop(0, Player.Red);
        }

        Assert.False(board.CanPlay(0));
        Assert.Equal(-1, board.LandingIndex(0));
        Assert.Throws<InvalidOperationException>(() => board.Drop(0, Player.Yellow));
    }

    [Fact]
    public void Lift_UndoesADrop()
    {
        var board = new Board();
        board.Drop(2, Player.Red);
        int index = board.Drop(2, Player.Yellow);

        Assert.Equal(index, board.Lift(2));
        Assert.Equal(Cell.Empty, board[index]);
        Assert.Equal(1, board.HeightOf(2));
        Assert.Equal(Cell.Red, board[5, 2]);
    }

    [Fact]
    public void AlignmentThrough_FindsTheFourInARow()
    {
        Board board = Board.FromRows(
            ".......",
            ".......",
            ".......",
            ".......",
            "..JJJ..",
            "..RRRR.");

        IReadOnlyList<int>? line = board.AlignmentThrough(Grid.Index(5, 3));

        Assert.NotNull(line);
        Assert.Equal([Grid.Index(5, 2), Grid.Index(5, 3), Grid.Index(5, 4), Grid.Index(5, 5)], line);
        Assert.Null(board.AlignmentThrough(Grid.Index(4, 3)));
    }

    [Fact]
    public void AlignmentThrough_FindsAColumnAndBothDiagonals()
    {
        Board vertical = Board.FromRows(
            ".......",
            ".......",
            "...J...",
            "...J...",
            "...J...",
            "...J...");

        Board rising = Board.FromRows(
            ".......",
            ".......",
            "...R...",
            "..RJ...",
            ".RJJ...",
            "RJRJ...");

        Board falling = Board.FromRows(
            ".......",
            ".......",
            "...R...",
            "...JR..",
            "...JJR.",
            "...JRJR");

        Assert.Equal(4, vertical.AlignmentThrough(Grid.Index(5, 3))?.Count);
        Assert.Equal(4, rising.AlignmentThrough(Grid.Index(2, 3))?.Count);
        Assert.Equal(4, falling.AlignmentThrough(Grid.Index(2, 3))?.Count);
    }

    [Fact]
    public void AlignmentThrough_KeepsTheFifthDiscOfALongerRow()
    {
        Board board = Board.FromRows(
            ".......",
            ".......",
            ".......",
            ".......",
            "..J.J..",
            ".RRRRR.");

        Assert.Equal(5, board.AlignmentThrough(Grid.Index(5, 3))?.Count);
    }

    [Fact]
    public void FromRows_RefusesAFloatingDisc()
    {
        FormatException error = Assert.Throws<FormatException>(() => Board.FromRows(
            ".......",
            ".......",
            ".......",
            "...R...",
            ".......",
            "...J..."));

        Assert.Contains("flottant", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromMoves_PlaysTheColumnsInTurnStartingWithRed()
    {
        Board board = Board.FromMoves("4453");

        Assert.Equal(Cell.Red, board[5, 3]);
        Assert.Equal(Cell.Yellow, board[4, 3]);
        Assert.Equal(Cell.Red, board[5, 4]);
        Assert.Equal(Cell.Yellow, board[5, 2]);
        Assert.Equal(4, board.TotalDiscs);
    }

    [Fact]
    public void ToRows_RoundTripsThroughFromRows()
    {
        string[] rows =
        [
            ".......",
            ".......",
            ".......",
            "...R...",
            "...J...",
            "..RJRJ.",
        ];

        Assert.Equal(rows, Board.FromRows(rows).ToRows());
    }

    [Fact]
    public void Render_DrawsTheGridWithItsColumnNumbers()
    {
        string render = Board.FromMoves("44").Render();

        Assert.StartsWith("  1   2   3   4   5   6   7", render, StringComparison.Ordinal);
        Assert.Contains("| . | . | . | J | . | . | . |", render, StringComparison.Ordinal);
        Assert.Contains("| . | . | . | R | . | . | . |", render, StringComparison.Ordinal);
    }
}
