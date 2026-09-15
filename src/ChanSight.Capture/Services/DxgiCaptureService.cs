using System.Threading.Channels;
using ChanSight.Core.Interfaces;
using ChanSight.Core.Models;
using OpenCvSharp;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using static Vortice.Direct3D11.D3D11;

namespace ChanSight.Capture.Services;

public sealed class DxgiCaptureService : IScreenCaptureService, IFrameSource
{
    private readonly object syncRoot = new();
    private readonly WgcCaptureOptions options;
    private readonly Channel<CapturedFrame> channel;
    private ID3D11Device? device;
    private ID3D11DeviceContext? context;
    private IDXGIOutputDuplication? duplication;
    private CancellationTokenSource? captureCancellation;
    private Task? captureTask;
    private WindowBounds targetBounds;
    private WindowBounds outputBounds;
    private DateTimeOffset lastAcceptedFrameTimestamp = DateTimeOffset.MinValue;
    private long sequenceNumber;
    private bool disposed;
    private bool failureFinalized;

    public DxgiCaptureService()
        : this(WgcCaptureOptions.Default)
    {
    }

    public DxgiCaptureService(WgcCaptureOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.options.Validate();

        channel = Channel.CreateBounded<CapturedFrame>(new BoundedChannelOptions(options.ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite, // DropWrite: rejected frames are disposed by caller; DropOldest would leak evicted frames
            SingleReader = true,
            SingleWriter = false
        });
    }

    public bool IsRunning { get; private set; }

    public IFrameSource FrameSource => this;

    public ChannelReader<CapturedFrame> Frames => channel.Reader;

    public event EventHandler? CaptureEnded;

    public event EventHandler<FrameDroppedEventArgs>? FrameDropped;

    public ValueTask StartAsync(WindowTarget target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        lock (syncRoot)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("Capture is already running.");
            }

            targetBounds = target.PhysicalBounds;
            CreateDevice();
            duplication = CreateDuplication(target.PhysicalBounds, out outputBounds);
            captureCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            failureFinalized = false;
            captureTask = Task.Run(() => CaptureLoopAsync(captureCancellation.Token), CancellationToken.None);
            lastAcceptedFrameTimestamp = DateTimeOffset.MinValue;
            IsRunning = true;
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? cancellation;
        Task? runningTask;

        lock (syncRoot)
        {
            cancellation = captureCancellation;
            runningTask = captureTask;
            captureCancellation = null;
            captureTask = null;
            IsRunning = false;
        }

        if (cancellation is not null)
        {
            try
            {
                await cancellation.CancelAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                // already disposed by failure path
            }
            cancellation.Dispose();
        }

        if (runningTask is not null)
        {
            await runningTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        lock (syncRoot)
        {
            ReleaseDxgiResources();
        }
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

    private void CreateDevice()
    {
        D3D11CreateDevice(
            null,
            DriverType.Hardware,
            DeviceCreationFlags.BgraSupport,
            new[]
            {
                FeatureLevel.Level_11_1,
                FeatureLevel.Level_11_0,
                FeatureLevel.Level_10_1,
                FeatureLevel.Level_10_0
            },
            out device,
            out _,
            out context).CheckError();
    }

    private IDXGIOutputDuplication CreateDuplication(WindowBounds bounds, out WindowBounds selectedOutputBounds)
    {
        using var dxgiDevice = device!.QueryInterface<IDXGIDevice>();
        dxgiDevice.GetAdapter(out var adapter).CheckError();
        using (adapter)
        {
            IDXGIOutput? bestOutput = null;
            try
            {
                for (uint index = 0; ; index++)
                {
                    var result = adapter.EnumOutputs(index, out var output);
                    if (result.Failure)
                    {
                        break;
                    }

                    if (Contains(output.Description.DesktopCoordinates, bounds))
                    {
                        bestOutput = output;
                        break;
                    }

                    bestOutput ??= output;
                }

                if (bestOutput is null)
                {
                    throw new InvalidOperationException("No DXGI output was found for desktop duplication.");
                }

                var coordinates = bestOutput.Description.DesktopCoordinates;
                selectedOutputBounds = new WindowBounds(
                    coordinates.Left,
                    coordinates.Top,
                    coordinates.Right - coordinates.Left,
                    coordinates.Bottom - coordinates.Top);

                using var output1 = bestOutput.QueryInterface<IDXGIOutput1>();
                return output1.DuplicateOutput(device);
            }
            finally
            {
                bestOutput?.Dispose();
            }
        }
    }

    private async Task CaptureLoopAsync(CancellationToken cancellationToken)
    {
        var failed = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            IDXGIResource? desktopResource = null;
            var acquired = false;

            try
            {
                var result = duplication!.AcquireNextFrame(100, out _, out desktopResource);
                if (result == Vortice.DXGI.ResultCode.WaitTimeout)
                {
                    continue;
                }

                if (result == Vortice.DXGI.ResultCode.AccessLost)
                {
                    failed = true;
                    break;
                }

                result.CheckError();
                acquired = true;

                using var desktopTexture = desktopResource.QueryInterface<ID3D11Texture2D>();
                var capturedFrame = CopyTextureToFrame(desktopTexture);
                if (ShouldAccept(capturedFrame.Timestamp))
                {
                    if (channel.Reader.Count >= options.ChannelCapacity ||
                        !channel.Writer.TryWrite(capturedFrame))
                    {
                        var seq = capturedFrame.SequenceNumber;
                        var ts = capturedFrame.Timestamp;
                        capturedFrame.Dispose();
                        RaiseFrameDropped(FrameDropReason.ChannelFull, seq, ts);
                    }
                }
                else
                {
                    var seq = capturedFrame.SequenceNumber;
                    var ts = capturedFrame.Timestamp;
                    capturedFrame.Dispose();
                    RaiseFrameDropped(FrameDropReason.Throttled, seq, ts);
                }
            }
            catch (Exception)
            {
                failed = true;
                break;
            }
            finally
            {
                desktopResource?.Dispose();
                if (acquired)
                {
                    duplication?.ReleaseFrame();
                }
            }

            await Task.Yield();
        }

        if (failed && !cancellationToken.IsCancellationRequested)
        {
            FinalizeCaptureFailure();
        }
    }

    private void FinalizeCaptureFailure()
    {
        CancellationTokenSource? staleCancellation = null;

        lock (syncRoot)
        {
            var transition = ComputeFailureFinalize(failureFinalized, IsRunning);
            if (transition.ShouldTransition)
            {
                IsRunning = false;
                ReleaseDxgiResources();
                staleCancellation = captureCancellation;
                captureCancellation = null;
            }

            if (transition.Finalized)
            {
                failureFinalized = true;
            }

            if (!transition.ShouldNotify)
            {
                return;
            }
        }

        staleCancellation?.Dispose();
        CaptureEnded?.Invoke(this, EventArgs.Empty);
    }

    internal static (bool Finalized, bool ShouldTransition, bool ShouldNotify) ComputeFailureFinalize(
        bool failureFinalized,
        bool isRunning)
    {
        if (failureFinalized)
        {
            return (Finalized: false, ShouldTransition: false, ShouldNotify: false);
        }

        if (!isRunning)
        {
            return (Finalized: true, ShouldTransition: false, ShouldNotify: false);
        }

        return (Finalized: true, ShouldTransition: true, ShouldNotify: true);
    }

    private CapturedFrame CopyTextureToFrame(ID3D11Texture2D sourceTexture)
    {
        var description = sourceTexture.Description;
        description.BindFlags = BindFlags.None;
        description.CPUAccessFlags = CpuAccessFlags.Read;
        description.Usage = ResourceUsage.Staging;
        description.MiscFlags = ResourceOptionFlags.None;
        description.SampleDescription = new SampleDescription(1, 0);

        using var stagingTexture = device!.CreateTexture2D(description);
        context!.CopyResource(stagingTexture, sourceTexture);

        var mapped = context.Map(stagingTexture, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
        try
        {
            using var desktopBgra = Mat.FromPixelData(
                (int)description.Height,
                (int)description.Width,
                MatType.CV_8UC4,
                mapped.DataPointer,
                mapped.RowPitch);
            var crop = GetCropRect(description);
            using var windowBgra = new Mat(desktopBgra, crop);
            var bgr = new Mat();
            Cv2.CvtColor(windowBgra, bgr, ColorConversionCodes.BGRA2BGR);

            return new CapturedFrame(
                bgr,
                DateTimeOffset.UtcNow,
                Interlocked.Increment(ref sequenceNumber));
        }
        finally
        {
            context.Unmap(stagingTexture, 0);
        }
    }

    private Rect GetCropRect(Texture2DDescription description)
    {
        return GetCropRect(targetBounds, outputBounds, ((int)description.Width, (int)description.Height));
    }

    internal static Rect GetCropRect(WindowBounds target, WindowBounds output, (int w, int h) texSize)
    {
        var x = Math.Max(0, target.Left - output.Left);
        var y = Math.Max(0, target.Top - output.Top);
        var maxWidth = Math.Max(1, texSize.w - x);
        var maxHeight = Math.Max(1, texSize.h - y);
        var width = Math.Min(target.Width, maxWidth);
        var height = Math.Min(target.Height, maxHeight);

        return new Rect(x, y, width, height);
    }

    private bool ShouldAccept(DateTimeOffset timestamp)
    {
        lock (syncRoot)
        {
            if (!IsRunning)
            {
                return false;
            }

            var accepted = ShouldAcceptFrame(
                lastAcceptedFrameTimestamp == DateTimeOffset.MinValue ? 0L : lastAcceptedFrameTimestamp.UtcTicks,
                timestamp.UtcTicks,
                options.MinimumFrameInterval,
                out var newLastTicks);

            lastAcceptedFrameTimestamp = new DateTimeOffset(newLastTicks, TimeSpan.Zero);
            return accepted;
        }
    }

    internal static bool ShouldAcceptFrame(long lastTicks, long nowTicks, TimeSpan minInterval, out long newLast)
    {
        if (lastTicks != 0 && nowTicks - lastTicks < minInterval.Ticks)
        {
            newLast = lastTicks;
            return false;
        }

        newLast = nowTicks;
        return true;
    }

    private void ReleaseDxgiResources()
    {
        duplication?.Dispose();
        duplication = null;
        context?.Dispose();
        context = null;
        device?.Dispose();
        device = null;
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

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(DxgiCaptureService));
        }
    }

    private static bool Contains(Vortice.RawRect output, WindowBounds bounds)
    {
        return bounds.Left >= output.Left &&
               bounds.Top >= output.Top &&
               bounds.Left < output.Right &&
               bounds.Top < output.Bottom;
    }
}