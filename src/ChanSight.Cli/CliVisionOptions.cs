namespace ChanSight.Cli;

public sealed record CliVisionOptions
{
    public bool VisionDryRun { get; init; }
    public string? ProbeModelPath { get; init; }

    public CliVisionOptions()
    {
    }

    public static CliVisionOptions Parse(string[] args)
    {
        var dryRun = false;
        string? probePath = null;

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
            }
        }

        return new CliVisionOptions { VisionDryRun = dryRun, ProbeModelPath = probePath };
    }
}