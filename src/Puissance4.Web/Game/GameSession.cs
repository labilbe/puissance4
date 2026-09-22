using Puissance4.Core;
using Puissance4.Core.Pcn;

namespace Puissance4.Web.Game;

/// <summary>
/// Le pilote de la partie côté interface : il tient l'état du jeu, le relevé des coups et
/// la chute des jetons. Toute la logique de règles vient de Puissance4.Core.
/// </summary>
public sealed class GameSession
{
    private const int ReleaseDelay = 30;
    private const int FallMilliseconds = 340;
    private const int ThinkingPaintDelay = 60;

    private readonly List<DiscView> _discs = [];
    private readonly List<MoveRecord> _history = [];

    private GameState _state = new();
    private SearchEngine _engine;
    private int _nextDiscId;

    /// <summary>Vrai tant qu'une boucle de jeu de l'ordinateur tourne : il ne doit y en avoir qu'une.</summary>
    private bool _computerBusy;

    /// <summary>Incrémenté à chaque remise à zéro, pour qu'une boucle en cours abandonne la partie abandonnée.</summary>
    private int _generation;

    public GameSession()
    {
        _engine = CreateEngine(Difficulty.Medium);
        Reset();
    }

    /// <summary>Signale à l'interface qu'il faut redessiner.</summary>
    public event Action? Changed;

    public IReadOnlyList<DiscView> Discs => _discs;

    public IReadOnlyList<MoveRecord> History => _history;

    public Player SideToMove => _state.SideToMove;

    public GameResult Result { get; private set; } = GameResult.InProgress;

    public Seat RedSeat { get; private set; } = Seat.Human;

    public Seat YellowSeat { get; private set; } = Seat.Computer;

    public Difficulty Difficulty { get; private set; } = Difficulty.Medium;

    public bool ShowNumbers { get; set; } = true;

    /// <summary>Vrai si la partie a commencé par un coup des Jaunes, ce qui décale le relevé.</summary>
    public bool OpensWithYellow => _state.StartingSide == Player.Yellow;

    public bool IsThinking { get; private set; }

    public bool IsAnimating { get; private set; }

    public bool Busy => IsThinking || IsAnimating;

    /// <summary>La case atteinte par le dernier jeton posé, pour la signaler sur la grille.</summary>
    public int? LastCell => _state.LastCell;

    public int RedCount => _state.Board.CountOf(Player.Red);

    public int YellowCount => _state.Board.CountOf(Player.Yellow);

    public bool CanUndo => _state.PlyCount > 0 && !Busy;

    public Seat SeatOf(Player player) => player == Player.Red ? RedSeat : YellowSeat;

    public bool IsHumanTurn => !Result.IsOver() && SeatOf(SideToMove) == Seat.Human;

    /// <summary>Vrai si le joueur peut lâcher un jeton dans cette colonne maintenant.</summary>
    public bool CanPlay(int column) => IsHumanTurn && !Busy && _state.Board.CanPlay(column);

    /// <summary>Rangée où se poserait un jeton lâché dans cette colonne, -1 si elle est pleine.</summary>
    public int LandingRow(int column) => _state.Board.LandingRow(column);

    /// <summary>
    /// Le camp qui menace de gagner au coup suivant, s'il y en a un : on l'annonce, plutôt
    /// que de laisser le joueur découvrir l'alignement une fois qu'il est fait.
    /// </summary>
    public bool IsUnderThreat =>
        !Result.IsOver() && MoveGenerator.WinningMove(_state.Board, SideToMove.Opponent()) is not null;

    public async Task NewGameAsync()
    {
        _generation++;
        Reset();
        Changed?.Invoke();
        await RunComputerAsync();
    }

    /// <summary>La partie en cours au format PCN, prête à être copiée ou enregistrée.</summary>
    public string ToPcn() => PcnFile.Write(_state, [
        new PcnTag("Event", "Partie amicale"),
        new PcnTag("Site", "Puissance 4, dans le navigateur"),
        new PcnTag("Red", SeatName(Player.Red)),
        new PcnTag("Yellow", SeatName(Player.Yellow)),
    ]);

    /// <summary>
    /// Remplace la partie en cours par celle décrite en PCN. En cas de texte invalide,
    /// la partie en cours n'est pas touchée et le message d'erreur explique où ça coince.
    /// </summary>
    public async Task<string?> ImportPcnAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "Collez d'abord un PCN.";
        }

        PcnGame game;
        try
        {
            game = PcnFile.Parse(text);
        }
        catch (PcnException error)
        {
            return error.Message;
        }
        catch (ArgumentException error)
        {
            return error.Message;
        }

        _generation++;
        Load(game.ToGameState());
        Changed?.Invoke();
        await RunComputerAsync();
        return null;
    }

    private string SeatName(Player player) =>
        SeatOf(player) == Seat.Human ? "Humain" : $"Ordinateur ({SearchOptions.ToFrench(Difficulty)})";

    public async Task SetSeatAsync(Player player, Seat seat)
    {
        if (player == Player.Red)
        {
            RedSeat = seat;
        }
        else
        {
            YellowSeat = seat;
        }

        Changed?.Invoke();
        await RunComputerAsync();
    }

    public async Task SetDifficultyAsync(Difficulty difficulty)
    {
        Difficulty = difficulty;
        _engine = CreateEngine(difficulty);
        Changed?.Invoke();
        await Task.CompletedTask;
    }

    /// <summary>Annule le dernier coup humain, et la réponse de l'ordinateur s'il y en a une.</summary>
    public void Undo()
    {
        if (Busy || _state.PlyCount == 0)
        {
            return;
        }

        _generation++;
        int steps = RedSeat == YellowSeat ? 1 : 2;
        for (int i = 0; i < steps && _state.PlyCount > 0; i++)
        {
            _state.UnmakeMove();
            _history.RemoveAt(_history.Count - 1);
        }

        RebuildDiscs();
        Refresh();
        Changed?.Invoke();
    }

    /// <summary>Traite un clic sur une colonne.</summary>
    public async Task ClickAsync(int column)
    {
        if (!CanPlay(column))
        {
            return;
        }

        await PlayAsync(new Move(column));
        await RunComputerAsync();
    }

    private static SearchEngine CreateEngine(Difficulty difficulty)
    {
        // Le navigateur n'a qu'un fil d'exécution : on raccourcit la réflexion
        // pour que l'interface ne reste jamais figée plus de deux secondes.
        TimeSpan limit = difficulty switch
        {
            Difficulty.Easy => TimeSpan.FromMilliseconds(120),
            Difficulty.Medium => TimeSpan.FromMilliseconds(400),
            Difficulty.Hard => TimeSpan.FromMilliseconds(1000),
            _ => TimeSpan.FromMilliseconds(2000),
        };

        return new SearchEngine(SearchOptions.For(difficulty) with { TimeLimit = limit });
    }

    private void Reset() => Load(new GameState());

    /// <summary>Prend une partie comme état courant et reconstruit tout ce que l'affichage en tire.</summary>
    private void Load(GameState state)
    {
        _state = state;

        _history.Clear();
        Player side = state.StartingSide;
        foreach (Move move in state.History)
        {
            _history.Add(new MoveRecord(side, move.ToNotation()));
            side = side.Opponent();
        }

        RebuildDiscs();
        Refresh();
    }

    private void RebuildDiscs()
    {
        _discs.Clear();
        _nextDiscId = 0;

        for (int index = 0; index < Grid.CellCount; index++)
        {
            Cell cell = _state.Board[index];
            if (cell.IsEmpty())
            {
                continue;
            }

            _discs.Add(new DiscView
            {
                Id = _nextDiscId++,
                Owner = cell.Owner(),
                Col = Grid.Col(index),
                Row = Grid.Row(index),
            });
        }
    }

    private void Refresh()
    {
        Result = _state.Result();

        var winning = _state.Alignment?.ToHashSet() ?? [];
        foreach (DiscView disc in _discs)
        {
            disc.Winning = winning.Contains(Grid.Index(disc.Row, disc.Col));
        }
    }

    private async Task PlayAsync(Move move)
    {
        _history.Add(new MoveRecord(SideToMove, move.ToNotation()));

        var disc = new DiscView
        {
            Id = _nextDiscId++,
            Owner = SideToMove,
            Col = move.Column,
            Row = DiscView.AboveTheGrid,
        };

        int landingRow = _state.Board.LandingRow(move.Column);
        _state.MakeMove(move);

        IsAnimating = true;
        _discs.Add(disc);

        // Le jeton est d'abord dessiné au-dessus de la grille, puis déplacé sur sa case :
        // c'est ce second rendu qui déclenche la transition, donc la chute.
        Changed?.Invoke();
        await Task.Delay(ReleaseDelay);

        disc.Row = landingRow;
        Changed?.Invoke();
        await Task.Delay(FallMilliseconds);

        IsAnimating = false;
        Refresh();
        Changed?.Invoke();
    }

    /// <summary>
    /// Fait jouer l'ordinateur tant que c'est à lui. Une seule boucle tourne à la fois : changer
    /// de camp en cours de partie ne doit pas en démarrer une seconde, la boucle en place voit le
    /// changement d'elle-même. Une remise à zéro la fait abandonner, puis en relance une propre.
    /// </summary>
    private async Task RunComputerAsync()
    {
        if (_computerBusy)
        {
            return;
        }

        _computerBusy = true;
        int generation = _generation;

        try
        {
            while (generation == _generation && !Result.IsOver() && SeatOf(SideToMove) == Seat.Computer)
            {
                IsThinking = true;
                Changed?.Invoke();

                // Laisse le navigateur peindre l'état « réfléchit » avant de bloquer le fil sur la recherche.
                await Task.Delay(ThinkingPaintDelay);

                SearchResult result = _engine.Search(_state);
                IsThinking = false;

                if (generation != _generation || result.BestMove is not Move move)
                {
                    Changed?.Invoke();
                    break;
                }

                await PlayAsync(move);
            }
        }
        finally
        {
            _computerBusy = false;
        }

        if (generation != _generation)
        {
            await RunComputerAsync();
        }
    }
}
