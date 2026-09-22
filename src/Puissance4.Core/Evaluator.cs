namespace Puissance4.Core;

/// <summary>
/// Évaluation statique d'une position, du point de vue des Rouges. On ne compte pas le
/// matériel — les deux camps ont toujours le même nombre de jetons, à un près — mais les
/// alignements de quatre cases encore ouverts : celui qui en tient le plus a le plus de
/// façons de gagner.
/// </summary>
public static class Evaluator
{
    /// <summary>Score attribué à un gain ; on en retranche la profondeur pour préférer les gains rapides.</summary>
    public const int WinScore = 1_000_000;

    /// <summary>Valeur d'une fenêtre de quatre cases selon le nombre de jetons qu'on y a déjà posés.</summary>
    private static readonly int[] WindowValue = [0, 1, 12, 70, WinScore];

    /// <summary>Une colonne centrale participe à sept alignements, une colonne de bord à trois.</summary>
    private const int CentreBonus = 6;

    /// <summary>Les 69 alignements de quatre cases de la grille, chacun listé une seule fois.</summary>
    private static readonly int[][] Windows = BuildWindows();

    /// <summary>Score de la position, positif si les Rouges sont mieux.</summary>
    public static int Evaluate(Board board)
    {
        int score = 0;

        foreach (int[] window in Windows)
        {
            int red = 0;
            int yellow = 0;

            foreach (int cell in window)
            {
                switch (board[cell])
                {
                    case Cell.Red:
                        red++;
                        break;
                    case Cell.Yellow:
                        yellow++;
                        break;
                }
            }

            // Une fenêtre où les deux couleurs se croisent est morte : plus personne n'y gagnera.
            if (red > 0 && yellow > 0)
            {
                continue;
            }

            score += WindowValue[red] - WindowValue[yellow];
        }

        int centre = Grid.Columns / 2;
        for (int row = 0; row < Grid.Rows; row++)
        {
            Cell cell = board[row, centre];
            if (!cell.IsEmpty())
            {
                score += cell.Owner() == Player.Red ? CentreBonus : -CentreBonus;
            }
        }

        return score;
    }

    /// <summary>Score du point de vue du camp donné.</summary>
    public static int Evaluate(Board board, Player player)
    {
        int score = Evaluate(board);
        return player == Player.Red ? score : -score;
    }

    private static int[][] BuildWindows()
    {
        var windows = new List<int[]>(69);

        for (int row = 0; row < Grid.Rows; row++)
        {
            for (int col = 0; col < Grid.Columns; col++)
            {
                foreach ((int dr, int dc) in Grid.Axes)
                {
                    int endRow = row + (dr * (Grid.WinLength - 1));
                    int endCol = col + (dc * (Grid.WinLength - 1));
                    if (!Grid.IsInside(endRow, endCol))
                    {
                        continue;
                    }

                    var window = new int[Grid.WinLength];
                    for (int step = 0; step < Grid.WinLength; step++)
                    {
                        window[step] = Grid.Index(row + (dr * step), col + (dc * step));
                    }

                    windows.Add(window);
                }
            }
        }

        return [.. windows];
    }

    /// <summary>Nombre d'alignements de quatre cases que compte la grille : 69.</summary>
    public static int WindowCount => Windows.Length;
}
