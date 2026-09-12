using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;
using OpenCvSharp;

namespace ChanSight.Vision.Services;

public sealed class GridSlicerService : IGridSlicerService
{
    private readonly IRoiMapperService _roiMapper;

    public GridSlicerService(IRoiMapperService roiMapper)
    {
        _roiMapper = roiMapper ?? throw new ArgumentNullException(nameof(roiMapper));
    }

    public IReadOnlyList<BenchSlot> SliceBenchSlots(Mat frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var benchPhysicalRect = _roiMapper.MapToPhysical(
            CanonicalRoiDefinitions.BenchBounds,
            frame.Width,
            frame.Height);

        benchPhysicalRect = ClampRect(frame.Width, frame.Height, benchPhysicalRect);

        var canonicalCenters = CanonicalRoiDefinitions.GenerateBenchCenters();
        var results = new List<BenchSlot>(CanonicalRoiDefinitions.BenchSlotCount);

        for (int i = 0; i < CanonicalRoiDefinitions.BenchSlotCount; i++)
        {
            var physicalCenter = _roiMapper.MapPointToPhysical(
                canonicalCenters[i],
                frame.Width,
                frame.Height);

            double slotWidth = (double)benchPhysicalRect.Width / CanonicalRoiDefinitions.BenchSlotCount;
            int halfWidth = (int)Math.Round(slotWidth / 2.0);
            int halfHeight = benchPhysicalRect.Height / 2;

            int cx = (int)Math.Round(physicalCenter.X);
            int cy = (int)Math.Round(physicalCenter.Y);

            int cropX = Math.Max(0, cx - halfWidth);
            int cropY = Math.Max(0, cy - halfHeight);
            int cropW = Math.Min(halfWidth * 2, frame.Width - cropX);
            int cropH = Math.Min(halfHeight * 2, frame.Height - cropY);

            if (cropW < 1) cropW = 1;
            if (cropH < 1) cropH = 1;

            var cropRect = new Rect(cropX, cropY, cropW, cropH);
            Mat cellImage;

            try
            {
                cellImage = new Mat(frame, cropRect);
            }
            catch
            {
                cellImage = new Mat();
            }

            results.Add(new BenchSlot(i, physicalCenter, cropRect, cellImage));
        }

        return results;
    }

    public IReadOnlyList<BoardHexSlot> SliceBoardHexagons(Mat frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var results = new List<BoardHexSlot>(28);

        var physicalBoardRect = _roiMapper.MapToPhysical(
            CanonicalRoiDefinitions.BoardArea,
            frame.Width,
            frame.Height);

        double scaleX = (double)physicalBoardRect.Width / CanonicalRoiDefinitions.BoardArea.Width;
        double scaleY = (double)physicalBoardRect.Height / CanonicalRoiDefinitions.BoardArea.Height;
        double scale = Math.Min(scaleX, scaleY);

        double hexRadius = CanonicalRoiDefinitions.BoardHexRadius * scale;
        int halfCrop = (int)Math.Ceiling(hexRadius);
        int cropSize = halfCrop * 2;

        for (int row = 0; row < 4; row++)
        {
            for (int col = 0; col < 7; col++)
            {
                int index = CanonicalRoiDefinitions.GetBoardHexIndex(row, col);
                var canonicalCenter = CanonicalRoiDefinitions.BoardHexCenters[index];

                var physicalCenter = _roiMapper.MapPointToPhysical(
                    canonicalCenter,
                    frame.Width,
                    frame.Height);

                int cx = (int)Math.Round(physicalCenter.X);
                int cy = (int)Math.Round(physicalCenter.Y);

                int cropX = Math.Max(0, cx - halfCrop);
                int cropY = Math.Max(0, cy - halfCrop);
                int cropW = Math.Min(cropSize, frame.Width - cropX);
                int cropH = Math.Min(cropSize, frame.Height - cropY);

                if (cropW < 1) cropW = 1;
                if (cropH < 1) cropH = 1;

                var cropRect = new Rect(cropX, cropY, cropW, cropH);
                Mat cellImage;

                try
                {
                    cellImage = new Mat(frame, cropRect);
                }
                catch
                {
                    cellImage = new Mat();
                }

                results.Add(new BoardHexSlot(row, col, physicalCenter, cropRect, cellImage));
            }
        }

        return results;
    }

    public Mat? CropHexCell(Mat frame, int row, int col)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (row < 0 || row >= 4) throw new ArgumentOutOfRangeException(nameof(row));
        if (col < 0 || col >= 7) throw new ArgumentOutOfRangeException(nameof(col));

        int index = CanonicalRoiDefinitions.GetBoardHexIndex(row, col);
        var canonicalCenter = CanonicalRoiDefinitions.BoardHexCenters[index];

        var physicalBoardRect = _roiMapper.MapToPhysical(
            CanonicalRoiDefinitions.BoardArea,
            frame.Width,
            frame.Height);

        double scaleX = (double)physicalBoardRect.Width / CanonicalRoiDefinitions.BoardArea.Width;
        double scaleY = (double)physicalBoardRect.Height / CanonicalRoiDefinitions.BoardArea.Height;
        double scale = Math.Min(scaleX, scaleY);

        double hexRadius = CanonicalRoiDefinitions.BoardHexRadius * scale;
        int halfCrop = (int)Math.Ceiling(hexRadius);
        int cropSize = halfCrop * 2;

        var physicalCenter = _roiMapper.MapPointToPhysical(
            canonicalCenter,
            frame.Width,
            frame.Height);

        int cx = (int)Math.Round(physicalCenter.X);
        int cy = (int)Math.Round(physicalCenter.Y);

        int cropX = Math.Max(0, cx - halfCrop);
        int cropY = Math.Max(0, cy - halfCrop);
        int cropW = Math.Min(cropSize, frame.Width - cropX);
        int cropH = Math.Min(cropSize, frame.Height - cropY);

        if (cropW < 1 || cropH < 1)
            return null;

        var cropRect = new Rect(cropX, cropY, cropW, cropH);

        try
        {
            return new Mat(frame, cropRect);
        }
        catch
        {
            return null;
        }
    }

    private static Rect ClampRect(int frameWidth, int frameHeight, Rect rect)
    {
        int x = Math.Max(0, rect.X);
        int y = Math.Max(0, rect.Y);
        int w = Math.Min(rect.Width, frameWidth - x);
        int h = Math.Min(rect.Height, frameHeight - y);

        if (w < 1) w = 1;
        if (h < 1) h = 1;

        return new Rect(x, y, w, h);
    }
}