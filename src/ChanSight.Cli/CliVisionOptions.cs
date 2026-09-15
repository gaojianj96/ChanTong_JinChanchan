namespace ChanSight.Cli;

public sealed record CliVisionOptions
{
    public bool VisionDryRun { get; init; }
    public string? ProbeModelPath { get; init; }
    public int? ProbeSize { get; init; }

    public CliVisionOptions()
    {
    }

    public static CliVisionOptions Parse(string[] args)
    {
        var dryRun = false;
        string? probePath = null;
        int? probeSize = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--vision-dry-run":
                    dryRun = true;
                    break;
                case "--probe" when i + 1 < args.Length:
                    probePath = args[++i];
                    break;
                case "--probe-size" when i + 1 < args.Length && int.TryParse(args[i + 1], out var sz):
                    probeSize = sz;
                    i++;
                    break;
            }
        }

        return new CliVisionOptions { VisionDryRun = dryRun, ProbeModelPath = probePath, ProbeSize = probeSize };
    }
}