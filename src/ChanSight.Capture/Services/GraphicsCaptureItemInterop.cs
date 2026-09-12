using System.Runtime.InteropServices;
using Windows.Graphics.Capture;
using WinRT;

namespace ChanSight.Capture.Services;

internal static class GraphicsCaptureItemInterop
{
    public static GraphicsCaptureItem CreateForWindow(nint hwnd)
    {
        if (hwnd == 0)
        {
            throw new ArgumentException("Window handle cannot be zero.", nameof(hwnd));
        }

        var interop = GraphicsCaptureItem.As<IGraphicsCaptureItemInterop>();
        var iid = typeof(GraphicsCaptureItem).GUID;
        var itemPointer = interop.CreateForWindow(hwnd, ref iid);
        try
        {
            return MarshalInterface<GraphicsCaptureItem>.FromAbi(itemPointer);
        }
        finally
        {
            Marshal.Release(itemPointer);
        }
    }

    [ComImport]
    [Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        nint CreateForWindow(nint window, ref Guid iid);
    }
}
