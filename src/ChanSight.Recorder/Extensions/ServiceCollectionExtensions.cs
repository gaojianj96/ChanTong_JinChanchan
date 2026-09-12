using ChanSight.Core.Interfaces;
using ChanSight.Recorder.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ChanSight.Recorder.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddChanSightRecorder(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IFileSystem, FileSystem>();
        services.TryAddSingleton<INativeHotKeyApi, NativeHotKeyApi>();

        services.AddSingleton<DatasetSamplerOptions>(_ => DatasetSamplerOptions.Default);
        services.AddSingleton<DatasetSamplerService>();
        services.AddSingleton<IDatasetSampler>(sp => sp.GetRequiredService<DatasetSamplerService>());
        services.AddTransient<IVideoRecorder, VideoRecorderService>();
        services.AddTransient<IGlobalHotKeyService, GlobalHotKeyService>();

        return services;
    }
}