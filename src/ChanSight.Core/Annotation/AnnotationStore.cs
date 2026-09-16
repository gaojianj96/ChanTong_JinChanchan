using System.Text.Json;

namespace ChanSight.Core.Annotation;

/// <summary>
/// JSONL 追加存储: 实时修正(<see cref="CorrectionRecord"/>) 与金标(<see cref="GoldLabel"/>)。
/// append-only: 不修改已写行; 金标撤销通过追加同 Id 的 Revoked=true 记录, 加载时说 latest-wins。
/// 存储路径: {baseDirectory}/{matchId}/corrections.jsonl 与 {baseDirectory}/{matchId}/gold_labels.jsonl。
/// </summary>
public sealed class AnnotationStore
{
    private const string DefaultBaseDirectory = "corrections";
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = null };

    private readonly IFileSystem _fileSystem;
    private readonly string _baseDirectory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AnnotationStore()
        : this(new AnnotationFileSystem(), DefaultBaseDirectory)
    {
    }

    public AnnotationStore(string baseDirectory)
        : this(new AnnotationFileSystem(), baseDirectory)
    {
    }

    public AnnotationStore(IFileSystem fileSystem, string baseDirectory)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _baseDirectory = baseDirectory ?? throw new ArgumentNullException(nameof(baseDirectory));
    }

    public Task AppendCorrectionAsync(CorrectionRecord r, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(r);
        Validate(r.Id, r.MatchId, r.RegionType, r.CorrectedValue);
        return AppendLineAsync(GetCorrectionsPath(r.MatchId), r, ct);
    }

    public Task AppendGoldLabelAsync(GoldLabel g, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(g);
        Validate(g.Id, g.MatchId, g.RegionType, g.CorrectedValue);
        if (!CorrectionTypes.IsValid(g.CorrectionType))
        {
            throw new ArgumentException($"Unknown CorrectionType '{g.CorrectionType}'.", nameof(g));
        }

        return AppendLineAsync(GetGoldLabelsPath(g.MatchId), g, ct);
    }

    public async Task<IReadOnlyList<CorrectionRecord>> LoadCorrectionsAsync(string matchId, CancellationToken ct = default)
    {
        var result = await LoadCorrectionsWithIssuesAsync(matchId, ct).ConfigureAwait(false);
        return result.Records;
    }

    public async Task<IReadOnlyList<GoldLabel>> LoadGoldLabelsAsync(string matchId, CancellationToken ct = default)
    {
        var result = await LoadGoldLabelsWithIssuesAsync(matchId, ct).ConfigureAwait(false);
        return result.Records;
    }

    public async Task<IReadOnlyList<GoldLabel>> LoadGoldLabelsIncludingRevokedAsync(string matchId, CancellationToken ct = default)
    {
        var result = await ReadLinesAsync<GoldLabel>(GetGoldLabelsPath(matchId), ct).ConfigureAwait(false);
        return result.Records;
    }

    public Task<AnnotationLoadResult<CorrectionRecord>> LoadCorrectionsWithIssuesAsync(string matchId, CancellationToken ct = default)
        => ReadLinesAsync<CorrectionRecord>(GetCorrectionsPath(matchId), ct);

    public async Task<AnnotationLoadResult<GoldLabel>> LoadGoldLabelsWithIssuesAsync(string matchId, CancellationToken ct = default)
    {
        var raw = await ReadLinesAsync<GoldLabel>(GetGoldLabelsPath(matchId), ct).ConfigureAwait(false);
        return new AnnotationLoadResult<GoldLabel>
        {
            Records = ResolveLatestWins(raw.Records),
            Issues = raw.Issues
        };
    }

    private async Task AppendLineAsync<T>(string path, T record, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var line = JsonSerializer.Serialize(record, SerializerOptions);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _fileSystem.AppendLineAsync(path, line, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<AnnotationLoadResult<T>> ReadLinesAsync<T>(string path, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(path);

        var records = new List<T>();
        var issues = new List<string>();

        var content = await _fileSystem.ReadAllTextAsync(path, ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(content))
        {
            return new AnnotationLoadResult<T> { Records = records, Issues = issues };
        }

        var lines = content.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            ct.ThrowIfCancellationRequested();

            T? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<T>(line, SerializerOptions);
            }
            catch (JsonException ex)
            {
                issues.Add($"Line {i + 1} is not valid JSON: {ex.Message}");
                continue;
            }

            if (parsed is null)
            {
                issues.Add($"Line {i + 1} could not be deserialized into {typeof(T).Name}.");
                continue;
            }

            records.Add(parsed);
        }

        return new AnnotationLoadResult<T> { Records = records, Issues = issues };
    }

    private static IReadOnlyList<GoldLabel> ResolveLatestWins(IReadOnlyList<GoldLabel> records)
    {
        var latest = new Dictionary<string, GoldLabel>(StringComparer.Ordinal);
        var order = new List<string>();

        foreach (var record in records)
        {
            if (!latest.ContainsKey(record.Id))
            {
                order.Add(record.Id);
            }

            latest[record.Id] = record;
        }

        return order
            .Select(id => latest[id])
            .Where(static record => !record.Revoked)
            .ToList();
    }

    private static void Validate(string id, string matchId, string regionType, string correctedValue)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Id must not be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(matchId))
        {
            throw new ArgumentException("MatchId must not be empty.", nameof(matchId));
        }

        if (!RegionTypes.IsValid(regionType))
        {
            throw new ArgumentException($"Unknown RegionType '{regionType}'.", nameof(regionType));
        }

        if (string.IsNullOrWhiteSpace(correctedValue))
        {
            throw new ArgumentException("CorrectedValue must not be null or whitespace; use valid JSON such as {\"empty\":true}.", nameof(correctedValue));
        }

        try
        {
            using var _ = JsonDocument.Parse(correctedValue);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"CorrectedValue must be valid JSON: {ex.Message}", nameof(correctedValue));
        }
    }

    private string GetCorrectionsPath(string matchId) => Path.Combine(_baseDirectory, matchId, "corrections.jsonl");

    private string GetGoldLabelsPath(string matchId) => Path.Combine(_baseDirectory, matchId, "gold_labels.jsonl");
}