using ChanSight.Capture.Services;
using ChanSight.Core.Models;
using FluentAssertions;
using OpenCvSharp;

namespace ChanSight.Tests.Capture;

public sealed class DxgiCaptureServiceTests
{
    [Fact]
    public void GetCropRect_TargetFullyInsideOutput_ReturnsExactOffsetAndSize()
    {
        var target = new WindowBounds(100, 100, 640, 480);
        var output = new WindowBounds(0, 0, 1920, 1080);

        var crop = DxgiCaptureService.GetCropRect(target, output, (1920, 1080));

        crop.Should().Be(new Rect(100, 100, 640, 480));
    }

    [Fact]
    public void GetCropRect_TargetOffsetAboveLeftOfOutput_ClampsToOrigin()
    {
        var target = new WindowBounds(-50, -20, 640, 480);
        var output = new WindowBounds(0, 0, 1920, 1080);

        var crop = DxgiCaptureService.GetCropRect(target, output, (1920, 1080));

        crop.Should().Be(new Rect(0, 0, 640, 480));
    }

    [Fact]
    public void GetCropRect_TargetExceedsOutputBottomRight_ClampsToTextureBounds()
    {
        var target = new WindowBounds(1500, 800, 1000, 700);
        var output = new WindowBounds(0, 0, 1920, 1080);

        var crop = DxgiCaptureService.GetCropRect(target, output, (1920, 1080));

        crop.Should().Be(new Rect(1500, 800, 420, 280));
    }

    [Fact]
    public void GetCropRect_MultiMonitorOutput_OffsetsRelativeToOutputOrigin()
    {
        var target = new WindowBounds(2000, 100, 640, 480);
        var output = new WindowBounds(1920, 0, 1920, 1080);

        var crop = DxgiCaptureService.GetCropRect(target, output, (1920, 1080));

        crop.Should().Be(new Rect(80, 100, 640, 480));
    }

    [Theory]
    [InlineData(100, 100, 640, 480, 0, 0, 1920, 1080)]
    [InlineData(-50, -20, 640, 480, 0, 0, 1920, 1080)]
    [InlineData(1500, 800, 1000, 700, 0, 0, 1920, 1080)]
    [InlineData(2000, 100, 640, 480, 1920, 0, 1920, 1080)]
    [InlineData(-100, -100, 100, 100, 1920, 0, 1920, 1080)]
    public void GetCropRect_NeverProducesOutOfBoundsCrop(
        int targetLeft, int targetTop, int targetWidth, int targetHeight,
        int outputLeft, int outputTop, int outputWidth, int outputHeight)
    {
        var target = new WindowBounds(targetLeft, targetTop, targetWidth, targetHeight);
        var output = new WindowBounds(outputLeft, outputTop, outputWidth, outputHeight);
        var texSize = (outputWidth, outputHeight);

        var crop = DxgiCaptureService.GetCropRect(target, output, texSize);

        crop.Left.Should().BeGreaterThanOrEqualTo(0);
        crop.Top.Should().BeGreaterThanOrEqualTo(0);
        crop.Width.Should().BeGreaterThanOrEqualTo(1);
        crop.Height.Should().BeGreaterThanOrEqualTo(1);
        crop.Right.Should().BeLessThanOrEqualTo(outputWidth);
        crop.Bottom.Should().BeLessThanOrEqualTo(outputHeight);
    }

    [Fact]
    public void ShouldAcceptFrame_FirstFrame_IsAcceptedAndRecordsTimestamp()
    {
        var interval = TimeSpan.FromMilliseconds(100);
        var now = 1_000_000L;

        var accepted = DxgiCaptureService.ShouldAcceptFrame(0, now, interval, out var newLast);

        accepted.Should().BeTrue();
        newLast.Should().Be(now);
    }

    [Fact]
    public void ShouldAcceptFrame_BelowMinimumInterval_IsRejectedAndKeepsLast()
    {
        var interval = TimeSpan.FromMilliseconds(100);
        var last = 1_000_000L;

        var accepted = DxgiCaptureService.ShouldAcceptFrame(last, last + interval.Ticks / 2, interval, out var newLast);

        accepted.Should().BeFalse();
        newLast.Should().Be(last);
    }

    [Fact]
    public void ShouldAcceptFrame_AtMinimumInterval_IsAccepted()
    {
        var interval = TimeSpan.FromMilliseconds(100);
        var last = 1_000_000L;
        var now = last + interval.Ticks;

        var accepted = DxgiCaptureService.ShouldAcceptFrame(last, now, interval, out var newLast);

        accepted.Should().BeTrue();
        newLast.Should().Be(now);
    }

    [Fact]
    public void ShouldAcceptFrame_AboveMinimumInterval_IsAcceptedAndAdvancesLast()
    {
        var interval = TimeSpan.FromMilliseconds(100);
        var last = 1_000_000L;
        var now = last + 2 * interval.Ticks;

        var accepted = DxgiCaptureService.ShouldAcceptFrame(last, now, interval, out var newLast);

        accepted.Should().BeTrue();
        newLast.Should().Be(now);
    }

    [Fact]
    public void ComputeFailureFinalize_WhenRunning_TransitionsToStoppedAndNotifies()
    {
        var result = DxgiCaptureService.ComputeFailureFinalize(failureFinalized: false, isRunning: true);

        result.Finalized.Should().BeTrue();
        result.ShouldTransition.Should().BeTrue();
        result.ShouldNotify.Should().BeTrue();
    }

    [Fact]
    public void ComputeFailureFinalize_WhenAlreadyStopped_FinalizesWithoutTransitionOrNotify()
    {
        var result = DxgiCaptureService.ComputeFailureFinalize(failureFinalized: false, isRunning: false);

        result.Finalized.Should().BeTrue();
        result.ShouldTransition.Should().BeFalse();
        result.ShouldNotify.Should().BeFalse();
    }

    [Fact]
    public void ComputeFailureFinalize_WhenAlreadyFinalized_IsNoOp()
    {
        var result = DxgiCaptureService.ComputeFailureFinalize(failureFinalized: true, isRunning: true);

        result.Finalized.Should().BeFalse();
        result.ShouldTransition.Should().BeFalse();
        result.ShouldNotify.Should().BeFalse();
    }
}
