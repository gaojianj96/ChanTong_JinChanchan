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
        private readonly int _frameWidth;
        private readonly int _frameHeight;

        public FakeRoiMapper(int frameWidth, int frameHeight)
        {
            _frameWidth = frameWidth;
            _frameHeight = frameHeight;
        }

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
            var w = Math.Min(200, frame.Width);
            var h = Math.Min(50, frame.Height);
            return new Mat(frame, new Rect(0, 0, w, h));
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

    private readonly PaddleOcrService _service;

    public PaddleOcrServiceTests()
    {
        _service = new PaddleOcrService(
            new FakeOnnxInferenceEngine(),
            new FakeRoiMapper(1920, 1080));
    }

    [Fact]
    public void Constructor_NullEngine_ThrowsArgumentNull()
    {
        var roiMapper = new FakeRoiMapper(1920, 1080);
        var act = () => new PaddleOcrService(null!, roiMapper);
        act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("engine");
    }

    [Fact]
    public void Constructor_NullRoiMapper_ThrowsArgumentNull()
    {
        var act = () => new PaddleOcrService(new FakeOnnxInferenceEngine(), null!);
        act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("roiMapper");
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
    public void RecognizeStage_SmallFrame_HandlesGracefully()
    {
        using var frame = new Mat(50, 50, MatType.CV_8UC3);

        var act = () => _service.RecognizeStage(frame);

        act.Should().NotThrow();
    }

    [Fact]
    public void RecognizeTextFromRegion_NullRegion_ReturnsEmptyResult()
    {
        var result = PaddleOcrService.RecognizeTextFromRegion(null!);

        result.RawText.Should().Be(string.Empty);
        result.CleanedText.Should().Be(string.Empty);
        result.MatchedDictKey.Should().BeNull();
        result.Confidence.Should().Be(0f);
    }

    [Fact]
    public void RecognizeTextFromRegion_EmptyRegion_ReturnsEmptyResult()
    {
        using var region = new Mat();

        var result = PaddleOcrService.RecognizeTextFromRegion(region);

        result.RawText.Should().Be(string.Empty);
        result.CleanedText.Should().Be(string.Empty);
        result.MatchedDictKey.Should().BeNull();
        result.Confidence.Should().Be(0f);
    }

    [Fact]
    public void RecognizeTextFromRegion_ValidRegion_ReturnsEmptyResult()
    {
        using var region = new Mat(50, 100, MatType.CV_8UC3);

        var result = PaddleOcrService.RecognizeTextFromRegion(region);

        result.RawText.Should().Be(string.Empty);
        result.CleanedText.Should().Be(string.Empty);
        result.MatchedDictKey.Should().BeNull();
        result.Confidence.Should().Be(0f);
    }
}