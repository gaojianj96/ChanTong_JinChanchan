namespace ChanSight.Capture.Services;

internal interface INativeWindowApi
{
    IReadOnlyList<nint> EnumerateTopLevelWindows();

    NativeWindowInfo? GetWindowInfo(nint hwnd);
}
