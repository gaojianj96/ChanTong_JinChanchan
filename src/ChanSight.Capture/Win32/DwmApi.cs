using System.Runtime.InteropServices;

namespace ChanSight.Capture.Win32;

internal static partial class DwmApi
{
    internal const int DwmwaExtendedFrameBounds = 9;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    internal static extern int DwmGetWindowAttribute(
        nint hwnd,
        int dwAttribute,
        out NativeRect pvAttribute,
        int cbAttribute);

    internal static bool TryGetExtendedFrameBounds(nint hwnd, out NativeRect bounds)
    {
        var result = DwmGetWindowAttribute(
            hwnd,
            DwmwaExtendedFrameBounds,
            out bounds,
            Marshal.SizeOf<NativeRect>());

        return result == 0 && bounds.Width > 0 && bounds.Height > 0;
    }
}
