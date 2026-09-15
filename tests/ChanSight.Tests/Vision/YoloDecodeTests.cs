namespace ChanSight.Tests.Vision;

using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using FluentAssertions;
using OpenCvSharp;

public sealed class YoloDecodeTests
{
    [Fact]
    public void Decode_BasicBox()
    {
        var output = new float[] { 320f, 320f, 100f, 100f, 0.9f };
        var shape = new long[] { 1, 5, 1 };

        var result = YoloDetectorService.DecodeOutput(
            output, shape, 640, scale: 1f, padX: 0, padY: 0,
            originalWidth: 640, originalHeight: 640, confidenceThreshold: 0.25f);

        result.Should().HaveCount(1);
        result[0].ClassId.Should().Be(0);
        result[0].Confidence.Should().BeApproximately(0.9, 1e-6);
        result[0].Box.Width.Should().BeGreaterThan(0);
        result[0].Box.Height.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Decode_FiltersLowClassScore()
    {
        var output = new float[] { 320f, 320f, 100f, 100f, 0.1f };
        var shape = new long[] { 1, 5, 1 };

        var result = YoloDetectorService.DecodeOutput(
            output, shape, 640, 1f, 0, 0, 640, 640, 0.25f);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Decode_EmptyWhenStrideInvalid()
    {
        var result = YoloDetectorService.DecodeOutput(
            new float[] { 1f, 2f, 3f }, new long[] { 1, 3, 1 }, 640, 1f, 0, 0, 640, 640, 0.25f);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ToChwFloatTensor_ProducesNormalizedChannels()
    {
        using var img = new Mat(2, 2, MatType.CV_8UC3);
        img.SetTo(new Scalar(255, 255, 255));

        var data = YoloDetectorService.ToChwFloatTensor(img);

        data.Should().HaveCount(12);
        data.Should().OnlyContain(v => v == 1.0f);
    }

    [Fact]
    public void ToChwFloatTensor_ConvertsBgrToRgbOrder()
    {
        using var img = new Mat(2, 2, MatType.CV_8UC3);
        img.SetTo(new Scalar(0, 0, 255));

        var data = YoloDetectorService.ToChwFloatTensor(img);

        var plane = 2 * 2;
        data[0].Should().Be(1.0f);
        data[2 * plane].Should().Be(0.0f);
    }
}