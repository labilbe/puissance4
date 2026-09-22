using Puissance4.Core;

namespace Puissance4.Core.Tests;

/// <summary>
/// Comptage du nombre de parties distinctes à faible profondeur depuis la grille vide.
/// Toute erreur dans la détection des alignements ou dans le remplissage des colonnes
/// fait diverger ces nombres immédiatement.
/// </summary>
public class PerftTests
{
    [Theory]
    [InlineData(1, 7L)]
    [InlineData(2, 49L)]
    [InlineData(3, 343L)]
    [InlineData(4, 2401L)]
    [InlineData(5, 16807L)]
    [InlineData(6, 117649L)]
    public void Perft_CountsSevenChoicesPerPly_UntilTheGridStartsToConstrain(int depth, long expected)
    {
        // Avant le septième coup, rien ne peut restreindre le choix : il faut sept jetons
        // pour remplir une colonne, et huit — quatre de chaque camp — pour qu'un alignement
        // termine la partie. Les six premiers niveaux comptent donc exactement 7 puissance n.
        Assert.Equal(expected, Perft(new GameState(), depth));
    }

    [Fact]
    public void Perft_AtSevenPlies_LosesExactlyTheSevenLinesThatFillAColumn()
    {
        // Au septième coup, la seule restriction possible est une colonne remplie par les
        // six premiers coups : il y a sept lignes de ce genre, une par colonne, et chacune
        // n'offre plus que six suites au lieu de sept.
        Assert.Equal((long)Math.Pow(Grid.Columns, 7) - Grid.Columns, Perft(new GameState(), 7));
    }

    [Fact]
    public void Perft_GivesTheSameCountWhetherMovesAreUnmadeOrTheGameIsReplayed()
    {
        // Le comptage de référence rejoue chaque ligne depuis le début, sans jamais annuler
        // un coup : si UnmakeMove oubliait quoi que ce soit, les deux totaux différeraient.
        Assert.Equal(PerftByReplay([], 7), Perft(new GameState(), 7));
    }

    private static long Perft(GameState state, int depth)
    {
        List<Move> moves = state.LegalMoves();
        if (depth <= 1 || moves.Count == 0)
        {
            return moves.Count;
        }

        long total = 0;
        foreach (Move move in moves)
        {
            state.MakeMove(move);
            total += Perft(state, depth - 1);
            state.UnmakeMove();
        }

        return total;
    }

    private static long PerftByReplay(List<Move> line, int depth)
    {
        var state = new GameState();
        foreach (Move move in line)
        {
            state.MakeMove(move);
        }

        List<Move> moves = state.LegalMoves();
        if (depth <= 1 || moves.Count == 0)
        {
            return moves.Count;
        }

        long total = 0;
        foreach (Move move in moves)
        {
            line.Add(move);
            total += PerftByReplay(line, depth - 1);
            line.RemoveAt(line.Count - 1);
        }

        return total;
    }
}
