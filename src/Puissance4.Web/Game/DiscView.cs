using Puissance4.Core;

namespace Puissance4.Web.Game;

/// <summary>Qui tient un camp.</summary>
public enum Seat
{
    Human,
    Computer,
}

/// <summary>
/// Un jeton tel qu'il est affiché. Son identité survit à sa chute, ce qui permet de
/// l'animer de la main du joueur jusqu'à sa case plutôt que de le faire apparaître.
/// </summary>
public sealed class DiscView
{
    /// <summary>Rangée d'où part un jeton qui tombe : juste au-dessus de la grille.</summary>
    public const int AboveTheGrid = -1;

    public required int Id { get; init; }

    public required Player Owner { get; init; }

    public required int Col { get; init; }

    public int Row { get; set; } = AboveTheGrid;

    /// <summary>Vrai quand le jeton fait partie de l'alignement gagnant : il s'éclaire.</summary>
    public bool Winning { get; set; }
}

/// <summary>Une entrée du relevé de la partie.</summary>
public sealed record MoveRecord(Player Player, string Notation);
