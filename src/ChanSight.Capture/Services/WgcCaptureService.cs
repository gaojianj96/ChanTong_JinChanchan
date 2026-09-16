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
    private long _maxCallbackTicks;

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
            FullMode = BoundedChannelFullMode.DropWrite, // DropWrite: rejected frames are disposed by caller; DropOldest would leak evicted frames
            SingleReader = false,
            SingleWriter = false
        };

        channel = Channel.CreateBounded<CapturedFrame>(channelOptions);
    }

    public bool IsRunning { get; private set; }

    public IFrameSource FrameSource => this;

    public ChannelReader<CapturedFrame> Frames => channel.Reader;

    public double MaxFrameCallbackMilliseconds =>
        Interlocked.Read(ref _maxCallbackTicks) / (double)System.Diagnostics.Stopwatch.Frequency * 1000.0;

    public event EventHandler? CaptureEnded;

    public event EventHandler<FrameDroppedEventArgs>? FrameDropped;

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
        var started = System.Diagnostics.Stopwatch.GetTimestamp();

        try
        {
            if (!ShouldAccept(frame.Timestamp))
            {
                var seq = frame.SequenceNumber;
                var ts = frame.Timestamp;
                frame.Dispose();
                RaiseFrameDropped(FrameDropReason.Throttled, seq, ts);
                return;
            }

            if (channel.Reader.Count >= options.ChannelCapacity ||
                !channel.Writer.TryWrite(frame))
            {
                var seq = frame.SequenceNumber;
                var ts = frame.Timestamp;
                frame.Dispose();
                RaiseFrameDropped(FrameDropReason.ChannelFull, seq, ts);
            }
        }
        finally
        {
            var elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - started;
            var max = Interlocked.Read(ref _maxCallbackTicks);
            while (elapsed > max && Interlocked.CompareExchange(ref _maxCallbackTicks, elapsed, max) != max)
            {
                max = Interlocked.Read(ref _maxCallbackTicks);
            }
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

    private void RaiseFrameDropped(FrameDropReason reason, long sequenceNumber, DateTimeOffset timestamp)
    {
        try
        {
            FrameDropped?.Invoke(this, new FrameDroppedEventArgs(sequenceNumber, reason, timestamp));
        }
        catch
        {
            // subscriber exceptions must not break the frame pipeline
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
                timestamp - lastAcceptedFrameTimestamp < options.FrameInterval)
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