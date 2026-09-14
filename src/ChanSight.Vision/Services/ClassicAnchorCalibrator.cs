namespace ChanSight.Vision.Services;

using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;
using OpenCvSharp;

public sealed class ClassicAnchorCalibrator : IAnchorCalibrator
{
    // 经验衰减系数: 重投影误差放大 5 倍映射到 [0,1] 置信度区间(C2 契约,无理论依据,待实机校准复核)
    private const double ConfidenceDecayFactor = 5.0;

    // 比例合理性上限: 防御异常分辨率(极端窄小/超大窗口)
    private const double MaxPlausibleScale = 20.0;

    public AnchorCalibrationResult Calibrate(Mat frame, AnchorCalibrationOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new AnchorCalibrationOptions();
        cancellationToken.ThrowIfCancellationRequested();

        var invalid = new AnchorCalibrationResult
        {
            IsValid = false,
            Confidence = 0.0,
            ScaleX = 0,
            ScaleY = 0,
            OffsetX = 0,
            OffsetY = 0,
            MaxReprojectionErrorPx = double.NaN,
            ObservedAnchorPoints = Array.Empty<Point2d>()
        };

        if (frame is null || frame.Empty() || frame.Width <= 0 || frame.Height <= 0)
            return invalid;

        var scaleX = (double)frame.Width / options.CanonicalWidth;
        var scaleY = (double)frame.Height / options.CanonicalHeight;

        if (options.AnchorPairs is not null && options.AnchorPairs.Count >= 4)
        {
            try
            {
                var (sx, sy, ox, oy) = FitAffineLeastSquares(options.AnchorPairs);

                double maxErr = 0;
                foreach (var pair in options.AnchorPairs)
                {
                    var p = new Point2d(pair.Canonical.X * sx + ox, pair.Canonical.Y * sy + oy);
                    var dx = p.X - pair.Observed.X;
                    var dy = p.Y - pair.Observed.Y;
                    var err = Math.Sqrt(dx * dx + dy * dy);
                    if (err > maxErr)
                        maxErr = err;
                }

                var fitsFrame = Math.Abs(sx - scaleX) <= Math.Max(scaleX, 1.0) * 0.5 &&
                                Math.Abs(sy - scaleY) <= Math.Max(scaleY, 1.0) * 0.5;
                var isValid = fitsFrame && maxErr <= options.MaxReprojectionErrorPx;

                return new AnchorCalibrationResult
                {
                    IsValid = isValid,
                    Confidence = isValid
                        ? Math.Round(Math.Clamp(1.0 - maxErr / (options.MaxReprojectionErrorPx * ConfidenceDecayFactor), 0.0, 1.0), 3)
                        : 0.0,
                    ScaleX = sx,
                    ScaleY = sy,
                    OffsetX = ox,
                    OffsetY = oy,
                    MaxReprojectionErrorPx = maxErr,
                    ObservedAnchorPoints = options.AnchorPairs.Select(p => p.Observed).ToArray()
                };
            }
            catch (InvalidOperationException)
            {
                return invalid;
            }
        }

        var sensible = scaleX > 0 && scaleY > 0 && scaleX < MaxPlausibleScale && scaleY < MaxPlausibleScale;
        return new AnchorCalibrationResult
        {
            IsValid = sensible,
            Confidence = sensible ? 0.5 : 0.0,
            ScaleX = scaleX,
            ScaleY = scaleY,
            OffsetX = 0.0,
            OffsetY = 0.0,
            MaxReprojectionErrorPx = double.NaN,
            ObservedAnchorPoints = Array.Empty<Point2d>()
        };
    }

    // 每轴独立一元最小二乘(缩放+平移); 不建模旋转/剪切(金铲铲窗口为轴对齐缩放, B3 负责窗口原点)
    private static (double sx, double sy, double ox, double oy) FitAffineLeastSquares(IReadOnlyList<AnchorPointPair> pairs)
    {
        double n = pairs.Count;
        double sxSum = 0, sySum = 0, sx2Sum = 0, sy2Sum = 0, oxSum = 0, oySum = 0, sxoSum = 0, syoSum = 0;

        foreach (var p in pairs)
        {
            sxSum += p.Canonical.X;
            sySum += p.Canonical.Y;
            sx2Sum += p.Canonical.X * p.Canonical.X;
            sy2Sum += p.Canonical.Y * p.Canonical.Y;
            oxSum += p.Observed.X;
            oySum += p.Observed.Y;
            sxoSum += p.Canonical.X * p.Observed.X;
            syoSum += p.Canonical.Y * p.Observed.Y;
        }

        var denomX = n * sx2Sum - sxSum * sxSum;
        var denomY = n * sy2Sum - sySum * sySum;

        // 相对退化阈值: 随 canonical 量纲缩放,避免绝对 epsilon 误判
        var tolX = 1e-9 * (n * sx2Sum + 1.0);
        var tolY = 1e-9 * (n * sy2Sum + 1.0);

        if (Math.Abs(denomX) < tolX || Math.Abs(denomY) < tolY)
            throw new InvalidOperationException("Degenerate anchor set.");

        var sx = (n * sxoSum - sxSum * oxSum) / denomX;
        var sy = (n * syoSum - sySum * oySum) / denomY;
        var ox = (oxSum - sx * sxSum) / n;
        var oy = (oySum - sy * sySum) / n;

        return (sx, sy, ox, oy);
    }
}