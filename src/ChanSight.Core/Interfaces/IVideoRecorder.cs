using ChanSight.Core.Models;

namespace ChanSight.Core.Interfaces;

public interface IVideoRecorder : IAsyncDisposable
{
    bool IsRecording { get; }

    SessionMeta? CurrentSession { get; }

    ValueTask StartAsync(SessionMeta session, IFrameSource frameSource, CancellationToken cancellationToken = default);

    ValueTask StopAsync(CancellationToken cancellationToken = default);
}
