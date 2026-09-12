using ChanSight.Capture.Services;
using ChanSight.Core.Interfaces;
using ChanSight.Core.Models;
using FluentAssertions;

namespace ChanSight.Tests.Capture;

public sealed class WindowStateMonitorTests
{
    [Fact]
    public async Task Start_RaisesBoundsChangedWhenWindowMoves()
    {
        var initial = Target(new WindowBounds(0, 0, 1280, 720));
        var moved = Target(new WindowBounds(10, 20, 1280, 720));
        var finder = new SequenceWindowFinder(initial, moved);
        var monitor = new WindowStateMonitor(finder);
        var changed = new TaskCompletionSource<WindowTarget>(TaskCreationOptions.RunContinuationsAsynchronously);
        monitor.WindowBoundsChanged += (_, target) => changed.TrySetResult(target);

        monitor.Start(initial, TimeSpan.FromMilliseconds(5));

        var target = await changed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        target.PhysicalBounds.Should().Be(moved.PhysicalBounds);

        await monitor.DisposeAsync();
    }

    [Fact]
    public async Task Start_RaisesClosedWhenWindowDisappears()
    {
        var initial = Target(new WindowBounds(0, 0, 1280, 720));
        var finder = new SequenceWindowFinder([null]);
        var monitor = new WindowStateMonitor(finder);
        var closed = new TaskCompletionSource<nint>(TaskCreationOptions.RunContinuationsAsynchronously);
        monitor.WindowClosed += (_, hwnd) => closed.TrySetResult(hwnd);

        monitor.Start(initial, TimeSpan.FromMilliseconds(5));

        var hwnd = await closed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        hwnd.Should().Be(initial.Hwnd);

        await monitor.DisposeAsync();
    }

    private static WindowTarget Target(WindowBounds bounds)
    {
        return new WindowTarget(99, "MuMu模拟器", bounds, DpiInfo.Default);
    }

    private sealed class SequenceWindowFinder : IWindowFinder
    {
        private readonly Queue<WindowTarget?> sequence;
        private WindowTarget? last;

        public SequenceWindowFinder(params WindowTarget?[] sequence)
        {
            this.sequence = new Queue<WindowTarget?>(sequence);
        }

        public ValueTask<IReadOnlyList<WindowTarget>> FindWindowsAsync(
            WindowSearchOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<IReadOnlyList<WindowTarget>>(last is null ? [] : [last]);
        }

        public ValueTask<WindowTarget?> GetWindowTargetAsync(nint hwnd, CancellationToken cancellationToken = default)
        {
            last = sequence.Count > 0 ? sequence.Dequeue() : last;
            return ValueTask.FromResult(last);
        }
    }
}
