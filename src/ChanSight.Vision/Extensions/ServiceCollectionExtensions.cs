using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ChanSight.Vision.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddChanSightVision(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IPerceptualHashService, PerceptualHashService>();
        services.AddSingleton<IDatasetCleanerService, DatasetCleanerService>();
        services.AddSingleton<IYoloDatasetExporter, YoloDatasetExporter>();
        services.AddSingleton<IRoiMapperService, RoiMapperService>();
        services.AddSingleton<IGridSlicerService, GridSlicerService>();

        return services;
    }
}