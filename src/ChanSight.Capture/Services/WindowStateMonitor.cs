using ChanSight.Core.Interfaces;
using ChanSight.Core.Models;

namespace ChanSight.Capture.Services;

public sealed class WindowStateMonitor : IWindowStateMonitor
{
    private readonly IWindowFinder windowFinder;
    private readonly object gate = new();
    private CancellationTokenSource? cancellation;
    private Task? monitorTask;

    public WindowStateMonitor(IWindowFinder windowFinder)
    {
        this.windowFinder = windowFinder;
    }

    public event EventHandler<WindowTarget>? WindowBoundsChanged;

    public event EventHandler<nint>? WindowClosed;

    public void Start(WindowTarget target, TimeSpan? pollingInterval = null)
    {
        ArgumentNullException.ThrowIfNull(target);

        lock (gate)
        {
            StopCore();

            cancellation = new CancellationTokenSource();
            monitorTask = MonitorAsync(target, pollingInterval ?? TimeSpan.FromMilliseconds(500), cancellation.Token);
        }
    }

    public void Stop()
    {
        lock (gate)
        {
            StopCore();
        }
    }

    public async ValueTask DisposeAsync()
    {
        Task? task;
        lock (gate)
        {
            task = StopCore();
        }

        if (task is not null)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private async Task MonitorAsync(WindowTarget initialTarget, TimeSpan pollingInterval, CancellationToken cancellationToken)
    {
        var lastTarget = initialTarget;

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(pollingInterval, cancellationToken).ConfigureAwait(false);

            var current = await windowFinder.GetWindowTargetAsync(initialTarget.Hwnd, cancellationToken)
                .ConfigureAwait(false);

            if (current is null)
            {
                WindowClosed?.Invoke(this, initialTarget.Hwnd);
                return;
            }

            if (current.PhysicalBounds != lastTarget.PhysicalBounds || current.Dpi != lastTarget.Dpi)
            {
                lastTarget = current;
                WindowBoundsChanged?.Invoke(this, current);
            }
        }
    }

    private Task? StopCore()
    {
        cancellation?.Cancel();
        cancellation?.Dispose();
        cancellation = null;

        var task = monitorTask;
        monitorTask = null;
        return task;
    }
}
