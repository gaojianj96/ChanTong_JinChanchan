using ChanSight.Cli.Retrospective;

namespace ChanSight.Cli;

public sealed record CliVisionOptions
{
    public bool VisionDryRun { get; init; }
    public string? ProbeModelPath { get; init; }
    public int? ProbeSize { get; init; }
    public bool DecisionDryRun { get; init; }
    public ReplayOptions? Replay { get; init; }

    public CliVisionOptions()
    {
    }

    public static CliVisionOptions Parse(string[] args)
    {
        var dryRun = false;
        string? probePath = null;
        int? probeSize = null;
        var decisionDryRun = false;

        string? replayPath = null;
        var replayExport = false;
        string? replayExportPath = null;
        var replayPrivacy = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--vision-dry-run":
                    dryRun = true;
                    break;
                case "--decision-dry-run":
                    decisionDryRun = true;
                    break;
                case "--probe" when i + 1 < args.Length:
                    probePath = args[++i];
                    break;
                case "--probe-size" when i + 1 < args.Length && int.TryParse(args[i + 1], out var sz):
                    probeSize = sz;
                    i++;
                    break;
                case "--replay" when i + 1 < args.Length:
                    replayPath = args[++i];
                    break;
                case "--replay-export":
                    replayExport = true;
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        replayExportPath = args[++i];
                    }

                    break;
                case "--replay-privacy":
                    replayPrivacy = true;
                    break;
            }
        }

        return new CliVisionOptions
        {
            VisionDryRun = dryRun,
            ProbeModelPath = probePath,
            ProbeSize = probeSize,
            DecisionDryRun = decisionDryRun,
            Replay = replayPath is null ? null : new ReplayOptions(replayPath, replayExport, replayExportPath, replayPrivacy),
        };
    }
}