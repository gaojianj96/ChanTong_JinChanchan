namespace ChanSight.Core.Models;

public readonly record struct DpiInfo(uint DpiX, uint DpiY)
{
    public static DpiInfo Default { get; } = new(96, 96);

    public double ScaleX => DpiX / 96.0;

    public double ScaleY => DpiY / 96.0;
}
