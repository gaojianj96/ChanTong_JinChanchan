using ChanSight.Vision.Models;

namespace ChanSight.Vision.Interfaces;

public interface IOnnxInferenceEngine : IDisposable
{
    InferenceDeviceType CurrentDevice { get; }
    void LoadModel(string modelPath);
    OrtValueTensor RunInference(string inputName, ReadOnlySpan<float> inputData, long[] inputShape);
    OrtValueTensor RunInference(
        string inputName,
        ReadOnlySpan<float> inputData,
        long[] inputShape,
        IReadOnlyDictionary<string, long[]> outputShapes);

    InferenceLatencyStats ProbeLatency(string modelPath, int warmup = 3, int iterations = 10, CancellationToken cancellationToken = default);
}