using System.Text.Json;
using ChanSight.Core.Season;
using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;

namespace ChanSight.Vision.Services;

/// <summary>
/// Batches changed-cell crops into a structured prompt, delegates recognition to
/// an <see cref="IVlmClient"/>, then parses and fuzzy-corrects the returned JSON
/// into per-cell verdicts. Any unrecoverable failure degrades every cell to a
/// low-confidence verdict instead of throwing.
///
/// This adapter runs on the automatic (live) recognition path only — the manual
/// whole-frame VLM button uses <see cref="IVlmClient"/> directly — so its calls
/// are bounded by a short budget. A hang or timeout degrades softly instead of
/// stalling the live frame loop (the upstream <see cref="OpenRouterVlmClient"/>
/// defaults to a 60s request timeout, which would otherwise stretch a single
/// frame's cadence out to a minute).
/// </summary>
public sealed class VlmRecognitionAdapter
{
    private const int MaxLevenshteinDistance = 2;
    private const int MaxAttempts = 2;

    /// <summary>
    /// Upper bound for one auto-path VLM round (including the single retry). Kept
    /// small so a slow/hung VLM cannot drag the live loop below ~2fps.
    /// </summary>
    public static readonly TimeSpan DefaultAutoTimeout = TimeSpan.FromSeconds(5);

    private readonly IVlmClient _client;
    private readonly SeasonRuntime _runtime;
    private readonly TimeSpan _timeout;

    public VlmRecognitionAdapter(IVlmClient client, SeasonRuntime? runtime = null, TimeSpan? timeout = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _runtime = runtime ?? SeasonRuntime.CreateDefault();
        _timeout = timeout ?? DefaultAutoTimeout;
    }

    public async Task<VlmRecognitionResult> RecognizeAsync(
        IReadOnlyList<VlmCellInput> cells,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(cells);

        if (cells.Count == 0)
            return new VlmRecognitionResult(Array.Empty<VlmCellVerdict>());

        var prompt = BuildPrompt(cells);
        var images = cells.Select(c => (c.Mime, c.ImageData)).ToList();

        // Bound the whole round (retry included) with a short auto-path budget so a
        // hung VLM can never stall the frame cadence. The caller's token is linked in
        // so a genuine stop cancels promptly; the *budget* timeout alone degrades
        // instead of propagating (it must never cancel the live loop).
        using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budgetCts.CancelAfter(_timeout);

        string json;
        try
        {
            json = await CompleteWithSingleRetryAsync(prompt, images, budgetCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Auto-path budget expired: degrade rather than letting a 60s upstream
            // timeout stall the next frame.
            return Degrade(cells);
        }
        catch (Exception)
        {
            return Degrade(cells);
        }

        try
        {
            return ParseVerdicts(json, cells);
        }
        catch (JsonException)
        {
            return Degrade(cells);
        }
    }

    private async Task<string> CompleteWithSingleRetryAsync(
        string prompt,
        IReadOnlyList<(string mime, byte[] data)> images,
        CancellationToken ct)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                return await _client.CompleteAsync(prompt, images, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception) when (attempt + 1 < MaxAttempts)
            {
                // fall through and retry once
            }
        }
    }

    private VlmRecognitionResult ParseVerdicts(string json, IReadOnlyList<VlmCellInput> cells)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            return Degrade(cells);

        var byIndex = new Dictionary<int, VlmCellVerdict>();
        foreach (var element in root.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
                continue;

            if (!element.TryGetProperty("cellIndex", out var indexProp) ||
                !indexProp.TryGetInt32(out var cellIndex))
            {
                continue;
            }

            string? name = null;
            if (element.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
            {
                var rawName = nameProp.GetString();
                if (!string.IsNullOrWhiteSpace(rawName))
                {
                    name = GameSeasonDictionary.TryFuzzyMatch(
                        rawName!,
                        _runtime.Heroes,
                        MaxLevenshteinDistance) ?? rawName;
                }
            }

            int? star = null;
            if (element.TryGetProperty("star", out var starProp) && starProp.TryGetInt32(out var parsedStar))
                star = parsedStar;

            double confidence = 0.0;
            if (element.TryGetProperty("confidence", out var confidenceProp) &&
                confidenceProp.TryGetDouble(out var parsedConfidence))
            {
                confidence = parsedConfidence;
            }

            byIndex[cellIndex] = new VlmCellVerdict(
                cellIndex,
                name,
                star,
                confidence,
                SourceTier.T2,
                element.GetRawText());
        }

        var verdicts = new List<VlmCellVerdict>(cells.Count);
        foreach (var cell in cells)
        {
            if (byIndex.TryGetValue(cell.CellIndex, out var verdict))
                verdicts.Add(verdict);
            else
                verdicts.Add(new VlmCellVerdict(cell.CellIndex, null, null, 0.0));
        }

        return new VlmRecognitionResult(verdicts);
    }

    private static VlmRecognitionResult Degrade(IReadOnlyList<VlmCellInput> cells) =>
        new(cells.Select(c => new VlmCellVerdict(c.CellIndex, null, null, 0.0)).ToList());

    private string BuildPrompt(IReadOnlyList<VlmCellInput> cells)
    {
        var heroTable = string.Join("、", _runtime.Heroes);
        var cellIndices = string.Join(", ", cells.Select(c => c.CellIndex));

        return $$"""
            你是《金铲铲之战》棋盘格识别助手。下面会按顺序给出若干张棋盘格截图,每张截图对应一个 cellIndex,依次为: {{cellIndices}}。
            请识别每张截图中的英雄,只能从以下候选英雄全表中选取一个名字(空格子填 null):
            {{heroTable}}

            输出要求:
            1. 只输出一个 JSON 数组,不要输出任何解释、前缀或 markdown 代码块。
            2. JSON 数组中的每个元素按截图顺序对应一个单元格,且必须遵守以下 schema:
            [
              {
                "cellIndex": 整数(与输入截图序号一致),
                "name": 候选英雄名或 null,
                "star": 整数(0-3 的星级),
                "confidence": 0 到 1 之间的浮点数
              }
            ]
            3. 无法确定或空格子时 name 填 null、confidence 填 0。
            """;
    }
}