using ChanSight.Core.Data;
using Microsoft.Extensions.DependencyInjection;

namespace ChanSight.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddChanSightCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton(static _ =>
        {
            var knowledgeBase = new CompKnowledgeBase();
            knowledgeBase.Load(Path.Combine(AppContext.BaseDirectory, "Data", "CompTemplates"));
            return knowledgeBase;
        });
        return services;
    }
}
