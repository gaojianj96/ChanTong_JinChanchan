using OpenCvSharp;

namespace ChanSight.Vision.Models;

public sealed class DetectedUnit
{
    public string Name { get; }
    public int Star { get; }
    public int Cost { get; }
    public IReadOnlyList<string> Items { get; }
    public float Confidence { get; }
    public Rect BoundingBox { get; }
    public Point2d? BoardPosition { get; }
    public int? BenchIndex { get; }

    public DetectedUnit(
        string name,
        int star,
        int cost,
        IReadOnlyList<string> items,
        float confidence,
        Rect boundingBox,
        Point2d? boardPosition = null,
        int? benchIndex = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (confidence < 0 || confidence > 1)
            throw new ArgumentOutOfRangeException(nameof(confidence));

        Name = name;
        Star = star;
        Cost = cost;
        Items = items ?? Array.Empty<string>();
        Confidence = confidence;
        BoundingBox = boundingBox;
        BoardPosition = boardPosition;
        BenchIndex = benchIndex;
    }
}