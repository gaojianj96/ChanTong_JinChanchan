using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using FluentAssertions;
using OpenCvSharp;

namespace ChanSight.Tests.Vision;

public sealed class BoardAnchorDetectorTests
{
    private static Mat CreateFrame(int width = 3840, int height = 2160)
        => new(height, width, MatType.CV_8UC3, Scalar.All(128));

    // Canonical BoardArea corners scaled by 2.0 (a 3840x2160 frame).
    private const string ValidCornersJson = """
        {
          "board": {"tl":[1200,870],"tr":[2692,870],"br":[2692,1370],"bl":[1200,1370]},
          "gold": [1740,120],
          "stage": [1680,20],
          "confidence": 0.95
        }
        """;

    private static BoardAnchorDetector CreateDetector(FakeVlmClient fake, IAnchorCalibrator? calibrator = null) =>
        new(fake, calibrator ?? new ClassicAnchorCalibrator());

    [Fact]
    public async Task DetectAsync_ValidCorners_ReturnsValidCalibration()
    {
        var fake = new FakeVlmClient().QueueResult(ValidCornersJson);
        var detector = CreateDetector(fake);
        using var frame = CreateFrame();

        var result = await detector.DetectAsync(frame);

        result.IsValid.Should().BeTrue();
        result.Confidence.Should().BeGreaterThan(0.0);
        result.ScaleX.Should().BeApproximately(2.0, 1e-6);
        result.ScaleY.Should().BeApproximately(2.0, 1e-6);
    }

    [Fact]
    public async Task DetectAsync_MissingCorner_ReturnsInvalidWithoutThrowing()
    {
        const string json = """
            {"board":{"tl":[1200,900],"tr":[2692,900],"br":[2692,1370]}, "confidence":0.9}
            """;
        var fake = new FakeVlmClient().QueueResult(json);
        var detector = CreateDetector(fake);
        using var frame = CreateFrame();

        var result = await detector.DetectAsync(frame);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task DetectAsync_BadJson_ReturnsInvalidWithoutThrowing()
    {
        var fake = new FakeVlmClient().QueueResult("not json at all");
        var detector = CreateDetector(fake);
        using var frame = CreateFrame();

        var result = await detector.DetectAsync(frame);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task DetectAsync_VlmThrows_ReturnsInvalidWithoutThrowing()
    {
        var fake = new FakeVlmClient().QueueException(new InvalidOperationException("boom"));
        var detector = CreateDetector(fake);
        using var frame = CreateFrame();

        var result = await detector.DetectAsync(frame);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task DetectAsync_LowConfidence_ReturnsInvalid()
    {
        const string json = """
            {"board":{"tl":[600,900],"tr":[2692,900],"br":[2692,1370],"bl":[1200,1370]}, "confidence":0.1}
            """;
        var fake = new FakeVlmClient().QueueResult(json);
        var detector = CreateDetector(fake);
        using var frame = CreateFrame();

        var result = await detector.DetectAsync(frame);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task DetectAsync_CornerOrder_PairingFollowsTlTrBrBl()
    {
        // Exact 2.0 scale of canonical BoardArea corners produces a clean affine,
        // and the recorded observed points must follow tl/tr/br/bl ordering.
        var fake = new FakeVlmClient().QueueResult(ValidCornersJson);
        var detector = CreateDetector(fake);
        using var frame = CreateFrame();

        var result = await detector.DetectAsync(frame);

        result.IsValid.Should().BeTrue();
        result.ObservedAnchorPoints.Should().HaveCount(4);
        result.ObservedAnchorPoints[0].Should().Be(new Point2d(1200, 870));
        result.ObservedAnchorPoints[1].Should().Be(new Point2d(2692, 870));
        result.ObservedAnchorPoints[2].Should().Be(new Point2d(2692, 1370));
        result.ObservedAnchorPoints[3].Should().Be(new Point2d(1200, 1370));
    }

    private sealed class FakeVlmClient : IVlmClient
    {
        private readonly Queue<object> _script = new();

        public FakeVlmClient QueueResult(string json)
        {
            _script.Enqueue(json);
            return this;
        }

        public FakeVlmClient QueueException(Exception exception)
        {
            _script.Enqueue(exception);
            return this;
        }

        public Task<string> CompleteAsync(
            string prompt,
            IReadOnlyList<(string mime, byte[] data)> images,
            CancellationToken ct)
        {
            var next = _script.Count > 0
                ? _script.Dequeue()
                : throw new InvalidOperationException("FakeVlmClient script exhausted.");

            if (next is Exception exception)
            {
                throw exception;
            }

            return Task.FromResult((string)next);
        }
    }
}