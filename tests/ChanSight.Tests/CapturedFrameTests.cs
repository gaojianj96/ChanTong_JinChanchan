using ChanSight.Core.Models;
using FluentAssertions;
using OpenCvSharp;

namespace ChanSight.Tests;

public sealed class CapturedFrameTests
{
    [Fact]
    public void Constructor_CapturesMetadataFromMat()
    {
        using var mat = new Mat(24, 32, MatType.CV_8UC3);
        using var frame = new CapturedFrame(mat, DateTimeOffset.UnixEpoch, 7);

        frame.SequenceNumber.Should().Be(7);
        frame.Timestamp.Should().Be(DateTimeOffset.UnixEpoch);
        frame.Resolution.Should().Be(new FrameResolution(32, 24));
        frame.Image.Should().BeSameAs(mat);
    }

    [Fact]
    public void Dispose_IsIdempotentAndGuardsImageAccess()
    {
        var frame = new CapturedFrame(new Mat(8, 16, MatType.CV_8UC3), DateTimeOffset.UnixEpoch, 1);

        frame.Dispose();
        frame.Dispose();

        frame.IsDisposed.Should().BeTrue();
        frame.Resolution.Should().Be(new FrameResolution(16, 8));
        Action accessImage = () => _ = frame.Image;
        accessImage.Should().Throw<ObjectDisposedException>();
    }
}
