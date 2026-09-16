using ChanSight.Capture.Extensions;
using ChanSight.Cli;
using ChanSight.Cli.Dashboard;
using ChanSight.Cli.Retrospective;
using ChanSight.Core.Engine;
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
        services.AddChanSightRetrospective();

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

if (cliOptions.DecisionDryRun)
{
    RunDecisionDryRun(host.Services);
    return;
}

if (cliOptions.Replay is not null)
{
    var replayCommand = host.Services.GetRequiredService<ReplayCommand>();
    await replayCommand.RunAsync(cliOptions.Replay, CancellationToken.None);
    return;
}

var dashboard = host.Services.GetRequiredService<InteractiveDashboard>();
await dashboard.RunAsync(CancellationToken.None);

static void RunDecisionDryRun(IServiceProvider services)
{
    var pipeline = services.GetRequiredService<RecognitionDecisionPipeline>();

    var frame = BuildSyntheticRecognitionFrame();

    var result = pipeline.Process(frame);

    Console.WriteLine("== ChanSight decision panel (dry-run) ==");
    Console.WriteLine($"state: stage={result.State.Stage} phase={result.State.Phase} gold={result.State.Gold} level={result.State.Level}");
    Console.WriteLine("algorithm advice:");

    if (result.AlgorithmAdvice.Count == 0)
    {
        Console.WriteLine("  No advice");
    }
    else
    {
        foreach (var advice in result.AlgorithmAdvice)
        {
            Console.WriteLine($"  [{advice.Kind}/{advice.Verdict}] {advice.Reason}");
        }
    }

    Console.WriteLine("advisor advice: <none> (manual button not triggered)");
}

static RecognitionFrame BuildSyntheticRecognitionFrame()
{
    static UnitCell unit(string name) => new(name, 1, Array.Empty<ItemStack>(), 0.95, SourceTier.T0);
    static UnitCell empty() => new(null, 0, Array.Empty<ItemStack>(), 1.0, SourceTier.T3);

    var board = Enumerable.Range(0, 28).Select(_ => empty()).ToArray();
    board[0] = unit("盖伦");
    board[1] = unit("拉克丝");
    board[2] = unit("佐伊");

    var bench = Enumerable.Range(0, 9).Select(_ => empty()).ToArray();
    bench[0] = unit("拉克丝");
    bench[1] = unit("艾希");

    var shop = Enumerable.Range(0, 5)
        .Select(_ => new ShopCard(string.Empty, 0, 1.0, SourceTier.T3))
        .ToArray();

    return new RecognitionFrame
    {
        SourceTier = SourceTier.T0,
        CorrectionFlag = false,
        Timestamp = DateTime.UtcNow.ToString("O"),
        Confidence = 1.0,
        Gold = 60,
        Level = 5,
        Stage = "3-1",
        Hp = 80,
        Exp = 0,
        BoardCells = board,
        BenchCells = bench,
        ShopCards = shop,
    };
}