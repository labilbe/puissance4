namespace Puissance4.Core;

/// <summary>
/// Un coup : la colonne dans laquelle le jeton est lâché. La case atteinte n'en fait pas
/// partie, elle dépend de la grille — c'est tout le jeu.
/// </summary>
public readonly record struct Move(int Column)
{
    /// <summary>Le coup tel qu'il s'écrit et se saisit : le numéro de colonne, de 1 à 7.</summary>
    public string ToNotation() => Grid.NumberOf(Column).ToString();

    public static Move FromNumber(int number) => new(Grid.ColumnOf(number));

    public override string ToString() => ToNotation();
}
