namespace ChanSight.Tests.Vision;

using ChanSight.Vision.Services;
using FluentAssertions;

public sealed class StubInferenceEngineTests
{
    [Fact]
    public void Probe_ReturnsZeroCostStats()
    {
        using var engine = new StubInferenceEngine();

        var stats = engine.ProbeLatency("model.onnx", warmup: 3, iterations: 10);

        stats.MeanMs.Should().BeApproximately(0.05, 0.001);
        stats.Device.Should().Be("Stub");
    }

    [Fact]
    public void RunInference_ReturnsSingleElementTensor()
    {
        using var engine = new StubInferenceEngine();

        var input = new float[] { 1f };

        var act = () => engine.RunInference("input", input, new long[] { 1 });
        act.Should().NotThrow();

        var tensor = engine.RunInference("input", input, new long[] { 1 });
        tensor.Should().NotBeNull();
    }

    [Fact]
    public void Lifecycle_Ok()
    {
        var act = () =>
        {
            var engine = new StubInferenceEngine();
            engine.LoadModel("model.onnx");
            engine.Dispose();
        };

        act.Should().NotThrow();
    }
}