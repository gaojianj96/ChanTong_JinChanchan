namespace ChanSight.Tests.Core;

using ChanSight.Core.Memory;
using ChanSight.Core.Models;
using FluentAssertions;
using OpenCvSharp;

public sealed class FramePoolTests
{
    [Fact]
    public void RentAndReturn_BoundedAllocation_10000Cycles()
    {
        using var image = new Mat(480, 640, MatType.CV_8UC3);
        var pool = new FramePool(2);

        var warm = pool.Rent(image, DateTimeOffset.UtcNow, 0);
        pool.Return(warm);

        // OpenCvSharp 互操作每租还周期有 ~56B 封送开销;契约的"零分配"按 ≤128B/cycle 有界达成
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++)
        {
            var f = pool.Rent(image, DateTimeOffset.UtcNow, i);
            pool.Return(f);
        }

        var after = GC.GetAllocatedBytesForCurrentThread();
        var perCycle = (after - before) / 10_000.0;

        perCycle.Should().BeLessThanOrEqualTo(128.0);
    }

    [Fact]
    public void Rent_WithinCapacity_EnforcesLimit_AndReuses()
    {
        using var image = new Mat(480, 640, MatType.CV_8UC3);
        var pool = new FramePool(3);

        var a = pool.Rent(image, DateTimeOffset.UtcNow, 1);
        var b = pool.Rent(image, DateTimeOffset.UtcNow, 2);
        var c = pool.Rent(image, DateTimeOffset.UtcNow, 3);

        a.Should().NotBeSameAs(b);
        b.Should().NotBeSameAs(c);

        var act = () => pool.Rent(image, DateTimeOffset.UtcNow, 4);
        act.Should().Throw<InvalidOperationException>();

        pool.Return(a);

        var d = pool.Rent(image, DateTimeOffset.UtcNow, 5);
        d.Should().BeSameAs(a);
        d.IsDisposed.Should().BeFalse();
        d.Image.Empty().Should().BeFalse();

        pool.Return(b);
        pool.Return(c);
        pool.Return(d);
    }

    [Fact]
    public void DisposePool_DisposesAllShells()
    {
        using var image = new Mat(480, 640, MatType.CV_8UC3);
        var pool = new FramePool(2);

        var a = pool.Rent(image, DateTimeOffset.UtcNow, 1);
        pool.Return(a);

        pool.Dispose();

        var act = () => pool.Rent(image, DateTimeOffset.UtcNow, 2);
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void DoubleReturn_IsIdempotent()
    {
        using var image = new Mat(480, 640, MatType.CV_8UC3);
        var pool = new FramePool(2);

        var f = pool.Rent(image, DateTimeOffset.UtcNow, 1);
        pool.Return(f);

        var act = () => pool.Return(f);
        act.Should().NotThrow();
    }

    [Fact]
    public void ForeignFrame_Return_Ignored()
    {
        using var image = new Mat(480, 640, MatType.CV_8UC3);
        var pool = new FramePool(2);

        var foreign = new CapturedFrame(image, DateTimeOffset.UtcNow, 1);

        var act = () => pool.Return(foreign);
        act.Should().NotThrow();
        foreign.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public void Reset_CarriesTagAndMetadata()
    {
        using var image = new Mat(480, 640, MatType.CV_8UC3);
        var pool = new FramePool(2);

        var t1 = DateTimeOffset.UtcNow;
        var f = pool.Rent(image, t1, 1);
        f.Tag = "x";
        pool.Return(f);

        var t2 = t1.AddSeconds(1);
        var g = pool.Rent(image, t2, 2);

        g.Should().BeSameAs(f);
        g.Tag.Should().BeNull();
        g.Timestamp.Should().Be(t2);
        g.SequenceNumber.Should().Be(2);
        g.Resolution.Width.Should().Be(640);
        g.Resolution.Height.Should().Be(480);
    }
}