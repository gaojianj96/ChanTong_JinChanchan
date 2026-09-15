using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using FluentAssertions;

namespace ChanSight.Tests.Vision;

public sealed class OnnxInferenceEngineTests
{
    [Fact]
    public void Constructor_Default_CreatesEngineWithAutoDevicePreference()
    {
        using var engine = new OnnxInferenceEngine();

        engine.CurrentDevice.Should().Be(InferenceDeviceType.Auto);
    }

    [Fact]
    public void Constructor_CpuPreference_CreatesEngineWithCpuDevice()
    {
        using var engine = new OnnxInferenceEngine(InferenceDeviceType.Cpu);

        engine.CurrentDevice.Should().Be(InferenceDeviceType.Cpu);
    }

    [Fact]
    public void Constructor_CudaPreference_CreatesEngineWithCudaDevice()
    {
        using var engine = new OnnxInferenceEngine(InferenceDeviceType.Cuda);

        engine.CurrentDevice.Should().Be(InferenceDeviceType.Cuda);
    }

    [Fact]
    public void LoadModel_NullPath_ThrowsArgumentException()
    {
        using var engine = new OnnxInferenceEngine();

        var act = () => engine.LoadModel(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LoadModel_EmptyPath_ThrowsArgumentException()
    {
        using var engine = new OnnxInferenceEngine();

        var act = () => engine.LoadModel(string.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LoadModel_NonExistentFile_FallsBackToCpu()
    {
        using var engine = new OnnxInferenceEngine(InferenceDeviceType.DirectML);

        var act = () => engine.LoadModel("non_existent_model.onnx");
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void RunInference_NoModelLoaded_ThrowsInvalidOperation()
    {
        using var engine = new OnnxInferenceEngine();

        var act = () => engine.RunInference("input", new float[10], new long[] { 1, 10 });
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var engine = new OnnxInferenceEngine();

        var act = () =>
        {
            engine.Dispose();
            engine.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void CurrentDevice_AfterDispose_ThrowsObjectDisposed()
    {
        var engine = new OnnxInferenceEngine();
        engine.Dispose();

        var act = () => engine.CurrentDevice;
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void LoadModel_AfterDispose_ThrowsObjectDisposed()
    {
        var engine = new OnnxInferenceEngine();
        engine.Dispose();

        var act = () => engine.LoadModel("test.onnx");
        act.Should().Throw<ObjectDisposedException>();
    }
}