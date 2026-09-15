using ChanSight.Capture.Extensions;
using ChanSight.Cli;
using ChanSight.Cli.Dashboard;
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

var dashboard = host.Services.GetRequiredService<InteractiveDashboard>();
await dashboard.RunAsync(CancellationToken.None);

static void RunDecisionDryRun(IServiceProvider services)
{
    var panel = services.GetRequiredService<DecisionPanelService>();

    var demoState = new GameStateSnapshot(
        Stage: "3-1",
        Phase: GamePhase.Planning,
        Gold: 60,
        Level: 5,
        Exp: 0,
        Hp: 80,
        Streak: 2,
        BoardUnits: new BoardUnitState[]
        {
            new(0, "盖伦", 1, 1),
            new(1, "拉克丝", 1, 1),
            new(2, "佐伊", 1, 1),
        },
        BenchUnits: new BoardUnitState[]
        {
            new(3, "拉克丝", 1, 1),
            new(4, "艾希", 1, 1),
        },
        ShopCards: Array.Empty<ShopCardState>(),
        Opponents: Array.Empty<OpponentSnapshot>(),
        Version: 0);

    var result = panel.EvaluateAlgorithm(demoState);

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