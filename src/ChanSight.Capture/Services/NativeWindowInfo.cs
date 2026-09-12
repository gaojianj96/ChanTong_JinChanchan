using ChanSight.Core.Models;

namespace ChanSight.Capture.Services;

internal sealed record NativeWindowInfo(
    nint Hwnd,
    string Title,
    string ClassName,
    int ProcessId,
    bool IsVisible,
    bool IsMinimized,
    bool IsForeground,
    WindowBounds? ClientBounds,
    WindowBounds? ExtendedFrameBounds,
    DpiInfo Dpi);
