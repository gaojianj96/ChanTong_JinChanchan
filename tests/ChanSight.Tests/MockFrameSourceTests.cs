using ChanSight.Core.Models;
using ChanSight.Tests.Mocks;
using FluentAssertions;
using OpenCvSharp;

namespace ChanSight.Tests;

public sealed class MockFrameSourceTests
{
    [Fact]
    public async Task ReadAllAsync_StreamsFramesInOrder()
    {
        var source = new MockFrameSource();
        source.TryWrite(CreateFrame(1)).Should().BeTrue();
        source.TryWrite(CreateFrame(2)).Should().BeTrue();
        source.Complete();

        var sequenceNumbers = new List<long>();

        await foreach (var frame in source.ReadAllAsync())
        {
            using (frame)
            {
                sequenceNumbers.Add(frame.SequenceNumber);
            }
        }

        sequenceNumbers.Should().Equal(1, 2);
    }

    [Fact]
    public async Task BoundedChannel_DropsOldestFrameWhenFull()
    {
        var source = new MockFrameSource(capacity: 1);
        source.TryWrite(CreateFrame(1)).Should().BeTrue();
        source.TryWrite(CreateFrame(2)).Should().BeTrue();
        source.Complete();

        CapturedFrame? remainingFrame = null;

        await foreach (var frame in source.ReadAllAsync())
        {
            remainingFrame = frame;
        }

        remainingFrame.Should().NotBeNull();

        using var retainedFrame = remainingFrame!;
        retainedFrame.SequenceNumber.Should().Be(2);
    }

    private static CapturedFrame CreateFrame(long sequenceNumber)
    {
        return new CapturedFrame(
            new Mat(10, 10, MatType.CV_8UC3),
            DateTimeOffset.UnixEpoch.AddMilliseconds(sequenceNumber),
            sequenceNumber);
    }
}
