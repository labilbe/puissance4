namespace Puissance4.Core;

/// <summary>Les deux camps. Les Rouges commencent toujours.</summary>
public enum Player
{
    Red,
    Yellow,
}

/// <summary>Contenu d'une case de la grille.</summary>
public enum Cell : byte
{
    Empty = 0,
    Red,
    Yellow,
}

public static class CellExtensions
{
    public static bool IsEmpty(this Cell c) => c == Cell.Empty;

    public static Player Owner(this Cell c) => c == Cell.Red ? Player.Red : Player.Yellow;

    public static bool BelongsTo(this Cell c, Player p) => c != Cell.Empty && c.Owner() == p;

    public static Player Opponent(this Player p) => p == Player.Red ? Player.Yellow : Player.Red;

    public static Cell Disc(this Player p) => p == Player.Red ? Cell.Red : Cell.Yellow;

    public static string ToFrench(this Player p) => p == Player.Red ? "Rouges" : "Jaunes";
}

/// <summary>
/// Géométrie de la grille : sept colonnes numérotées de 1 à 7, six rangées.
/// La rangée 0 est en haut, la rangée 5 au fond : un jeton lâché dans une colonne
/// se pose sur la rangée libre de plus grand indice.
/// </summary>
public static class Grid
{
    public const int Columns = 7;
    public const int Rows = 6;
    public const int CellCount = Columns * Rows;

    /// <summary>Nombre de jetons à aligner pour gagner.</summary>
    public const int WinLength = 4;

    /// <summary>
    /// Les quatre axes d'alignement, sous forme (deltaRangée, deltaColonne) :
    /// horizontale, verticale, et les deux diagonales. On ne garde qu'un sens par axe,
    /// la recherche d'alignement explorant les deux.
    /// </summary>
    public static readonly (int Dr, int Dc)[] Axes =
    [
        (0, 1), (1, 0), (1, 1), (1, -1),
    ];

    public static bool IsInside(int row, int col) =>
        row >= 0 && row < Rows && col >= 0 && col < Columns;

    public static int Index(int row, int col) => (row * Columns) + col;

    public static int Row(int index) => index / Columns;

    public static int Col(int index) => index % Columns;

    /// <summary>Numéro de colonne affiché au joueur (1 à 7) pour une colonne interne (0 à 6).</summary>
    public static int NumberOf(int column) => column + 1;

    /// <summary>Colonne interne (0 à 6) portant le numéro donné (1 à 7).</summary>
    public static int ColumnOf(int number)
    {
        if (number is < 1 or > Columns)
        {
            throw new ArgumentOutOfRangeException(nameof(number), number, "Le numéro de colonne doit être compris entre 1 et 7.");
        }

        return number - 1;
    }

    /// <summary>
    /// Les colonnes, de la plus centrale à la plus excentrée : 4, 3, 5, 2, 6, 1, 7.
    /// C'est le bon ordre pour explorer un arbre de Puissance 4, une colonne centrale
    /// participant à bien plus d'alignements qu'une colonne de bord.
    /// </summary>
    public static readonly int[] FromTheCentre =
        [.. Enumerable.Range(0, Columns).OrderBy(c => Math.Abs(c - (Columns / 2))).ThenBy(c => c)];

    /// <summary>Nombre d'alignements de quatre cases passant par la case donnée.</summary>
    public static int AlignmentsThrough(int row, int col)
    {
        int total = 0;
        foreach ((int dr, int dc) in Axes)
        {
            for (int offset = 0; offset < WinLength; offset++)
            {
                int startRow = row - (dr * offset);
                int startCol = col - (dc * offset);
                int endRow = startRow + (dr * (WinLength - 1));
                int endCol = startCol + (dc * (WinLength - 1));

                if (IsInside(startRow, startCol) && IsInside(endRow, endCol))
                {
                    total++;
                }
            }
        }

        return total;
    }
}
