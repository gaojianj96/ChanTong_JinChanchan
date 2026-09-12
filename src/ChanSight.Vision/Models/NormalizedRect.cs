namespace ChanSight.Vision.Models;

public sealed record NormalizedRect
{
    public double XRatio { get; init; }
    public double YRatio { get; init; }
    public double WidthRatio { get; init; }
    public double HeightRatio { get; init; }

    public NormalizedRect(double xRatio, double yRatio, double widthRatio, double heightRatio)
    {
        if (xRatio < 0 || xRatio > 1) throw new ArgumentOutOfRangeException(nameof(xRatio));
        if (yRatio < 0 || yRatio > 1) throw new ArgumentOutOfRangeException(nameof(yRatio));
        if (widthRatio <= 0 || widthRatio > 1) throw new ArgumentOutOfRangeException(nameof(widthRatio));
        if (heightRatio <= 0 || heightRatio > 1) throw new ArgumentOutOfRangeException(nameof(heightRatio));
        if (xRatio + widthRatio > 1) throw new ArgumentOutOfRangeException(nameof(widthRatio));
        if (yRatio + heightRatio > 1) throw new ArgumentOutOfRangeException(nameof(heightRatio));

        XRatio = xRatio;
        YRatio = yRatio;
        WidthRatio = widthRatio;
        HeightRatio = heightRatio;
    }

    public static NormalizedRect FromCanonical(int x, int y, int width, int height)
    {
        const double canonicalWidth = 1920.0;
        const double canonicalHeight = 1080.0;

        return new NormalizedRect(
            x / canonicalWidth,
            y / canonicalHeight,
            width / canonicalWidth,
            height / canonicalHeight);
    }
}