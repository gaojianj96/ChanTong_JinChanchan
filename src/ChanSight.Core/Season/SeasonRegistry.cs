using System.Text.Json;

namespace ChanSight.Core.Season;

/// <summary>
/// 维护 <c>seasonId → patch</c> 映射(如 "S16.5" → "9.11-9.16"),
/// 供"选定赛季 → 加载对应 MetaInfo patch"使用。
///
/// 内置一份默认静态清单, 并可从 JSON(<c>data/season/registry.json</c>)加载覆盖/追加:
///   { "S16.5": "9.11-9.16", ... }
/// </summary>
public sealed class SeasonRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static readonly Dictionary<string, string> DefaultMappings = new(StringComparer.Ordinal)
    {
        ["S16.5"] = "9.11-9.16",
    };

    private readonly Dictionary<string, string> _mappings = new(DefaultMappings, StringComparer.Ordinal);

    public SeasonRegistry()
    {
    }

    public SeasonRegistry(string registryPath)
        : this()
    {
        Load(registryPath);
    }

    /// <summary>从 registry.json 加载映射, 与内置默认合并(文件中的条目覆盖同 key 默认值)。</summary>
    public void Load(string registryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registryPath);

        if (!File.Exists(registryPath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(registryPath);
            var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);
            if (loaded is null)
            {
                return;
            }

            foreach (var (seasonId, patch) in loaded)
            {
                _mappings[seasonId] = patch;
            }
        }
        catch (JsonException)
        {
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>未知 season 返回 <c>null</c>。</summary>
    public string? MapSeasonToPatch(string seasonId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seasonId);

        return _mappings.TryGetValue(seasonId, out var patch) ? patch : null;
    }

    private static JsonSerializerOptions CreateJsonOptions()
        => new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };
}