using System.Globalization;
using System.Text;

namespace Puissance4.Core.Pcn;

/// <summary>
/// Lecture et écriture du PCN, le format texte de ce projet pour échanger une partie de
/// Puissance 4. Il n'existe pas de norme comparable au PGN des échecs ou au PDN des dames :
/// on en reprend donc la forme — des en-têtes entre crochets, puis les coups numérotés —
/// avec pour seule notation le numéro de colonne, de 1 à 7.
///
/// À la lecture, on accepte aussi la suite de colonnes nue, « 4453 », qui est la façon dont
/// les solveurs et les bases de positions publient les parties de Puissance 4.
/// </summary>
public static class PcnFile
{
    /// <summary>Code du jeu, pour qu'un fichier d'un autre jeu soit refusé plutôt que mal lu.</summary>
    public const string ConnectFourGameType = "Connect4-7x6";

    private const int WrapColumn = 80;

    private static readonly string[] SevenTagRoster =
        ["Event", "Site", "Date", "Round", "Red", "Yellow", "Result"];

    /// <summary>
    /// Écrit une partie au format PCN. Les en-têtes fournies sont reprises telles quelles ;
    /// celles du sept-tag roster manquantes sont complétées, et la position de départ n'est
    /// écrite que si la partie ne commence pas sur une grille vide.
    /// </summary>
    public static string Write(GameState state, IEnumerable<PcnTag>? tags = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        List<PcnTag> header = BuildHeader(state, tags);
        var sb = new StringBuilder();

        foreach (PcnTag tag in header)
        {
            sb.Append('[').Append(tag.Name).Append(" \"").Append(Escape(tag.Value)).Append("\"]").Append('\n');
        }

        sb.Append('\n');
        sb.Append(WriteMoveText(state, header));
        sb.Append('\n');
        return sb.ToString();
    }

    /// <summary>Lit la première partie d'un texte PCN.</summary>
    public static PcnGame Parse(string text)
    {
        IReadOnlyList<PcnGame> games = ParseAll(text);
        return games.Count > 0 ? games[0] : throw new PcnException("Aucune partie trouvée dans ce PCN.");
    }

    /// <summary>Lit toutes les parties d'un texte PCN.</summary>
    public static IReadOnlyList<PcnGame> ParseAll(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var games = new List<PcnGame>();
        var builder = new GameBuilder();
        int index = 0;

        while (index < text.Length)
        {
            char c = text[index];

            if (char.IsWhiteSpace(c))
            {
                index++;
            }
            else if (c == '{')
            {
                index = SkipUntil(text, index + 1, '}');
            }
            else if (c == ';')
            {
                while (index < text.Length && text[index] != '\n')
                {
                    index++;
                }
            }
            else if (c == '(')
            {
                index = SkipVariation(text, index);
            }
            else if (c == '[')
            {
                // Une en-tête qui suit des coups ouvre la partie suivante.
                if (builder.HasMoveText)
                {
                    games.Add(builder.Build());
                    builder = new GameBuilder();
                }

                index = ReadTag(text, index, builder);
            }
            else
            {
                index = ReadMoveToken(text, index, builder);
            }
        }

        if (builder.HasContent)
        {
            games.Add(builder.Build());
        }

        return games;
    }

    private static List<PcnTag> BuildHeader(GameState state, IEnumerable<PcnTag>? tags)
    {
        List<PcnTag> given = tags?.ToList() ?? [];
        var header = new List<PcnTag>();

        foreach (string name in SevenTagRoster)
        {
            PcnTag? found = Find(given, name);
            header.Add(new PcnTag(name, found?.Value ?? DefaultTagValue(name, state)));
        }

        header.Add(new PcnTag("GameType", Find(given, "GameType")?.Value ?? ConnectFourGameType));

        Board start = state.StartingBoard;
        if (start.TotalDiscs > 0 || state.StartingSide != Player.Red)
        {
            header.Add(new PcnTag("SetUp", "1"));
            header.Add(new PcnTag("FEN", PcnFen.Write(start, state.StartingSide)));
        }

        // Les en-têtes supplémentaires du client viennent après les en-têtes normalisées.
        foreach (PcnTag tag in given)
        {
            if (!header.Any(h => string.Equals(h.Name, tag.Name, StringComparison.OrdinalIgnoreCase)))
            {
                header.Add(tag);
            }
        }

        return header;
    }

    private static string DefaultTagValue(string name, GameState state) => name switch
    {
        "Event" => "Partie amicale",
        "Site" => "?",
        "Date" => DateTime.Now.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture),
        "Round" => "-",
        "Red" or "Yellow" => "?",
        "Result" => ResultTag(state.Result()),
        _ => "?",
    };

    private static string WriteMoveText(GameState state, List<PcnTag> header)
    {
        var line = new StringBuilder();
        var sb = new StringBuilder();

        Player side = state.StartingSide;
        int moveNumber = 1;

        for (int ply = 0; ply < state.History.Count; ply++)
        {
            bool redToPlay = side == Player.Red;

            if (redToPlay || ply == 0)
            {
                Append(sb, line, redToPlay ? $"{moveNumber}." : $"{moveNumber}...");
            }

            Append(sb, line, state.History[ply].ToNotation());

            if (!redToPlay)
            {
                moveNumber++;
            }

            side = side.Opponent();
        }

        Append(sb, line, Find(header, "Result")?.Value ?? "*");

        if (line.Length > 0)
        {
            sb.Append(line);
        }

        return sb.ToString();
    }

    private static void Append(StringBuilder sb, StringBuilder line, string token)
    {
        if (line.Length > 0 && line.Length + 1 + token.Length > WrapColumn)
        {
            sb.Append(line).Append('\n');
            line.Clear();
        }

        if (line.Length > 0)
        {
            line.Append(' ');
        }

        line.Append(token);
    }

    private static int SkipUntil(string text, int index, char terminator)
    {
        while (index < text.Length && text[index] != terminator)
        {
            index++;
        }

        return Math.Min(index + 1, text.Length);
    }

    private static int SkipVariation(string text, int index)
    {
        int depth = 0;
        while (index < text.Length)
        {
            char c = text[index++];
            if (c == '(')
            {
                depth++;
            }
            else if (c == ')' && --depth == 0)
            {
                break;
            }
        }

        return index;
    }

    private static int ReadTag(string text, int index, GameBuilder builder)
    {
        int close = text.IndexOf(']', index);
        if (close < 0)
        {
            throw new PcnException("En-tête non terminée : il manque un « ] ».");
        }

        string body = text[(index + 1)..close].Trim();
        int quote = body.IndexOf('"');
        if (quote < 0)
        {
            throw new PcnException($"En-tête sans valeur entre guillemets : « [{body}] ».");
        }

        string name = body[..quote].Trim();
        string value = body[(quote + 1)..].TrimEnd();
        if (value.EndsWith('"'))
        {
            value = value[..^1];
        }

        builder.AddTag(new PcnTag(name, Unescape(value)));
        return close + 1;
    }

    private static int ReadMoveToken(string text, int index, GameBuilder builder)
    {
        int start = index;
        while (index < text.Length && !char.IsWhiteSpace(text[index]) && text[index] is not ('{' or '(' or ';' or '['))
        {
            index++;
        }

        string token = text[start..index];
        if (token.Length == 0)
        {
            return index + 1;
        }

        if (token.StartsWith('$'))
        {
            return index; // Annotation numérique, sans effet sur la partie.
        }

        if (IsResultToken(token))
        {
            builder.AddResult(ParseResult(token));
            return index;
        }

        // Un numéro de coup peut être collé au coup : « 1.4 ».
        int dot = token.IndexOf('.');
        if (dot > 0 && token[..dot].All(char.IsAsciiDigit))
        {
            token = token[dot..].TrimStart('.');
            if (token.Length == 0)
            {
                return index;
            }
        }

        // Un jeton de plusieurs chiffres est une suite de coups : c'est la forme nue, « 4453 ».
        foreach (char c in token)
        {
            builder.AddMove(c.ToString());
        }

        return index;
    }

    private static string ResultTag(GameResult result) => result switch
    {
        GameResult.RedWins => "1-0",
        GameResult.YellowWins => "0-1",
        GameResult.Draw => "1/2-1/2",
        _ => "*",
    };

    internal static GameResult ParseResult(string token) => token switch
    {
        "1-0" => GameResult.RedWins,
        "0-1" => GameResult.YellowWins,
        "1/2-1/2" or "1-1" => GameResult.Draw,
        _ => GameResult.InProgress,
    };

    internal static bool IsResultToken(string token) =>
        token is "1-0" or "0-1" or "1/2-1/2" or "1-1" or "*";

    private static PcnTag? Find(IEnumerable<PcnTag> tags, string name)
    {
        foreach (PcnTag tag in tags)
        {
            if (string.Equals(tag.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return tag;
            }
        }

        return null;
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string Unescape(string value) =>
        value.Replace("\\\"", "\"", StringComparison.Ordinal).Replace("\\\\", "\\", StringComparison.Ordinal);

    /// <summary>Accumule une partie au fil de la lecture et valide chaque coup contre les règles.</summary>
    private sealed class GameBuilder
    {
        private readonly List<PcnTag> _tags = [];
        private readonly List<Move> _moves = [];
        private GameState? _state;
        private GameResult _result = GameResult.InProgress;
        private (Board Board, Player SideToMove)? _setup;

        public bool HasMoveText { get; private set; }

        public bool HasContent => _tags.Count > 0 || HasMoveText;

        public void AddTag(PcnTag tag)
        {
            if (string.Equals(tag.Name, "GameType", StringComparison.OrdinalIgnoreCase)
                && tag.Value.Trim().Length > 0
                && !string.Equals(tag.Value.Trim(), ConnectFourGameType, StringComparison.OrdinalIgnoreCase))
            {
                throw new PcnException(
                    $"Ce PCN décrit un autre jeu (GameType « {tag.Value} »). " +
                    $"Seul « {ConnectFourGameType} » est pris en charge.");
            }

            _tags.Add(tag);
        }

        public void AddResult(GameResult result)
        {
            HasMoveText = true;
            _result = result;
        }

        public void AddMove(string token)
        {
            HasMoveText = true;

            GameState state = State();
            if (state.Result().IsOver())
            {
                throw new PcnException(
                    $"Coup {_moves.Count + 1} du PCN, « {token} » : la partie est déjà finie ({state.Result().ToFrench()}).");
            }

            if (!Notation.TryParse(token, state.LegalMoves(), out Move? move, out string? error))
            {
                throw new PcnException($"Coup {_moves.Count + 1} du PCN, « {token} » : {error}");
            }

            _moves.Add(move!.Value);
            state.MakeMove(move.Value);
        }

        public PcnGame Build()
        {
            GameState state = State();
            GameResult declared = _result != GameResult.InProgress ? _result : state.Result();
            (Board board, Player side) = Setup();
            return new PcnGame(_tags, board.Clone(), side, _moves, declared);
        }

        private GameState State()
        {
            if (_state is null)
            {
                (Board board, Player side) = Setup();
                _state = new GameState(board.Clone(), side);
            }

            return _state;
        }

        /// <summary>
        /// La position de départ, lue une seule fois : les en-têtes précèdent toujours les coups,
        /// donc le FEN éventuel est connu avant que le premier coup ne soit joué.
        /// </summary>
        private (Board Board, Player SideToMove) Setup()
        {
            if (_setup is null)
            {
                PcnTag? fen = Find(_tags, "FEN");
                _setup = fen is null
                    ? (new Board(), Player.Red)
                    : PcnFen.Parse(fen.Value.Value);
            }

            return _setup.Value;
        }
    }
}
