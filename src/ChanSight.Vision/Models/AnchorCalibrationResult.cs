namespace ChanSight.Vision.Models;

using OpenCvSharp;

public sealed record AnchorCalibrationResult
{
    public bool IsValid { get; init; }
    public double Confidence { get; init; }
    public double ScaleX { get; init; }
    public double ScaleY { get; init; }
    public double OffsetX { get; init; }
    public double OffsetY { get; init; }
    public double MaxReprojectionErrorPx { get; init; }
    public IReadOnlyList<Point2d> ObservedAnchorPoints { get; init; } = Array.Empty<Point2d>();

    public Point2d MapCanonical(Point2d canonical) =>
        IsValid
            ? new Point2d(canonical.X * ScaleX + OffsetX, canonical.Y * ScaleY + OffsetY)
            : throw new InvalidOperationException("Cannot map canonical coordinates with an invalid calibration.");
}