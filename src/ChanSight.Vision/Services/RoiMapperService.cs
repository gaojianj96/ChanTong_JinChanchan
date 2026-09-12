using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;
using OpenCvSharp;

namespace ChanSight.Vision.Services;

public sealed class RoiMapperService : IRoiMapperService
{
    public Rect MapToPhysical(Rect canonicalRect, int frameWidth, int frameHeight)
    {
        if (frameWidth <= 0) throw new ArgumentOutOfRangeException(nameof(frameWidth));
        if (frameHeight <= 0) throw new ArgumentOutOfRangeException(nameof(frameHeight));

        double scaleX = (double)frameWidth / CanonicalRoiDefinitions.CanonicalWidth;
        double scaleY = (double)frameHeight / CanonicalRoiDefinitions.CanonicalHeight;
        double scale = Math.Min(scaleX, scaleY);

        double scaledContentWidth = CanonicalRoiDefinitions.CanonicalWidth * scale;
        double scaledContentHeight = CanonicalRoiDefinitions.CanonicalHeight * scale;
        double offsetX = (frameWidth - scaledContentWidth) / 2.0;
        double offsetY = (frameHeight - scaledContentHeight) / 2.0;

        int physicalX = (int)Math.Round(canonicalRect.X * scale + offsetX);
        int physicalY = (int)Math.Round(canonicalRect.Y * scale + offsetY);
        int physicalWidth = (int)Math.Round(canonicalRect.Width * scale);
        int physicalHeight = (int)Math.Round(canonicalRect.Height * scale);

        physicalX = Math.Max(0, physicalX);
        physicalY = Math.Max(0, physicalY);
        physicalWidth = Math.Min(physicalWidth, frameWidth - physicalX);
        physicalHeight = Math.Min(physicalHeight, frameHeight - physicalY);

        if (physicalWidth < 1) physicalWidth = 1;
        if (physicalHeight < 1) physicalHeight = 1;

        return new Rect(physicalX, physicalY, physicalWidth, physicalHeight);
    }

    public Point2d MapPointToPhysical(Point2d canonicalPoint, int frameWidth, int frameHeight)
    {
        if (frameWidth <= 0) throw new ArgumentOutOfRangeException(nameof(frameWidth));
        if (frameHeight <= 0) throw new ArgumentOutOfRangeException(nameof(frameHeight));

        double scaleX = (double)frameWidth / CanonicalRoiDefinitions.CanonicalWidth;
        double scaleY = (double)frameHeight / CanonicalRoiDefinitions.CanonicalHeight;
        double scale = Math.Min(scaleX, scaleY);

        double scaledContentWidth = CanonicalRoiDefinitions.CanonicalWidth * scale;
        double scaledContentHeight = CanonicalRoiDefinitions.CanonicalHeight * scale;
        double offsetX = (frameWidth - scaledContentWidth) / 2.0;
        double offsetY = (frameHeight - scaledContentHeight) / 2.0;

        double physicalX = canonicalPoint.X * scale + offsetX;
        double physicalY = canonicalPoint.Y * scale + offsetY;

        return new Point2d(physicalX, physicalY);
    }

    public Mat CropRoi(Mat frame, RoiRegionType regionType)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var canonicalRect = CanonicalRoiDefinitions.GetCanonicalRect(regionType);
        var physicalRect = MapToPhysical(canonicalRect, frame.Width, frame.Height);

        physicalRect = ClampRect(frame.Width, frame.Height, physicalRect);

        if (physicalRect.Width < 1 || physicalRect.Height < 1)
            return new Mat();

        return new Mat(frame, physicalRect);
    }

    public IReadOnlyList<Mat> CropShopSlots(Mat frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var shopPhysicalRect = MapToPhysical(
            CanonicalRoiDefinitions.ShopBounds,
            frame.Width,
            frame.Height);

        shopPhysicalRect = ClampRect(frame.Width, frame.Height, shopPhysicalRect);

        if (shopPhysicalRect.Width < 1 || shopPhysicalRect.Height < 1)
            return Array.Empty<Mat>();

        int slotCount = CanonicalRoiDefinitions.ShopSlotCount;
        double slotWidth = (double)shopPhysicalRect.Width / slotCount;
        var results = new List<Mat>(slotCount);

        for (int i = 0; i < slotCount; i++)
        {
            int slotX = shopPhysicalRect.X + (int)Math.Round(slotWidth * i);
            int slotWidthInt = (int)Math.Round(slotWidth);

            slotX = Math.Max(0, slotX);
            slotWidthInt = Math.Min(slotWidthInt, frame.Width - slotX);

            if (slotWidthInt < 1)
            {
                results.Add(new Mat());
                continue;
            }

            var slotRect = new Rect(slotX, shopPhysicalRect.Y, slotWidthInt, shopPhysicalRect.Height);
            results.Add(new Mat(frame, slotRect));
        }

        return results;
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