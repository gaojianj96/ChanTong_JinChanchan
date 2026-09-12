namespace ChanSight.Vision.Models;

public sealed class YoloExportResult
{
    public string ExportDirectory { get; set; } = string.Empty;
    public int TrainImages { get; set; }
    public int ValImages { get; set; }
    public int TotalImages { get; set; }
}