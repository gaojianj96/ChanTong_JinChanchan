namespace ChanSight.Tests.Vision;

using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using FluentAssertions;
using OpenCvSharp;

public sealed class ClassicAnchorCalibratorTests
{
    private readonly IAnchorCalibrator _calibrator = new ClassicAnchorCalibrator();

    [Fact]
    public void Proportional_ScalesPerAxis()
    {
        using var frame = new Mat(2088, 3840, MatType.CV_8UC3);

        var result = _calibrator.Calibrate(frame);

        result.IsValid.Should().BeTrue();
        result.Confidence.Should().Be(0.5);
        result.ScaleX.Should().BeApproximately(2.0, 1e-9);
        result.ScaleY.Should().BeApproximately(2088.0 / 1080.0, 1e-9);
        result.OffsetX.Should().Be(0.0);
        result.OffsetY.Should().Be(0.0);

        var mapped = result.MapCanonical(new Point2d(547.5, 444.75));
        mapped.X.Should().BeApproximately(1095.0, 0.1);
        mapped.Y.Should().BeApproximately(859.8, 0.1);
    }

    [Fact]
    public void Proportional_1280x720()
    {
        using var frame = new Mat(720, 1280, MatType.CV_8UC3);

        var result = _calibrator.Calibrate(frame);

        result.IsValid.Should().BeTrue();
        result.ScaleX.Should().BeApproximately(2.0 / 3.0, 1e-9);
        result.ScaleY.Should().BeApproximately(2.0 / 3.0, 1e-9);

        var mapped = result.MapCanonical(new Point2d(547.5, 444.75));
        mapped.X.Should().BeApproximately(365.0, 0.1);
        mapped.Y.Should().BeApproximately(296.5, 0.1);
    }

    [Fact]
    public void WithAnchors_LowError_Valid()
    {
        using var frame = new Mat(2160, 3840, MatType.CV_8UC3);

        var pairs = new List<AnchorPointPair>
        {
            new(new Point2d(0, 0), new Point2d(0, 0)),
            new(new Point2d(1920, 0), new Point2d(3840, 0)),
            new(new Point2d(0, 1080), new Point2d(0, 2160)),
            new(new Point2d(1920, 1080), new Point2d(3840, 2160))
        };

        var options = new AnchorCalibrationOptions { AnchorPairs = pairs };

        var result = _calibrator.Calibrate(frame, options);

        result.IsValid.Should().BeTrue();
        result.Confidence.Should().BeApproximately(1.0, 1e-9);
        result.MaxReprojectionErrorPx.Should().BeApproximately(0.0, 1e-6);
        result.ScaleX.Should().BeApproximately(2.0, 1e-9);
        result.ScaleY.Should().BeApproximately(2.0, 1e-9);
    }

    [Fact]
    public void WithAnchors_HighError_Invalid()
    {
        using var frame = new Mat(2160, 3840, MatType.CV_8UC3);

        var pairs = new List<AnchorPointPair>
        {
            new(new Point2d(0, 0), new Point2d(50, 50)),
            new(new Point2d(1920, 0), new Point2d(3840, 50)),
            new(new Point2d(0, 1080), new Point2d(50, 2160)),
            new(new Point2d(1920, 1080), new Point2d(3830, 2150))
        };

        var options = new AnchorCalibrationOptions { AnchorPairs = pairs };

        var result = _calibrator.Calibrate(frame, options);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void DegenerateAnchors_ReturnsInvalidWithoutThrowing()
    {
        using var frame = new Mat(1080, 1920, MatType.CV_8UC3);

        var pairs = new List<AnchorPointPair>
        {
            new(new Point2d(100, 100), new Point2d(200, 200)),
            new(new Point2d(100, 100), new Point2d(201, 200)),
            new(new Point2d(100, 100), new Point2d(200, 201)),
            new(new Point2d(100, 100), new Point2d(202, 202))
        };

        var result = _calibrator.Calibrate(frame, new AnchorCalibrationOptions { AnchorPairs = pairs });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void WithAnchors_NegativeOffset_Regression()
    {
        using var frame = new Mat(2160, 3840, MatType.CV_8UC3);

        var pairs = new List<AnchorPointPair>
        {
            new(new Point2d(0, 0), new Point2d(-100, -80)),
            new(new Point2d(1920, 0), new Point2d(3740, -80)),
            new(new Point2d(0, 1080), new Point2d(-100, 2080)),
            new(new Point2d(1920, 1080), new Point2d(3740, 2080))
        };

        var result = _calibrator.Calibrate(frame, new AnchorCalibrationOptions { AnchorPairs = pairs });

        result.IsValid.Should().BeTrue();
        result.OffsetX.Should().BeApproximately(-100.0, 1e-9);
        result.OffsetY.Should().BeApproximately(-80.0, 1e-9);
        result.ScaleX.Should().BeApproximately(2.0, 1e-9);
        result.ScaleY.Should().BeApproximately(2.0, 1e-9);
    }

    [Fact]
    public void EmptyFrame_ReturnsInvalid()
    {
        using var frame = new Mat();

        var result = _calibrator.Calibrate(frame);

        result.IsValid.Should().BeFalse();
    }
}