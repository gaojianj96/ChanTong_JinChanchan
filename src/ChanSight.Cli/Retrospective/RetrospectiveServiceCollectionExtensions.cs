using ChanSight.Recorder.Services;
using ChanSight.Vision.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ChanSight.Cli.Retrospective;

public static class RetrospectiveServiceCollectionExtensions
{
    public static IServiceCollection AddChanSightRetrospective(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<RetrospectiveScorer>();
        services.AddSingleton<RetrospectiveReportGenerator>(sp => new RetrospectiveReportGenerator(
            sp.GetRequiredService<IVlmClient>(),
            sp.GetRequiredService<RetrospectiveScorer>()));

        return services;
    }
}