namespace ChanSight.Vision.Models;

/// <summary>
/// Validates a <see cref="RecognitionFrame"/> against the V0 contract and returns a list of errors
/// (empty when valid). Uses fixed counts (28 board / 9 bench / 5 shop) and hard field ranges.
/// </summary>
public static class RecognitionFrameSchemaValidator
{
    public const int BoardCellCount = 28;
    public const int BenchCellCount = 9;
    public const int ShopCardCount = 5;

    public static IReadOnlyList<string> Validate(RecognitionFrame frame)
    {
        var errors = new List<string>();

        if (frame is null)
        {
            return new[] { "RecognitionFrame is null." };
        }

        ValidateCounts(frame, errors);
        ValidateDeterministicFields(frame, errors);
        ValidateBoardCells(frame, errors);
        ValidateBenchCells(frame, errors);
        ValidateShopCards(frame, errors);

        return errors;
    }

    private static void ValidateCounts(RecognitionFrame frame, List<string> errors)
    {
        if (frame.BoardCells is null || frame.BoardCells.Count != BoardCellCount)
            errors.Add($"boardCells must contain exactly {BoardCellCount} entries, got {frame.BoardCells?.Count ?? 0}.");

        if (frame.BenchCells is null || frame.BenchCells.Count != BenchCellCount)
            errors.Add($"benchCells must contain exactly {BenchCellCount} entries, got {frame.BenchCells?.Count ?? 0}.");

        if (frame.ShopCards is null || frame.ShopCards.Count != ShopCardCount)
            errors.Add($"shopCards must contain exactly {ShopCardCount} entries, got {frame.ShopCards?.Count ?? 0}.");
    }

    private static void ValidateDeterministicFields(RecognitionFrame frame, List<string> errors)
    {
        if (frame.Gold < 0)
            errors.Add($"gold must be >= 0, got {frame.Gold}.");
        if (frame.Level is < 0 or > 9)
            errors.Add($"level must be in [0,9], got {frame.Level}.");
        if (string.IsNullOrWhiteSpace(frame.Stage))
            errors.Add("stage must be a non-empty string.");
        if (frame.Hp < 0)
            errors.Add($"hp must be >= 0, got {frame.Hp}.");
        if (frame.Exp < 0)
            errors.Add($"exp must be >= 0, got {frame.Exp}.");
        if (frame.Confidence < 0 || frame.Confidence > 1)
            errors.Add($"frame confidence must be in [0,1], got {frame.Confidence}.");
    }

    private static void ValidateBoardCells(RecognitionFrame frame, List<string> errors)
    {
        for (var i = 0; i < frame.BoardCells.Count; i++)
            ValidateUnitCell(frame.BoardCells[i], $"boardCells[{i}]", errors);
    }

    private static void ValidateBenchCells(RecognitionFrame frame, List<string> errors)
    {
        for (var i = 0; i < frame.BenchCells.Count; i++)
            ValidateUnitCell(frame.BenchCells[i], $"benchCells[{i}]", errors);
    }

    private static void ValidateUnitCell(UnitCell cell, string path, List<string> errors)
    {
        if (cell.Star is < 0 or > 3)
            errors.Add($"{path}.star must be in [0,3], got {cell.Star}.");
        if (cell.Confidence < 0 || cell.Confidence > 1)
            errors.Add($"{path}.confidence must be in [0,1], got {cell.Confidence}.");
        if (cell.Items is not null)
        {
            for (var i = 0; i < cell.Items.Count; i++)
            {
                var item = cell.Items[i];
                if (string.IsNullOrWhiteSpace(item.IconId))
                    errors.Add($"{path}.items[{i}].iconId must be a non-empty string.");
                if (item.Count < 0)
                    errors.Add($"{path}.items[{i}].count must be >= 0, got {item.Count}.");
            }
        }
    }

    private static void ValidateShopCards(RecognitionFrame frame, List<string> errors)
    {
        for (var i = 0; i < frame.ShopCards.Count; i++)
        {
            var card = frame.ShopCards[i];
            if (string.IsNullOrWhiteSpace(card.Name))
                errors.Add($"shopCards[{i}].name must be a non-empty string.");
            if (card.Cost < 0)
                errors.Add($"shopCards[{i}].cost must be >= 0, got {card.Cost}.");
            if (card.Confidence < 0 || card.Confidence > 1)
                errors.Add($"shopCards[{i}].confidence must be in [0,1], got {card.Confidence}.");
        }
    }
}