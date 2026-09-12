namespace ChanSight.Vision.Models;

public sealed class CleanReport
{
    public int TotalFrames { get; set; }
    public int EffectiveFrames { get; set; }
    public int DuplicatesFiltered { get; set; }
    public int BadFramesFiltered { get; set; }
    public string InputDirectory { get; set; } = string.Empty;
    public string OutputDirectory { get; set; } = string.Empty;
    public DatasetCleanOptions Options { get; set; } = new();
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}