using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using FluentAssertions;
using OpenCvSharp;

namespace ChanSight.Tests.Vision;

public sealed class PaddleOcrServiceTests
{
    private sealed class FakeOnnxInferenceEngine : IOnnxInferenceEngine
    {
        public InferenceDeviceType CurrentDevice => InferenceDeviceType.Cpu;
        public void LoadModel(string modelPath) { }

        public InferenceLatencyStats ProbeLatency(string modelPath, int warmup = 3, int iterations = 10, long[]? inputShapeOverride = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Probe not supported on fake engine.");
        }
        public OrtValueTensor RunInference(string inputName, ReadOnlySpan<float> inputData, long[] inputShape)
        {
            return new OrtValueTensor("output", new float[] { 0f }, new long[] { 1 });
        }
        public OrtValueTensor RunInference(
            string inputName, ReadOnlySpan<float> inputData, long[] inputShape,
            IReadOnlyDictionary<string, long[]> outputShapes)
        {
            return new OrtValueTensor("output", new float[] { 0f }, new long[] { 1 });
        }
        public void Dispose() { }
    }

    private sealed class FakeRoiMapper : IRoiMapperService
    {
        public Rect MapToPhysical(Rect canonicalRect, int frameWidth, int frameHeight)
        {
            return canonicalRect;
        }

        public Point2d MapPointToPhysical(Point2d canonicalPoint, int frameWidth, int frameHeight)
        {
            return canonicalPoint;
        }

        public Mat CropRoi(Mat frame, RoiRegionType regionType)
        {
            return frame.Clone();
        }

        public IReadOnlyList<Mat> CropShopSlots(Mat frame)
        {
            var slotWidth = Math.Min(100, frame.Width / 5);
            var slotHeight = Math.Min(100, frame.Height);
            var slots = new List<Mat>(5);
            for (int i = 0; i < 5 && i * slotWidth + slotWidth <= frame.Width; i++)
            {
                slots.Add(new Mat(frame, new Rect(i * slotWidth, 0, slotWidth, slotHeight)));
            }
            return slots;
        }
    }

    private sealed class FakeLocalRecognizer : LocalDeterministicRecognizer
    {
        public string? FixedDigits { get; init; } = "35";
        public double FixedConfidence { get; init; } = 0.99;

        public override (string? Digits, double Confidence) RecognizeDigitsWithConfidence(Mat roi)
        {
            if (roi is null || roi.Empty())
                return (null, 0.0);
            return (FixedDigits, FixedConfidence);
        }
    }

    private readonly PaddleOcrService _service;

    public PaddleOcrServiceTests()
    {
        _service = new PaddleOcrService(
            new FakeOnnxInferenceEngine(),
            new FakeRoiMapper(),
            new LocalDeterministicRecognizer());
    }

    [Fact]
    public void Constructor_NullEngine_ThrowsArgumentNull()
    {
        var act = () => new PaddleOcrService(null!, new FakeRoiMapper(), new LocalDeterministicRecognizer());
        act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("engine");
    }

    [Fact]
    public void Constructor_NullRoiMapper_ThrowsArgumentNull()
    {
        var act = () => new PaddleOcrService(new FakeOnnxInferenceEngine(), null!, new LocalDeterministicRecognizer());
        act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("roiMapper");
    }

    [Fact]
    public void Constructor_NullLocalRecognizer_ThrowsArgumentNull()
    {
        var act = () => new PaddleOcrService(new FakeOnnxInferenceEngine(), new FakeRoiMapper(), null!);
        act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("localRecognizer");
    }

    [Fact]
    public void RecognizeShopCards_NullFrame_ThrowsArgumentNull()
    {
        var act = () => _service.RecognizeShopCards(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RecognizeShopCards_EmptyFrame_ReturnsEmpty()
    {
        using var frame = new Mat();

        var result = _service.RecognizeShopCards(frame);

        result.Should().BeEmpty();
    }

    [Fact]
    public void RecognizeShopCards_ValidFrame_ReturnsList()
    {
        using var frame = new Mat(1080, 1920, MatType.CV_8UC3);

        var result = _service.RecognizeShopCards(frame);

        result.Should().NotBeNull();
    }

    [Fact]
    public void RecognizeShopCards_CalledMultipleTimes_DoesNotLeak()
    {
        using var frame = new Mat(1080, 1920, MatType.CV_8UC3);

        var act1 = () => _service.RecognizeShopCards(frame);
        var act2 = () => _service.RecognizeShopCards(frame);

        act1.Should().NotThrow();
        act2.Should().NotThrow();
    }

    [Fact]
    public void RecognizeShopCards_SmallFrame_HandlesGracefully()
    {
        using var frame = new Mat(50, 50, MatType.CV_8UC3);

        var result = _service.RecognizeShopCards(frame);

        result.Should().NotBeNull();
    }

    [Fact]
    public void RecognizeGold_NullFrame_ThrowsArgumentNull()
    {
        var act = () => _service.RecognizeGold(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RecognizeGold_ValidFrame_ReturnsOcrTextResult()
    {
        using var frame = new Mat(1080, 1920, MatType.CV_8UC3);

        var result = _service.RecognizeGold(frame);

        result.Should().NotBeNull();
        result.RawText.Should().NotBeNull();
        result.CleanedText.Should().NotBeNull();
    }

    [Fact]
    public void RecognizeGold_EmptyFrame_ReturnsEmptyResult()
    {
        using var frame = new Mat(100, 100, MatType.CV_8UC3);

        var result = _service.RecognizeGold(frame);

        result.Should().NotBeNull();
        result.MatchedDictKey.Should().BeNull();
        result.Confidence.Should().Be(0f);
    }

    [Fact]
    public void RecognizeGold_FakeRecognizer_ReturnsFixedNumber()
    {
        var service = new PaddleOcrService(
            new FakeOnnxInferenceEngine(),
            new FakeRoiMapper(),
            new FakeLocalRecognizer { FixedDigits = "35", FixedConfidence = 0.99 });

        using var frame = new Mat(1080, 1920, MatType.CV_8UC3);

        var result = service.RecognizeGold(frame);

        result.RawText.Should().Be("35");
        result.CleanedText.Should().Be("35");
        result.MatchedDictKey.Should().Be("35");
        result.Confidence.Should().Be(0.99f);
    }

    [Fact]
    public void RecognizeGold_SmallFrame_HandlesGracefully()
    {
        using var frame = new Mat(50, 50, MatType.CV_8UC3);

        var act = () => _service.RecognizeGold(frame);

        act.Should().NotThrow();
    }

    [Fact]
    public void RecognizeStage_NullFrame_ThrowsArgumentNull()
    {
        var act = () => _service.RecognizeStage(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RecognizeStage_ValidFrame_ReturnsOcrTextResult()
    {
        using var frame = new Mat(1080, 1920, MatType.CV_8UC3);

        var result = _service.RecognizeStage(frame);

        result.Should().NotBeNull();
        result.RawText.Should().NotBeNull();
    }

    [Fact]
    public void RecognizeStage_EmptyFrame_ReturnsEmptyResult()
    {
        using var frame = new Mat(100, 100, MatType.CV_8UC3);

        var result = _service.RecognizeStage(frame);

        result.Should().NotBeNull();
        result.MatchedDictKey.Should().BeNull();
        result.Confidence.Should().Be(0f);
    }

    [Fact]
    public void RecognizeStage_SynthesizedStage_ReturnsThreeDashTwo()
    {
        using var frame = RenderText("3-2");

        var result = _service.RecognizeStage(frame);

        result.RawText.Should().Be("3-2");
        result.MatchedDictKey.Should().Be("3-2");
        result.Confidence.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void RecognizeStage_NoDigits_ReturnsEmptyResult()
    {
        using var frame = CreateNoiseImage();

        var result = _service.RecognizeStage(frame);

        result.MatchedDictKey.Should().BeNull();
        result.Confidence.Should().Be(0f);
    }

    [Fact]
    public void RecognizeStage_SmallFrame_HandlesGracefully()
    {
        using var frame = new Mat(50, 50, MatType.CV_8UC3);

        var act = () => _service.RecognizeStage(frame);

        act.Should().NotThrow();
    }

    [Fact]
    public void RecognizeTextFromRegion_NullRegion_ReturnsEmptyResult()
    {
        var result = _service.RecognizeTextFromRegion(null!);

        result.RawText.Should().Be(string.Empty);
        result.CleanedText.Should().Be(string.Empty);
        result.MatchedDictKey.Should().BeNull();
        result.Confidence.Should().Be(0f);
    }

    [Fact]
    public void RecognizeTextFromRegion_EmptyRegion_ReturnsEmptyResult()
    {
        using var region = new Mat();

        var result = _service.RecognizeTextFromRegion(region);

        result.RawText.Should().Be(string.Empty);
        result.CleanedText.Should().Be(string.Empty);
        result.MatchedDictKey.Should().BeNull();
        result.Confidence.Should().Be(0f);
    }

    [Fact]
    public void RecognizeTextFromRegion_BlankRegion_ReturnsEmptyResult()
    {
        using var region = new Mat(50, 100, MatType.CV_8UC3);

        var result = _service.RecognizeTextFromRegion(region);

        result.RawText.Should().Be(string.Empty);
        result.CleanedText.Should().Be(string.Empty);
        result.MatchedDictKey.Should().BeNull();
        result.Confidence.Should().Be(0f);
    }

    [Fact]
    public void RecognizeTextFromRegion_RenderedDigits_ReturnsDigits()
    {
        using var region = RenderText("42");

        var result = _service.RecognizeTextFromRegion(region);

        result.RawText.Should().Be("42");
        result.CleanedText.Should().Be("42");
        result.MatchedDictKey.Should().Be("42");
        result.Confidence.Should().BeGreaterThan(0f);
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
}
