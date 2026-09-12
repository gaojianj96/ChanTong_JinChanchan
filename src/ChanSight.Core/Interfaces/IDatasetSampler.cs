using ChanSight.Core.Models;

namespace ChanSight.Core.Interfaces;

public interface IDatasetSampler : IAsyncDisposable
{
    ValueTask StartAsync(SessionMeta session, IFrameSource frameSource, CancellationToken cancellationToken = default);

    ValueTask StopAsync(CancellationToken cancellationToken = default);
}
