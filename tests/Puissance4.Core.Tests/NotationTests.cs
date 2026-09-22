using Puissance4.Core;

namespace Puissance4.Core.Tests;

public class NotationTests
{
    private static readonly List<Move> AllColumns = MoveGenerator.Generate(new Board());

    [Theory]
    [InlineData("4", 3)]
    [InlineData(" 1 ", 0)]
    [InlineData("7", 6)]
    [InlineData("d", 3)]
    [InlineData("G", 6)]
    public void TryParse_ReadsAColumn(string input, int expected)
    {
        Assert.True(Notation.TryParse(input, AllColumns, out Move? move, out string? error));

        Assert.Equal(expected, move?.Column);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("8")]
    [InlineData("0")]
    [InlineData("z")]
    [InlineData("44")]
    public void TryParse_RefusesWhatIsNotAColumn(string input)
    {
        Assert.False(Notation.TryParse(input, AllColumns, out Move? move, out string? error));

        Assert.Null(move);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryParse_SaysSoWhenTheColumnIsFull()
    {
        Board board = Board.FromMoves("111111");
        List<Move> legal = MoveGenerator.Generate(board);

        Assert.False(Notation.TryParse("1", legal, out _, out string? error));

        Assert.Equal("La colonne 1 est pleine.", error);
    }

    [Fact]
    public void Describe_PointsOutTheWinningAndTheSavingColumn()
    {
        Board board = Board.FromRows(
            ".......",
            ".......",
            ".......",
            ".......",
            "..JJJ..",
            "..RRR..");

        Assert.Equal("colonne 2 (alignement gagnant)", Notation.Describe(board, Player.Red, new Move(1)));
        Assert.Equal("colonne 4", Notation.Describe(board, Player.Red, new Move(3)));

        board.Drop(1, Player.Red); // Les Rouges gagnent la rangée du bas ; reste la rangée du dessus.
        Assert.Equal("colonne 2 (contre)", Notation.Describe(board, Player.Red, new Move(1)));
    }
}
