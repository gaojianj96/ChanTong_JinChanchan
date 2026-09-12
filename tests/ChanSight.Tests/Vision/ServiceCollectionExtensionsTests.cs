using ChanSight.Vision.Extensions;
using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ChanSight.Tests.Vision;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddChanSightVision_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddChanSightVision();

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddChanSightVision_RegistersPerceptualHashService()
    {
        var services = new ServiceCollection();

        services.AddChanSightVision();
        var provider = services.BuildServiceProvider();

        var service = provider.GetService<IPerceptualHashService>();
        service.Should().NotBeNull();
        service.Should().BeOfType<PerceptualHashService>();
    }

    [Fact]
    public void AddChanSightVision_RegistersDatasetCleanerService()
    {
        var services = new ServiceCollection();

        services.AddChanSightVision();
        var provider = services.BuildServiceProvider();

        var service = provider.GetService<IDatasetCleanerService>();
        service.Should().NotBeNull();
        service.Should().BeOfType<DatasetCleanerService>();
    }

    [Fact]
    public void AddChanSightVision_RegistersYoloDatasetExporter()
    {
        var services = new ServiceCollection();

        services.AddChanSightVision();
        var provider = services.BuildServiceProvider();

        var service = provider.GetService<IYoloDatasetExporter>();
        service.Should().NotBeNull();
        service.Should().BeOfType<YoloDatasetExporter>();
    }

    [Fact]
    public void AddChanSightVision_RegistersServicesAsSingletons()
    {
        var services = new ServiceCollection();

        services.AddChanSightVision();
        var provider = services.BuildServiceProvider();

        var hash1 = provider.GetRequiredService<IPerceptualHashService>();
        var hash2 = provider.GetRequiredService<IPerceptualHashService>();
        hash1.Should().BeSameAs(hash2);

        var cleaner1 = provider.GetRequiredService<IDatasetCleanerService>();
        var cleaner2 = provider.GetRequiredService<IDatasetCleanerService>();
        cleaner1.Should().BeSameAs(cleaner2);
    }

    [Fact]
    public void AddChanSightVision_NullServices_ThrowsArgumentNullException()
    {
        var act = () => ServiceCollectionExtensions.AddChanSightVision(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}