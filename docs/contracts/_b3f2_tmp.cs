using System;
using System.Threading;
using System.Threading.Tasks;
using ChanSight.Capture.Models;
using ChanSight.Capture.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChanSight.Tests.Capture;

public sealed class WgcCaptureServiceTests
{
    [Fact]
    public async Task Frames_UsesBoundedDropWriteChannel()
    {
        var service = new WgcCaptureService(NullLogger<WgcCaptureService>.Instance);

        var received = new System.Collections.Generic.List<int>();

        service.FrameReady += (_, frame) =>
        {
            received.Add(frame.Sequence);
        };

        await service.StartAsync(CancellationToken.None);

        service.OnFrameArrived(new CapturedFrame(1));
        service.OnFrameArrived(new CapturedFrame(2));
        service.OnFrameArrived(new CapturedFrame(3));

        await WaitForAsync(() => received.Count >= 2);

        received.Should().Equal(1, 2);

        await service.StopAsync(CancellationToken.None);
    }

    private static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (condition())
            {
                break;
            }

            await Task.Delay(10);
        }

        condition().Should().BeTrue();
    }
}