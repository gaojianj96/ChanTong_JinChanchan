using System.Runtime.InteropServices;
using Windows.Graphics.Capture;

namespace ChanSight.Capture.Services;

internal static class GraphicsCaptureItemInterop
{
    public static GraphicsCaptureItem CreateForWindow(nint hwnd)
    {
        if (hwnd == 0)
            throw new ArgumentException("Window handle cannot be zero.", nameof(hwnd));

        var windowId = new Windows.UI.WindowId((ulong)hwnd);
        var item = GraphicsCaptureItem.TryCreateFromWindowId(windowId);

        if (item is null)
        {
            throw new InvalidOperationException(
                $"Failed to create GraphicsCaptureItem for window 0x{hwnd:X}. The window may be minimized, hidden, or not a valid capture target.");
        }

        return item;
    }
}
