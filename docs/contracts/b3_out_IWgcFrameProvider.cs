using ChanSight.Core.Models;

namespace ChanSight.Capture.Services;

internal interface IWgcFrameProvider : IAsyncDisposable
{
    event EventHandler<CapturedFrame>? FrameReady;

    event EventHandler? CaptureEnded;

    ValueTask StartAsync(WindowTarget target, CancellationToken cancellationToken);

    ValueTask StopAsync(CancellationToken cancellationToken);
}