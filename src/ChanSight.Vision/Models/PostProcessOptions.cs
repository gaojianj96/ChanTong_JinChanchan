namespace ChanSight.Vision.Models;

using OpenCvSharp;

public sealed record PostProcessOptions
{
    public double ConfidenceThreshold { get; init; } = 0.25;
    public double NmsThreshold { get; init; } = 0.45;
    public Rect? RoiBounds { get; init; }
    public int MaxDetections { get; init; } = 256;
}