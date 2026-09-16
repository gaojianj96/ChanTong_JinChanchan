namespace ChanSight.Capture.Services;

public sealed record WgcCaptureOptions
{
    public const int DefaultChannelCapacity = 2;
    public const int DefaultFrameIntervalMilliseconds = 500;
    public const int MinimumFrameIntervalMilliseconds = 50;
    public const int MaximumFrameIntervalMilliseconds = 10_000;

    public static WgcCaptureOptions Default { get; } = new();

    public TimeSpan FrameInterval { get; init; } = TimeSpan.FromMilliseconds(DefaultFrameIntervalMilliseconds);

    public int ChannelCapacity { get; init; } = DefaultChannelCapacity;

    internal void Validate()
    {
        if (FrameInterval < TimeSpan.FromMilliseconds(MinimumFrameIntervalMilliseconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(FrameInterval),
                $"Frame interval must be at least {MinimumFrameIntervalMilliseconds} ms.");
        }

        if (FrameInterval > TimeSpan.FromMilliseconds(MaximumFrameIntervalMilliseconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(FrameInterval),
                $"Frame interval must be at most {MaximumFrameIntervalMilliseconds} ms.");
        }

        if (ChannelCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ChannelCapacity), "Channel capacity must be positive.");
        }
    }
}