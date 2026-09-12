using ChanSight.Capture.Extensions;
using ChanSight.Cli.Dashboard;
using ChanSight.Core.Extensions;
using ChanSight.Core.Interfaces;
using ChanSight.Recorder.Extensions;
using ChanSight.Recorder.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChanSight.Tests.Cli;

public sealed class CliServiceCollectionExtensionsTests
{
    private static IServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services;
    }

    [Fact]
    public void AddChanSightServices_Dashboard_CanBeResolved()
    {
        var services = CreateServices();

        services.AddChanSightCore();
        services.AddChanSightCapture();
        services.AddChanSightRecorder();
        services.AddSingleton<InteractiveDashboard>();

        var provider = services.BuildServiceProvider();

        var dashboard = provider.GetRequiredService<InteractiveDashboard>();
        dashboard.Should().NotBeNull();
    }

    [Fact]
    public void AddChanSightServices_ResolvesAllCoreServices()
    {
        var services = CreateServices();

        services.AddChanSightCore();
        services.AddChanSightCapture();
        services.AddChanSightRecorder();

        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IWindowFinder>().Should().NotBeNull();
        provider.GetRequiredService<IWindowStateMonitor>().Should().NotBeNull();
        provider.GetRequiredService<IScreenCaptureService>().Should().NotBeNull();
        provider.GetRequiredService<IDatasetSampler>().Should().NotBeNull();
        provider.GetRequiredService<IVideoRecorder>().Should().NotBeNull();
        provider.GetRequiredService<IGlobalHotKeyService>().Should().NotBeNull();
    }

    [Fact]
    public void AddChanSightServices_DatasetSampler_IsSingleton()
    {
        var services = CreateServices();

        services.AddChanSightCore();
        services.AddChanSightCapture();
        services.AddChanSightRecorder();

        var provider = services.BuildServiceProvider();

        var instance1 = provider.GetRequiredService<DatasetSamplerService>();
        var instance2 = provider.GetRequiredService<DatasetSamplerService>();

        instance1.Should().BeSameAs(instance2);
    }

    [Fact]
    public void AddChanSightServices_DatasetSampler_MatchesIDatasetSampler()
    {
        var services = CreateServices();

        services.AddChanSightCore();
        services.AddChanSightCapture();
        services.AddChanSightRecorder();

        var provider = services.BuildServiceProvider();

        var concrete = provider.GetRequiredService<DatasetSamplerService>();
        var interface_svc = provider.GetRequiredService<IDatasetSampler>();

        concrete.Should().BeSameAs(interface_svc);
    }
}