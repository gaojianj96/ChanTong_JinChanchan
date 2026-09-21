using ChanSight.Core.Data;
using ChanSight.Core.Engine;
using ChanSight.Core.Season;
using Microsoft.Extensions.DependencyInjection;

namespace ChanSight.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddChanSightCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<PoolConfig>();
        services.AddSingleton<IHypergeometricEngine, HypergeometricEngine>();
        services.AddSingleton<GameStateManager>();
        services.AddSingleton<OpponentScoutTracker>();
        services.AddSingleton<ITacticalAdvisor>(static sp =>
            new TacticalAdvisor(
                sp.GetRequiredService<IHypergeometricEngine>(),
                sp.GetRequiredService<CompKnowledgeBase>(),
                options: null,
                runtime: sp.GetRequiredService<SeasonRuntime>()));

        services.AddSingleton(static _ =>
        {
            var knowledgeBase = new CompKnowledgeBase();
            knowledgeBase.Load(Path.Combine(AppContext.BaseDirectory, "Data", "CompTemplates"));
            return knowledgeBase;
        });

        services.AddSingleton(SeasonDictionaryStoreFactory);
        services.AddSingleton<ISeasonDictionaryReader>(static sp => sp.GetRequiredService<SeasonDictionaryStore>());
        services.AddSingleton<ISeasonDictionaryWriter>(static sp => sp.GetRequiredService<SeasonDictionaryStore>());
        services.AddSingleton<SeasonRuntime>();

        return services;
    }

    private static SeasonDictionaryStore SeasonDictionaryStoreFactory(IServiceProvider _)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "data", "season");
        var seeds = new Dictionary<string, SeasonDictionary>(StringComparer.Ordinal)
        {
            [SeasonDictionarySeed.DefaultSeasonId] = SeasonDictionarySeed.DefaultDictionary,
            [S18DictionarySeed.SeasonId] = S18DictionarySeed.Seed,
        };

        return new SeasonDictionaryStore(root, seeds);
    }
}