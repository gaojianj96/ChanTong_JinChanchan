using System.Net.Http;
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
        services.AddSingleton<IOnnxInferenceEngine, OnnxInferenceEngine>();
        services.AddSingleton<IYoloDetectorService, YoloDetectorService>();
        services.AddSingleton<IPaddleOcrService, PaddleOcrService>();
        services.AddSingleton<IAnchorCalibrator, ClassicAnchorCalibrator>();
        services.AddSingleton(typeof(IPostProcessor<>), typeof(ClassicPostProcessor<>));
        services.AddSingleton<CellChangeDetector>();
        services.AddSingleton<HttpClient>(static _ => new HttpClient());
        services.AddSingleton<IVlmClient, OpenRouterVlmClient>();
        services.AddSingleton<VlmRecognitionAdapter>();

        return services;
    }
}