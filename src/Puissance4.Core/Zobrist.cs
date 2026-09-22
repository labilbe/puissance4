namespace Puissance4.Core;

/// <summary>Clés de hachage Zobrist, utilisées par la table de transposition de la recherche.</summary>
internal static class Zobrist
{
    private const int CellKinds = 3; // Vide + les deux couleurs

    private static readonly ulong[,] CellKeys = new ulong[Grid.CellCount, CellKinds];

    internal static readonly ulong YellowToMove;

    static Zobrist()
    {
        var rng = new Random(20260922);
        Span<byte> buffer = stackalloc byte[8];

        for (int cell = 0; cell < Grid.CellCount; cell++)
        {
            for (int kind = 1; kind < CellKinds; kind++)
            {
                rng.NextBytes(buffer);
                CellKeys[cell, kind] = BitConverter.ToUInt64(buffer);
            }
        }

        rng.NextBytes(buffer);
        YellowToMove = BitConverter.ToUInt64(buffer);
    }

    internal static ulong Key(int cell, Cell content) =>
        content == Cell.Empty ? 0UL : CellKeys[cell, (int)content];

    internal static ulong Compute(Board board, Player sideToMove)
    {
        ulong hash = 0;
        for (int index = 0; index < Grid.CellCount; index++)
        {
            hash ^= Key(index, board[index]);
        }

        if (sideToMove == Player.Yellow)
        {
            hash ^= YellowToMove;
        }

        return hash;
    }
}
