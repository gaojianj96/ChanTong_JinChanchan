namespace ChanSight.Core.Models;

public sealed record WindowTarget
{
    public WindowTarget(nint hwnd, string title, FrameResolution physicalSize, DpiInfo dpi)
        : this(hwnd, title, new WindowBounds(0, 0, physicalSize.Width, physicalSize.Height), dpi)
    {
    }

    public WindowTarget(nint hwnd, string title, WindowBounds physicalBounds, DpiInfo dpi)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Window title cannot be empty.", nameof(title));
        }

        if (physicalBounds.IsEmpty)
        {
            throw new ArgumentOutOfRangeException(nameof(physicalBounds), "Window bounds must have a positive size.");
        }

        Hwnd = hwnd;
        Title = title;
        PhysicalBounds = physicalBounds;
        PhysicalSize = new FrameResolution(physicalBounds.Width, physicalBounds.Height);
        Dpi = dpi;
    }

    public nint Hwnd { get; }

    public string Title { get; }

    public WindowBounds PhysicalBounds { get; }

    public FrameResolution PhysicalSize { get; }

    public DpiInfo Dpi { get; }
}
