using Puissance4.Core;

namespace Puissance4.Core.Tests;

public class MoveGeneratorTests
{
    [Fact]
    public void Generate_OffersEveryColumn_FromTheCentreOutwards()
    {
        List<Move> moves = MoveGenerator.Generate(new Board());

        Assert.Equal(Grid.Columns, moves.Count);
        Assert.Equal([3, 2, 4, 1, 5, 0, 6], moves.Select(m => m.Column));
    }

    [Fact]
    public void Generate_SkipsAFullColumn()
    {
        Board board = Board.FromMoves("111111"); // Six jetons empilés dans la colonne 1.

        List<Move> moves = MoveGenerator.Generate(board);

        Assert.Equal(Grid.Columns - 1, moves.Count);
        Assert.DoesNotContain(new Move(0), moves);
    }

    [Fact]
    public void WinningMove_FindsTheColumnThatCompletesTheRow()
    {
        Board board = Board.FromRows(
            ".......",
            ".......",
            ".......",
            ".......",
            "..JJJ..",
            "..RRR..");

        Assert.Equal(new Move(1), MoveGenerator.WinningMove(board, Player.Red));

        // Les Jaunes ont le même trio, une rangée plus haut, mais les deux cases qui le
        // complètent sont en l'air : leur alignement ne se joue pas encore.
        Assert.Null(MoveGenerator.WinningMove(board, Player.Yellow));
    }

    [Fact]
    public void WinningMove_LeavesTheBoardUntouched()
    {
        Board board = Board.FromMoves("4455");
        string[] before = board.ToRows();

        MoveGenerator.WinningMove(board, Player.Red);

        Assert.Equal(before, board.ToRows());
        Assert.Equal(4, board.TotalDiscs);
    }

    [Fact]
    public void WinningMove_IgnoresAnAlignmentThatWouldNeedAFloatingDisc()
    {
        // Les Rouges tiennent trois cases de la rangée du milieu, mais la quatrième
        // est en l'air : rien ne la soutient encore.
        Board board = Board.FromRows(
            ".......",
            ".......",
            ".......",
            "..RRR..",
            "..JJJ..",
            "..RJR..");

        Assert.Null(MoveGenerator.WinningMove(board, Player.Red));
    }

    [Fact]
    public void LosingMoves_NamesTheColumnThatUncoversTheOpponentsWin()
    {
        // Les Jaunes tiennent les colonnes 3, 4 et 5 de l'avant-dernière rangée. En jouant
        // la colonne 2, les Rouges posent leur jeton juste sous la case qui les complète.
        Board board = Board.FromRows(
            ".......",
            ".......",
            ".......",
            ".......",
            "..JJJR.",
            "..RJRJ.");

        List<Move> losing = MoveGenerator.LosingMoves(board, Player.Red);

        Assert.Equal([new Move(1)], losing);
    }
}
