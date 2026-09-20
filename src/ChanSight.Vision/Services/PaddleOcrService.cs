using ChanSight.Core.Season;
using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;
using OpenCvSharp;

namespace ChanSight.Vision.Services;

public sealed class PaddleOcrService : IPaddleOcrService
{
    private readonly IOnnxInferenceEngine _engine;
    private readonly IRoiMapperService _roiMapper;
    private readonly SeasonRuntime _runtime;

    private const int MaxLevenshteinDistance = 2;

    public PaddleOcrService(IOnnxInferenceEngine engine, IRoiMapperService roiMapper, SeasonRuntime? runtime = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _roiMapper = roiMapper ?? throw new ArgumentNullException(nameof(roiMapper));
        _runtime = runtime ?? SeasonRuntime.CreateDefault();
    }

    public IReadOnlyList<DetectedShopCard> RecognizeShopCards(Mat frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (frame.Empty())
            return Array.Empty<DetectedShopCard>();

        var shopSlots = _roiMapper.CropShopSlots(frame);
        var results = new List<DetectedShopCard>(shopSlots.Count);

        try
        {
            for (int i = 0; i < shopSlots.Count; i++)
            {
                var result = RecognizeTextFromRegion(shopSlots[i]);
                var heroMatch = GameSeasonDictionary.TryFuzzyMatch(
                    result.CleanedText,
                    _runtime.Heroes,
                    MaxLevenshteinDistance);

                if (heroMatch is not null)
                {
                    results.Add(new DetectedShopCard(
                        i, heroMatch, EstimateCost(heroMatch), result.Confidence));
                }
            }
        }
        finally
        {
            foreach (var slot in shopSlots)
                slot.Dispose();
        }

        return results;
    }

    public OcrTextResult RecognizeGold(Mat frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        using var goldRoi = _roiMapper.CropRoi(frame, RoiRegionType.Gold);
        var result = RecognizeTextFromRegion(goldRoi);
        var numericValue = GameSeasonDictionary.TryParseNumeric(result.CleanedText);

        return new OcrTextResult(
            result.RawText,
            result.CleanedText,
            numericValue?.ToString(),
            result.Confidence);
    }

    public OcrTextResult RecognizeStage(Mat frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        using var stageRoi = _roiMapper.CropRoi(frame, RoiRegionType.StageRound);
        var result = RecognizeTextFromRegion(stageRoi);

        return new OcrTextResult(
            result.RawText,
            result.CleanedText,
            CleanStageText(result.CleanedText),
            result.Confidence);
    }

    public static OcrTextResult RecognizeTextFromRegion(Mat region)
    {
        if (region is null || region.Empty())
            return new OcrTextResult(string.Empty, string.Empty, null, 0f);

        return new OcrTextResult(string.Empty, string.Empty, null, 0f);
    }

    private static int EstimateCost(string heroName)
    {
        var cost1Heroes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "波比", "吉格斯", "艾希", "墨菲特", "弗拉基米尔"
        };

        var cost2Heroes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "布隆", "蔚", "卡蜜尔", "瑟庄妮", "慎", "图奇", "婕拉"
        };

        var cost3Heroes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "阿狸", "德莱文", "莫甘娜", "厄斐琉斯", "希瓦娜", "俄洛伊"
        };

        var cost4Heroes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "凯尔", "亚索", "崔斯特", "千珏", "拉克丝"
        };

        var cost5Heroes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "永恩", "劫", "奥瑞利安·索尔"
        };

        if (cost1Heroes.Contains(heroName)) return 1;
        if (cost2Heroes.Contains(heroName)) return 2;
        if (cost3Heroes.Contains(heroName)) return 3;
        if (cost4Heroes.Contains(heroName)) return 4;
        if (cost5Heroes.Contains(heroName)) return 5;

        return 1;
    }

    public static string? CleanStageText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var lower = text.Trim()
            .Replace("阶段", string.Empty)
            .Replace("Stage", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("第", string.Empty)
            .Replace("局", string.Empty);

        return lower.Trim();
    }
}