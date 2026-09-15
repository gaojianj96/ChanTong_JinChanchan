namespace ChanSight.Vision.Models;

public sealed record InferenceLatencyStats
{
    public int Warmup { get; init; }
    public int Iterations { get; init; }
    public double MeanMs { get; init; }
    public double MinMs { get; init; }
    public double MaxMs { get; init; }
    public double P95Ms { get; init; }
    public string Device { get; init; } = string.Empty;
    public string InputShape { get; init; } = string.Empty;
}