using ChanSight.Capture.Extensions;
using ChanSight.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ChanSight.Tests.Capture;

public sealed class CaptureServiceCollectionExtensionsTests
{
    [Fact]
    public async Task AddChanSightCapture_RegistersCaptureServices()
    {
        var services = new ServiceCollection();

        services.AddChanSightCapture();
        await using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IWindowFinder>().Should().NotBeNull();
        provider.GetRequiredService<IWindowStateMonitor>().Should().NotBeNull();
    }
}
