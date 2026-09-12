using ChanSight.Capture.Services;
using ChanSight.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ChanSight.Capture.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddChanSightCapture(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<INativeWindowApi, NativeWindowApi>();
        services.AddSingleton<IWindowFinder, WindowFinder>();
        services.AddTransient<IWindowStateMonitor, WindowStateMonitor>();
        services.AddTransient<IScreenCaptureService, WgcCaptureService>();

        return services;
    }
}
