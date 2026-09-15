using OpenCvSharp;

namespace ChanSight.Vision.Models;

/// <summary>
/// Canonical 1920x1080 ROI definitions. Board/bench centers measured 2026-09-12
/// (community template + real-frame vision QA verification, docs/qa_runs/a2/template_calibration.md).
/// </summary>
public static class CanonicalRoiDefinitions
{
    public const int CanonicalWidth = 1920;
    public const int CanonicalHeight = 1080;

    // Measured 2026-09-12.
    public const double HexRowPitchY = 76.0;
    public const double HexColPitchX = 130.0;
    public const double HexRowOffsetX = 48.0;
    public const double BoardHexRadius = 60.0;

    public const int BenchSlotCount = 9;
    public const int ShopSlotCount = 5;

    public static readonly Rect BoardArea = new Rect(475, 445, 905, 267);
    public static readonly Rect BenchBounds = new Rect(365, 749, 1031, 68);
    public static readonly Rect ShopBounds = new Rect(285, 865, 650, 100);

    public static readonly IReadOnlyDictionary<RoiRegionType, Rect> Regions = new Dictionary<RoiRegionType, Rect>
    {
        [RoiRegionType.BoardArea] = BoardArea,
        [RoiRegionType.PlayerBench] = BenchBounds,
        [RoiRegionType.ShopCards] = ShopBounds,
        [RoiRegionType.Gold] = new Rect(870, 30, 80, 35),
        [RoiRegionType.Level] = new Rect(870, 70, 80, 35),
        [RoiRegionType.Hp] = new Rect(10, 20, 80, 35),
        [RoiRegionType.StageRound] = new Rect(840, 5, 100, 30),
        [RoiRegionType.OpponentsSidebar] = new Rect(1170, 110, 350, 750),
        [RoiRegionType.ActiveTraits] = new Rect(5, 360, 150, 520),
    };

    private static readonly Point2d[] BoardHexCentersTable =
    {
        new Point2d(547.5, 444.75), new Point2d(664.5, 444.75), new Point2d(781.5, 448.5),
        new Point2d(893.25, 446.25), new Point2d(1011.0, 445.5), new Point2d(1126.5, 442.5),
        new Point2d(1241.25, 442.5),
        new Point2d(592.5, 519.75), new Point2d(712.5, 518.25), new Point2d(832.5, 519.0),
        new Point2d(951.75, 519.0), new Point2d(1069.5, 516.75), new Point2d(1192.5, 517.5),
        new Point2d(1308.75, 513.75),
        new Point2d(519.75, 592.5), new Point2d(643.5, 594.0), new Point2d(767.25, 591.75),
        new Point2d(891.0, 591.0), new Point2d(1013.25, 591.75), new Point2d(1140.75, 591.75),
        new Point2d(1260.0, 591.75),
        new Point2d(567.75, 672.0), new Point2d(697.5, 675.75), new Point2d(825.75, 675.75),
        new Point2d(954.0, 675.75), new Point2d(1083.0, 672.75), new Point2d(1206.0, 674.25),
        new Point2d(1334.25, 675.0),
    };

    public static IReadOnlyList<Point2d> BoardHexCenters => BoardHexCentersTable;

    public static int GetBoardHexIndex(int row, int col)
    {
        if (row < 0 || row > 3) throw new ArgumentOutOfRangeException(nameof(row));
        if (col < 0 || col > 6) throw new ArgumentOutOfRangeException(nameof(col));
        return row * 7 + col;
    }

    public static Rect GetCanonicalRect(RoiRegionType regionType) { return Regions[regionType]; }

    public static IReadOnlyList<Point2d> GenerateBenchCenters()
    {
        return new List<Point2d>
        {
            new Point2d(527.25, 784.0), new Point2d(630.28, 784.0), new Point2d(733.31, 784.0),
            new Point2d(836.34, 784.0), new Point2d(939.37, 784.0), new Point2d(1042.40, 784.0),
            new Point2d(1145.43, 784.0), new Point2d(1248.47, 784.0), new Point2d(1351.50, 784.0),
        };
    }

    public static IReadOnlyList<Point2d> GenerateShopCenters()
    {
        var centers = new List<Point2d>(ShopSlotCount);
        double slotWidth = (double)ShopBounds.Width / ShopSlotCount;

        for (int i = 0; i < ShopSlotCount; i++)
        {
            double cx = ShopBounds.X + slotWidth * (i + 0.5);
            double cy = ShopBounds.Y + ShopBounds.Height / 2.0;
            centers.Add(new Point2d(cx, cy));
        }

        return centers;
    }
}