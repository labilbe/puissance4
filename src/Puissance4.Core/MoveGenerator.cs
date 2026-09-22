namespace Puissance4.Core;

/// <summary>
/// Les coups jouables dans une position. Au Puissance 4 ils ne dépendent pas du camp :
/// une colonne non pleine est jouable par tout le monde. Ce qui change d'un camp à
/// l'autre, ce sont les coups qui gagnent — d'où les autres méthodes de cette classe.
/// </summary>
public static class MoveGenerator
{
    /// <summary>
    /// Les coups légaux, de la colonne la plus centrale à la plus excentrée. Cet ordre
    /// n'a aucune valeur de règle : il sert la recherche, qui coupe d'autant plus tôt
    /// qu'on lui présente les coups prometteurs en premier.
    /// </summary>
    public static List<Move> Generate(Board board)
    {
        var moves = new List<Move>(Grid.Columns);
        foreach (int column in Grid.FromTheCentre)
        {
            if (board.CanPlay(column))
            {
                moves.Add(new Move(column));
            }
        }

        return moves;
    }

    /// <summary>Vrai si ce coup aligne quatre jetons du camp donné.</summary>
    public static bool IsWinning(Board board, Player player, Move move)
    {
        if (!board.CanPlay(move.Column))
        {
            return false;
        }

        int index = board.Drop(move.Column, player);
        bool wins = board.IsWinning(index);
        board.Lift(move.Column);
        return wins;
    }

    /// <summary>Un coup qui gagne immédiatement pour le camp donné, s'il y en a un.</summary>
    public static Move? WinningMove(Board board, Player player)
    {
        foreach (int column in Grid.FromTheCentre)
        {
            var move = new Move(column);
            if (IsWinning(board, player, move))
            {
                return move;
            }
        }

        return null;
    }

    /// <summary>
    /// Les colonnes que le camp donné ne peut pas jouer sans offrir le gain : poser un jeton
    /// y découvre une case gagnante pour l'adversaire, juste au-dessus. Un coup qui gagne
    /// sur-le-champ n'en fait évidemment pas partie — l'adversaire n'aura pas la main.
    /// </summary>
    public static List<Move> LosingMoves(Board board, Player player)
    {
        var losing = new List<Move>();

        foreach (int column in Grid.FromTheCentre)
        {
            if (!board.CanPlay(column) || IsWinning(board, player, new Move(column)))
            {
                continue;
            }

            board.Drop(column, player);
            if (board.CanPlay(column) && IsWinning(board, player.Opponent(), new Move(column)))
            {
                losing.Add(new Move(column));
            }

            board.Lift(column);
        }

        return losing;
    }
}
