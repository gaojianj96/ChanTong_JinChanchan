using System.Threading.Channels;
using ChanSight.Core.Models;

namespace ChanSight.Core.Interfaces;

public interface IFrameSource
{
    ChannelReader<CapturedFrame> Frames { get; }

    IAsyncEnumerable<CapturedFrame> ReadAllAsync(CancellationToken cancellationToken = default);
}
