using System.Threading.Channels;
using ChanSight.Capture.Services;
using ChanSight.Core.Models;
using Xunit;

namespace ChanSight.Tests.Capture;

public sealed class WgcCaptureServiceTests
{
    [Fact]
    public async Task StartAsync_WhenProviderStarts_IsRunningTrue()
    {
        var provider = new TestWgcFrameProvider();
        var service = new WgcCaptureService(WgcCaptureOptions.Default, () => provider);

        await service.StartAsync(new WindowTarget((nint)1, new WindowBounds(0, 0, 100, 100)));

        Assert.True(service.IsRunning);
        Assert.True(provider.Started);

        await service.StopAsync();
    }

    [Fact]
    public async Task StopAsync_WhenRunning_StopsAndDisposesProvider()
    {
        var provider = new TestWgcFrameProvider();
        var service = new WgcCaptureService(WgcCaptureOptions.Default, () => provider);

        await service.StartAsync(new WindowTarget((nint)1, new WindowBounds(0, 0, 100, 100)));
        await service.StopAsync();

        Assert.False(service.IsRunning);
        Assert.True(provider.Stopped);
        Assert.True(provider.Disposed);
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyRunning_Throws()
    {
        var provider = new TestWgcFrameProvider();
        var service = new WgcCaptureService(WgcCaptureOptions.Default, () => provider);

        await service.StartAsync(new WindowTarget((nint)1, new WindowBounds(0, 0, 100, 100)));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.StartAsync(new WindowTarget((nint)1, new WindowBounds(0, 0, 100, 100))));

        await service.StopAsync();
    }

    [Fact]
    public async Task StartAsync_WhenProviderThrows_ResetsState()
    {
        var provider = new TestWgcFrameProvider { ThrowOnStart = true };
        var service = new WgcCaptureService(WgcCaptureOptions.Default, () => provider);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.StartAsync(new WindowTarget((nint)1, new WindowBounds(0, 0, 100, 100))));

        Assert.False(service.IsRunning);
        Assert.True(provider.Disposed);
    }

    [Fact]
    public async Task ProviderRaisingCaptureEnded_StopsServiceAndRaisesEvent()
    {
        var provider = new TestWgcFrameProvider();
        var service = new WgcCaptureService(WgcCaptureOptions.Default, () => provider);

        var endedCount = 0;
        service.CaptureEnded += (_, _) => Interlocked.Increment(ref endedCount);

        await service.StartAsync(new WindowTarget((nint)1, new WindowBounds(0, 0, 100, 100)));

        provider.RaiseCaptureEnded();

        Assert.False(service.IsRunning);
        Assert.Equal(1, Volatile.Read(ref endedCount));

        await WaitForAsync(() => provider.Disposed);
        Assert.True(provider.Disposed);
    }

    [Fact]
    public async Task CaptureEnded_EventHandlerNeverThrows_SwallowsProviderDisposeFailure()
    {
        var provider = new TestWgcFrameProvider { ThrowOnDispose = true };
        var service = new WgcCaptureService(WgcCaptureOptions.Default, () => provider);

        await service.StartAsync(new WindowTarget((nint)1, new WindowBounds(0, 0, 100, 100)));

        provider.RaiseCaptureEnded();

        Assert.False(service.IsRunning);

        await WaitForAsync(() => provider.DisposeAttempted);
        Assert.True(provider.DisposeAttempted);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100; i++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }
    }

    private sealed class TestWgcFrameProvider : IWgcFrameProvider
    {
        public event EventHandler<CapturedFrame>? FrameReady;

        public event EventHandler? CaptureEnded;

        public bool Started { get; private set; }

        public bool Stopped { get; private set; }

        public bool Disposed { get; private set; }

        public bool DisposeAttempted { get; private set; }

        public bool ThrowOnStart { get; init; }

        public bool ThrowOnDispose { get; init; }

        public ValueTask StartAsync(WindowTarget target, CancellationToken cancellationToken)
        {
            if (ThrowOnStart)
            {
                throw new InvalidOperationException("start failed");
            }

            Started = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask StopAsync(CancellationToken cancellationToken)
        {
            Stopped = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeAttempted = true;

            if (ThrowOnDispose)
            {
                throw new InvalidOperationException("dispose failed");
            }

            Disposed = true;
            return ValueTask.CompletedTask;
        }

        public void RaiseCaptureEnded()
        {
            CaptureEnded?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseFrameReady(CapturedFrame frame)
        {
            FrameReady?.Invoke(this, frame);
        }
    }
}