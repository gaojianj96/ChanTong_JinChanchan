using ChanSight.Vision.Models;
using OpenCvSharp;

namespace ChanSight.Vision.Services;

/// <summary>
/// Frame-difference change detector over already-calibrated, fixed cells.
/// Cell indices are laid out row-major: board 0-27, bench 28-36, shop 37-41.
/// </summary>
public sealed class CellChangeDetector
{
    public const double DefaultThreshold = 10.0;

    // Fraction of the neighbouring-cell pitch used to carve each cell's local
    // region (the "cave" around the cell centre). 0.8 leaves a ~20% gutter so
    // neighbouring regions never overlap and a bright patch in one cell cannot
    // bleed into the region of an adjacent cell.
    private const double RegionFraction = 0.8;

    private readonly double _threshold;
    private readonly BoardGeometry _geometry;

    public CellChangeDetector(double threshold = DefaultThreshold)
    {
        if (threshold < 0)
            throw new ArgumentOutOfRangeException(nameof(threshold), "Threshold must be non-negative.");

        _threshold = threshold;
        _geometry = BoardGeometry.CreateCanonical();
    }

    public IReadOnlyList<int> DetectChangedCells(Mat previous, Mat current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        if (previous.Width <= 0 || previous.Height <= 0)
            throw new ArgumentException("previous must have positive dimensions.", nameof(previous));
        if (current.Width != previous.Width || current.Height != previous.Height)
            throw new ArgumentException("previous and current must have identical dimensions.", nameof(current));

        using var prevGray = ToGray(previous);
        using var currGray = ToGray(current);
        using var diff = new Mat();
        Cv2.Absdiff(prevGray, currGray, diff);

        double scaleX = (double)previous.Width / _geometry.CanonicalWidth;
        double scaleY = (double)previous.Height / _geometry.CanonicalHeight;

        // Board cells: width from adjacent-column pitch, height from row pitch.
        double boardHalfWidth = _geometry.ColPitchX * scaleX * RegionFraction / 2.0;
        double boardHalfHeight = _geometry.RowPitchY * scaleY * RegionFraction / 2.0;

        // Bench/shop are single rows with no vertical neighbour, so fall back to
        // their own adjacent-slot spacing for the vertical extent (square region).
        double benchPitch = (_geometry.BenchCells[1].X - _geometry.BenchCells[0].X) * scaleX;
        double shopPitch = (_geometry.ShopCells[1].X - _geometry.ShopCells[0].X) * scaleX;
        double benchHalf = benchPitch * RegionFraction / 2.0;
        double shopHalf = shopPitch * RegionFraction / 2.0;

        var changed = new List<int>();
        int flatIndex = 0;

        foreach (var cell in _geometry.BoardCells)
        {
            if (IsRegionChanged(diff, cell.X * scaleX, cell.Y * scaleY, boardHalfWidth, boardHalfHeight))
                changed.Add(flatIndex);
            flatIndex++;
        }

        foreach (var cell in _geometry.BenchCells)
        {
            if (IsRegionChanged(diff, cell.X * scaleX, cell.Y * scaleY, benchHalf, benchHalf))
                changed.Add(flatIndex);
            flatIndex++;
        }

        foreach (var cell in _geometry.ShopCells)
        {
            if (IsRegionChanged(diff, cell.X * scaleX, cell.Y * scaleY, shopHalf, shopHalf))
                changed.Add(flatIndex);
            flatIndex++;
        }

        return changed;
    }

    private bool IsRegionChanged(Mat diff, double centerX, double centerY, double halfWidth, double halfHeight)
    {
        var rect = BuildRect(diff.Width, diff.Height, centerX, centerY, halfWidth, halfHeight);
        if (rect.Width <= 0 || rect.Height <= 0)
            return false;

        using var roi = new Mat(diff, rect);
        return Cv2.Mean(roi).Val0 > _threshold;
    }

    private static Rect BuildRect(int frameWidth, int frameHeight, double centerX, double centerY, double halfWidth, double halfHeight)
    {
        int cx = (int)Math.Round(centerX);
        int cy = (int)Math.Round(centerY);
        int halfW = Math.Max(1, (int)Math.Round(halfWidth));
        int halfH = Math.Max(1, (int)Math.Round(halfHeight));

        int x = Math.Max(0, Math.Min(frameWidth - 1, cx - halfW));
        int y = Math.Max(0, Math.Min(frameHeight - 1, cy - halfH));
        int w = Math.Min(halfW * 2, frameWidth - x);
        int h = Math.Min(halfH * 2, frameHeight - y);

        return new Rect(x, y, w, h);
    }

    private static Mat ToGray(Mat src)
    {
        if (src.Channels() == 1)
            return src.Clone();

        var gray = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        return gray;
    }
}