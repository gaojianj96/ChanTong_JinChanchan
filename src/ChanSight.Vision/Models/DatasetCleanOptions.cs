namespace ChanSight.Vision.Models;

public sealed class DatasetCleanOptions
{
    public int HammingDistanceThreshold { get; set; } = 5;
    public double VarianceThreshold { get; set; } = 10.0;
    public string[] ImageExtensions { get; set; } = [".png", ".jpg", ".jpeg", ".bmp"];
}