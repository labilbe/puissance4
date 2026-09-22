using Puissance4.Core;

namespace Puissance4.Core.Tests;

public class GridTests
{
    [Fact]
    public void FromTheCentre_GoesFromTheMiddleColumnOutwards()
    {
        Assert.Equal([3, 2, 4, 1, 5, 0, 6], Grid.FromTheCentre);
    }

    [Theory]
    [InlineData(0, 0, 3)]
    [InlineData(5, 3, 7)]
    [InlineData(2, 3, 13)]
    public void AlignmentsThrough_CountsTheWindowsCoveringACell(int row, int col, int expected)
    {
        // Le coin en tient trois, le milieu du fond sept, le centre de la grille treize :
        // c'est ce déséquilibre qui fait jouer au centre.
        Assert.Equal(expected, Grid.AlignmentsThrough(row, col));
    }

    [Fact]
    public void AlignmentsThrough_SummedOverTheGrid_CountsEveryWindowFourTimes()
    {
        int total = 0;
        for (int row = 0; row < Grid.Rows; row++)
        {
            for (int col = 0; col < Grid.Columns; col++)
            {
                total += Grid.AlignmentsThrough(row, col);
            }
        }

        Assert.Equal(69, Evaluator.WindowCount);
        Assert.Equal(69 * Grid.WinLength, total);
    }

    [Fact]
    public void ColumnOf_RejectsANumberOutsideTheGrid()
    {
        Assert.Equal(0, Grid.ColumnOf(1));
        Assert.Equal(6, Grid.ColumnOf(7));
        Assert.Throws<ArgumentOutOfRangeException>(() => Grid.ColumnOf(8));
    }
}
