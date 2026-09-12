using ChanSight.Core.Models;

namespace ChanSight.Core.Interfaces;

public interface IScreenCaptureService : IAsyncDisposable
{
    bool IsRunning { get; }

    IFrameSource FrameSource { get; }

    ValueTask StartAsync(WindowTarget target, CancellationToken cancellationToken = default);

    ValueTask StopAsync(CancellationToken cancellationToken = default);
}
