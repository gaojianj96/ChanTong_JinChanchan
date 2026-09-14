namespace ChanSight.Vision.Models;

public sealed record GoNoGoResult
{
    public bool Passed { get; init; }
    public double ThresholdMs { get; init; }
    public double ObservedMs { get; init; }
    public double RecommendedDownscaleFactor { get; init; } = 1.0;
    public string Note { get; init; } = string.Empty;
}