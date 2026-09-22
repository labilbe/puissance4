using System.Diagnostics;

namespace Puissance4.Core;

/// <summary>Résultat d'une recherche : le coup retenu et les statistiques associées.</summary>
public sealed record SearchResult(
    Move? BestMove,
    int Score,
    int Depth,
    long Nodes,
    TimeSpan Elapsed,
    IReadOnlyList<Move> PrincipalVariation);

/// <summary>
/// Moteur de recherche : negamax avec élagage alpha-bêta, approfondissement itératif,
/// table de transposition, coups tueurs et exploration des colonnes du centre vers les
/// bords — une colonne centrale participe à sept alignements, une colonne de bord à trois.
/// </summary>
public sealed class SearchEngine
{
    private const int TableSizeBits = 20;
    private const int TableSize = 1 << TableSizeBits;
    private const int MaxPly = Grid.CellCount + 1;
    private const int Infinity = Evaluator.WinScore * 2;

    private readonly SearchOptions _options;
    private readonly Random _random;
    private readonly TranspositionEntry[] _table = new TranspositionEntry[TableSize];
    private readonly Move?[,] _killers = new Move?[MaxPly, 2];

    private Stopwatch _clock = new();
    private CancellationToken _cancellation;
    private long _nodes;
    private bool _aborted;

    public SearchEngine(Difficulty difficulty)
        : this(SearchOptions.For(difficulty))
    {
    }

    public SearchEngine(SearchOptions options)
    {
        _options = options;
        _random = options.RandomSeed is int seed ? new Random(seed) : new Random();
    }

    public SearchOptions Options => _options;

    /// <summary>Cherche le meilleur coup pour le camp au trait.</summary>
    public SearchResult Search(GameState state, CancellationToken cancellationToken = default)
    {
        _cancellation = cancellationToken;
        _nodes = 0;
        _aborted = false;
        _clock = Stopwatch.StartNew();
        Array.Clear(_killers);

        List<Move> rootMoves = state.LegalMoves();
        if (rootMoves.Count == 0)
        {
            return new SearchResult(null, -Evaluator.WinScore, 0, 0, _clock.Elapsed, []);
        }

        if (rootMoves.Count == 1)
        {
            return new SearchResult(rootMoves[0], 0, 1, 1, _clock.Elapsed, [rootMoves[0]]);
        }

        // Un alignement à portée de main se joue sans réfléchir, même au niveau facile :
        // une IA qui laisse passer le gain qu'elle a sous les yeux n'est pas faible, elle est cassée.
        if (MoveGenerator.WinningMove(state.Board, state.SideToMove) is Move winner)
        {
            return new SearchResult(winner, Evaluator.WinScore - 1, 1, 1, _clock.Elapsed, [winner]);
        }

        bool fullWindowRoot = _options.Randomness > 0;
        var ordered = new List<Move>(rootMoves);
        List<(Move Move, int Score)> bestScores = [];
        int completedDepth = 0;

        for (int depth = 1; depth <= _options.MaxDepth; depth++)
        {
            int alpha = -Infinity;
            var iteration = new List<(Move Move, int Score)>(ordered.Count);

            foreach (Move move in ordered)
            {
                state.MakeMove(move);
                int score = -Negamax(state, depth - 1, -Infinity, fullWindowRoot ? Infinity : -alpha, 1);
                state.UnmakeMove();

                if (_aborted)
                {
                    break;
                }

                iteration.Add((move, score));
                if (score > alpha)
                {
                    alpha = score;
                }
            }

            if (iteration.Count > 0)
            {
                // Le premier coup exploré est le meilleur de l'itération précédente :
                // une itération partielle ne peut donc que confirmer ou améliorer le choix.
                iteration.Sort((a, b) => b.Score.CompareTo(a.Score));
                bestScores = iteration;
                ordered = [.. iteration.Select(x => x.Move), .. ordered.Where(m => !iteration.Any(x => x.Move == m))];
                if (!_aborted)
                {
                    completedDepth = depth;
                }
            }

            if (_aborted || _clock.Elapsed >= _options.TimeLimit)
            {
                break;
            }

            if (Math.Abs(bestScores[0].Score) > Evaluator.WinScore - MaxPly)
            {
                break; // Gain ou perte forcés : chercher plus loin n'apprendrait rien.
            }
        }

        (Move chosen, int chosenScore) = Choose(bestScores);
        return new SearchResult(chosen, chosenScore, completedDepth, _nodes, _clock.Elapsed, ExtractPrincipalVariation(state, chosen));
    }

    private (Move Move, int Score) Choose(List<(Move Move, int Score)> scored)
    {
        if (_options.Randomness <= 0 || scored.Count == 1)
        {
            return scored[0];
        }

        int threshold = scored[0].Score - _options.Randomness;
        List<(Move Move, int Score)> candidates = [.. scored.Where(x => x.Score >= threshold)];
        return candidates[_random.Next(candidates.Count)];
    }

    private int Negamax(GameState state, int depth, int alpha, int beta, int ply)
    {
        if (_aborted)
        {
            return 0;
        }

        if ((++_nodes & 1023) == 0 && (_clock.Elapsed >= _options.TimeLimit || _cancellation.IsCancellationRequested))
        {
            _aborted = true;
            return 0;
        }

        // Le coup précédent vient d'aligner quatre jetons : le camp au trait a perdu.
        if (state.Alignment is not null)
        {
            return -Evaluator.WinScore + ply;
        }

        if (state.Board.IsFull)
        {
            return 0;
        }

        // Gagner tout de suite court-circuite tout le reste, y compris l'horizon de la
        // recherche : s'arrêter juste avant un alignement disponible donnerait un score faux.
        if (MoveGenerator.WinningMove(state.Board, state.SideToMove) is not null)
        {
            return Evaluator.WinScore - (ply + 1);
        }

        if (depth <= 0)
        {
            return Evaluator.Evaluate(state.Board, state.SideToMove);
        }

        ulong hash = state.Hash;
        int slot = (int)(hash & (TableSize - 1));
        Move? tableMove = null;
        int alphaOrigin = alpha;

        ref TranspositionEntry entry = ref _table[slot];
        if (entry.Hash == hash && entry.Best is not null)
        {
            tableMove = entry.Best;
            if (entry.Depth >= depth)
            {
                switch (entry.Flag)
                {
                    case NodeFlag.Exact:
                        return entry.Score;
                    case NodeFlag.LowerBound when entry.Score > alpha:
                        alpha = entry.Score;
                        break;
                    case NodeFlag.UpperBound when entry.Score < beta:
                        beta = entry.Score;
                        break;
                }

                if (alpha >= beta)
                {
                    return entry.Score;
                }
            }
        }

        List<Move> moves = MoveGenerator.Generate(state.Board);
        OrderMoves(moves, tableMove, ply);

        int best = -Infinity;
        Move? bestMove = null;

        foreach (Move move in moves)
        {
            state.MakeMove(move);
            int score = -Negamax(state, depth - 1, -beta, -alpha, ply + 1);
            state.UnmakeMove();

            if (_aborted)
            {
                return 0;
            }

            if (score > best)
            {
                best = score;
                bestMove = move;
            }

            if (best > alpha)
            {
                alpha = best;
            }

            if (alpha >= beta)
            {
                StoreKiller(move, ply);
                break;
            }
        }

        if (entry.Hash != hash || entry.Depth <= depth)
        {
            entry.Hash = hash;
            entry.Depth = depth;
            entry.Score = best;
            entry.Best = bestMove;
            entry.Flag = best <= alphaOrigin ? NodeFlag.UpperBound
                : best >= beta ? NodeFlag.LowerBound
                : NodeFlag.Exact;
        }

        return best;
    }

    /// <summary>
    /// Les coups sont déjà rangés du centre vers les bords ; on remonte devant eux le coup
    /// retenu par la table de transposition, puis les coups tueurs.
    /// </summary>
    private void OrderMoves(List<Move> moves, Move? tableMove, int ply)
    {
        Move? killer0 = ply < MaxPly ? _killers[ply, 0] : null;
        Move? killer1 = ply < MaxPly ? _killers[ply, 1] : null;

        moves.Sort((a, b) => Rank(b).CompareTo(Rank(a)));

        int Rank(Move move)
        {
            if (tableMove == move)
            {
                return 1_000_000;
            }

            if (killer0 == move)
            {
                return 900;
            }

            if (killer1 == move)
            {
                return 890;
            }

            // À défaut, la colonne la plus centrale d'abord.
            return Grid.Columns - Math.Abs(move.Column - (Grid.Columns / 2));
        }
    }

    private void StoreKiller(Move move, int ply)
    {
        if (ply >= MaxPly || _killers[ply, 0] == move)
        {
            return;
        }

        _killers[ply, 1] = _killers[ply, 0];
        _killers[ply, 0] = move;
    }

    private List<Move> ExtractPrincipalVariation(GameState state, Move first)
    {
        var line = new List<Move>();
        int applied = 0;

        Move? next = first;
        while (next is Move move && line.Count < Grid.CellCount)
        {
            if (!state.LegalMoves().Contains(move))
            {
                break;
            }

            line.Add(move);
            state.MakeMove(move);
            applied++;

            ref TranspositionEntry entry = ref _table[(int)(state.Hash & (TableSize - 1))];
            next = entry.Hash == state.Hash ? entry.Best : null;
        }

        for (int i = 0; i < applied; i++)
        {
            state.UnmakeMove();
        }

        return line;
    }

    private enum NodeFlag : byte
    {
        Exact,
        LowerBound,
        UpperBound,
    }

    private struct TranspositionEntry
    {
        public ulong Hash;
        public int Score;
        public int Depth;
        public Move? Best;
        public NodeFlag Flag;
    }
}
