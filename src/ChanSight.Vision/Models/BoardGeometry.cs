using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChanSight.Vision.Models;

public sealed record BoardCellPoint(
    [property: JsonPropertyName("row")] int Row,
    [property: JsonPropertyName("col")] int Col,
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y,
    [property: JsonPropertyName("nx")] double Nx,
    [property: JsonPropertyName("ny")] double Ny);

public sealed record BenchCellPoint(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y,
    [property: JsonPropertyName("nx")] double Nx,
    [property: JsonPropertyName("ny")] double Ny);

public sealed record ShopCellPoint(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y,
    [property: JsonPropertyName("nx")] double Nx,
    [property: JsonPropertyName("ny")] double Ny);

public sealed record BoardGeometry
{
    [JsonPropertyName("version")] public int Version { get; init; } = 1;
    [JsonPropertyName("source")] public string Source { get; init; } = "community-template+vision-QA 2026-09-12";
    [JsonPropertyName("canonicalWidth")] public int CanonicalWidth { get; init; } = 1920;
    [JsonPropertyName("canonicalHeight")] public int CanonicalHeight { get; init; } = 1080;
    [JsonPropertyName("boardCells")] public IReadOnlyList<BoardCellPoint> BoardCells { get; init; } = Array.Empty<BoardCellPoint>();
    [JsonPropertyName("benchCells")] public IReadOnlyList<BenchCellPoint> BenchCells { get; init; } = Array.Empty<BenchCellPoint>();
    [JsonPropertyName("shopCells")] public IReadOnlyList<ShopCellPoint> ShopCells { get; init; } = Array.Empty<ShopCellPoint>();
    [JsonPropertyName("rowPitchY")] public double RowPitchY { get; init; }
    [JsonPropertyName("colPitchX")] public double ColPitchX { get; init; }
    [JsonPropertyName("staggerX")] public double StaggerX { get; init; }

    public BoardGeometry()
    {
    }

    public static BoardGeometry CreateCanonical()
    {
        var board = new List<BoardCellPoint>(28);

        for (var row = 0; row < 4; row++)
        {
            for (var col = 0; col < 7; col++)
            {
                var (x, y) = (row, col) switch
                {
                    (0, 0) => (633.74, 444.75), (0, 1) => (736.19, 444.75), (0, 2) => (838.65, 448.5),
                    (0, 3) => (936.51, 446.25), (0, 4) => (1039.62, 445.5), (0, 5) => (1140.76, 442.5),
                    (0, 6) => (1241.25, 442.5),
                    (1, 0) => (681.53, 519.75), (1, 1) => (786.62, 518.25), (1, 2) => (891.70, 519.0),
                    (1, 3) => (996.13, 519.0), (1, 4) => (1099.24, 516.75), (1, 5) => (1206.95, 517.5),
                    (1, 6) => (1308.75, 513.75),
                    (2, 0) => (611.77, 592.5), (2, 1) => (720.13, 594.0), (2, 2) => (828.50, 591.75),
                    (2, 3) => (936.87, 591.0), (2, 4) => (1043.92, 591.75), (2, 5) => (1155.57, 591.75),
                    (2, 6) => (1260.00, 591.75),
                    (3, 0) => (663.03, 672.0), (3, 1) => (776.65, 675.75), (3, 2) => (888.96, 675.75),
                    (3, 3) => (1001.27, 675.75), (3, 4) => (1114.23, 672.75), (3, 5) => (1221.94, 674.25),
                    (3, 6) => (1334.25, 675.0),
                    _ => throw new InvalidOperationException("Unexpected board cell.")
                };

                board.Add(new BoardCellPoint(row, col, x, y, x / 1920.0, y / 1080.0));
            }
        }

        var bench = new List<BenchCellPoint>(9);
        var benchValues = new (double X, double Y)[]
        {
            (527.25, 784.0), (630.28, 784.0), (733.31, 784.0), (836.34, 784.0), (939.37, 784.0),
            (1042.40, 784.0), (1145.43, 784.0), (1248.47, 784.0), (1351.50, 784.0)
        };
        for (var i = 0; i < benchValues.Length; i++)
        {
            var (x, y) = benchValues[i];
            bench.Add(new BenchCellPoint(i, x, y, x / 1920.0, y / 1080.0));
        }

        var shop = new List<ShopCellPoint>(5);
        for (var i = 0; i < 5; i++)
        {
            var x = 350.0 + 130.0 * i;
            const double y = 915.0;
            shop.Add(new ShopCellPoint(i, x, y, x / 1920.0, y / 1080.0));
        }

        return new BoardGeometry
        {
            Version = 1,
            Source = "community-template+vision-QA 2026-09-12",
            CanonicalWidth = 1920,
            CanonicalHeight = 1080,
            BoardCells = board,
            BenchCells = bench,
            ShopCells = shop,
            RowPitchY = ComputeRowPitchY(board),
            ColPitchX = ComputeColPitchX(board),
            StaggerX = ComputeStaggerX(board)
        };
    }

    public static BoardGeometry FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("json is null or empty", nameof(json));

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var geometry = JsonSerializer.Deserialize<BoardGeometry>(json, options)
            ?? throw new InvalidOperationException("Failed to deserialize BoardGeometry.");

        if (geometry.BoardCells.Count != 28)
            throw new InvalidOperationException($"BoardCells must contain 28 entries, got {geometry.BoardCells.Count}.");
        if (geometry.BenchCells.Count != 9)
            throw new InvalidOperationException($"BenchCells must contain 9 entries, got {geometry.BenchCells.Count}.");
        if (geometry.ShopCells.Count != 5)
            throw new InvalidOperationException($"ShopCells must contain 5 entries, got {geometry.ShopCells.Count}.");

        var boardKeys = new HashSet<(int Row, int Col)>();
        foreach (var cell in geometry.BoardCells)
        {
            if (cell.Row is < 0 or > 3 || cell.Col is < 0 or > 6)
                throw new InvalidOperationException($"Board cell index out of range: r={cell.Row}, c={cell.Col}.");
            if (!boardKeys.Add((cell.Row, cell.Col)))
                throw new InvalidOperationException($"Duplicate board cell: r={cell.Row}, c={cell.Col}.");
        }

        var benchIndexes = new HashSet<int>();
        foreach (var cell in geometry.BenchCells)
        {
            if (cell.Index is < 0 or > 8)
                throw new InvalidOperationException($"Bench cell index out of range: {cell.Index}.");
            if (!benchIndexes.Add(cell.Index))
                throw new InvalidOperationException($"Duplicate bench cell: {cell.Index}.");
        }

        var shopIndexes = new HashSet<int>();
        foreach (var cell in geometry.ShopCells)
        {
            if (cell.Index is < 0 or > 4)
                throw new InvalidOperationException($"Shop cell index out of range: {cell.Index}.");
            if (!shopIndexes.Add(cell.Index))
                throw new InvalidOperationException($"Duplicate shop cell: {cell.Index}.");
        }

        return geometry;
    }

    private static double ComputeRowPitchY(IReadOnlyList<BoardCellPoint> cells)
    {
        double sum = 0;
        for (var r = 0; r < 3; r++)
        {
            double yA = 0, yB = 0;
            for (var c = 0; c < 7; c++)
            {
                yA += cells[r * 7 + c].Y;
                yB += cells[(r + 1) * 7 + c].Y;
            }

            sum += (yB - yA) / 7.0;
        }

        return sum / 3.0;
    }

    private static double ComputeColPitchX(IReadOnlyList<BoardCellPoint> cells)
    {
        double sum = 0;
        var count = 0;
        for (var r = 0; r < 4; r++)
        {
            for (var c = 0; c < 6; c++)
            {
                sum += cells[r * 7 + c + 1].X - cells[r * 7 + c].X;
                count++;
            }
        }

        return sum / count;
    }

    private static double ComputeStaggerX(IReadOnlyList<BoardCellPoint> cells)
    {
        double sum = 0;
        for (var r = 0; r < 3; r++)
        {
            sum += Math.Abs(cells[(r + 1) * 7].X - cells[r * 7].X);
        }

        return sum / 3.0;
    }
}