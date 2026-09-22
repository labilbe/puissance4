using Puissance4.Core;
using Puissance4.Core.Pcn;

namespace Puissance4.Core.Tests;

public class PcnTests
{
    [Fact]
    public void Write_ThenParse_GivesBackTheSameGame()
    {
        var state = new GameState();
        foreach (char column in "4453671")
        {
            state.MakeMove(Move.FromNumber(column - '0'));
        }

        PcnGame reread = PcnFile.Parse(PcnFile.Write(state));

        Assert.Equal(state.History, reread.Moves);
        Assert.Equal(state.Board.ToRows(), reread.ToGameState().Board.ToRows());
    }

    [Fact]
    public void Write_NumbersTheTurnsAndClosesOnTheResult()
    {
        var state = new GameState();
        foreach (char column in "1212121")
        {
            state.MakeMove(Move.FromNumber(column - '0'));
        }

        string pcn = PcnFile.Write(state, [new PcnTag("Red", "Alice"), new PcnTag("Yellow", "Bob")]);

        Assert.Contains("[Red \"Alice\"]", pcn, StringComparison.Ordinal);
        Assert.Contains("[Result \"1-0\"]", pcn, StringComparison.Ordinal);
        Assert.Contains("1. 1 2 2. 1 2 3. 1 2 4. 1 1-0", pcn, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_ReadsTheBareColumnSequenceUsedBySolvers()
    {
        PcnGame game = PcnFile.Parse("4453");

        Assert.Equal([new Move(3), new Move(3), new Move(4), new Move(2)], game.Moves);
        Assert.Equal(Cell.Yellow, game.ToGameState().Board[4, 3]);
    }

    [Fact]
    public void Parse_IgnoresCommentsVariationsAndAnnotations()
    {
        PcnGame game = PcnFile.Parse("""
            [Event "Essai"]

            1. 4 {le coup d'école} 4 2. 5 $2 (2... 3 3. 2) 3 *
            ; une ligne de commentaire
            """);

        Assert.Equal([new Move(3), new Move(3), new Move(4), new Move(2)], game.Moves);
        Assert.Equal("Essai", game.Tag("Event"));
        Assert.Equal(GameResult.InProgress, game.Result);
    }

    [Fact]
    public void Parse_KeepsAStartingPositionGivenByAFen()
    {
        var start = new GameState(Board.FromMoves("444"), Player.Yellow);
        start.MakeMove(new Move(1));

        string pcn = PcnFile.Write(start);
        PcnGame reread = PcnFile.Parse(pcn);

        Assert.Contains("[SetUp \"1\"]", pcn, StringComparison.Ordinal);
        Assert.Equal(Player.Yellow, reread.StartingSide);
        Assert.Equal(start.Board.ToRows(), reread.ToGameState().Board.ToRows());
    }

    [Fact]
    public void ParseAll_ReadsSeveralGamesFromOneFile()
    {
        IReadOnlyList<PcnGame> games = PcnFile.ParseAll("""
            [Event "Première"]

            1. 4 4 1-0

            [Event "Seconde"]

            1. 1 7 0-1
            """);

        Assert.Equal(2, games.Count);
        Assert.Equal("Seconde", games[1].Tag("Event"));
        Assert.Equal(GameResult.YellowWins, games[1].Result);
    }

    [Fact]
    public void Parse_RefusesAnIllegalMove()
    {
        PcnException error = Assert.Throws<PcnException>(() => PcnFile.Parse("1111111"));

        Assert.Contains("La colonne 1 est pleine.", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_RefusesAFileFromAnotherGame()
    {
        PcnException error = Assert.Throws<PcnException>(() => PcnFile.Parse("""
            [GameType "Connect4-8x7"]

            1. 4 4 *
            """));

        Assert.Contains("un autre jeu", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Fen_WritesAndReadsBackAPosition()
    {
        Board board = Board.FromMoves("4453");

        string fen = PcnFen.Write(board, Player.Red);
        (Board reread, Player side) = PcnFen.Parse(fen);

        Assert.Equal("7/7/7/7/3J3/2JRR2 r", fen);
        Assert.Equal(Player.Red, side);
        Assert.Equal(board.ToRows(), reread.ToRows());
    }

    [Fact]
    public void Fen_RefusesAFloatingDisc()
    {
        Assert.Throws<PcnException>(() => PcnFen.Parse("7/7/7/3R3/7/7 r"));
    }
}
