using System.Text.Json;

namespace ChanSight.Core.Season;

/// <summary>
/// 按 season 隔离、读写分离的赛季字典存储(同时实现 <see cref="ISeasonDictionaryReader"/> 与
/// <see cref="ISeasonDictionaryWriter"/>)。
///
/// 目录约定(相对 <c>rootDirectory</c>, 调用方通常传 <c>data/season</c>):
///   {root}/{seasonId}/dictionary.json   正式字典(heroes/items/traits)
///   {root}/{seasonId}/candidates.json   待确认候选列表
///
/// 正式字典与候选分开持久化; 每次 AddCandidate/Confirm/Revoke 后立即落盘; 全部操作线程安全(锁)。
/// </summary>
public sealed class SeasonDictionaryStore : ISeasonDictionaryReader, ISeasonDictionaryWriter
{
    private const string DictionaryFileName = "dictionary.json";
    private const string CandidatesFileName = "candidates.json";
    private const string HeroSource = "hero";
    private const string ItemSource = "item";
    private const string TraitSource = "trait";

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly object _gate = new();
    private readonly Dictionary<string, SeasonState> _seasons = new(StringComparer.Ordinal);
    private readonly IReadOnlyDictionary<string, SeasonDictionary> _builtInSeeds;
    private string _rootDirectory;

    public SeasonDictionaryStore(string rootDirectory)
        : this(rootDirectory, builtInSeeds: null)
    {
    }

    /// <summary>
    /// 可携带内置种子: 当某赛季的 <c>dictionary.json</c> 不存在时, 用种子在内存中填充正式字典,
    /// 保证默认赛季(如 S16.5)在未落盘前也能读到完整英雄表。
    /// </summary>
    public SeasonDictionaryStore(
        string rootDirectory,
        IReadOnlyDictionary<string, SeasonDictionary>? builtInSeeds)
    {
        _rootDirectory = rootDirectory ?? throw new ArgumentNullException(nameof(rootDirectory));
        _builtInSeeds = builtInSeeds ?? new Dictionary<string, SeasonDictionary>(StringComparer.Ordinal);
    }

    /// <summary>切换根目录并清空内存缓存(后续按需重新加载)。</summary>
    public void Load(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        lock (_gate)
        {
            _rootDirectory = rootDirectory;
            _seasons.Clear();
        }
    }

    public SeasonDictionary? Get(string seasonId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seasonId);

        lock (_gate)
        {
            var state = EnsureLoaded(seasonId);
            if (!state.HasFormalEntries)
            {
                return null;
            }

            return new SeasonDictionary(
                seasonId,
                Copy(state.Heroes),
                Copy(state.Items),
                Copy(state.Traits));
        }
    }

    public bool TryGetHero(string seasonId, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seasonId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (_gate)
        {
            return EnsureLoaded(seasonId).Heroes.Contains(name);
        }
    }

    public bool TryGetItem(string seasonId, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seasonId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (_gate)
        {
            return EnsureLoaded(seasonId).Items.Contains(name);
        }
    }

    public void AddCandidate(string seasonId, CandidateMeta candidate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seasonId);
        ArgumentNullException.ThrowIfNull(candidate);

        lock (_gate)
        {
            var state = EnsureLoaded(seasonId);

            if (FindCandidate(state, candidate.Entity) is null)
            {
                state.Candidates.Add(candidate);
                SaveCandidates(state, seasonId);
            }
        }
    }

    public void Confirm(string seasonId, string entity, ConfirmKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seasonId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity);

        lock (_gate)
        {
            var state = EnsureLoaded(seasonId);

            var candidate = FindCandidate(state, entity);
            if (candidate is not null)
            {
                RemoveCandidate(state, entity);
            }

            if (kind == ConfirmKind.New)
            {
                AddToFormal(state, candidate?.Source, entity);
                SaveDictionary(state, seasonId);
            }
            // MapExisting: 语义为 alias(alias 表留待后续), 此处仅移除候选、不新增正式条目。
            // Ignore: 仅移除候选。
            else if (candidate is not null)
            {
                SaveCandidates(state, seasonId);
            }
        }
    }

    public void Revoke(string seasonId, string entity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seasonId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity);

        lock (_gate)
        {
            var state = EnsureLoaded(seasonId);

            state.Heroes.Remove(entity);
            state.Items.Remove(entity);
            state.Traits.Remove(entity);

            SaveDictionary(state, seasonId);
        }
    }

    public IReadOnlyList<CandidateMeta> GetPendingCandidates(string seasonId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seasonId);

        lock (_gate)
        {
            return EnsureLoaded(seasonId).Candidates.ToArray();
        }
    }

    private SeasonState EnsureLoaded(string seasonId)
    {
        if (_seasons.TryGetValue(seasonId, out var state))
        {
            return state;
        }

        state = new SeasonState();
        state.Loaded = true;

        LoadDictionary(state, seasonId);
        LoadCandidates(state, seasonId);

        _seasons[seasonId] = state;
        return state;
    }

    private static void AddToFormal(SeasonState state, string? source, string entity)
    {
        if (string.Equals(source, ItemSource, StringComparison.OrdinalIgnoreCase))
        {
            state.Items.Add(entity);
        }
        else if (string.Equals(source, TraitSource, StringComparison.OrdinalIgnoreCase))
        {
            state.Traits.Add(entity);
        }
        else
        {
            state.Heroes.Add(entity);
        }
    }

    private static CandidateMeta? FindCandidate(SeasonState state, string entity)
    {
        foreach (var candidate in state.Candidates)
        {
            if (string.Equals(candidate.Entity, entity, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private static void RemoveCandidate(SeasonState state, string entity)
    {
        state.Candidates.RemoveAll(c => string.Equals(c.Entity, entity, StringComparison.OrdinalIgnoreCase));
    }

    private void LoadDictionary(SeasonState state, string seasonId)
    {
        var path = GetDictionaryPath(seasonId);
        if (!File.Exists(path))
        {
            SeedFromBuiltIn(state, seasonId);
            return;
        }

        try
        {
            var json = File.ReadAllText(path);
            var dto = JsonSerializer.Deserialize<DictionaryDto>(json, JsonOptions);
            if (dto is null)
            {
                return;
            }

            foreach (var hero in dto.Heroes)
            {
                state.Heroes.Add(hero);
            }

            foreach (var item in dto.Items)
            {
                state.Items.Add(item);
            }

            foreach (var trait in dto.Traits)
            {
                state.Traits.Add(trait);
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

    private void SeedFromBuiltIn(SeasonState state, string seasonId)
    {
        if (!_builtInSeeds.TryGetValue(seasonId, out var seed))
        {
            return;
        }

        foreach (var hero in seed.Heroes)
        {
            state.Heroes.Add(hero);
        }

        foreach (var item in seed.Items)
        {
            state.Items.Add(item);
        }

        foreach (var trait in seed.Traits)
        {
            state.Traits.Add(trait);
        }
    }

    private void LoadCandidates(SeasonState state, string seasonId)
    {
        var path = GetCandidatesPath(seasonId);
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(path);
            var candidates = JsonSerializer.Deserialize<List<CandidateMeta>>(json, JsonOptions);
            if (candidates is null)
            {
                return;
            }

            state.Candidates.AddRange(candidates);
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

    private void SaveDictionary(SeasonState state, string seasonId)
    {
        var dto = new DictionaryDto
        {
            SeasonId = seasonId,
            Heroes = Order(state.Heroes),
            Items = Order(state.Items),
            Traits = Order(state.Traits),
        };

        WriteJson(GetDictionaryPath(seasonId), dto);
    }

    private void SaveCandidates(SeasonState state, string seasonId)
    {
        WriteJson(GetCandidatesPath(seasonId), state.Candidates);
    }

    private void WriteJson<T>(string path, T value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, json);
    }

    private string GetDictionaryPath(string seasonId)
        => Path.Combine(_rootDirectory, seasonId, DictionaryFileName);

    private string GetCandidatesPath(string seasonId)
        => Path.Combine(_rootDirectory, seasonId, CandidatesFileName);

    private static List<string> Order(IReadOnlySet<string> values)
        => values.OrderBy(static v => v, StringComparer.Ordinal).ToList();

    private static IReadOnlySet<string> Copy(IReadOnlySet<string> values)
        => new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);

    private static JsonSerializerOptions CreateJsonOptions()
        => new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };

    private sealed class SeasonState
    {
        public bool Loaded { get; set; }

        public HashSet<string> Heroes { get; } = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> Items { get; } = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> Traits { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<CandidateMeta> Candidates { get; } = new();

        public bool HasFormalEntries => Heroes.Count > 0 || Items.Count > 0 || Traits.Count > 0;
    }

    private sealed class DictionaryDto
    {
        public string SeasonId { get; set; } = string.Empty;

        public List<string> Heroes { get; set; } = new();

        public List<string> Items { get; set; } = new();

        public List<string> Traits { get; set; } = new();
    }
}