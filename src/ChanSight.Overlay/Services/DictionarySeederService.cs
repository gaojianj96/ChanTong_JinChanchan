using ChanSight.Core.Season;

namespace ChanSight.Overlay.Services;

/// <summary>
/// 网络搜索字典 seeder: 按赛季生成候选字典包(离线), 供人工审核后通过
/// <see cref="ISeasonDictionaryWriter.AddCandidate"/> 合并。不直接写正式字典。
///
/// 真实搜索引擎未接入(无可用搜索 API/凭据), <see cref="SearchAsync"/> 返回内置占位候选,
/// 但结构完整: 候选带 source/url/confidence 并按 Url 去重, 可直接作为真实搜索实现的上位替换点。
/// </summary>
public sealed class DictionarySeederService
{
    public const string WebSearchSource = "web-search";
    public const double DefaultConfidence = 0.5;

    private static readonly Dictionary<string, IReadOnlyList<CandidateMeta>> PlaceholderIndex =
        new(StringComparer.Ordinal)
        {
            ["亚索"] =
            [
                New(SeasonDictionarySeed.DefaultSeasonId, "亚索", "https://game.garena.tw/lol/heroes/yasuo", DefaultConfidence),
            ],
            ["拉克丝"] =
            [
                New(SeasonDictionarySeed.DefaultSeasonId, "拉克丝", "https://game.garena.tw/lol/heroes/lux", DefaultConfidence),
            ],
            ["金克丝"] =
            [
                New(SeasonDictionarySeed.DefaultSeasonId, "金克丝", "https://game.garena.tw/lol/heroes/jinx", DefaultConfidence),
            ],
        };

    private readonly ISeasonDictionaryWriter _writer;

    public DictionarySeederService(ISeasonDictionaryWriter writer)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    /// <summary>
    /// 按关键词搜索候选字典条目(占位实现, 返回内置候选并按 Url 去重)。不写正式字典。
    /// </summary>
    public Task<IReadOnlyList<CandidateMeta>> SearchAsync(string seasonId, string keyword, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seasonId);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyword);
        ct.ThrowIfCancellationRequested();

        if (!PlaceholderIndex.TryGetValue(keyword, out var candidates))
        {
            return Task.FromResult<IReadOnlyList<CandidateMeta>>(Array.Empty<CandidateMeta>());
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<CandidateMeta>();
        foreach (var candidate in candidates)
        {
            if (candidate.Url is not null && seen.Add(candidate.Url))
            {
                result.Add(candidate);
            }
        }

        return Task.FromResult<IReadOnlyList<CandidateMeta>>(result);
    }

    /// <summary>把候选列表写入该赛季的候选区(进候选, 人审后 Confirm 才进正式字典)。</summary>
    public void AddCandidates(string seasonId, IReadOnlyList<CandidateMeta> candidates)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seasonId);
        ArgumentNullException.ThrowIfNull(candidates);

        foreach (var candidate in candidates)
        {
            _writer.AddCandidate(seasonId, candidate);
        }
    }

    private static CandidateMeta New(string seasonId, string entity, string url, double confidence) =>
        new(entity, WebSearchSource, url, confidence, DateTimeOffset.UtcNow);
}