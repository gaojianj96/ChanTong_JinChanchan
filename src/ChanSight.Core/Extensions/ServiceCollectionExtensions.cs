using ChanSight.Core.Data;
using ChanSight.Core.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace ChanSight.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddChanSightCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IHypergeometricEngine, HypergeometricEngine>();
        services.AddSingleton<GameStateManager>();
        services.AddSingleton<ITacticalAdvisor, TacticalAdvisor>();

        services.AddSingleton(static _ =>
        {
            var knowledgeBase = new CompKnowledgeBase();
            knowledgeBase.Load(Path.Combine(AppContext.BaseDirectory, "Data", "CompTemplates"));
            return knowledgeBase;
        });

        return services;
    }
}