namespace ChanSight.Core.MetaInfo;

public sealed record BoardPlacement
{
    public const int MaxRow = 3;

    public const int MaxCol = 6;

    public BoardPlacement(string heroName, int row, int col)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfNegative(col);

        if (row > MaxRow)
        {
            throw new ArgumentOutOfRangeException(nameof(row), row, $"Row must be between 0 and {MaxRow}.");
        }

        if (col > MaxCol)
        {
            throw new ArgumentOutOfRangeException(nameof(col), col, $"Col must be between 0 and {MaxCol}.");
        }

        HeroName = heroName;
        Row = row;
        Col = col;
    }

    public string HeroName { get; init; }

    public int Row { get; init; }

    public int Col { get; init; }
}

public sealed record BoardLayout
{
    public IReadOnlyList<BoardPlacement> Placements { get; init; } = Array.Empty<BoardPlacement>();

    public string? PositioningNote { get; init; }
}
