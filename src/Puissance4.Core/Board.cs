using System.Text;

namespace Puissance4.Core;

/// <summary>
/// Une grille de Puissance 4, mutable : sept colonnes de six cases, avec la hauteur
/// atteinte par chaque colonne pour poser un jeton sans avoir à parcourir la grille.
/// </summary>
public sealed class Board
{
    private const char EmptyGlyph = '.';
    private const char RedGlyph = 'R';
    private const char YellowGlyph = 'J';

    private readonly Cell[] _cells;
    private readonly int[] _heights;

    public Board()
    {
        _cells = new Cell[Grid.CellCount];
        _heights = new int[Grid.Columns];
    }

    private Board(Cell[] cells, int[] heights)
    {
        _cells = cells;
        _heights = heights;
    }

    public Cell this[int index] => _cells[index];

    public Cell this[int row, int col] => _cells[Grid.Index(row, col)];

    /// <summary>Nombre de jetons déjà empilés dans la colonne donnée.</summary>
    public int HeightOf(int column) => _heights[column];

    /// <summary>Vrai si la colonne peut encore recevoir un jeton.</summary>
    public bool CanPlay(int column) => _heights[column] < Grid.Rows;

    /// <summary>Rangée sur laquelle se poserait un jeton lâché dans la colonne, -1 si elle est pleine.</summary>
    public int LandingRow(int column) => CanPlay(column) ? Grid.Rows - 1 - _heights[column] : -1;

    /// <summary>Case sur laquelle se poserait un jeton lâché dans la colonne, -1 si elle est pleine.</summary>
    public int LandingIndex(int column) =>
        CanPlay(column) ? Grid.Index(LandingRow(column), column) : -1;

    /// <summary>Nombre de jetons posés, toutes couleurs confondues.</summary>
    public int TotalDiscs { get; private set; }

    public bool IsFull => TotalDiscs == Grid.CellCount;

    /// <summary>Lâche un jeton dans une colonne et renvoie la case atteinte.</summary>
    public int Drop(int column, Player player)
    {
        if (!CanPlay(column))
        {
            throw new InvalidOperationException($"La colonne {Grid.NumberOf(column)} est pleine.");
        }

        int index = Grid.Index(LandingRow(column), column);
        _cells[index] = player.Disc();
        _heights[column]++;
        TotalDiscs++;
        return index;
    }

    /// <summary>Retire le jeton posé au sommet d'une colonne et renvoie la case libérée.</summary>
    public int Lift(int column)
    {
        if (_heights[column] == 0)
        {
            throw new InvalidOperationException($"La colonne {Grid.NumberOf(column)} est vide.");
        }

        _heights[column]--;
        TotalDiscs--;
        int index = Grid.Index(Grid.Rows - 1 - _heights[column], column);
        _cells[index] = Cell.Empty;
        return index;
    }

    public Board Clone() => new((Cell[])_cells.Clone(), (int[])_heights.Clone()) { TotalDiscs = TotalDiscs };

    public int CountOf(Player player)
    {
        int total = 0;
        foreach (Cell cell in _cells)
        {
            if (cell.BelongsTo(player))
            {
                total++;
            }
        }

        return total;
    }

    /// <summary>
    /// L'alignement gagnant passant par la case donnée, ou <c>null</c> s'il n'y en a pas.
    /// Les cases renvoyées sont dans l'ordre de l'axe, et peuvent être plus de quatre :
    /// un cinquième jeton dans le prolongement appartient au même alignement.
    /// </summary>
    public IReadOnlyList<int>? AlignmentThrough(int index)
    {
        Cell piece = _cells[index];
        if (piece.IsEmpty())
        {
            return null;
        }

        int row = Grid.Row(index);
        int col = Grid.Col(index);

        foreach ((int dr, int dc) in Grid.Axes)
        {
            int back = Reach(row, col, -dr, -dc, piece);
            int forward = Reach(row, col, dr, dc, piece);

            if (back + forward + 1 < Grid.WinLength)
            {
                continue;
            }

            var line = new List<int>(back + forward + 1);
            for (int step = -back; step <= forward; step++)
            {
                line.Add(Grid.Index(row + (dr * step), col + (dc * step)));
            }

            return line;
        }

        return null;
    }

    /// <summary>Vrai si la case donnée appartient à un alignement de quatre.</summary>
    public bool IsWinning(int index) => AlignmentThrough(index) is not null;

    /// <summary>Nombre de cases de même couleur d'affilée à partir de (row, col), exclue, dans une direction.</summary>
    private int Reach(int row, int col, int dr, int dc, Cell piece)
    {
        int count = 0;
        int r = row + dr;
        int c = col + dc;

        while (Grid.IsInside(r, c) && _cells[Grid.Index(r, c)] == piece)
        {
            count++;
            r += dr;
            c += dc;
        }

        return count;
    }

    /// <summary>
    /// Construit une grille à partir de ses six rangées, de haut en bas : un point pour
    /// une case vide, R pour un jeton rouge, J pour un jaune. Un jeton flottant est refusé,
    /// une grille de Puissance 4 n'ayant pas de trou.
    /// </summary>
    public static Board FromRows(params string[] rows)
    {
        if (rows.Length != Grid.Rows)
        {
            throw new FormatException($"Il faut {Grid.Rows} rangées, {rows.Length} fournie(s).");
        }

        var board = new Board();

        for (int col = 0; col < Grid.Columns; col++)
        {
            // On remplit chaque colonne par le bas : dès qu'une case est vide,
            // tout ce qui est au-dessus doit l'être aussi.
            bool empty = false;
            for (int row = Grid.Rows - 1; row >= 0; row--)
            {
                string line = rows[row];
                if (line.Length != Grid.Columns)
                {
                    throw new FormatException($"La rangée « {line} » n'a pas {Grid.Columns} colonnes.");
                }

                Player? player = line[col] switch
                {
                    EmptyGlyph or ' ' => null,
                    RedGlyph or 'r' => Player.Red,
                    YellowGlyph or 'j' => Player.Yellow,
                    char other => throw new FormatException($"Caractère inattendu dans la grille : « {other} »."),
                };

                if (player is null)
                {
                    empty = true;
                    continue;
                }

                if (empty)
                {
                    throw new FormatException(
                        $"Jeton flottant en colonne {Grid.NumberOf(col)} : un jeton ne peut pas rester en l'air.");
                }

                board.Drop(col, player.Value);
            }
        }

        return board;
    }

    /// <summary>
    /// Construit une grille en jouant une suite de colonnes, les Rouges commençant :
    /// "4453" pose un jeton rouge en 4, un jaune en 4, un rouge en 5, un jaune en 3.
    /// </summary>
    public static Board FromMoves(string columns)
    {
        var board = new Board();
        Player player = Player.Red;

        foreach (char c in columns)
        {
            if (char.IsWhiteSpace(c))
            {
                continue;
            }

            if (c is < '1' or > '7')
            {
                throw new FormatException($"« {c} » n'est pas une colonne : les colonnes vont de 1 à {Grid.Columns}.");
            }

            board.Drop(Grid.ColumnOf(c - '0'), player);
            player = player.Opponent();
        }

        return board;
    }

    /// <summary>Les six rangées de la grille, de haut en bas, dans le format lu par <see cref="FromRows"/>.</summary>
    public string[] ToRows()
    {
        var rows = new string[Grid.Rows];
        for (int row = 0; row < Grid.Rows; row++)
        {
            var line = new char[Grid.Columns];
            for (int col = 0; col < Grid.Columns; col++)
            {
                line[col] = GlyphOf(this[row, col]);
            }

            rows[row] = new string(line);
        }

        return rows;
    }

    /// <summary>Représentation texte de la grille, la rangée du haut en premier.</summary>
    public string Render(bool showNumbers = true)
    {
        var sb = new StringBuilder();
        string separator = "+" + string.Concat(Enumerable.Repeat("---+", Grid.Columns));

        if (showNumbers)
        {
            for (int col = 0; col < Grid.Columns; col++)
            {
                sb.Append("  ").Append(Grid.NumberOf(col)).Append(' ');
            }

            sb.AppendLine();
        }

        sb.AppendLine(separator);
        for (int row = 0; row < Grid.Rows; row++)
        {
            sb.Append('|');
            for (int col = 0; col < Grid.Columns; col++)
            {
                sb.Append(' ').Append(GlyphOf(this[row, col])).Append(' ').Append('|');
            }

            sb.AppendLine();
            sb.AppendLine(separator);
        }

        return sb.ToString();
    }

    private static char GlyphOf(Cell cell) => cell switch
    {
        Cell.Red => RedGlyph,
        Cell.Yellow => YellowGlyph,
        _ => EmptyGlyph,
    };

    public override string ToString() => Render();
}
