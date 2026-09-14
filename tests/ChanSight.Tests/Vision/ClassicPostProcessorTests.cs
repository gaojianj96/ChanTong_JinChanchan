namespace ChanSight.Tests.Vision;

using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using FluentAssertions;
using OpenCvSharp;

public sealed class ClassicPostProcessorTests
{
    private sealed record Fake(int ClassId, Rect Box, double Confidence) : IDetection;

    private readonly IPostProcessor<Fake> _pp = new ClassicPostProcessor<Fake>();

    [Fact]
    public void Nms_SuppressesOverlappingSameClass()
    {
        var input = new List<Fake> { new(0, new Rect(10, 10, 100, 100), 0.9), new(0, new Rect(20, 20, 100, 100), 0.8) };

        var result = _pp.Process(input, new PostProcessOptions());

        result.Should().HaveCount(1);
        result[0].Confidence.Should().Be(0.9);
    }

    [Fact]
    public void Nms_KeepsDifferentClasses()
    {
        var input = new List<Fake> { new(0, new Rect(10, 10, 100, 100), 0.9), new(1, new Rect(20, 20, 100, 100), 0.8) };

        _pp.Process(input, new PostProcessOptions()).Should().HaveCount(2);
    }

    [Fact]
    public void ConfidenceFilter_RemovesLow()
    {
        var input = new List<Fake> { new(0, new Rect(10, 10, 100, 100), 0.1), new(0, new Rect(200, 200, 100, 100), 0.9) };

        var result = _pp.Process(input, new PostProcessOptions { ConfidenceThreshold = 0.25 });

        result.Should().HaveCount(1);
    }

    [Fact]
    public void RoiFilter_KeepsOnlyCentersInside()
    {
        var input = new List<Fake> { new(0, new Rect(50, 50, 100, 100), 0.9), new(0, new Rect(300, 300, 100, 100), 0.8) };

        var result = _pp.Process(input, new PostProcessOptions { RoiBounds = new Rect(0, 0, 200, 200) });

        result.Should().HaveCount(1);
        result[0].Box.X.Should().Be(50);
    }

    [Fact]
    public void EmptyInput_ReturnsEmpty()
    {
        _pp.Process(new List<Fake>(), new PostProcessOptions()).Should().BeEmpty();
    }

    [Fact]
    public void Nms_IoUAtThreshold_IsKept()
    {
        var options = new PostProcessOptions { NmsThreshold = 0.5 };
        var a = new Rect(0, 0, 100, 100);
        var b = new Rect(50, 0, 100, 100);
        var input = new List<Fake> { new(0, a, 0.9), new(0, b, 0.8) };

        var result = _pp.Process(input, options);

        result.Should().HaveCount(2);
    }

    [Fact]
    public void Nms_EqualConfidence_DeterministicOrder()
    {
        var input = new List<Fake>
        {
            new(0, new Rect(200, 200, 10, 10), 0.9),
            new(0, new Rect(10, 10, 10, 10), 0.9)
        };

        var result = _pp.Process(input, new PostProcessOptions());

        result.Should().HaveCount(2);
        result[0].Box.X.Should().Be(10);
        result[1].Box.X.Should().Be(200);
    }

    [Fact]
    public void MaxDetections_Caps()
    {
        var input = new List<Fake> { new(0, new Rect(10, 10, 10, 10), 0.9), new(0, new Rect(200, 200, 10, 10), 0.8) };

        _pp.Process(input, new PostProcessOptions { MaxDetections = 1 }).Should().HaveCount(1);
    }
}