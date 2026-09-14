using System.Threading.Channels;
using ChanSight.Core.Interfaces;
using ChanSight.Core.Models;

namespace ChanSight.Capture.Services;

public sealed class WgcCaptureService : IScreenCaptureService, IFrameSource
{
    private readonly object syncRoot = new();
    private readonly WgcCaptureOptions options;
    private readonly Func<IWgcFrameProvider> providerFactory;
    private readonly Channel<CapturedFrame> channel;
    private IWgcFrameProvider? provider;
    private DateTimeOffset lastAcceptedFrameTimestamp = DateTimeOffset.MinValue;
    private bool disposed;

    public WgcCaptureService()
        : this(WgcCaptureOptions.Default)
    {
    }

    public WgcCaptureService(WgcCaptureOptions options)
        : this(options, static () => new WindowsGraphicsCaptureFrameProvider())
    {
    }

    internal WgcCaptureService(WgcCaptureOptions options, Func<IWgcFrameProvider> providerFactory)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.options.Validate();
        this.providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));

        var channelOptions = new BoundedChannelOptions(options.ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        };

        channel = Channel.CreateBounded<CapturedFrame>(channelOptions);
    }

    public bool IsRunning { get; private set; }

    public IFrameSource FrameSource => this;

    public ChannelReader<CapturedFrame> Frames => channel.Reader;

    public event EventHandler? CaptureEnded;

    public async ValueTask StartAsync(WindowTarget target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ThrowIfDisposed();

        IWgcFrameProvider nextProvider;
        lock (syncRoot)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("Capture is already running.");
            }

            nextProvider = providerFactory();
            provider = nextProvider;
            lastAcceptedFrameTimestamp = DateTimeOffset.MinValue;
            IsRunning = true;
        }

        nextProvider.FrameReady += OnFrameReady;
        nextProvider.CaptureEnded += OnProviderCaptureEnded;

        try
        {
            await nextProvider.StartAsync(target, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            nextProvider.FrameReady -= OnFrameReady;
            nextProvider.CaptureEnded -= OnProviderCaptureEnded;
            await nextProvider.DisposeAsync().ConfigureAwait(false);

            lock (syncRoot)
            {
                if (ReferenceEquals(provider, nextProvider))
                {
                    provider = null;
                    IsRunning = false;
                }
            }

            throw;
        }
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        IWgcFrameProvider? currentProvider;
        lock (syncRoot)
        {
            currentProvider = provider;
            provider = null;
            IsRunning = false;
        }

        if (currentProvider is null)
        {
            return;
        }

        currentProvider.FrameReady -= OnFrameReady;
        currentProvider.CaptureEnded -= OnProviderCaptureEnded;
        await currentProvider.StopAsync(cancellationToken).ConfigureAwait(false);
        await currentProvider.DisposeAsync().ConfigureAwait(false);
    }

    public async IAsyncEnumerable<CapturedFrame> ReadAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var frame in Frames.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return frame;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await StopAsync().ConfigureAwait(false);
        channel.Writer.TryComplete();
    }

    private void OnFrameReady(object? sender, CapturedFrame frame)
    {
        if (!ShouldAccept(frame.Timestamp))
        {
            frame.Dispose();
            return;
        }

        if (!channel.Writer.TryWrite(frame))
        {
            frame.Dispose();
        }
    }

    private void OnProviderCaptureEnded(object? sender, EventArgs e)
    {
        lock (syncRoot)
        {
            if (!ReferenceEquals(provider, sender))
            {
                return;
            }

            provider = null;
            IsRunning = false;
        }

        CaptureEnded?.Invoke(this, EventArgs.Empty);

        if (sender is IWgcFrameProvider endedProvider)
        {
            _ = DisposeEndedProviderAsync(endedProvider);
        }
    }

    private async ValueTask DisposeEndedProviderAsync(IWgcFrameProvider endedProvider)
    {
        try
        {
            endedProvider.FrameReady -= OnFrameReady;
            endedProvider.CaptureEnded -= OnProviderCaptureEnded;
            await endedProvider.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private bool ShouldAccept(DateTimeOffset timestamp)
    {
        lock (syncRoot)
        {
            if (!IsRunning)
            {
                return false;
            }

            if (lastAcceptedFrameTimestamp != DateTimeOffset.MinValue &&
                timestamp - lastAcceptedFrameTimestamp < options.MinimumFrameInterval)
            {
                return false;
            }

            lastAcceptedFrameTimestamp = timestamp;
            return true;
        }
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(WgcCaptureService));
        }
    }
}