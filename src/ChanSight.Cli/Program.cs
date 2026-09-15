using ChanSight.Capture.Extensions;
using ChanSight.Cli;
using ChanSight.Cli.Dashboard;
using ChanSight.Core.Extensions;
using ChanSight.Recorder.Extensions;
using ChanSight.Vision.Extensions;
using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var cliOptions = CliVisionOptions.Parse(args);

using var host = Host
    .CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddChanSightCore();
        services.AddChanSightCapture();
        services.AddChanSightRecorder();
        services.AddChanSightVision();

        if (cliOptions.VisionDryRun)
        {
            services.AddSingleton<IOnnxInferenceEngine, StubInferenceEngine>();
        }

        services.AddSingleton<InteractiveDashboard>();
    })
    .Build();

if (!string.IsNullOrWhiteSpace(cliOptions.ProbeModelPath))
{
    long[]? overrideShape = cliOptions.ProbeSize is int s ? new long[] { 1, 3, s, s } : null;

    try
    {
        using var dmlEngine = new OnnxInferenceEngine(InferenceDeviceType.DirectML);
        var dmlStats = dmlEngine.ProbeLatency(cliOptions.ProbeModelPath, warmup: 3, iterations: 10, inputShapeOverride: overrideShape);

        using var cpuEngine = new OnnxInferenceEngine(InferenceDeviceType.Cpu);
        var cpuStats = cpuEngine.ProbeLatency(cliOptions.ProbeModelPath, warmup: 2, iterations: 5, inputShapeOverride: overrideShape);

        var autoPick = OnnxInferenceEngine.ChoosePreferredDevice(cliOptions.ProbeModelPath);

        Console.WriteLine($"[probe/dml] device={dmlStats.Device} input={dmlStats.InputShape} mean={dmlStats.MeanMs}ms p95={dmlStats.P95Ms}ms min={dmlStats.MinMs}ms max={dmlStats.MaxMs}ms");
        Console.WriteLine($"[probe/cpu-ref] device={cpuStats.Device} mean={cpuStats.MeanMs}ms p95={cpuStats.P95Ms}ms");
        Console.WriteLine($"[probe/auto] auto-select would pick: {autoPick}");

        var gate = OnnxInferenceEngine.EvaluateLatencyGate(autoPick == InferenceDeviceType.DirectML ? dmlStats : cpuStats);
        Console.WriteLine($"[go/nogo] passed={gate.Passed} observed={gate.ObservedMs}ms threshold={gate.ThresholdMs}ms downscale={gate.RecommendedDownscaleFactor}");
        Console.WriteLine($"[go/nogo] {gate.Note}");
    }
    catch (Exception ex) when (overrideShape is not null)
    {
        Console.WriteLine($"[probe] FAILED at size {overrideShape[2]}x{overrideShape[3]}: model input is static 640x640; re-export with dynamic=True to benchmark other sizes.");
        Console.WriteLine($"[probe] detail: {ex.Message.Split('\n')[0]}");
    }

    return;
}

var dashboard = host.Services.GetRequiredService<InteractiveDashboard>();
await dashboard.RunAsync(CancellationToken.None);