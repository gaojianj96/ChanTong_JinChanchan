using ChanSight.Core.Interfaces;
using ChanSight.Core.Models;

namespace ChanSight.Capture.Services;

public sealed class WindowFinder : IWindowFinder
{
    private readonly INativeWindowApi nativeWindowApi;

    public WindowFinder()
        : this(new NativeWindowApi())
    {
    }

    internal WindowFinder(INativeWindowApi nativeWindowApi)
    {
        this.nativeWindowApi = nativeWindowApi;
    }

    public ValueTask<IReadOnlyList<WindowTarget>> FindWindowsAsync(
        WindowSearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new WindowSearchOptions();

        var targets = new List<WindowTarget>();
        foreach (var hwnd in nativeWindowApi.EnumerateTopLevelWindows())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var info = nativeWindowApi.GetWindowInfo(hwnd);
            if (info is null || !Matches(info, options))
            {
                continue;
            }

            var target = CreateTarget(info);
            if (target is not null)
            {
                targets.Add(target);
            }
        }

        return ValueTask.FromResult<IReadOnlyList<WindowTarget>>(targets);
    }

    public ValueTask<WindowTarget?> GetWindowTargetAsync(nint hwnd, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var info = nativeWindowApi.GetWindowInfo(hwnd);
        return ValueTask.FromResult(info is null ? null : CreateTarget(info));
    }

    private static bool Matches(NativeWindowInfo info, WindowSearchOptions options)
    {
        if (!info.IsVisible)
        {
            return false;
        }

        if (info.IsMinimized && !options.IncludeMinimized)
        {
            return false;
        }

        if (options.Hwnd is { } hwnd)
        {
            return info.Hwnd == hwnd;
        }

        if (options.ProcessId is { } processId && info.ProcessId != processId)
        {
            return false;
        }

        var keywords = options.TitleKeywords;
        if (keywords.Count == 0)
        {
            return !string.IsNullOrWhiteSpace(info.Title);
        }

        return keywords.Any(keyword =>
            !string.IsNullOrWhiteSpace(keyword) &&
            info.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static WindowTarget? CreateTarget(NativeWindowInfo info)
    {
        if (string.IsNullOrWhiteSpace(info.Title))
        {
            return null;
        }

        var bounds = info.ClientBounds ?? info.ExtendedFrameBounds;
        return bounds is { IsEmpty: false }
            ? new WindowTarget(info.Hwnd, info.Title, bounds.Value, info.Dpi)
            : null;
    }
}
