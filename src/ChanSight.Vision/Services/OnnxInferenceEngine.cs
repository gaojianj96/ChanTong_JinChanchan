using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;
using Microsoft.ML.OnnxRuntime;

namespace ChanSight.Vision.Services;

public sealed class OnnxInferenceEngine : IOnnxInferenceEngine
{
    private InferenceSession? _session;
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
        _currentDevice = preferredDevice;
    }

    public void LoadModel(string modelPath)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);

        lock (_lock)
        {
            DisposeSession();

            if (_currentDevice == InferenceDeviceType.DirectML)
            {
                try
                {
                    var options = CreateDirectMLOptions();
                    _session = new InferenceSession(modelPath, options);
                    return;
                }
                catch (Exception)
                {
                    _currentDevice = InferenceDeviceType.Cpu;
                }
            }

            if (_currentDevice == InferenceDeviceType.Cuda)
            {
                try
                {
                    var options = CreateCudaOptions();
                    _session = new InferenceSession(modelPath, options);
                    return;
                }
                catch (Exception)
                {
                    _currentDevice = InferenceDeviceType.Cpu;
                }
            }

            var cpuOptions = new SessionOptions();
            cpuOptions.AppendExecutionProvider_CPU();
            _session = new InferenceSession(modelPath, cpuOptions);
        }
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