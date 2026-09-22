using Puissance4.Core;

namespace Puissance4.Core.Tests;

public class SearchEngineTests
{
    [Fact]
    public void Search_ReturnsALegalMoveFromTheEmptyGrid()
    {
        var state = new GameState();
        var engine = new SearchEngine(Difficulty.Medium);

        SearchResult result = engine.Search(state);

        Assert.NotNull(result.BestMove);
        Assert.Contains(result.BestMove.Value, state.LegalMoves());
        Assert.True(result.Nodes > 0);
    }

    [Fact]
    public void Search_LeavesTheGameStateUntouched()
    {
        var state = new GameState();
        state.MakeMove(new Move(3));
        string before = state.Board.Render();
        ulong hashBefore = state.Hash;

        new SearchEngine(Difficulty.Medium).Search(state);

        Assert.Equal(before, state.Board.Render());
        Assert.Equal(hashBefore, state.Hash);
        Assert.Equal(Player.Yellow, state.SideToMove);
        Assert.Equal(1, state.PlyCount);
    }

    [Theory]
    [InlineData(Difficulty.Easy)]
    [InlineData(Difficulty.Medium)]
    [InlineData(Difficulty.Hard)]
    public void Search_TakesTheWinItHasInHand_AtEveryLevel(Difficulty difficulty)
    {
        var state = new GameState(Board.FromRows(
            ".......",
            ".......",
            ".......",
            ".......",
            "..JJJ..",
            "..RRR.."), Player.Red);

        SearchResult result = new SearchEngine(difficulty).Search(state);

        Assert.Contains(result.BestMove, new Move?[] { new Move(1), new Move(5) });
    }

    [Fact]
    public void Search_BlocksTheOpponentsThreat()
    {
        // Les Jaunes alignent trois jetons sur la rangée du bas. L'autre bout de la rangée
        // étant déjà occupé, la colonne 6 est le seul coup qui ne perde pas sur-le-champ.
        var state = new GameState(Board.FromRows(
            ".......",
            ".......",
            ".......",
            ".......",
            ".R.....",
            "RRJJJ.."), Player.Red);

        SearchResult result = new SearchEngine(Difficulty.Hard).Search(state);

        Assert.Equal(new Move(5), result.BestMove);
    }

    [Fact]
    public void Search_PrefersTheQuickerWin()
    {
        var state = new GameState(Board.FromRows(
            ".......",
            ".......",
            ".......",
            ".......",
            "..JJJ..",
            "..RRR.."), Player.Red);

        SearchResult result = new SearchEngine(Difficulty.Hard).Search(state);

        Assert.True(result.Score > Evaluator.WinScore - 100, $"Score attendu proche du gain, obtenu {result.Score}.");
        Assert.Single(result.PrincipalVariation);
    }

    [Fact]
    public void Search_SeesThatTheCentreIsWorthMoreThanTheEdge()
    {
        var engine = new SearchEngine(SearchOptions.For(Difficulty.Hard) with { MaxDepth = 4 });

        SearchResult result = engine.Search(new GameState());

        Assert.Equal(3, result.BestMove?.Column);
    }

    [Fact]
    public void Search_ReportsNoMoveWhenTheGameIsOver()
    {
        var state = new GameState(Board.FromRows(
            ".......",
            ".......",
            ".......",
            ".......",
            "..JJJ..",
            "..RRRR."), Player.Yellow);

        SearchResult result = new SearchEngine(Difficulty.Easy).Search(state);

        Assert.Null(result.BestMove);
    }

    [Fact]
    public void Search_IsDeterministicWhenTheLevelHasNoRandomness()
    {
        var state = new GameState(Board.FromMoves("4435"), Player.Red);

        Move? first = new SearchEngine(Difficulty.Hard).Search(state).BestMove;
        Move? second = new SearchEngine(Difficulty.Hard).Search(state).BestMove;

        Assert.Equal(first, second);
    }
}
