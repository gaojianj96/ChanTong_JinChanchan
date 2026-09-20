using ChanSight.Core.Annotation;
using ChanSight.Core.FrameStorage;
using ChanSight.Core.Season;
using ChanSight.Overlay.Models;
using ChanSight.Vision.Models;
using OpenCvSharp;

namespace ChanSight.Overlay.Services;

/// <summary>
/// 回顾窗口核心服务: 载入关键帧(图 + 布局元数据 + 识别结果), 把人工决策写成金标 <see cref="GoldLabel"/>。
/// 语义吸收终审:
/// <list type="bullet">
/// <item>「未修正=正确」: 完成一帧校验时, 未修正的占用格自动补 <c>confirm-correct</c> 金标(RecognizedValue == CorrectedValue), 使准确率分母完整;</item>
/// <item>金标撤销: 对已确认格改判时, 追加同 Id 的 Revoked=true 墓碑记录, 加载时 latest-wins;</item>
/// <item>FrameId 锚定: 回顾针对关键帧, 不针对直播流。</item>
/// </list>
/// </summary>
public sealed class ReviewService
{
    public const string RecognizerVersion = "1";

    private const int BoardCellCount = 28;
    private const int BenchCellCount = 9;
    private const int ShopCellCount = 5;

    /// <summary><see cref="FrameArchive"/> 内部关键帧目录名(根目录下 frames 子目录)。</summary>
    private const string FramesDirectoryName = "frames";

    public delegate Task<RecognitionFrame> FrameRecognitionFunc(Mat frame, CancellationToken ct);

    private static readonly IReadOnlyList<string> HeroRegions = new[]
    {
        RegionTypes.BoardHero,
        RegionTypes.BenchHero,
        RegionTypes.ShopHero,
    };

    private readonly FrameArchive _archive;
    private readonly AnnotationStore _store;
    private readonly FrameRecognitionFunc _recognize;
    private readonly ISeasonDictionaryWriter? _writer;

    public ReviewService(
        FrameArchive archive,
        AnnotationStore store,
        FrameRecognitionFunc recognize,
        ISeasonDictionaryWriter? writer = null)
    {
        _archive = archive ?? throw new ArgumentNullException(nameof(archive));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _recognize = recognize ?? throw new ArgumentNullException(nameof(recognize));
        _writer = writer;
    }

    /// <summary>该局已持久化的关键帧列表(按 SnapshotVersion 升序)。</summary>
    public IReadOnlyList<FrameKey> ListKeyFrames(string matchId) => _archive.ListKeyFrames(matchId);

    /// <summary>
    /// 归档中已存在的对局 ID 列表: 枚举 FrameArchive 根目录 frames 子目录下的 matchId 目录名。
    /// </summary>
    public IReadOnlyList<string> ListMatchIds()
    {
        var framesDirectory = Path.Combine(_archive.RootDirectory, FramesDirectoryName);
        if (!Directory.Exists(framesDirectory))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateDirectories(framesDirectory)
            .Select(static path => Path.GetFileName(path) ?? string.Empty)
            .Where(static name => name.Length > 0)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// 读回关键帧图 + 布局元数据 + 该帧识别结果。识别结果未随帧存档, 一律经
    /// <see cref="FrameRecognitionFunc"/> 重算(<see cref="RecognitionPipeline"/> 对同帧幂等且无 VLM 成本)。
    /// </summary>
    public ReviewFrame LoadFrame(string matchId, string frameId)
    {
        var (image, meta) = _archive.LoadKeyFrameWithMeta(matchId, frameId);
        try
        {
            var recognition = _recognize(image, CancellationToken.None).GetAwaiter().GetResult();
            return new ReviewFrame(
                matchId,
                frameId,
                image,
                meta ?? new Dictionary<string, string>(StringComparer.Ordinal),
                recognition);
        }
        catch
        {
            image.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 把用户决策写成金标(逐格一条)。未修正的占用格自动补 <c>confirm-correct</c>;
    /// 对已存在的同 Id 金标先追加 Revoked=true 墓碑再写新值。
    /// </summary>
    public async Task<IReadOnlyList<GoldLabel>> CommitReviewAsync(
        string matchId,
        string frameId,
        IReadOnlyList<ReviewDecision> decisions,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(matchId);
        ArgumentException.ThrowIfNullOrWhiteSpace(frameId);
        ArgumentNullException.ThrowIfNull(decisions);

        var review = LoadFrame(matchId, frameId);
        try
        {
            var snapshotVersion = ParseSnapshotVersion(review.Meta);
            var full = CompleteDecisions(review.Recognition, decisions);

            var existing = await _store.LoadGoldLabelsAsync(matchId, ct).ConfigureAwait(false);
            var existingById = existing
                .Where(label => label.FrameId == frameId)
                .ToDictionary(label => label.Id, StringComparer.Ordinal);

            var labels = new List<GoldLabel>(full.Count);
            foreach (var decision in full)
            {
                var label = BuildLabel(matchId, frameId, snapshotVersion, review.Recognition, decision);
                if (existingById.TryGetValue(label.Id, out var previous))
                {
                    await _store.AppendGoldLabelAsync(previous with { Revoked = true }, ct).ConfigureAwait(false);
                }

                await _store.AppendGoldLabelAsync(label, ct).ConfigureAwait(false);
                labels.Add(label);
            }

            return labels;
        }
        finally
        {
            review.FrameImage.Dispose();
        }
    }

    /// <summary>
    /// 三选一确认候选: 用户在回顾侧把某格修正为字典外名字(New) / 映射已有英雄(MapExisting) /
    /// 标记为空或忽略(Ignore) 时, 由 <see cref="ReviewViewModel"/> 调用, 经写入视图落到赛季字典。
    /// </summary>
    public Task ConfirmCandidateAsync(string seasonId, string entity, ConfirmKind kind, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seasonId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity);

        _writer?.Confirm(seasonId, entity, kind);
        return Task.CompletedTask;
    }

    /// <summary>补全未修正占用格为 confirm-correct, 保持逐格一条且准确率分母完整。</summary>
    private static IReadOnlyList<ReviewDecision> CompleteDecisions(
        RecognitionFrame? recognition,
        IReadOnlyList<ReviewDecision> decisions)
    {
        var result = new List<ReviewDecision>(decisions.Count);
        var covered = new HashSet<string>(StringComparer.Ordinal);

        foreach (var decision in decisions)
        {
            result.Add(decision);
            covered.Add(CellKey(decision.RegionType, decision.CellIndex));
        }

        foreach (var region in HeroRegions)
        {
            for (var cellIndex = 0; cellIndex < GetCellCount(region); cellIndex++)
            {
                string? recognized = RecognizedPlain(recognition, region, cellIndex);
                if (string.IsNullOrWhiteSpace(recognized) || covered.Contains(CellKey(region, cellIndex)))
                {
                    continue;
                }

                string encoded = EncodeConfirmCorrect(recognized);
                result.Add(new ReviewDecision(region, cellIndex, recognized, encoded, CorrectionTypes.ConfirmCorrect, null));
            }
        }

        return result;
    }

    private static GoldLabel BuildLabel(
        string matchId,
        string frameId,
        long snapshotVersion,
        RecognitionFrame? recognition,
        ReviewDecision decision)
    {
        bool confirmCorrect = string.Equals(decision.CorrectionType, CorrectionTypes.ConfirmCorrect, StringComparison.Ordinal);

        // confirm-correct 字样保证 RecognizedValue == CorrectedValue(未修正即正确)。
        string? recognized = confirmCorrect ? decision.CorrectedValue : decision.RecognizedValue;
        string? correctionType = confirmCorrect
            ? CorrectionTypes.ConfirmCorrect
            : decision.CorrectionType ?? CorrectionTypes.Classification;

        return new GoldLabel(
            Id: ComputeLabelId(matchId, frameId, decision.RegionType, decision.CellIndex),
            CorrectionId: decision.CorrectionId,
            MatchId: matchId,
            FrameId: frameId,
            SnapshotVersion: snapshotVersion,
            RegionType: decision.RegionType,
            CellIndex: decision.CellIndex,
            RecognizedValue: recognized,
            CorrectedValue: decision.CorrectedValue,
            Confidence: recognition?.Confidence ?? 1.0,
            SourceTier: recognition?.SourceTier.ToString() ?? "T0",
            RecognizerVersion: RecognizerVersion,
            CorrectionType: correctionType)
        {
            ConfirmedAt = DateTimeOffset.UtcNow,
        };
    }

    private static string? RecognizedPlain(RecognitionFrame? recognition, string regionType, int cellIndex)
    {
        if (recognition is null)
        {
            return null;
        }

        return regionType switch
        {
            RegionTypes.BoardHero => cellIndex < recognition.BoardCells.Count ? recognition.BoardCells[cellIndex].Name : null,
            RegionTypes.BenchHero => cellIndex < recognition.BenchCells.Count ? recognition.BenchCells[cellIndex].Name : null,
            RegionTypes.ShopHero => cellIndex < recognition.ShopCards.Count
                ? NullIfEmpty(recognition.ShopCards[cellIndex].Name)
                : null,
            _ => null,
        };
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string EncodeConfirmCorrect(string recognized) =>
        string.IsNullOrWhiteSpace(recognized) ? CorrectionValue.Empty() : CorrectionValue.Hero(recognized);

    private static string CellKey(string regionType, int cellIndex) => $"{regionType}:{cellIndex}";

    private static int GetCellCount(string regionType) => regionType switch
    {
        RegionTypes.BoardHero => BoardCellCount,
        RegionTypes.BenchHero => BenchCellCount,
        RegionTypes.ShopHero => ShopCellCount,
        _ => 0,
    };

    private static string ComputeLabelId(string matchId, string frameId, string regionType, int cellIndex)
        => $"{matchId}:{frameId}:{regionType}:{cellIndex}";

    private static long ParseSnapshotVersion(IReadOnlyDictionary<string, string> meta)
    {
        if (meta.TryGetValue("snapshotVersion", out var value) && long.TryParse(value, out var version))
        {
            return version;
        }

        return 0;
    }
}