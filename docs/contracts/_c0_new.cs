using System;
using System.Collections.Generic;
using OpenCvSharp;

namespace ChanSight.Vision.Models
{
    /// <summary>
    /// Canonical 1920x1080 ROI definitions for the puppet board geometry.
    /// Board/bench centers measured 2026-09-12 (normalized -> canonical rebase).
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

        public static readonly IReadOnlyDictionary<string, Rect> Regions =
            new Dictionary<string, Rect>
            {
                ["Board"] = BoardArea,
                ["PlayerBench"] = BenchBounds,
                ["Shop"] = ShopBounds,
                ["Gold"] = new Rect(0, 0, 0, 0),
                ["Level"] = new Rect(0, 0, 0, 0),
                ["Hp"] = new Rect(0, 0, 0, 0),
                ["StageRound"] = new Rect(0, 0, 0, 0),
                ["OpponentsSidebar"] = new Rect(0, 0, 0, 0),
                ["ActiveTraits"] = new Rect(0, 0, 0, 0),
                ["ShopCards"] = new Rect(0, 0, 0, 0),
            };

        private static readonly Point2d[] BoardHexCentersTable =
        {
            // r0
            new Point2d(547.5, 444.75),
            new Point2d(664.5, 444.75),
            new Point2d(781.5, 448.5),
            new Point2d(893.25, 446.25),
            new Point2d(1011.0, 445.5),
            new Point2d(1126.5, 442.5),
            new Point2d(1241.25, 442.5),
            // r1
            new Point2d(592.5, 519.75),
            new Point2d(712.5, 518.25),
            new Point2d(832.5, 519.0),
            new Point2d(951.75, 519.0),
            new Point2d(1069.5, 516.75),
            new Point2d(1192.5, 517.5),
            new Point2d(1308.75, 513.75),
            // r2
            new Point2d(519.75, 592.5),
            new Point2d(643.5, 594.0),
            new Point2d(767.25, 591.75),
            new Point2d(891.0, 591.0),
            new Point2d(1013.25, 591.75),
            new Point2d(1140.75, 591.75),
            new Point2d(1260.0, 591.75),
            // r3
            new Point2d(567.75, 672.0),
            new Point2d(697.5, 675.75),
            new Point2d(825.75, 675.75),
            new Point2d(954.0, 675.75),
            new Point2d(1083.0, 672.75),
            new Point2d(1206.0, 674.25),
            new Point2d(1334.25, 675.0),
        };

        public static IReadOnlyList<Point2d> BoardHexCenters => BoardHexCentersTable;

        public static int GetBoardHexIndex(int row, int col)
        {
            if (row < 0 || row > 3) throw new ArgumentOutOfRangeException(nameof(row));
            if (col < 0 || col > 6) throw new ArgumentOutOfRangeException(nameof(col));
            return row * 7 + col;
        }

        public static Rect GetCanonicalRect(string regionName)
        {
            if (regionName == null) throw new ArgumentNullException(nameof(regionName));
            if (!Regions.TryGetValue(regionName, out var rect))
                throw new ArgumentException($"Unknown region: {regionName}", nameof(regionName));
            return rect;
        }

        public static IReadOnlyList<Point2d> GenerateBenchCenters()
        {
            var centers = new List<Point2d>(BenchSlotCount);
            double startX = 410.25;
            double endX = 1351.5;
            double y = 782.25;
            double step = (endX - startX) / (BenchSlotCount - 1);
            for (int i = 0; i < BenchSlotCount; i++)
            {
                centers.Add(new Point2d(startX + step * i, y));
            }
            return centers;
        }

        public static IReadOnlyList<Point2d> GenerateShopCenters()
        {
            var centers = new List<Point2d>(ShopSlotCount);
            double startX = 350.0;
            double endX = 870.0;
            double y = 915.0;
            double step = (endX - startX) / (ShopSlotCount - 1);
            for (int i = 0; i < ShopSlotCount; i++)
            {
                centers.Add(new Point2d(startX + step * i, y));
            }
            return centers;
        }
    }
}