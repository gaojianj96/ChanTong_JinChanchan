namespace ChanSight.Vision.Models;

using OpenCvSharp;

public sealed record AnchorCalibrationOptions
{
    public int CanonicalWidth { get; init; } = 1920;
    public int CanonicalHeight { get; init; } = 1080;
    public double MaxReprojectionErrorPx { get; init; } = 2.0;
    public IReadOnlyList<AnchorPointPair>? AnchorPairs { get; init; }
}

public sealed record AnchorPointPair(Point2d Canonical, Point2d Observed);