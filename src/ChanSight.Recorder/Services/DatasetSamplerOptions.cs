using ChanSight.Core.Interfaces;

namespace ChanSight.Recorder.Services;

public sealed record DatasetSamplerOptions
{
    public const double DefaultIntervalSeconds = 2.0;
    public const string DefaultImageFormat = "jpg";
    public static readonly HotKeyDefinition DefaultHotKey = new(VirtualKeyCode.F7, HotKeyModifiers.None, "Take manual snapshot");

    public static DatasetSamplerOptions Default { get; } = new();

    public string OutputDirectory { get; init; } = string.Empty;

    public double IntervalSeconds { get; init; } = DefaultIntervalSeconds;

    public string ImageFormat { get; init; } = DefaultImageFormat;

    public HotKeyDefinition? ManualSnapshotHotKey { get; init; } = DefaultHotKey;

    internal void Validate()
    {
        if (IntervalSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(IntervalSeconds), "Interval must be positive.");
        var fmt = ImageFormat.ToLowerInvariant();
        if (fmt is not "jpg" and not "jpeg" and not "png")
            throw new ArgumentException($"Unsupported image format: {ImageFormat}. Supported: jpg, jpeg, png.");
    }
}