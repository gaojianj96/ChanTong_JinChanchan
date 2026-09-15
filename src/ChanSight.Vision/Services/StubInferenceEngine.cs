namespace ChanSight.Vision.Services;

using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;

public sealed class StubInferenceEngine : IOnnxInferenceEngine
{
    public InferenceDeviceType CurrentDevice => InferenceDeviceType.Cpu;

    public void LoadModel(string modelPath)
    {
    }

    public OrtValueTensor RunInference(string inputName, ReadOnlySpan<float> inputData, long[] inputShape)
        => new("stub", new float[1], new long[] { 1 });

    public OrtValueTensor RunInference(string inputName, ReadOnlySpan<float> inputData, long[] inputShape, IReadOnlyDictionary<string, long[]> outputShapes)
        => new("stub", new float[1], new long[] { 1 });

    public InferenceLatencyStats ProbeLatency(string modelPath, int warmup = 3, int iterations = 10, long[]? inputShapeOverride = null, CancellationToken cancellationToken = default)
        => new() { Warmup = warmup, Iterations = iterations, MeanMs = 0.05, MinMs = 0.04, MaxMs = 0.1, P95Ms = 0.1, Device = "Stub" };

    public void Dispose()
    {
    }
}