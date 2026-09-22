using System.Text;

namespace Puissance4.Core.Pcn;

/// <summary>
/// Le champ FEN du format PCN, calqué sur celui des échecs : les six rangées de la grille,
/// du haut vers le bas, séparées par des barres obliques, un chiffre comptant les cases
/// vides consécutives, puis le trait. La grille vide s'écrit <c>7/7/7/7/7/7 r</c>.
/// </summary>
public static class PcnFen
{
    /// <summary>Écrit la position sous forme de FEN PCN.</summary>
    public static string Write(Board board, Player sideToMove)
    {
        var sb = new StringBuilder();

        for (int row = 0; row < Grid.Rows; row++)
        {
            if (row > 0)
            {
                sb.Append('/');
            }

            int empty = 0;
            for (int col = 0; col < Grid.Columns; col++)
            {
                Cell cell = board[row, col];
                if (cell.IsEmpty())
                {
                    empty++;
                    continue;
                }

                if (empty > 0)
                {
                    sb.Append(empty);
                    empty = 0;
                }

                sb.Append(cell == Cell.Red ? 'R' : 'J');
            }

            if (empty > 0)
            {
                sb.Append(empty);
            }
        }

        return sb.Append(' ').Append(sideToMove == Player.Red ? 'r' : 'j').ToString();
    }

    /// <summary>Lit un FEN PCN.</summary>
    public static (Board Board, Player SideToMove) Parse(string fen)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fen);

        string[] fields = fen.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length == 0)
        {
            throw new PcnException("FEN vide.");
        }

        Player sideToMove = fields.Length > 1
            ? fields[1].Trim().ToLowerInvariant() switch
            {
                "r" => Player.Red,
                "j" => Player.Yellow,
                var other => throw new PcnException($"Trait inconnu dans le FEN : « {other} ». Attendu r ou j."),
            }
            : Player.Red;

        string[] lines = fields[0].Split('/');
        if (lines.Length != Grid.Rows)
        {
            throw new PcnException($"Le FEN doit décrire {Grid.Rows} rangées, {lines.Length} trouvée(s).");
        }

        // On passe par la description en rangées, qui vérifie au passage qu'aucun jeton ne flotte.
        var rows = new string[Grid.Rows];
        for (int row = 0; row < Grid.Rows; row++)
        {
            rows[row] = Expand(lines[row]);
        }

        try
        {
            return (Board.FromRows(rows), sideToMove);
        }
        catch (FormatException error)
        {
            throw new PcnException($"FEN invalide : {error.Message}");
        }
    }

    private static string Expand(string line)
    {
        var sb = new StringBuilder(Grid.Columns);

        foreach (char c in line)
        {
            switch (c)
            {
                case 'R' or 'r':
                    sb.Append('R');
                    break;
                case 'J' or 'j':
                    sb.Append('J');
                    break;
                case >= '1' and <= '7':
                    sb.Append('.', c - '0');
                    break;
                default:
                    throw new PcnException($"Caractère inattendu dans le FEN : « {c} ».");
            }
        }

        if (sb.Length != Grid.Columns)
        {
            throw new PcnException($"La rangée « {line} » décrit {sb.Length} cases au lieu de {Grid.Columns}.");
        }

        return sb.ToString();
    }
}
