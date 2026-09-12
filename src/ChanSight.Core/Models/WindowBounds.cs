namespace ChanSight.Core.Models;

public readonly record struct WindowBounds(int Left, int Top, int Width, int Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public int Right => Left + Width;

    public int Bottom => Top + Height;
}
