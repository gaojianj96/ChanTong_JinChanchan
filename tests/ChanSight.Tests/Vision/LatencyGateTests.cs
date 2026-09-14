namespace ChanSight.Tests.Vision;

using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using FluentAssertions;

public sealed class LatencyGateTests
{
    [Fact]
    public void UnderBudget_Passes()
    {
        var stats = new InferenceLatencyStats { MeanMs = 10.0 };

        var result = OnnxInferenceEngine.EvaluateLatencyGate(stats, 16.0);

        result.Passed.Should().BeTrue();
        result.RecommendedDownscaleFactor.Should().Be(1.0);
    }

    [Fact]
    public void OverBudget_FailsAndRecommendsDownscale()
    {
        var stats = new InferenceLatencyStats { MeanMs = 64.0 };

        var result = OnnxInferenceEngine.EvaluateLatencyGate(stats, 16.0);

        result.Passed.Should().BeFalse();
        result.RecommendedDownscaleFactor.Should().BeApproximately(0.5, 0.01);
        result.Note.Should().Contain("downscaling");
    }

    [Fact]
    public void ClampsFactorToQuarter()
    {
        var stats = new InferenceLatencyStats { MeanMs = 5000.0 };

        var result = OnnxInferenceEngine.EvaluateLatencyGate(stats, 16.0);

        result.RecommendedDownscaleFactor.Should().BeApproximately(0.25, 0.01);
    }

    [Fact]
    public void NullStats_Throws()
    {
        var act = () => OnnxInferenceEngine.EvaluateLatencyGate(null!, 16.0);

        act.Should().Throw<ArgumentNullException>();
    }
}