using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;
using Microsoft.ML.OnnxRuntime;

namespace ChanSight.Vision.Services;

public sealed class OnnxInferenceEngine : IOnnxInferenceEngine
{
    private InferenceSession? _session;
    private readonly InferenceDeviceType _preferredDevice;
    private InferenceDeviceType _currentDevice;
    private readonly object _lock = new();
    private bool _disposed;

    public InferenceDeviceType CurrentDevice
    {
        get
        {
            ThrowIfDisposed();
            return _currentDevice;
        }
    }

    public OnnxInferenceEngine(InferenceDeviceType preferredDevice = InferenceDeviceType.DirectML)
    {
        _preferredDevice = preferredDevice;
        _currentDevice = preferredDevice;
    }

    public void LoadModel(string modelPath)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);

        lock (_lock)
        {
            DisposeSession();

            var device = _preferredDevice;
            try
            {
                if (device == InferenceDeviceType.DirectML)
                {
                    try
                    {
                        _session = new InferenceSession(modelPath, CreateDirectMLOptions());
                        _currentDevice = InferenceDeviceType.DirectML;
                        return;
                    }
                    catch
                    {
                    }
                }

                if (device == InferenceDeviceType.Cuda)
                {
                    try
                    {
                        _session = new InferenceSession(modelPath, CreateCudaOptions());
                        _currentDevice = InferenceDeviceType.Cuda;
                        return;
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            var cpuOptions = new SessionOptions();
            cpuOptions.AppendExecutionProvider_CPU();
            _session = new InferenceSession(modelPath, cpuOptions);
            _currentDevice = InferenceDeviceType.Cpu;
        }
    }

    public InferenceLatencyStats ProbeLatency(string modelPath, int warmup = 3, int iterations = 10, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        if (warmup < 0)
            throw new ArgumentOutOfRangeException(nameof(warmup));
        if (iterations < 1)
            throw new ArgumentOutOfRangeException(nameof(iterations));

        InferenceSession session;
        lock (_lock)
        {
            session = CreateSessionForProbe(modelPath);
        }

        using (session)
        {
            var inputName = session.InputNames[0];
            var dims = session.InputMetadata[inputName].Dimensions;
            var shape = new long[dims.Length];
            var elementCount = 1L;
            var hasDynamic = false;
            for (var i = 0; i < dims.Length; i++)
            {
                if (dims[i] <= 0)
                {
                    hasDynamic = true;
                    shape[i] = 1;
                }
                else
                {
                    shape[i] = dims[i];
                }

                elementCount *= shape[i];
            }

            var inputData = new float[elementCount];
            var inputTensor = OrtValue.CreateTensorValueFromMemory(
                OrtMemoryInfo.DefaultInstance, inputData.AsMemory(), shape);
            var inputs = new Dictionary<string, OrtValue> { [inputName] = inputTensor };

            for (var w = 0; w < warmup; w++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var runOptions = new RunOptions();
                using var _ = session.Run(runOptions, inputs, session.OutputNames);
            }

            var samples = new List<double>(iterations);
            for (var i = 0; i < iterations; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sw = System.Diagnostics.Stopwatch.StartNew();
                using var runOptions = new RunOptions();
                using var _ = session.Run(runOptions, inputs, session.OutputNames);
                sw.Stop();
                samples.Add(sw.Elapsed.TotalMilliseconds);
            }

            samples.Sort();
            var p95Index = Math.Min(samples.Count - 1, (int)Math.Ceiling(samples.Count * 0.95) - 1);
            var shapeLabel = string.Join("x", dims) + (hasDynamic ? " (dynamic, benchmarked as 1)" : string.Empty);
            return new InferenceLatencyStats
            {
                Warmup = warmup,
                Iterations = iterations,
                MeanMs = Math.Round(samples.Average(), 3),
                MinMs = Math.Round(samples[0], 3),
                MaxMs = Math.Round(samples[^1], 3),
                P95Ms = Math.Round(samples[p95Index], 3),
                Device = _currentDevice.ToString(),
                InputShape = shapeLabel
            };
        }
    }

    private InferenceSession CreateSessionForProbe(string modelPath)
    {
        var device = _preferredDevice;

        if (device == InferenceDeviceType.DirectML)
        {
            try
            {
                return new InferenceSession(modelPath, CreateDirectMLOptions());
            }
            catch
            {
            }
        }

        if (device == InferenceDeviceType.Cuda)
        {
            try
            {
                return new InferenceSession(modelPath, CreateCudaOptions());
            }
            catch
            {
            }
        }

        var cpuOptions = new SessionOptions();
        cpuOptions.AppendExecutionProvider_CPU();
        return new InferenceSession(modelPath, cpuOptions);
    }

    public static GoNoGoResult EvaluateLatencyGate(InferenceLatencyStats stats, double thresholdMs = 16.0)
    {
        ArgumentNullException.ThrowIfNull(stats);

        var passed = stats.MeanMs <= thresholdMs;
        double factor;
        string note;

        if (passed)
        {
            factor = 1.0;
            note = "Within latency budget.";
        }
        else
        {
            factor = Math.Clamp(Math.Sqrt(thresholdMs / Math.Max(stats.MeanMs, 1e-6)), 0.25, 1.0);
            note = $"Over budget; recommend downscaling inputs by factor {factor:F2} (1080p-class ROI fallback).";
        }

        return new GoNoGoResult
        {
            Passed = passed,
            ThresholdMs = thresholdMs,
            ObservedMs = stats.MeanMs,
            RecommendedDownscaleFactor = Math.Round(factor, 2),
            Note = note
        };
    }

    public OrtValueTensor RunInference(string inputName, ReadOnlySpan<float> inputData, long[] inputShape)
    {
        ThrowIfDisposed();
        if (_session is null)
            throw new InvalidOperationException("No model loaded. Call LoadModel first.");

        lock (_lock)
        {
            return RunInferenceCore(inputName, inputData, inputShape);
        }
    }

    public OrtValueTensor RunInference(
        string inputName,
        ReadOnlySpan<float> inputData,
        long[] inputShape,
        IReadOnlyDictionary<string, long[]> outputShapes)
    {
        ThrowIfDisposed();
        if (_session is null)
            throw new InvalidOperationException("No model loaded. Call LoadModel first.");

        lock (_lock)
        {
            return RunInferenceCore(inputName, inputData, inputShape);
        }
    }

    private OrtValueTensor RunInferenceCore(string inputName, ReadOnlySpan<float> inputData, long[] inputShape)
    {
        var inputTensor = OrtValue.CreateTensorValueFromMemory(
            OrtMemoryInfo.DefaultInstance,
            inputData.ToArray().AsMemory(),
            inputShape);

        var inputs = new Dictionary<string, OrtValue>
        {
            [inputName] = inputTensor
        };

        using var runOptions = new RunOptions();
        using var results = _session!.Run(runOptions, inputs, _session.OutputNames);

        var firstOutput = results.First();
        var outputData = firstOutput.GetTensorDataAsSpan<float>().ToArray();
        var outputShape = firstOutput.GetTensorTypeAndShape().Shape;

        return new OrtValueTensor(_session.OutputNames[0], outputData, outputShape);
    }

    private static SessionOptions CreateDirectMLOptions()
    {
        var options = new SessionOptions();
        options.AppendExecutionProvider_DML(0);
        options.AppendExecutionProvider_CPU();
        return options;
    }

    private static SessionOptions CreateCudaOptions()
    {
        var options = new SessionOptions();
        options.AppendExecutionProvider_CUDA();
        options.AppendExecutionProvider_CPU();
        return options;
    }

    public void Dispose()
    {
        if (_disposed) return;
        DisposeSession();
        _disposed = true;
    }

    private void DisposeSession()
    {
        _session?.Dispose();
        _session = null;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(OnnxInferenceEngine));
    }
}