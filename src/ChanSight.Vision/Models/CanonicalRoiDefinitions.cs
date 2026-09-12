using OpenCvSharp;

namespace ChanSight.Vision.Models;

public static class CanonicalRoiDefinitions
{
    public const int CanonicalWidth = 1920;
    public const int CanonicalHeight = 1080;

    public static readonly IReadOnlyDictionary<RoiRegionType, Rect> Regions = new Dictionary<RoiRegionType, Rect>
    {
        [RoiRegionType.BoardArea] = new Rect(165, 235, 990, 690),
        [RoiRegionType.PlayerBench] = new Rect(300, 930, 660, 60),
        [RoiRegionType.ShopCards] = new Rect(285, 865, 650, 100),
        [RoiRegionType.Gold] = new Rect(870, 30, 80, 35),
        [RoiRegionType.Level] = new Rect(870, 70, 80, 35),
        [RoiRegionType.Hp] = new Rect(10, 20, 80, 35),
        [RoiRegionType.StageRound] = new Rect(840, 5, 100, 30),
        [RoiRegionType.OpponentsSidebar] = new Rect(1170, 110, 350, 750),
        [RoiRegionType.ActiveTraits] = new Rect(5, 360, 150, 520),
    };

    public static readonly Rect BoardArea = new Rect(165, 235, 990, 690);

    public static readonly IReadOnlyList<Point2d> BoardHexCenters = GenerateBoardHexCenters();

    public const double BoardHexRadius = 60.0;

    public const double HexRowPitchY = 145.0;
    public const double HexColPitchX = 130.0;
    public const double HexRowOffsetX = 65.0;

    public static readonly Rect BenchBounds = new Rect(300, 930, 660, 60);

    public static readonly int BenchSlotCount = 9;

    public static readonly Rect ShopBounds = new Rect(285, 865, 650, 100);

    public static readonly int ShopSlotCount = 5;

    private static IReadOnlyList<Point2d> GenerateBoardHexCenters()
    {
        var centers = new List<Point2d>(28);

        double startX = BoardArea.X + HexColPitchX / 2.0;
        double startY = BoardArea.Y + HexRowPitchY / 2.0;

        for (int row = 0; row < 4; row++)
        {
            double rowOffsetX = (row % 2 == 1) ? HexRowOffsetX : 0;

            for (int col = 0; col < 7; col++)
            {
                double cx = startX + rowOffsetX + col * HexColPitchX;
                double cy = startY + row * HexRowPitchY;
                centers.Add(new Point2d(cx, cy));
            }
        }

        return centers;
    }

    public static IReadOnlyList<Point2d> GenerateBenchCenters()
    {
        var centers = new List<Point2d>(BenchSlotCount);
        double slotWidth = (double)BenchBounds.Width / BenchSlotCount;

        for (int i = 0; i < BenchSlotCount; i++)
        {
            double cx = BenchBounds.X + slotWidth * (i + 0.5);
            double cy = BenchBounds.Y + BenchBounds.Height / 2.0;
            centers.Add(new Point2d(cx, cy));
        }

        return centers;
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

    public static int GetBoardHexIndex(int row, int col)
    {
        if (row < 0 || row >= 4) throw new ArgumentOutOfRangeException(nameof(row));
        if (col < 0 || col >= 7) throw new ArgumentOutOfRangeException(nameof(col));
        return row * 7 + col;
    }

    public static Rect GetCanonicalRect(RoiRegionType regionType)
    {
        return Regions[regionType];
    }
}