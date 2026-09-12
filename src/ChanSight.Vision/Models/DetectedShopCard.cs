namespace ChanSight.Vision.Models;

public sealed class DetectedShopCard
{
    public int Slot { get; }
    public string Name { get; }
    public int Cost { get; }
    public float Confidence { get; }

    public DetectedShopCard(int slot, string name, int cost, float confidence)
    {
        if (slot < 0 || slot > 4)
            throw new ArgumentOutOfRangeException(nameof(slot));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (confidence < 0 || confidence > 1)
            throw new ArgumentOutOfRangeException(nameof(confidence));

        Slot = slot;
        Name = name;
        Cost = cost;
        Confidence = confidence;
    }
}