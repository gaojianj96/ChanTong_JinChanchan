namespace ChanSight.Core.Models;

public sealed record SessionMeta
{
    public SessionMeta(
        Guid id,
        string name,
        string outputDirectory,
        WindowTarget target,
        DateTimeOffset startedAt,
        int targetFps,
        FrameResolution resolution)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Session id cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Session name cannot be empty.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Output directory cannot be empty.", nameof(outputDirectory));
        }

        if (targetFps <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetFps), "Target FPS must be positive.");
        }

        if (resolution.IsEmpty)
        {
            throw new ArgumentOutOfRangeException(nameof(resolution), "Session resolution must be positive.");
        }

        Id = id;
        Name = name;
        OutputDirectory = outputDirectory;
        Target = target ?? throw new ArgumentNullException(nameof(target));
        StartedAt = startedAt;
        TargetFps = targetFps;
        Resolution = resolution;
    }

    public Guid Id { get; }

    public string Name { get; }

    public string OutputDirectory { get; }

    public WindowTarget Target { get; }

    public DateTimeOffset StartedAt { get; }

    public int TargetFps { get; }

    public FrameResolution Resolution { get; }
}
