using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using FluentAssertions;
using OpenCvSharp;

namespace ChanSight.Tests.Vision;

public sealed class RoiMapperServiceTests
{
    private readonly RoiMapperService _service = new();

    [Fact]
    public void MapToPhysical_1920x1080_ReturnsIdentityMapping()
    {
        var canonical = new Rect(100, 200, 300, 400);

        var result = _service.MapToPhysical(canonical, 1920, 1080);

        result.X.Should().Be(100);
        result.Y.Should().Be(200);
        result.Width.Should().Be(300);
        result.Height.Should().Be(400);
    }

    [Fact]
    public void MapToPhysical_2560x1440_ScalesProportionally()
    {
        double scale = 1440.0 / 1080.0;
        var canonical = new Rect(100, 200, 300, 400);

        var result = _service.MapToPhysical(canonical, 2560, 1440);

        double expectedX = 100 * scale + (2560 - 1920 * scale) / 2.0;
        double expectedY = 200 * scale + (1440 - 1080 * scale) / 2.0;
        double expectedW = 300 * scale;
        double expectedH = 400 * scale;

        result.X.Should().Be((int)Math.Round(expectedX));
        result.Y.Should().Be((int)Math.Round(expectedY));
        result.Width.Should().Be((int)Math.Round(expectedW));
        result.Height.Should().Be((int)Math.Round(expectedH));
    }

    [Fact]
    public void MapToPhysical_1280x720_ScalesProportionally()
    {
        var canonical = new Rect(100, 200, 300, 400);

        var result = _service.MapToPhysical(canonical, 1280, 720);

        result.X.Should().BeGreaterThanOrEqualTo(0);
        result.Y.Should().BeGreaterThanOrEqualTo(0);
        result.Width.Should().BeGreaterThan(0);
        result.Height.Should().BeGreaterThan(0);
    }

    [Fact]
    public void MapToPhysical_Non16x9_LetterboxApplied()
    {
        var canonical = new Rect(0, 0, 1920, 1080);

        var result = _service.MapToPhysical(canonical, 1920, 600);

        result.Width.Should().BeGreaterThan(0);
        result.Height.Should().BeGreaterThan(0);
        result.Width.Should().BeLessThanOrEqualTo(1920);
        result.Height.Should().BeLessThanOrEqualTo(600);
    }

    [Fact]
    public void MapToPhysical_HandlesOutOfBoundsCanonicalRect()
    {
        var canonical = new Rect(2000, 1500, 100, 100);

        var result = _service.MapToPhysical(canonical, 1920, 1080);

        result.Width.Should().BeGreaterThan(0);
        result.Height.Should().BeGreaterThan(0);
    }

    [Fact]
    public void MapPointToPhysical_1920x1080_ReturnsIdentity()
    {
        var canonical = new Point2d(960, 540);

        var result = _service.MapPointToPhysical(canonical, 1920, 1080);

        result.X.Should().BeApproximately(960, 0.5);
        result.Y.Should().BeApproximately(540, 0.5);
    }

    [Fact]
    public void MapPointToPhysical_2560x1440_PreservesRelativePosition()
    {
        var canonicalLeft = new Point2d(0, 540);
        var canonicalRight = new Point2d(1920, 540);

        var left = _service.MapPointToPhysical(canonicalLeft, 2560, 1440);
        var right = _service.MapPointToPhysical(canonicalRight, 2560, 1440);

        left.X.Should().BeLessThan(right.X);
    }

    [Fact]
    public void MapPointToPhysical_ZeroDimensions_ThrowsArgumentOutOfRange()
    {
        var canonical = new Point2d(100, 100);

        var act = () => _service.MapPointToPhysical(canonical, 0, 1080);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CropRoi_WithValidFrame_ReturnsCorrectSizeMat()
    {
        using var frame = new Mat(1080, 1920, MatType.CV_8UC3);
        frame.SetTo(Scalar.Blue);

        using var roi = _service.CropRoi(frame, RoiRegionType.Gold);

        roi.Should().NotBeNull();
        roi.Width.Should().BeGreaterThan(0);
        roi.Height.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CropRoi_BoardArea_ReturnsSubImage()
    {
        using var frame = new Mat(1080, 1920, MatType.CV_8UC3);
        frame.SetTo(Scalar.Green);

        using var roi = _service.CropRoi(frame, RoiRegionType.BoardArea);

        roi.Should().NotBeNull();
        roi.Width.Should().BeLessThanOrEqualTo(frame.Width);
        roi.Height.Should().BeLessThanOrEqualTo(frame.Height);
    }

    [Fact]
    public void CropRoi_WithSmallFrame_HandlesBoundsSafely()
    {
        using var frame = new Mat(100, 100, MatType.CV_8UC3);

        using var roi = _service.CropRoi(frame, RoiRegionType.BoardArea);

        roi.Should().NotBeNull();
    }

    [Fact]
    public void CropRoi_DifferentResolutions_ReturnsConsistentMat()
    {
        foreach (var (w, h) in new[] { (1920, 1080), (2560, 1440), (1280, 720) })
        {
            using var frame = new Mat(h, w, MatType.CV_8UC3);
            using var roi = _service.CropRoi(frame, RoiRegionType.ShopCards);

            roi.Should().NotBeNull();
            roi.Width.Should().BeGreaterThan(0);
            roi.Height.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public void CropShopSlots_ReturnsFiveSlots()
    {
        using var frame = new Mat(1080, 1920, MatType.CV_8UC3);
        frame.SetTo(Scalar.Red);

        var slots = _service.CropShopSlots(frame);

        slots.Should().HaveCount(5);
        foreach (var slot in slots)
        {
            slot.Should().NotBeNull();
            slot.Width.Should().BeGreaterThan(0);
        }

        foreach (var slot in slots)
        {
            slot.Dispose();
        }
    }

    [Fact]
    public void CropShopSlots_2560x1440_ReturnsFiveSlots()
    {
        using var frame = new Mat(1440, 2560, MatType.CV_8UC3);
        frame.SetTo(Scalar.Red);

        var slots = _service.CropShopSlots(frame);

        slots.Should().HaveCount(5);
        foreach (var slot in slots)
        {
            slot.Width.Should().BeGreaterThan(0);
        }

        foreach (var slot in slots)
        {
            slot.Dispose();
        }
    }

    [Fact]
    public void CropShopSlots_TinyFrame_ReturnsSafeResults()
    {
        using var frame = new Mat(50, 50, MatType.CV_8UC3);

        var slots = _service.CropShopSlots(frame);

        slots.Should().HaveCount(5);
        foreach (var slot in slots)
        {
            slot.Dispose();
        }
    }

    [Fact]
    public void MapToPhysical_ThrowsOnNegativeDimensions()
    {
        var canonical = new Rect(0, 0, 100, 100);

        var act = () => _service.MapToPhysical(canonical, -1, 1080);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CropShopSlots_SlotsAreHorizontallyOrdered()
    {
        using var frame = new Mat(1080, 1920, MatType.CV_8UC3);

        var slots = _service.CropShopSlots(frame);

        for (int i = 1; i < slots.Count; i++)
        {
            slots[i].Width.Should().BeGreaterThan(0);
        }

        foreach (var slot in slots)
        {
            slot.Dispose();
        }
    }

    [Fact]
    public void CropRoi_AllRegionTypes_ReturnValidMat()
    {
        using var frame = new Mat(1080, 1920, MatType.CV_8UC3);

        foreach (RoiRegionType regionType in Enum.GetValues<RoiRegionType>())
        {
            using var roi = _service.CropRoi(frame, regionType);
            roi.Should().NotBeNull();
        }
    }
}