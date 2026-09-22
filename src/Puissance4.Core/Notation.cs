namespace Puissance4.Core;

/// <summary>
/// Lecture de la saisie d'un joueur. Un coup de Puissance 4 tient dans un chiffre : le
/// numéro de la colonne, de 1 à 7. On accepte aussi la lettre de la colonne, de a à g,
/// que certains diagrammes utilisent.
/// </summary>
public static class Notation
{
    /// <summary>Résout une saisie en un coup de la liste des coups légaux.</summary>
    public static bool TryParse(string? input, IReadOnlyList<Move> legalMoves, out Move? move, out string? error)
    {
        move = null;
        error = null;

        string trimmed = (input ?? string.Empty).Trim();
        if (trimmed.Length != 1)
        {
            error = $"Indiquez une colonne, de 1 à {Grid.Columns}.";
            return false;
        }

        char c = char.ToLowerInvariant(trimmed[0]);
        int number = c switch
        {
            >= '1' and <= '7' => c - '0',
            >= 'a' and <= 'g' => c - 'a' + 1,
            _ => 0,
        };

        if (number == 0)
        {
            error = $"« {trimmed} » n'est pas une colonne : les colonnes vont de 1 à {Grid.Columns}.";
            return false;
        }

        var candidate = new Move(Grid.ColumnOf(number));
        if (!legalMoves.Contains(candidate))
        {
            error = $"La colonne {number} est pleine.";
            return false;
        }

        move = candidate;
        return true;
    }

    /// <summary>
    /// Description lisible d'un coup dans la position où il est joué, par exemple
    /// « colonne 4 (alignement gagnant) ».
    /// </summary>
    public static string Describe(Board board, Player player, Move move)
    {
        string where = $"colonne {Grid.NumberOf(move.Column)}";

        if (MoveGenerator.IsWinning(board, player, move))
        {
            return $"{where} (alignement gagnant)";
        }

        if (MoveGenerator.IsWinning(board, player.Opponent(), move))
        {
            // La même case aurait fait gagner l'adversaire : le coup lui prend sa victoire.
            return $"{where} (contre)";
        }

        return where;
    }
}
