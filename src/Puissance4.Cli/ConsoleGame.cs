using Puissance4.Core;
using Puissance4.Core.Pcn;

namespace Puissance4.Cli;

/// <summary>Déroulement d'une partie dans le terminal.</summary>
public sealed class ConsoleGame
{
    private const string DefaultPcnFile = "partie.pcn";

    private readonly GameSetup _setup;
    private readonly SearchEngine _engine;
    private readonly List<string> _history = [];
    private GameState _state = new();

    public ConsoleGame(GameSetup setup)
    {
        _setup = setup;
        _engine = new SearchEngine(setup.Difficulty);

        if (setup.PcnToOpen is string path)
        {
            OpenPcn(path);
        }
    }

    public void Run()
    {
        PrintIntroduction();

        while (true)
        {
            Draw();

            GameResult result = _state.Result();
            if (result.IsOver())
            {
                Console.WriteLine($"Fin de la partie : {result.ToFrench()}.");
                return;
            }

            bool keepPlaying = _setup.SeatOf(_state.SideToMove) == Seat.Human
                ? PlayHumanTurn()
                : PlayComputerTurn();

            if (!keepPlaying)
            {
                Console.WriteLine("Partie interrompue.");
                return;
            }
        }
    }

    private void PrintIntroduction()
    {
        Console.WriteLine("Puissance 4 — grille de 7 colonnes sur 6, quatre jetons alignés l'emportent.");
        Console.WriteLine("On joue en indiquant une colonne, de 1 à 7 : le jeton tombe au fond.");
        Console.WriteLine("Commandes : 'coups' liste les colonnes jouables, 'annuler' revient en arrière, 'quitter' abandonne.");
        Console.WriteLine($"            'enregistrer [fichier]' et 'ouvrir <fichier>' échangent la partie en PCN (défaut : {DefaultPcnFile}).");
        Console.WriteLine($"Niveau de l'ordinateur : {SearchOptions.ToFrench(_setup.Difficulty)}.");
        Console.WriteLine();
    }

    private void Draw()
    {
        Console.WriteLine(_state.Board.Render(_setup.ShowNumbers));
        Console.WriteLine("  R = jeton rouge   J = jeton jaune");
        Console.WriteLine($"  Rouges : {_state.Board.CountOf(Player.Red)} jetons   |   " +
                          $"Jaunes : {_state.Board.CountOf(Player.Yellow)} jetons");

        if (_history.Count > 0)
        {
            Console.WriteLine($"  Dernier coup : {_history[^1]}");
        }

        Console.WriteLine();
    }

    private bool PlayHumanTurn()
    {
        List<Move> legal = _state.LegalMoves();
        string camp = _state.SideToMove.ToFrench();

        while (true)
        {
            Console.Write($"{camp}, votre colonne : ");
            string? input = Console.ReadLine();

            if (input is null)
            {
                return false;
            }

            string trimmed = input.Trim();
            string verb = trimmed.Split(' ', 2)[0].ToLowerInvariant();
            string argument = trimmed.Contains(' ') ? trimmed[(trimmed.IndexOf(' ') + 1)..].Trim() : string.Empty;

            switch (verb)
            {
                case "":
                    continue;
                case "quitter" or "q":
                    return false;
                case "coups" or "?":
                    Console.WriteLine("  " + string.Join("   ", legal.OrderBy(m => m.Column)
                        .Select(m => Notation.Describe(_state.Board, _state.SideToMove, m))));
                    continue;
                case "annuler" or "u":
                    Undo();
                    return true;
                case "enregistrer":
                    SavePcn(argument.Length > 0 ? argument : DefaultPcnFile);
                    continue;
                case "ouvrir":
                    if (argument.Length == 0)
                    {
                        Console.WriteLine($"  Indiquez le fichier à ouvrir, par exemple : ouvrir {DefaultPcnFile}");
                        continue;
                    }

                    if (OpenPcn(argument))
                    {
                        return true;
                    }

                    continue;
            }

            if (!Notation.TryParse(trimmed, legal, out Move? move, out string? error))
            {
                Console.WriteLine($"  {error}");
                continue;
            }

            Apply(move!.Value);
            return true;
        }
    }

    private bool PlayComputerTurn()
    {
        string camp = _state.SideToMove.ToFrench();
        Console.Write($"{camp} (ordinateur) réfléchit… ");

        SearchResult result = _engine.Search(_state);
        if (result.BestMove is not Move move)
        {
            Console.WriteLine();
            return true;
        }

        Console.WriteLine($"{Notation.Describe(_state.Board, _state.SideToMove, move)}  " +
                          $"[profondeur {result.Depth}, {result.Nodes:N0} positions, {result.Elapsed.TotalSeconds:F1} s, " +
                          $"évaluation {FormatScore(result.Score)}]");
        Console.WriteLine();

        Apply(move);
        return true;
    }

    private void Apply(Move move)
    {
        string camp = _state.SideToMove.ToFrench();
        _history.Add($"{camp} {Notation.Describe(_state.Board, _state.SideToMove, move)}");
        _state.MakeMove(move);
    }

    /// <summary>Écrit la partie en cours dans un fichier PCN.</summary>
    private void SavePcn(string path)
    {
        try
        {
            File.WriteAllText(path, PcnFile.Write(_state, [
                new PcnTag("Site", "Puissance 4, en console"),
                new PcnTag("Red", NameOf(Player.Red)),
                new PcnTag("Yellow", NameOf(Player.Yellow)),
            ]));

            Console.WriteLine($"  Partie enregistrée dans {Path.GetFullPath(path)}");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"  Impossible d'écrire {path} : {error.Message}");
        }
    }

    /// <summary>
    /// Remplace la partie en cours par celle d'un fichier PCN. Tant que le fichier n'est pas
    /// entièrement relu et validé, la partie en cours reste intacte.
    /// </summary>
    private bool OpenPcn(string path)
    {
        try
        {
            PcnGame game = PcnFile.Parse(File.ReadAllText(path));
            GameState loaded = game.ToGameState();

            _state = loaded;
            RebuildHistory();

            string who = game.Tag("Event") is string name and not "?" ? $"« {name} », " : string.Empty;
            Console.WriteLine($"  Partie ouverte : {who}{loaded.PlyCount} coups rejoués.");
            return true;
        }
        catch (Exception error) when (error is PcnException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            Console.WriteLine($"  Impossible d'ouvrir {path} : {error.Message}");
            return false;
        }
    }

    private void RebuildHistory()
    {
        _history.Clear();
        var replay = new GameState(_state.StartingBoard, _state.StartingSide);

        foreach (Move move in _state.History)
        {
            _history.Add($"{replay.SideToMove.ToFrench()} {Notation.Describe(replay.Board, replay.SideToMove, move)}");
            replay.MakeMove(move);
        }
    }

    private string NameOf(Player player) =>
        _setup.SeatOf(player) == Seat.Human
            ? "Humain"
            : $"Ordinateur ({SearchOptions.ToFrench(_setup.Difficulty)})";

    /// <summary>Annule le dernier coup humain, et le coup de l'ordinateur qui le précède le cas échéant.</summary>
    private void Undo()
    {
        int steps = _setup.Red == Seat.Human && _setup.Yellow == Seat.Human ? 1 : 2;
        int done = 0;

        while (done < steps && _state.PlyCount > 0)
        {
            _state.UnmakeMove();
            _history.RemoveAt(_history.Count - 1);
            done++;
        }

        Console.WriteLine(done == 0 ? "  Rien à annuler." : $"  {done} coup(s) annulé(s).");
    }

    private static string FormatScore(int score)
    {
        if (Math.Abs(score) > Evaluator.WinScore - 1000)
        {
            return score > 0 ? "gain forcé" : "perte forcée";
        }

        return $"{score:+0;-0;0} points";
    }
}
