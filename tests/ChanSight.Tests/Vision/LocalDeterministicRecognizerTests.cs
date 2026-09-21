using ChanSight.Vision.Services;
using FluentAssertions;
using OpenCvSharp;

namespace ChanSight.Tests.Vision;

public sealed class LocalDeterministicRecognizerTests
{
    private readonly LocalDeterministicRecognizer _recognizer = new();

    [Fact]
    public void RecognizeDigits_TwoDigitNumber_ReturnsExpectedString()
    {
        using var roi = RenderText("42");

        var result = _recognizer.RecognizeDigits(roi);

        result.Should().Be("42");
    }

    [Fact]
    public void RecognizeDigits_SingleDigitNumber_ReturnsExpectedString()
    {
        using var roi = RenderText("7");

        var result = _recognizer.RecognizeDigits(roi);

        result.Should().Be("7");
    }

    [Fact]
    public void RecognizeDigits_NoiseImage_ReturnsNull()
    {
        using var roi = CreateNoiseImage();

        var result = _recognizer.RecognizeDigits(roi);

        result.Should().BeNull();
    }

    [Fact]
    public void RecognizeDigitsWithConfidence_RenderedDigits_ReportsConfidence()
    {
        using var roi = RenderText("42");

        var (digits, confidence) = _recognizer.RecognizeDigitsWithConfidence(roi);

        digits.Should().Be("42");
        confidence.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public void RecognizeDigitsWithConfidence_NoDigits_ReturnsNullAndZero()
    {
        using var roi = CreateNoiseImage();

        var (digits, confidence) = _recognizer.RecognizeDigitsWithConfidence(roi);

        digits.Should().BeNull();
        confidence.Should().Be(0.0);
    }

    [Fact]
    public void CountStars_TwoYellowBlobs_ReturnsTwo()
    {
        using var roi = CreateBgrBlobImage(new Scalar(0, 255, 255), 2);

        var result = _recognizer.CountStars(roi);

        result.Should().Be(2);
    }

    [Fact]
    public void CountStars_NoYellowBlobs_ReturnsZero()
    {
        using var roi = CreateBgrBlobImage(new Scalar(0, 0, 255), 2);

        var result = _recognizer.CountStars(roi);

        result.Should().Be(0);
    }

    private static Mat RenderText(string text)
    {
        const int width = 256;
        const int height = 160;
        var canvas = new Mat(height, width, MatType.CV_8UC1);
        canvas.SetTo(Scalar.Black);
        var size = Cv2.GetTextSize(
            text,
            LocalDeterministicRecognizer.TemplateFontFace,
            LocalDeterministicRecognizer.TemplateFontScale,
            LocalDeterministicRecognizer.TemplateThickness,
            out _);
        var org = new Point((width - size.Width) / 2, (height + size.Height) / 2);
        Cv2.PutText(
            canvas,
            text,
            org,
            LocalDeterministicRecognizer.TemplateFontFace,
            LocalDeterministicRecognizer.TemplateFontScale,
            Scalar.White,
            LocalDeterministicRecognizer.TemplateThickness,
            LineTypes.AntiAlias);
        return canvas;
    }

    private static Mat CreateNoiseImage()
    {
        var canvas = new Mat(160, 256, MatType.CV_8UC1);
        Cv2.Randu(canvas, 0, 255);
        return canvas;
    }

    private static Mat CreateBgrBlobImage(Scalar color, int blobCount)
    {
        const int width = 160;
        const int height = 160;
        const int radius = 30;
        using var canvas = new Mat(height, width, MatType.CV_8UC3);
        canvas.SetTo(Scalar.Black);

        var centers = new[]
        {
            new Point(50, 50),
            new Point(110, 110),
            new Point(50, 110),
        };

        for (int i = 0; i < blobCount; i++)
        {
            Cv2.Circle(canvas, centers[i], radius, color, -1, LineTypes.AntiAlias);
        }

        return canvas.Clone();
    }
}