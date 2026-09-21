using System.Text;
using OpenCvSharp;

namespace ChanSight.Vision.Services;

/// <summary>
/// Local deterministic recognizer: template-matched multi-digit numbers and
/// HSV-segmented star pips. This is a mechanism-level delivery — the rendered
/// reference glyphs use a host font that may differ from the on-device game font,
/// and the star hue window is calibrated against reference colors only. On-device
/// accuracy is tracked as a receipt follow-up.
/// </summary>
public class LocalDeterministicRecognizer
{
    // Template rendering parameters. Note: these use the Hershey stroke font as a
    // deterministic stand-in for the real game font (a "Segoe UI" render would need
    // System.Drawing, which is intentionally not pulled in as a new dependency).
    public const HersheyFonts TemplateFontFace = HersheyFonts.HersheySimplex;
    public const double TemplateFontScale = 2.0;
    public const int TemplateThickness = 4;
    public const int TemplateCanvasWidth = 128;
    public const int TemplateCanvasHeight = 160;

    private const int CanonicalGlyphWidth = 48;
    private const int CanonicalGlyphHeight = 64;
    private const int MaxDigits = 3;
    private const int MaxStars = 3;
    private const byte BinarizationThreshold = 127;
    private const int MinGlyphArea = 40;

    private readonly double _digitConfidenceThreshold;
    private readonly int _starHueLower;
    private readonly int _starHueUpper;
    private readonly int _starSaturationThreshold;
    private readonly int _starValueThreshold;
    private readonly int _starMinArea;

    public LocalDeterministicRecognizer(
        double digitConfidenceThreshold = 0.75,
        int starHueLower = 20,
        int starHueUpper = 40,
        int starSaturationThreshold = 120,
        int starValueThreshold = 120,
        int starMinArea = 100)
    {
        _digitConfidenceThreshold = digitConfidenceThreshold;
        _starHueLower = starHueLower;
        _starHueUpper = starHueUpper;
        _starSaturationThreshold = starSaturationThreshold;
        _starValueThreshold = starValueThreshold;
        _starMinArea = starMinArea;
    }

    /// <summary>
    /// Recognizes a single multi-digit number (up to 3 digits) in an ROI via
    /// template matching against runtime-rendered 0-9 glyphs. Returns <c>null</c>
    /// when no digits are found or the best match falls below the confidence threshold.
    /// </summary>
    public virtual string? RecognizeDigits(Mat roi)
        => RecognizeDigitsWithConfidence(roi).Digits;

    /// <summary>
    /// Same as <see cref="RecognizeDigits"/> but also reports the template-match
    /// confidence. <c>Digits</c> is <c>null</c> when no digits are found or the best
    /// match falls below the confidence threshold; <c>Confidence</c> is the lowest
    /// per-glyph match confidence across all recognized digits (0 when unrecognized).
    /// </summary>
    public virtual (string? Digits, double Confidence) RecognizeDigitsWithConfidence(Mat roi)
    {
        ArgumentNullException.ThrowIfNull(roi);
        if (roi.Empty()) return (null, 0.0);

        using var gray = new Mat();
        if (roi.Channels() == 1)
        {
            roi.CopyTo(gray);
        }
        else
        {
            var colorCode = roi.Channels() == 4
                ? ColorConversionCodes.BGRA2GRAY
                : ColorConversionCodes.BGR2GRAY;
            Cv2.CvtColor(roi, gray, colorCode);
        }

        using var binary = new Mat();
        Cv2.Threshold(gray, binary, BinarizationThreshold, 255, ThresholdTypes.Binary);

        var digitSlots = SegmentDigitSlots(binary);
        if (digitSlots.Count == 0 || digitSlots.Count > MaxDigits)
            return (null, 0.0);

        var templates = BuildTemplates();
        var digits = new StringBuilder(digitSlots.Count);
        double minConfidence = double.MaxValue;
        foreach (var slot in digitSlots)
        {
            using var canonical = CanonicalizeGlyph(binary, slot);
            var classified = ClassifyGlyph(canonical, templates);

            if (classified.Confidence < _digitConfidenceThreshold)
                return (null, 0.0);

            minConfidence = Math.Min(minConfidence, classified.Confidence);
            digits.Append(classified.Digit);
        }

        return (digits.ToString(), minConfidence);
    }

    /// <summary>
    /// Counts yellow star-pip blobs (0-3) inside a board-cell ROI using HSV color
    /// segmentation and connected-component counting. Returns 0 when the blobs are
    /// too noisy to interpret (more blobs than the valid star range).
    /// </summary>
    public int CountStars(Mat roi)
    {
        ArgumentNullException.ThrowIfNull(roi);
        if (roi.Empty()) return 0;

        using var hsv = new Mat();
        Cv2.CvtColor(roi, hsv, ColorConversionCodes.BGR2HSV);

        using var mask = new Mat();
        var lower = new Scalar(_starHueLower, _starSaturationThreshold, _starValueThreshold);
        var upper = new Scalar(_starHueUpper, 255, 255);
        Cv2.InRange(hsv, lower, upper, mask);

        using var clone = mask.Clone();
        Cv2.FindContours(clone, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        int count = 0;
        foreach (var contour in contours)
        {
            if (Cv2.ContourArea(contour) >= _starMinArea)
                count++;
        }

        return count > MaxStars ? 0 : count;
    }

    private static IReadOnlyList<Rect> SegmentDigitSlots(Mat binary)
    {
        using var clone = binary.Clone();
        Cv2.FindContours(clone, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var slots = new List<Rect>();
        foreach (var contour in contours)
        {
            var rect = Cv2.BoundingRect(contour);
            if (rect.Width * rect.Height < MinGlyphArea)
                continue;
            slots.Add(rect);
        }

        slots.Sort((a, b) => a.X.CompareTo(b.X));
        return slots;
    }

    private static IReadOnlyDictionary<char, Mat> BuildTemplates()
    {
        var templates = new Dictionary<char, Mat>();
        foreach (var ch in "0123456789")
        {
            using var glyph = RenderGlyph(ch);
            using var binary = new Mat();
            Cv2.Threshold(glyph, binary, BinarizationThreshold, 255, ThresholdTypes.Binary);
            templates[ch] = CanonicalizeGlyph(binary, FindGlyphRect(binary));
        }
        return templates;
    }

    private static Mat RenderGlyph(char digit)
    {
        var canvas = new Mat(TemplateCanvasHeight, TemplateCanvasWidth, MatType.CV_8UC1);
        canvas.SetTo(Scalar.Black);
        var text = digit.ToString();
        var size = Cv2.GetTextSize(text, TemplateFontFace, TemplateFontScale, TemplateThickness, out _);
        var org = new Point(
            (TemplateCanvasWidth - size.Width) / 2,
            (TemplateCanvasHeight + size.Height) / 2);
        Cv2.PutText(canvas, text, org, TemplateFontFace, TemplateFontScale, Scalar.White, TemplateThickness, LineTypes.AntiAlias);
        return canvas;
    }

    private static Rect FindGlyphRect(Mat binary)
    {
        using var points = new Mat();
        Cv2.FindNonZero(binary, points);
        int pointCount = points.Rows;
        if (pointCount == 0)
            return new Rect();

        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        for (int i = 0; i < pointCount; i++)
        {
            var point = points.At<Point>(i, 0);
            if (point.X < minX) minX = point.X;
            if (point.Y < minY) minY = point.Y;
            if (point.X > maxX) maxX = point.X;
            if (point.Y > maxY) maxY = point.Y;
        }

        return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private static Mat CanonicalizeGlyph(Mat binary, Rect glyphRect)
    {
        var result = new Mat(CanonicalGlyphHeight, CanonicalGlyphWidth, MatType.CV_8UC1);
        result.SetTo(Scalar.Black);

        const int pad = 2;
        int x = Math.Max(0, glyphRect.X - pad);
        int y = Math.Max(0, glyphRect.Y - pad);
        int right = Math.Min(binary.Width, glyphRect.X + glyphRect.Width + pad);
        int bottom = Math.Min(binary.Height, glyphRect.Y + glyphRect.Height + pad);

        if (right <= x || bottom <= y)
            return result;

        using var crop = new Mat(binary, new Rect(x, y, right - x, bottom - y));
        Cv2.Resize(crop, result, new Size(CanonicalGlyphWidth, CanonicalGlyphHeight), 0, 0, InterpolationFlags.Linear);
        return result;
    }

    private static (char Digit, double Confidence) ClassifyGlyph(
        Mat canonical,
        IReadOnlyDictionary<char, Mat> templates)
    {
        using var result = new Mat();
        char bestDigit = '\0';
        double bestConfidence = double.MinValue;

        foreach (var pair in templates)
        {
            Cv2.MatchTemplate(canonical, pair.Value, result, TemplateMatchModes.CCoeffNormed);
            double confidence = result.At<float>(0, 0);
            if (confidence > bestConfidence)
            {
                bestConfidence = confidence;
                bestDigit = pair.Key;
            }
        }

        return (bestDigit, bestConfidence);
    }
}