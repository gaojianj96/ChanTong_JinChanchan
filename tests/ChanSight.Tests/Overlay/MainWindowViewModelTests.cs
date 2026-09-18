using ChanSight.Core.Interfaces;
using ChanSight.Core.Models;
using ChanSight.Overlay.ViewModels;
using FluentAssertions;

namespace ChanSight.Tests.Overlay;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public async Task RefreshWindowsAsync_SingleMatch_AutoSelectsAndFillsOptions()
    {
        var vm = new MainWindowViewModel(Finder(
            Target(1, "MuMu模拟器", 1280, 720)));

        await vm.RefreshWindowsAsync();

        vm.WindowOptions.Should().ContainSingle()
            .Which.Should().Be("MuMu模拟器 [1280x720]");
        vm.SelectedWindowIndex.Should().Be(0);
        vm.HasWindows.Should().BeTrue();
        vm.SelectedTarget.Should().NotBeNull();
        vm.SelectedTarget!.Title.Should().Be("MuMu模拟器");
        vm.Status.Should().Be("已找到 1 个窗口, 准备捕获…");
    }

    [Fact]
    public async Task RefreshWindowsAsync_MultipleMatches_DoesNotAutoSelect()
    {
        var vm = new MainWindowViewModel(Finder(
            Target(1, "MuMu模拟器", 1280, 720),
            Target(2, "夜神模拟器", 1600, 900)));

        await vm.RefreshWindowsAsync();

        vm.WindowOptions.Should().HaveCount(2);
        vm.SelectedWindowIndex.Should().Be(-1);
        vm.HasWindows.Should().BeTrue();
        vm.SelectedTarget.Should().BeNull();
        vm.Status.Should().Contain("找到 2 个窗口");
    }

    [Fact]
    public async Task RefreshWindowsAsync_NoMatch_ClearsOptionsAndSetsNoWindowStatus()
    {
        var vm = new MainWindowViewModel(Finder());

        await vm.RefreshWindowsAsync();

        vm.WindowOptions.Should().BeEmpty();
        vm.HasWindows.Should().BeFalse();
        vm.SelectedWindowIndex.Should().Be(-1);
        vm.SelectedTarget.Should().BeNull();
        vm.Status.Should().Be("未找到游戏窗口");
        vm.NoWindowHint.Should().Contain("金铲铲之战");
    }

    private static IWindowFinder Finder(params WindowTarget[] targets)
        => new StubWindowFinder(targets);

    private static WindowTarget Target(nint hwnd, string title, int width, int height)
        => new(hwnd, title, new WindowBounds(0, 0, width, height), DpiInfo.Default);

    private sealed class StubWindowFinder : IWindowFinder
    {
        private readonly IReadOnlyList<WindowTarget> _targets;

        public StubWindowFinder(IReadOnlyList<WindowTarget> targets) => _targets = targets;

        public ValueTask<IReadOnlyList<WindowTarget>> FindWindowsAsync(
            WindowSearchOptions? options = null,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_targets);

        public ValueTask<WindowTarget?> GetWindowTargetAsync(nint hwnd, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<WindowTarget?>(_targets.FirstOrDefault(t => t.Hwnd == hwnd));
    }
}