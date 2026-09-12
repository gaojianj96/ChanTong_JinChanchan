using ChanSight.Capture.Services;
using ChanSight.Core.Models;
using FluentAssertions;

namespace ChanSight.Tests.Capture;

public sealed class WindowFinderTests
{
    [Fact]
    public async Task FindWindowsAsync_MatchesDefaultChineseGameKeyword()
    {
        var api = new FakeNativeWindowApi(
            Window(1, "金铲铲之战"),
            Window(2, "Calculator"));
        var finder = new WindowFinder(api);

        var targets = await finder.FindWindowsAsync();

        targets.Should().ContainSingle()
            .Which.Hwnd.Should().Be((nint)1);
    }

    [Fact]
    public async Task FindWindowsAsync_FiltersByProcessIdAndKeyword()
    {
        var api = new FakeNativeWindowApi(
            Window(1, "MuMu模拟器", processId: 100),
            Window(2, "MuMu模拟器", processId: 200));
        var finder = new WindowFinder(api);

        var targets = await finder.FindWindowsAsync(new WindowSearchOptions { ProcessId = 200 });

        targets.Should().ContainSingle()
            .Which.Hwnd.Should().Be((nint)2);
    }

    [Fact]
    public async Task FindWindowsAsync_HwndMatchBypassesKeyword()
    {
        var api = new FakeNativeWindowApi(Window(42, "Unexpected Game Window"));
        var finder = new WindowFinder(api);

        var targets = await finder.FindWindowsAsync(new WindowSearchOptions { Hwnd = 42 });

        targets.Should().ContainSingle()
            .Which.Title.Should().Be("Unexpected Game Window");
    }

    [Fact]
    public async Task GetWindowTargetAsync_UsesClientBoundsAndDpi()
    {
        var api = new FakeNativeWindowApi(Window(
            3,
            "Tencent手游助手",
            clientBounds: new WindowBounds(120, 80, 1600, 900),
            dpi: new DpiInfo(144, 144)));
        var finder = new WindowFinder(api);

        var target = await finder.GetWindowTargetAsync(3);

        target.Should().NotBeNull();
        target!.PhysicalBounds.Should().Be(new WindowBounds(120, 80, 1600, 900));
        target.PhysicalSize.Should().Be(new FrameResolution(1600, 900));
        target.Dpi.ScaleX.Should().BeApproximately(1.5, 0.001);
    }

    [Fact]
    public async Task FindWindowsAsync_SkipsMinimizedAndInvalidWindows()
    {
        var api = new FakeNativeWindowApi(
            Window(1, "MuMu模拟器", isMinimized: true),
            Window(2, string.Empty),
            Window(3, "夜神模拟器", clientBounds: new WindowBounds(0, 0, 0, 720)));
        var finder = new WindowFinder(api);

        var targets = await finder.FindWindowsAsync();

        targets.Should().BeEmpty();
    }

    private static NativeWindowInfo Window(
        nint hwnd,
        string title,
        int processId = 1,
        bool isVisible = true,
        bool isMinimized = false,
        WindowBounds? clientBounds = null,
        DpiInfo? dpi = null)
    {
        return new NativeWindowInfo(
            hwnd,
            title,
            "FakeWindow",
            processId,
            isVisible,
            isMinimized,
            false,
            clientBounds ?? new WindowBounds(10, 20, 1280, 720),
            null,
            dpi ?? DpiInfo.Default);
    }

    private sealed class FakeNativeWindowApi : INativeWindowApi
    {
        private readonly Dictionary<nint, NativeWindowInfo> windows;

        public FakeNativeWindowApi(params NativeWindowInfo[] windows)
        {
            this.windows = windows.ToDictionary(window => window.Hwnd);
        }

        public IReadOnlyList<nint> EnumerateTopLevelWindows()
        {
            return windows.Keys.ToArray();
        }

        public NativeWindowInfo? GetWindowInfo(nint hwnd)
        {
            return windows.GetValueOrDefault(hwnd);
        }
    }
}
