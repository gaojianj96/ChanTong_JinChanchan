namespace ChanSight.Core.Models;

public readonly record struct FrameResolution(int Width, int Height)
{
    public static FrameResolution Empty { get; } = new(0, 0);

    public bool IsEmpty => Width <= 0 || Height <= 0;
}
