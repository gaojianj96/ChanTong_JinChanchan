using System.Text.Json;
using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;
using OpenCvSharp;

namespace ChanSight.Vision.Services;

/// <summary>
/// Manually-triggered board anchor detection. Sends a single (downscaled)
/// full-frame screenshot to the VLM to locate the board's four corners, then
/// pairs those observed corners against the canonical <c>BoardArea</c> corners
/// and fits a per-axis affine (scale + offset) calibration via
/// <see cref="IAnchorCalibrator"/>. Any failure (VLM error / missing corners /
/// low confidence) degrades to <see cref="AnchorCalibrationResult.IsValid"/>=false
/// instead of throwing.
/// </summary>
public sealed class BoardAnchorDetector
{
    private const int MaxLongEdge = 1680;
    private const double MinConfidence = 0.5;

    private readonly IVlmClient _client;
    private readonly IAnchorCalibrator _calibrator;

    public BoardAnchorDetector(IVlmClient client, IAnchorCalibrator calibrator)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _calibrator = calibrator ?? throw new ArgumentNullException(nameof(calibrator));
    }

    /// <summary>
    /// Detects board-corner anchors from a full frame and returns the fitted
    /// calibration. Invalid results are returned (not thrown) on any failure.
    /// </summary>
    public async Task<AnchorCalibrationResult> DetectAsync(Mat fullFrame, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(fullFrame);

        if (fullFrame.Empty())
        {
            return Invalid();
        }

        string json;
        try
        {
            var prompt = BuildPrompt();
            var images = BuildImages(fullFrame);
            json = await _client.CompleteAsync(prompt, images, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Invalid();
        }

        var corners = Parse(json);
        if (corners is null)
        {
            return Invalid();
        }

        var pairs = BuildPairs(corners);

        var options = new AnchorCalibrationOptions { AnchorPairs = pairs };
        var result = _calibrator.Calibrate(fullFrame, options, ct);

        if (!result.IsValid || result.Confidence <= 0.0 || corners.Confidence < MinConfidence)
        {
            return Invalid(result.ObservedAnchorPoints);
        }

        return result with { Confidence = Math.Min(result.Confidence, corners.Confidence) };
    }

    public string BuildPromptForDisplay() => BuildPrompt();

    private static string BuildPrompt()
    {
        return """
            下面是一张《金铲铲之战》整帧游戏截图(为节省传输可能已按长边 ≤1680 等比缩放)。
            请定位并返回像素坐标(坐标以你收到的这张截图的实际像素尺寸为基准, 左上角为原点):

            1. 棋盘区域(28 格, 4 行 × 7 列)的四个角点坐标:
               - "tl": 棋盘左上角
               - "tr": 棋盘右上角
               - "br": 棋盘右下角
               - "bl": 棋盘左下角
            2. 顶部 HUD 金币图标的位置坐标(取图标中心): "gold"
            3. 顶部 HUD 阶段数字的位置坐标(取文本中心): "stage"
            4. 整体置信度 "confidence"(0 到 1 之间的浮点数)

            要求:
            1. 只输出一个 JSON 对象, 不要输出任何解释、前缀或 markdown 代码块。
            2. 坐标为 [x, y] 的整数数组。
            3. schema:
            {
              "board": {"tl":[x,y],"tr":[x,y],"br":[x,y],"bl":[x,y]},
              "gold": [x,y],
              "stage": [x,y],
              "confidence": 0.8
            }
            4. 无法确定的坐标填 null, 并调低 confidence。
            """;
    }

    private static IReadOnlyList<(string mime, byte[] data)> BuildImages(Mat fullFrame)
    {
        var resized = ResizeForVlm(fullFrame);
        var data = resized.Empty() ? Array.Empty<byte>() : resized.ImEncode(".jpg");
        return new List<(string mime, byte[] data)> { ("image/jpeg", data) };
    }

    private static Mat ResizeForVlm(Mat fullFrame)
    {
        if (fullFrame.Empty())
        {
            return fullFrame;
        }

        int maxEdge = Math.Max(fullFrame.Width, fullFrame.Height);
        if (maxEdge <= MaxLongEdge)
        {
            var unchanged = new Mat();
            fullFrame.CopyTo(unchanged);
            return unchanged;
        }

        double scale = (double)MaxLongEdge / maxEdge;
        int newWidth = Math.Max(1, (int)Math.Round(fullFrame.Width * scale));
        int newHeight = Math.Max(1, (int)Math.Round(fullFrame.Height * scale));

        var resized = new Mat();
        Cv2.Resize(fullFrame, resized, new Size(newWidth, newHeight), 0, 0, InterpolationFlags.Area);
        return resized;
    }

    /// <summary>
    /// Parses the VLM JSON. Coordinates are expected relative to the actual
    /// full-frame size (the VLM is instructed to return full-frame coordinates;
    /// the downscale is only for transmission).
    /// </summary>
    private static DetectedCorners? Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var boardProp = root.GetProperty("board");
            if (boardProp.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            Point2d? tl = ReadPoint(boardProp, "tl");
            Point2d? tr = ReadPoint(boardProp, "tr");
            Point2d? br = ReadPoint(boardProp, "br");
            Point2d? bl = ReadPoint(boardProp, "bl");

            if (tl is null || tr is null || br is null || bl is null)
            {
                return null;
            }

            double confidence = 0.0;
            if (root.TryGetProperty("confidence", out var confProp) && confProp.ValueKind == JsonValueKind.Number)
            {
                confidence = confProp.GetDouble();
            }

            return new DetectedCorners(
                tl.Value, tr.Value, br.Value, bl.Value, confidence);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static Point2d? ReadPoint(JsonElement board, string name)
    {
        if (!board.TryGetProperty(name, out var prop) || prop.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var arr = prop.EnumerateArray().ToArray();
        if (arr.Length < 2)
        {
            return null;
        }

        if (arr[0].ValueKind != JsonValueKind.Number || arr[1].ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        double x = arr[0].GetDouble();
        double y = arr[1].GetDouble();

        if (double.IsNaN(x) || double.IsNaN(y) || double.IsInfinity(x) || double.IsInfinity(y))
        {
            return null;
        }

        return new Point2d(x, y);
    }

    private static IReadOnlyList<AnchorPointPair> BuildPairs(DetectedCorners corners)
    {
        var canonical = CanonicalRoiDefinitions.BoardArea;

        Point2d tlC = new(canonical.Left, canonical.Top);
        Point2d trC = new(canonical.Right, canonical.Top);
        Point2d brC = new(canonical.Right, canonical.Bottom);
        Point2d blC = new(canonical.Left, canonical.Bottom);

        return new List<AnchorPointPair>
        {
            new(tlC, corners.Tl),
            new(trC, corners.Tr),
            new(brC, corners.Br),
            new(blC, corners.Bl),
        };
    }

    private static AnchorCalibrationResult Invalid(IReadOnlyList<Point2d>? observed = null) => new()
    {
        IsValid = false,
        Confidence = 0.0,
        ScaleX = 0,
        ScaleY = 0,
        OffsetX = 0,
        OffsetY = 0,
        MaxReprojectionErrorPx = double.NaN,
        ObservedAnchorPoints = observed ?? Array.Empty<Point2d>(),
    };

    private sealed record DetectedCorners(Point2d Tl, Point2d Tr, Point2d Br, Point2d Bl, double Confidence);
}