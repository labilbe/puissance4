namespace Puissance4.Core;

public enum Difficulty
{
    /// <summary>Recherche très courte et choix volontairement imparfait.</summary>
    Easy,

    /// <summary>Voit les menaces immédiates, mais pas les pièges à trois coups.</summary>
    Medium,

    /// <summary>Recherche sérieuse limitée à deux secondes.</summary>
    Hard,

    /// <summary>Recherche profonde limitée à six secondes.</summary>
    Expert,
}

/// <summary>Paramètres de recherche de l'IA.</summary>
public sealed record SearchOptions
{
    public required int MaxDepth { get; init; }

    public required TimeSpan TimeLimit { get; init; }

    /// <summary>
    /// Marge, en points d'évaluation, à l'intérieur de laquelle un coup est jugé
    /// « assez bon » et peut être tiré au sort. Zéro rend l'IA déterministe.
    /// Un alignement gagnant trouvé reste joué : l'aléa ne fait pas rater un gain immédiat.
    /// </summary>
    public int Randomness { get; init; }

    public int? RandomSeed { get; init; }

    public static SearchOptions For(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => new SearchOptions
        {
            MaxDepth = 2,
            TimeLimit = TimeSpan.FromMilliseconds(200),
            Randomness = 220,
        },
        Difficulty.Medium => new SearchOptions
        {
            MaxDepth = 6,
            TimeLimit = TimeSpan.FromMilliseconds(800),
            Randomness = 40,
        },
        Difficulty.Hard => new SearchOptions
        {
            MaxDepth = 14,
            TimeLimit = TimeSpan.FromSeconds(2),
        },
        Difficulty.Expert => new SearchOptions
        {
            MaxDepth = Grid.CellCount,
            TimeLimit = TimeSpan.FromSeconds(6),
        },
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty)),
    };

    public static string ToFrench(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => "Facile",
        Difficulty.Medium => "Moyen",
        Difficulty.Hard => "Difficile",
        Difficulty.Expert => "Expert",
        _ => difficulty.ToString(),
    };
}
