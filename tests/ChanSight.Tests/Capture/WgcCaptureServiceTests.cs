using ChanSight.Capture.Services;
using ChanSight.Core.Models;
using FluentAssertions;
using OpenCvSharp;

namespace ChanSight.Tests.Capture;

public sealed class WgcCaptureServiceTests
{
    [Fact]
    public async Task StartAsync_ExposesRunningFrameSourceAndStopReleasesProvider()
    {
        var provider = new TestWgcFrameProvider();
        await using var service = new WgcCaptureService(WgcCaptureOptions.Default, () => provider);

        await service.StartAsync(CreateTarget());

        service.IsRunning.Should().BeTrue();
        service.FrameSource.Should().BeSameAs(service);
        provider.StartCount.Should().Be(1);

        await service.StopAsync();

        service.IsRunning.Should().BeFalse();
        provider.StopCount.Should().Be(1);
        provider.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task Frames_UsesBoundedDropOldestChannel()
    {
        var provider = new TestWgcFrameProvider();
        var options = WgcCaptureOptions.Default with
        {
            TargetFramesPerSecond = 60,
            ChannelCapacity = 2
        };
        await using var service = new WgcCaptureService(options, () => provider);

        await service.StartAsync(CreateTarget());
        provider.Publish(CreateFrame(1, TimeSpan.Zero));
        provider.Publish(CreateFrame(2, TimeSpan.FromMilliseconds(20)));
        provider.Publish(CreateFrame(3, TimeSpan.FromMilliseconds(40)));
        await service.StopAsync();

        service.Frames.TryRead(out var first).Should().BeTrue();
        service.Frames.TryRead(out var second).Should().BeTrue();

        using (first)
        using (second)
        {
            first!.SequenceNumber.Should().Be(2);
            second!.SequenceNumber.Should().Be(3);
        }
    }

    [Fact]
    public async Task Frames_DropsFramesAboveTargetFrameRate()
    {
        var provider = new TestWgcFrameProvider();
        var options = WgcCaptureOptions.Default with { TargetFramesPerSecond = 10 };
        await using var service = new WgcCaptureService(options, () => provider);

        await service.StartAsync(CreateTarget());
        provider.Publish(CreateFrame(1, TimeSpan.Zero));
        provider.Publish(CreateFrame(2, TimeSpan.FromMilliseconds(50)));
        provider.Publish(CreateFrame(3, TimeSpan.FromMilliseconds(100)));
        await service.StopAsync();

        service.Frames.TryRead(out var first).Should().BeTrue();
        service.Frames.TryRead(out var second).Should().BeTrue();
        service.Frames.TryRead(out _).Should().BeFalse();

        using (first)
        using (second)
        {
            first!.SequenceNumber.Should().Be(1);
            second!.SequenceNumber.Should().Be(3);
        }
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyRunningThrows()
    {
        var provider = new TestWgcFrameProvider();
        await using var service = new WgcCaptureService(WgcCaptureOptions.Default, () => provider);

        await service.StartAsync(CreateTarget());
        var startAgain = async () => await service.StartAsync(CreateTarget());

        await startAgain.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task StopAsync_IsIdempotent()
    {
        var provider = new TestWgcFrameProvider();
        await using var service = new WgcCaptureService(WgcCaptureOptions.Default, () => provider);

        await service.StartAsync(CreateTarget());
        await service.StopAsync();
        await service.StopAsync();

        provider.StopCount.Should().Be(1);
        provider.DisposeCount.Should().Be(1);
    }

    private static WindowTarget CreateTarget()
    {
        return new WindowTarget(
            123,
            "test window",
            new FrameResolution(640, 480),
            DpiInfo.Default);
    }

    private static CapturedFrame CreateFrame(long sequenceNumber, TimeSpan offset)
    {
        return new CapturedFrame(
            new Mat(10, 10, MatType.CV_8UC3),
            DateTimeOffset.UnixEpoch.Add(offset),
            sequenceNumber);
    }

    private sealed class TestWgcFrameProvider : IWgcFrameProvider
    {
        public event EventHandler<CapturedFrame>? FrameReady;

        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public int DisposeCount { get; private set; }

        public ValueTask StartAsync(WindowTarget target, CancellationToken cancellationToken)
        {
            StartCount++;
            return ValueTask.CompletedTask;
        }

        public ValueTask StopAsync(CancellationToken cancellationToken)
        {
            StopCount++;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }

        public void Publish(CapturedFrame frame)
        {
            FrameReady?.Invoke(this, frame);
        }
    }
}
