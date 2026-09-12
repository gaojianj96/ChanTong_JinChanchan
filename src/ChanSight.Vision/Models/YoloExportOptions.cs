namespace ChanSight.Vision.Models;

public sealed class YoloExportOptions
{
    public double TrainRatio { get; set; } = 0.8;
    public int RandomSeed { get; set; } = 42;
    public Dictionary<int, string> ClassNames { get; set; } = new()
    {
        [0] = "champion",
    };
}