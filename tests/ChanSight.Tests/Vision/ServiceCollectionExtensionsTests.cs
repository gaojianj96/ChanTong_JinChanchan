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
    public void AddChanSightVision_RegistersRoiMapperService()
    {
        var services = new ServiceCollection();

        services.AddChanSightVision();
        var provider = services.BuildServiceProvider();

        var service = provider.GetService<IRoiMapperService>();
        service.Should().NotBeNull();
        service.Should().BeOfType<RoiMapperService>();
    }

    [Fact]
    public void AddChanSightVision_RegistersGridSlicerService()
    {
        var services = new ServiceCollection();

        services.AddChanSightVision();
        var provider = services.BuildServiceProvider();

        var service = provider.GetService<IGridSlicerService>();
        service.Should().NotBeNull();
        service.Should().BeOfType<GridSlicerService>();
    }

    [Fact]
    public void AddChanSightVision_NewServicesAreSingletons()
    {
        var services = new ServiceCollection();

        services.AddChanSightVision();
        var provider = services.BuildServiceProvider();

        var mapper1 = provider.GetRequiredService<IRoiMapperService>();
        var mapper2 = provider.GetRequiredService<IRoiMapperService>();
        mapper1.Should().BeSameAs(mapper2);

        var slicer1 = provider.GetRequiredService<IGridSlicerService>();
        var slicer2 = provider.GetRequiredService<IGridSlicerService>();
        slicer1.Should().BeSameAs(slicer2);
    }

    [Fact]
    public void AddChanSightVision_NullServices_ThrowsArgumentNullException()
    {
        var act = () => ServiceCollectionExtensions.AddChanSightVision(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddChanSightVision_RegistersLocalDeterministicRecognizerAsSingleton()
    {
        var services = new ServiceCollection();

        services.AddChanSightVision();
        var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<LocalDeterministicRecognizer>();
        var second = provider.GetRequiredService<LocalDeterministicRecognizer>();

        first.Should().NotBeNull();
        first.Should().BeSameAs(second);
    }
}