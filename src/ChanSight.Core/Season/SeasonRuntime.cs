namespace ChanSight.Core.Season;

/// <summary>
/// 当前赛季运行时: 持有当前 <see cref="SeasonContext"/> 与 <see cref="ISeasonDictionaryReader"/>,
/// 供识别/校验/prompt/UI 候选取用当前选中赛季的字典。默认赛季为 S16.5(恭喜发财),
/// 当 reader 尚未返回正式字典时回退到 <see cref="SeasonDictionarySeed"/>。
/// </summary>
public sealed class SeasonRuntime
{
    private readonly ISeasonDictionaryReader _reader;
    private SeasonContext _context;

    public SeasonRuntime(ISeasonDictionaryReader reader, SeasonContext? context = null)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _context = context ?? SeasonDictionarySeed.DefaultContext;
    }

    /// <summary>当前赛季上下文(seasonId + mode)。</summary>
    public SeasonContext Context => _context;

    /// <summary>当前赛季的正式字典; reader 未返回时回退到内置 S16.5 种子。</summary>
    public SeasonDictionary? Current => _reader.Get(_context.SeasonId);

    /// <summary>无正式字典时的回退来源(内置种子, 保证默认赛季英雄不丢失)。</summary>
    public SeasonDictionary Fallback => SeasonDictionarySeed.DefaultDictionary;

    public IReadOnlySet<string> Heroes => Current?.Heroes ?? Fallback.Heroes;

    public IReadOnlySet<string> Items => Current?.Items ?? Fallback.Items;

    public IReadOnlySet<string> Traits => Current?.Traits ?? Fallback.Traits;

    /// <summary>英雄是否在当前赛季字典(含回退)内。</summary>
    public bool TryGetHero(string name) => Heroes.Contains(name);

    /// <summary>装备是否在当前赛季字典(含回退)内。</summary>
    public bool TryGetItem(string name) => Items.Contains(name);

    /// <summary>英雄费用(1-5); 未知英雄返回 0。优先当前字典, 再回退到内置种子。</summary>
    public int HeroCost(string hero)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hero);

        if (Current?.HeroCosts.TryGetValue(hero, out var cost) == true)
        {
            return cost;
        }

        return Fallback.HeroCosts.TryGetValue(hero, out var fallbackCost) ? fallbackCost : 0;
    }

    /// <summary>切换当前赛季上下文(后续 UI 切换用; 本任务仅提供方法)。</summary>
    public void Select(string seasonId, string mode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seasonId);
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        _context = new SeasonContext(seasonId, mode);
    }

    /// <summary>构造一个仅依赖内置种子(reader 恒为空)的默认运行时, 供无 DI 的调用方使用。</summary>
    public static SeasonRuntime CreateDefault() => new(new EmptyDictionaryReader());

    private sealed class EmptyDictionaryReader : ISeasonDictionaryReader
    {
        public SeasonDictionary? Get(string seasonId) => null;

        public bool TryGetHero(string seasonId, string name) => false;

        public bool TryGetItem(string seasonId, string name) => false;
    }
}
