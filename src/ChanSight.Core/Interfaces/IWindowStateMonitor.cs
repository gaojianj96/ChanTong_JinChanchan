using ChanSight.Core.Models;

namespace ChanSight.Core.Interfaces;

public interface IWindowStateMonitor : IAsyncDisposable
{
    event EventHandler<WindowTarget>? WindowBoundsChanged;

    event EventHandler<nint>? WindowClosed;

    void Start(WindowTarget target, TimeSpan? pollingInterval = null);

    void Stop();
}
