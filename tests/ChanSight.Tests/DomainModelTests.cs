using ChanSight.Core.Models;
using FluentAssertions;

namespace ChanSight.Tests;

public sealed class DomainModelTests
{
    [Fact]
    public void WindowTarget_StoresHandleTitleSizeAndDpi()
    {
        var target = new WindowTarget(
            hwnd: 123,
            title: "Teamfight Tactics",
            physicalSize: new FrameResolution(1920, 1080),
            dpi: new DpiInfo(144, 144));

        target.Hwnd.Should().Be((nint)123);
        target.Title.Should().Be("Teamfight Tactics");
        target.PhysicalSize.Should().Be(new FrameResolution(1920, 1080));
        target.Dpi.ScaleX.Should().BeApproximately(1.5, 0.001);
    }

    [Fact]
    public void SessionMeta_RejectsInvalidFps()
    {
        var target = new WindowTarget(1, "TFT", new FrameResolution(1280, 720), DpiInfo.Default);

        var create = () => new SessionMeta(
            Guid.NewGuid(),
            "session",
            "out",
            target,
            DateTimeOffset.UnixEpoch,
            0,
            new FrameResolution(1280, 720));

        create.Should().Throw<ArgumentOutOfRangeException>();
    }
}
