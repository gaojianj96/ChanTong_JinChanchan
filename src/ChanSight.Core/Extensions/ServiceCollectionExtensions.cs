using ChanSight.Core.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace ChanSight.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddChanSightCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IHypergeometricEngine, HypergeometricEngine>();
        return services;
    }
}
