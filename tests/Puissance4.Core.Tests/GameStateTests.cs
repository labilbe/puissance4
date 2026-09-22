using Puissance4.Core;

namespace Puissance4.Core.Tests;

public class GameStateTests
{
    [Fact]
    public void MakeMove_PosesTheDiscAndPassesTheTurn()
    {
        var state = new GameState();

        state.MakeMove(new Move(3));

        Assert.Equal(Cell.Red, state.Board[5, 3]);
        Assert.Equal(Player.Yellow, state.SideToMove);
        Assert.Equal(Grid.Index(5, 3), state.LastCell);
        Assert.Equal(1, state.PlyCount);
    }

    [Fact]
    public void MakeMove_ThenUnmakeMove_RestoresBoardHashAndSideToMove()
    {
        var state = new GameState();
        string before = state.Board.Render();
        ulong hashBefore = state.Hash;

        var random = new Random(7);
        int played = 0;
        for (int i = 0; i < 20; i++)
        {
            List<Move> legal = state.LegalMoves();
            if (legal.Count == 0)
            {
                break;
            }

            state.MakeMove(legal[random.Next(legal.Count)]);
            played++;
        }

        Assert.NotEqual(before, state.Board.Render());

        for (int i = 0; i < played; i++)
        {
            state.UnmakeMove();
        }

        Assert.Equal(before, state.Board.Render());
        Assert.Equal(hashBefore, state.Hash);
        Assert.Equal(Player.Red, state.SideToMove);
        Assert.Equal(0, state.PlyCount);
        Assert.Null(state.LastCell);
    }

    [Fact]
    public void Hash_IsConsistentWithAFullRecomputation()
    {
        var state = new GameState();
        var random = new Random(31);

        for (int i = 0; i < 30; i++)
        {
            List<Move> legal = state.LegalMoves();
            if (legal.Count == 0)
            {
                break;
            }

            state.MakeMove(legal[random.Next(legal.Count)]);
            Assert.Equal(Zobrist.Compute(state.Board, state.SideToMove), state.Hash);
        }
    }

    [Fact]
    public void Result_AnnouncesTheWinnerAndFreezesTheGame()
    {
        // Les Rouges jouent la colonne 1, les Jaunes la 2 : la colonne 1 monte à quatre.
        var state = new GameState();
        foreach (Move move in new Move[] { new(0), new(1), new(0), new(1), new(0), new(1), new(0) })
        {
            state.MakeMove(move);
        }

        Assert.Equal(GameResult.RedWins, state.Result());
        Assert.Equal(Player.Red, state.Winner);
        Assert.Equal(4, state.Alignment?.Count);
        Assert.Empty(state.LegalMoves());
    }

    [Fact]
    public void UnmakeMove_TakesBackTheWin()
    {
        var state = new GameState(Board.FromRows(
            ".......",
            ".......",
            ".......",
            ".......",
            "..JJJ..",
            "..RRR.."), Player.Red);

        state.MakeMove(new Move(5));
        Assert.Equal(GameResult.RedWins, state.Result());

        state.UnmakeMove();

        Assert.Equal(GameResult.InProgress, state.Result());
        Assert.Null(state.Winner);
        Assert.Equal(Grid.Columns, state.LegalMoves().Count);
    }

    [Fact]
    public void Result_IsADrawWhenTheGridFillsUpWithoutAnAlignment()
    {
        // Une grille pleine sans alignement : les colonnes montent par paires de couleurs,
        // décalées d'une colonne sur deux.
        var state = new GameState(Board.FromRows(
            "JRJRJRJ",
            "JRJRJRJ",
            "RJRJRJR",
            "RJRJRJR",
            "JRJRJRJ",
            "JRJRJRJ"), Player.Red);

        Assert.True(state.Board.IsFull);
        Assert.Equal(GameResult.Draw, state.Result());
        Assert.Empty(state.LegalMoves());
    }

    [Fact]
    public void Constructor_SeesAnAlignmentAlreadyPresentInTheGrid()
    {
        var state = new GameState(Board.FromRows(
            ".......",
            ".......",
            ".......",
            "...J...",
            "...J...",
            "JJJJ..."), Player.Red);

        Assert.Equal(GameResult.YellowWins, state.Result());
    }
}
