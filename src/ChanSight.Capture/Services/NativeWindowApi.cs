using ChanSight.Capture.Win32;
using ChanSight.Core.Models;

namespace ChanSight.Capture.Services;

internal sealed class NativeWindowApi : INativeWindowApi
{
    public IReadOnlyList<nint> EnumerateTopLevelWindows()
    {
        var windows = new List<nint>();

        User32.EnumWindows((hwnd, _) =>
        {
            windows.Add(hwnd);
            return true;
        }, 0);

        return windows;
    }

    public NativeWindowInfo? GetWindowInfo(nint hwnd)
    {
        if (hwnd == 0)
        {
            return null;
        }

        var title = User32.GetWindowText(hwnd);
        var className = User32.GetClassName(hwnd);
        _ = User32.GetWindowThreadProcessId(hwnd, out var processId);

        return new NativeWindowInfo(
            hwnd,
            title,
            className,
            unchecked((int)processId),
            User32.IsWindowVisible(hwnd),
            User32.IsIconic(hwnd),
            hwnd == User32.GetForegroundWindow(),
            TryGetClientBounds(hwnd, out var clientBounds) ? clientBounds : null,
            DwmApi.TryGetExtendedFrameBounds(hwnd, out var extendedBounds) ? ToBounds(extendedBounds) : null,
            GetDpi(hwnd));
    }

    private static DpiInfo GetDpi(nint hwnd)
    {
        try
        {
            var dpi = User32.GetDpiForWindow(hwnd);
            return dpi == 0 ? DpiInfo.Default : new DpiInfo(dpi, dpi);
        }
        catch (EntryPointNotFoundException)
        {
            return DpiInfo.Default;
        }
    }

    private static bool TryGetClientBounds(nint hwnd, out WindowBounds bounds)
    {
        bounds = default;

        if (!User32.GetClientRect(hwnd, out var rect) || rect.Width <= 0 || rect.Height <= 0)
        {
            return false;
        }

        var origin = new NativePoint(0, 0);
        if (!User32.ClientToScreen(hwnd, ref origin))
        {
            return false;
        }

        bounds = new WindowBounds(origin.X, origin.Y, rect.Width, rect.Height);
        return true;
    }

    private static WindowBounds ToBounds(NativeRect rect)
    {
        return new WindowBounds(rect.Left, rect.Top, rect.Width, rect.Height);
    }
}
