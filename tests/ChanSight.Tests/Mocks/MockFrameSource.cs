using System.Threading.Channels;
using ChanSight.Core.Interfaces;
using ChanSight.Core.Models;

namespace ChanSight.Tests.Mocks;

public sealed class MockFrameSource : IFrameSource
{
    private readonly Channel<CapturedFrame> _channel;

    public MockFrameSource(int capacity = 4)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true
        };

        _channel = Channel.CreateBounded<CapturedFrame>(options);
    }

    public ChannelReader<CapturedFrame> Frames => _channel.Reader;

    public bool TryWrite(CapturedFrame frame) => _channel.Writer.TryWrite(frame);

    public void Complete() => _channel.Writer.TryComplete();

    public async IAsyncEnumerable<CapturedFrame> ReadAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var frame in Frames.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return frame;
        }
    }
}
