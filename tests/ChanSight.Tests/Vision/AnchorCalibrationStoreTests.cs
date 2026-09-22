using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using FluentAssertions;
using OpenCvSharp;

namespace ChanSight.Tests.Vision;

public sealed class AnchorCalibrationStoreTests
{
    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "chansight-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static AnchorCalibrationResult SampleResult() => new()
    {
        IsValid = true,
        Confidence = 0.95,
        ScaleX = 2.0,
        ScaleY = 2.0,
        OffsetX = 100.0,
        OffsetY = -50.0,
        MaxReprojectionErrorPx = 0.4,
        ObservedAnchorPoints = new[] { new Point2d(1200, 870), new Point2d(2692, 870), new Point2d(2692, 1370), new Point2d(1200, 1370) },
    };

    [Fact]
    public void SaveThenLoad_RoundTripsFields()
    {
        var dir = TempDir();
        try
        {
            var store = new AnchorCalibrationStore(dir);
            var original = SampleResult();

            store.Save(original, frameWidth: 3840, frameHeight: 2160);

            var loaded = store.Load();

            loaded.Should().NotBeNull();
            loaded!.IsValid.Should().Be(original.IsValid);
            loaded.Confidence.Should().Be(original.Confidence);
            loaded.ScaleX.Should().Be(original.ScaleX);
            loaded.ScaleY.Should().Be(original.ScaleY);
            loaded.OffsetX.Should().Be(original.OffsetX);
            loaded.OffsetY.Should().Be(original.OffsetY);
            loaded.MaxReprojectionErrorPx.Should().Be(original.MaxReprojectionErrorPx);
            loaded.ObservedAnchorPoints.Should().BeEquivalentTo(original.ObservedAnchorPoints);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Load_WhenFileMissing_ReturnsNull()
    {
        var store = new AnchorCalibrationStore(TempDir());

        store.Load().Should().BeNull();
    }

    [Fact]
    public void TryGet_ReturnsFalseWhenMissing()
    {
        var store = new AnchorCalibrationStore(TempDir());

        store.TryGet(out var result).Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void MapCanonical_SameSize_UsesScaleAndOffsetDirectly()
    {
        var dir = TempDir();
        try
        {
            var store = new AnchorCalibrationStore(dir);
            var result = SampleResult();
            store.Save(result, frameWidth: 3840, frameHeight: 2160);

            // canonical (600, 435) -> 600*2 + 100 = 1300, 435*2 - 50 = 820.
            var mapped = store.MapCanonical(new Point2d(600, 435), currentWidth: 3840, currentHeight: 2160);

            mapped.Should().NotBeNull();
            mapped!.Value.X.Should().BeApproximately(1300.0, 1e-6);
            mapped.Value.Y.Should().BeApproximately(820.0, 1e-6);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void MapCanonical_DifferentSize_ScalesOffsetByRatio()
    {
        var dir = TempDir();
        try
        {
            var store = new AnchorCalibrationStore(dir);
            var result = SampleResult();
            store.Save(result, frameWidth: 3840, frameHeight: 2160);

            // Halve the window: currentWidth 1920, currentHeight 1080.
            // offsetX scaled by 1920/3840 = 0.5 → 50; offsetY by 1080/2160 = 0.5 → -25.
            // canonical (600, 435) -> 600*2 + 50 = 1250, 435*2 - 25 = 845.
            var mapped = store.MapCanonical(new Point2d(600, 435), currentWidth: 1920, currentHeight: 1080);

            mapped.Should().NotBeNull();
            mapped!.Value.X.Should().BeApproximately(1250.0, 1e-6);
            mapped.Value.Y.Should().BeApproximately(845.0, 1e-6);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void MapCanonical_InvalidCalibration_ReturnsNull()
    {
        var dir = TempDir();
        try
        {
            var store = new AnchorCalibrationStore(dir);
            var invalid = new AnchorCalibrationResult { IsValid = false };
            store.Save(invalid, frameWidth: 3840, frameHeight: 2160);

            store.MapCanonical(new Point2d(600, 435), 1920, 1080).Should().BeNull();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}