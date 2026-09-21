using ChanSight.Core.Season;
using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;
using OpenCvSharp;

namespace ChanSight.Vision.Services;

public sealed class PaddleOcrService : IPaddleOcrService
{
    private readonly IOnnxInferenceEngine _engine;
    private readonly IRoiMapperService _roiMapper;
    private readonly LocalDeterministicRecognizer _localRecognizer;
    private readonly SeasonRuntime _runtime;

    private const int MaxLevenshteinDistance = 2;

    // Stage "n-m" splitting. The dash is a short, wide stroke vs. the tall digits,
    // so it is located by aspect ratio first; when that fails (merged glyphs) we fall
    // back to the largest vertical empty gap.
    private const int StageMinComponentArea = 8;
    private const double StageDashMaxHeightRatio = 0.45;
    private const double StageDashMinAspectRatio = 1.3;
    private const byte StageBinarizationThreshold = 127;

    public PaddleOcrService(
        IOnnxInferenceEngine engine,
        IRoiMapperService roiMapper,
        LocalDeterministicRecognizer localRecognizer,
        SeasonRuntime? runtime = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _roiMapper = roiMapper ?? throw new ArgumentNullException(nameof(roiMapper));
        _localRecognizer = localRecognizer ?? throw new ArgumentNullException(nameof(localRecognizer));
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
                var result = RecognizeTextFromRegion(shopSlots[i], "shop");
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
        var result = RecognizeTextFromRegion(goldRoi, "gold");
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
        var result = RecognizeStageText(stageRoi);
        LogOcr("stage", result.RawText, result.Confidence);
        return result;
    }

    public OcrTextResult RecognizeTextFromRegion(Mat region, string regionName = "region")
    {
        var result = RecognizeDigitsFromRegion(region);
        LogOcr(regionName, result.RawText, result.Confidence);
        return result;
    }

    private OcrTextResult RecognizeDigitsFromRegion(Mat region)
    {
        if (region is null || region.Empty())
            return new OcrTextResult(string.Empty, string.Empty, null, 0f);

        var (digits, confidence) = _localRecognizer.RecognizeDigitsWithConfidence(region);
        if (digits is null)
            return new OcrTextResult(string.Empty, string.Empty, null, 0f);

        var conf = ClampConfidence(confidence);
        return new OcrTextResult(digits, digits, digits, conf);
    }

    private OcrTextResult RecognizeStageText(Mat stageRoi)
    {
        if (stageRoi is null || stageRoi.Empty())
            return new OcrTextResult(string.Empty, string.Empty, null, 0f);

        var split = SplitStageRoi(stageRoi);
        if (split is null)
            return new OcrTextResult(string.Empty, string.Empty, null, 0f);

        using var left = split.Value.Left;
        using var right = split.Value.Right;

        var leftResult = _localRecognizer.RecognizeDigitsWithConfidence(left);
        var rightResult = _localRecognizer.RecognizeDigitsWithConfidence(right);

        if (leftResult.Digits is null || rightResult.Digits is null)
            return new OcrTextResult(string.Empty, string.Empty, null, 0f);

        var raw = $"{leftResult.Digits}-{rightResult.Digits}";
        var confidence = ClampConfidence(Math.Min(leftResult.Confidence, rightResult.Confidence));
        return new OcrTextResult(raw, raw, raw, confidence);
    }

    private static (Mat Left, Mat Right)? SplitStageRoi(Mat roi)
    {
        if (roi is null || roi.Empty())
            return null;

        using var gray = ToGray(roi);
        using var binary = new Mat();
        Cv2.Threshold(gray, binary, StageBinarizationThreshold, 255, ThresholdTypes.Binary);

        using var clone = binary.Clone();
        Cv2.FindContours(clone, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var rects = new List<Rect>();
        foreach (var contour in contours)
        {
            var rect = Cv2.BoundingRect(contour);
            if (rect.Width * rect.Height < StageMinComponentArea)
                continue;
            rects.Add(rect);
        }

        int leftWidth;
        int rightStart;

        // Prefer the dash: short vertically, wide horizontally. Split around the
        // entire dash so no dash remnant pollutes either digit segment.
        Rect? dash = null;
        foreach (var rect in rects)
        {
            if (rect.Height < binary.Height * StageDashMaxHeightRatio &&
                rect.Width > rect.Height * StageDashMinAspectRatio)
            {
                dash = rect;
                break;
            }
        }

        if (dash is not null)
        {
            leftWidth = dash.Value.X;
            rightStart = dash.Value.X + dash.Value.Width;
        }
        else
        {
            var splitX = FindLargestGap(binary);
            if (splitX < 0)
                return null;
            leftWidth = splitX;
            rightStart = splitX + 1;
        }

        if (leftWidth <= 0 || rightStart >= roi.Width)
            return null;

        var left = new Mat(roi, new Rect(0, 0, leftWidth, roi.Height));
        var right = new Mat(roi, new Rect(rightStart, 0, roi.Width - rightStart, roi.Height));

        if (left.Empty() || right.Empty())
        {
            left.Dispose();
            right.Dispose();
            return null;
        }

        return (left, right);
    }

    private static Mat ToGray(Mat roi)
    {
        if (roi.Channels() == 1)
            return roi.Clone();

        var colorCode = roi.Channels() == 4
            ? ColorConversionCodes.BGRA2GRAY
            : ColorConversionCodes.BGR2GRAY;
        var gray = new Mat();
        Cv2.CvtColor(roi, gray, colorCode);
        return gray;
    }

    private static int FindLargestGap(Mat binary)
    {
        var projection = new int[binary.Width];
        for (int x = 0; x < binary.Width; x++)
        {
            int count = 0;
            for (int y = 0; y < binary.Height; y++)
            {
                if (binary.At<byte>(y, x) > 0)
                    count++;
            }
            projection[x] = count;
        }

        int bestStart = -1;
        int bestLength = 0;
        int runStart = -1;
        for (int x = 0; x < binary.Width; x++)
        {
            if (projection[x] == 0)
            {
                if (runStart < 0)
                    runStart = x;
            }
            else if (runStart >= 0)
            {
                int length = x - runStart;
                if (length > bestLength)
                {
                    bestLength = length;
                    bestStart = runStart;
                }
                runStart = -1;
            }
        }

        if (runStart >= 0)
        {
            int length = binary.Width - runStart;
            if (length > bestLength)
            {
                bestLength = length;
                bestStart = runStart;
            }
        }

        return bestStart < 0 ? -1 : bestStart + bestLength / 2;
    }

    private static float ClampConfidence(double confidence) =>
        (float)Math.Clamp(confidence, 0.0, 1.0);

    private static void LogOcr(string region, string rawText, float confidence) =>
        Console.WriteLine($"[ocr] region={region} raw=\"{rawText}\" confidence={confidence:F3}");

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
