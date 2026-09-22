namespace Puissance4.Core;

public enum GameResult
{
    InProgress,
    RedWins,
    YellowWins,
    Draw,
}

public static class GameResultExtensions
{
    public static bool IsOver(this GameResult result) => result != GameResult.InProgress;

    public static string ToFrench(this GameResult result) => result switch
    {
        GameResult.InProgress => "partie en cours",
        GameResult.RedWins => "les Rouges gagnent",
        GameResult.YellowWins => "les Jaunes gagnent",
        GameResult.Draw => "grille pleine, partie nulle",
        _ => "inconnu",
    };

    /// <summary>Le résultat correspondant à la victoire du camp donné.</summary>
    public static GameResult Wins(this Player player) =>
        player == Player.Red ? GameResult.RedWins : GameResult.YellowWins;
}
