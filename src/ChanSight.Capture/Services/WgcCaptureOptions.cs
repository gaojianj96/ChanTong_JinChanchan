namespace ChanSight.Capture.Services;

public sealed record WgcCaptureOptions
{
    public const int DefaultChannelCapacity = 2;
    public const int DefaultTargetFramesPerSecond = 30;

    public static WgcCaptureOptions Default { get; } = new();

    public int TargetFramesPerSecond { get; init; } = DefaultTargetFramesPerSecond;

    public int ChannelCapacity { get; init; } = DefaultChannelCapacity;

    internal TimeSpan MinimumFrameInterval
    {
        get
        {
            var fps = TargetFramesPerSecond switch
            {
                <= 10 => 10,
                <= 30 => 30,
                _ => 60
            };

            return TimeSpan.FromSeconds(1d / fps);
        }
    }

    internal void Validate()
    {
        if (ChannelCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ChannelCapacity), "Channel capacity must be positive.");
        }

        if (TargetFramesPerSecond is not (10 or 30 or 60))
        {
            throw new ArgumentOutOfRangeException(
                nameof(TargetFramesPerSecond),
                "Target FPS must be one of 10, 30, or 60.");
        }
    }
}
