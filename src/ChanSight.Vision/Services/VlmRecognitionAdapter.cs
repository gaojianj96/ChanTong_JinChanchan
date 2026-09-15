using System.Text.Json;
using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;

namespace ChanSight.Vision.Services;

/// <summary>
/// Batches changed-cell crops into a structured prompt, delegates recognition to
/// an <see cref="IVlmClient"/>, then parses and fuzzy-corrects the returned JSON
/// into per-cell verdicts. Any unrecoverable failure degrades every cell to a
/// low-confidence verdict instead of throwing.
/// </summary>
public sealed class VlmRecognitionAdapter
{
    private const int MaxLevenshteinDistance = 2;
    private const int MaxAttempts = 2;

    private readonly IVlmClient _client;

    public VlmRecognitionAdapter(IVlmClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
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

        string json;
        try
        {
            json = await CompleteWithSingleRetryAsync(prompt, images, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
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

    private static VlmRecognitionResult ParseVerdicts(string json, IReadOnlyList<VlmCellInput> cells)
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
                        GameSeasonDictionary.Heroes,
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

    private static string BuildPrompt(IReadOnlyList<VlmCellInput> cells)
    {
        var heroTable = string.Join("、", GameSeasonDictionary.Heroes);
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