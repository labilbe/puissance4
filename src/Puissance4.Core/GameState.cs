namespace Puissance4.Core;

/// <summary>
/// L'état complet d'une partie : la grille, le trait, l'historique des coups et
/// l'alignement gagnant s'il y en a un. Optimisé pour un usage en recherche via
/// <see cref="MakeMove"/> / <see cref="UnmakeMove"/>.
/// </summary>
public sealed class GameState
{
    private readonly Stack<UndoInfo> _undo = new();
    private readonly List<Move> _played = [];
    private readonly Board _startingBoard;
    private ulong _hash;

    public GameState()
        : this(new Board(), Player.Red)
    {
    }

    public GameState(Board board, Player sideToMove)
    {
        Board = board;
        SideToMove = sideToMove;
        StartingSide = sideToMove;
        _startingBoard = board.Clone();
        _hash = Zobrist.Compute(board, sideToMove);
        Alignment = FindAlignment(board);
    }

    public Board Board { get; }

    /// <summary>La grille telle qu'elle était au premier coup de la partie.</summary>
    public Board StartingBoard => _startingBoard.Clone();

    /// <summary>Le camp qui avait le trait au début de la partie.</summary>
    public Player StartingSide { get; }

    /// <summary>Les coups joués depuis le début de la partie, dans l'ordre.</summary>
    public IReadOnlyList<Move> History => _played;

    public Player SideToMove { get; private set; }

    public ulong Hash => _hash;

    /// <summary>Nombre de coups joués depuis le début de la partie.</summary>
    public int PlyCount => _undo.Count;

    /// <summary>Les cases de l'alignement gagnant, ou <c>null</c> tant que personne n'a gagné.</summary>
    public IReadOnlyList<int>? Alignment { get; private set; }

    /// <summary>Le camp qui a gagné, ou <c>null</c>.</summary>
    public Player? Winner => Alignment is null ? null : Board[Alignment[0]].Owner();

    /// <summary>La case atteinte par le dernier jeton posé, ou <c>null</c> avant le premier coup.</summary>
    public int? LastCell { get; private set; }

    public List<Move> LegalMoves() =>
        Alignment is null ? MoveGenerator.Generate(Board) : [];

    public GameState Clone()
    {
        var copy = new GameState(Board.Clone(), SideToMove);
        return copy;
    }

    /// <summary>Lâche un jeton et passe le trait.</summary>
    public void MakeMove(Move move)
    {
        int index = Board.Drop(move.Column, SideToMove);

        _hash ^= Zobrist.Key(index, Board[index]);
        _hash ^= Zobrist.YellowToMove;

        _undo.Push(new UndoInfo(move, index, Alignment, LastCell));
        _played.Add(move);

        Alignment = Board.AlignmentThrough(index);
        LastCell = index;
        SideToMove = SideToMove.Opponent();
    }

    /// <summary>Annule le dernier coup joué.</summary>
    public void UnmakeMove()
    {
        UndoInfo undo = _undo.Pop();
        _played.RemoveAt(_played.Count - 1);

        _hash ^= Zobrist.Key(undo.Index, Board[undo.Index]);
        _hash ^= Zobrist.YellowToMove;

        Board.Lift(undo.Move.Column);

        Alignment = undo.PreviousAlignment;
        LastCell = undo.PreviousLastCell;
        SideToMove = SideToMove.Opponent();
    }

    /// <summary>
    /// Le résultat de la partie dans la position courante. Il n'y a ni nulle par répétition
    /// ni règle des cinquante coups : chaque coup ajoute un jeton, la partie dure au plus
    /// quarante-deux coups et s'arrête sur un alignement ou sur une grille pleine.
    /// </summary>
    public GameResult Result()
    {
        if (Winner is Player winner)
        {
            return winner.Wins();
        }

        return Board.IsFull ? GameResult.Draw : GameResult.InProgress;
    }

    /// <summary>Cherche un alignement déjà présent dans une grille, par exemple à l'ouverture d'une partie.</summary>
    private static IReadOnlyList<int>? FindAlignment(Board board)
    {
        for (int index = 0; index < Grid.CellCount; index++)
        {
            if (board[index].IsEmpty())
            {
                continue;
            }

            if (board.AlignmentThrough(index) is IReadOnlyList<int> line)
            {
                return line;
            }
        }

        return null;
    }

    private readonly record struct UndoInfo(
        Move Move,
        int Index,
        IReadOnlyList<int>? PreviousAlignment,
        int? PreviousLastCell);
}
