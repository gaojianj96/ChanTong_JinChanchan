using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using FluentAssertions;
using OpenCvSharp;

namespace ChanSight.Tests.Vision;

public sealed class YoloDetectorServiceTests
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
            string inputName,
            ReadOnlySpan<float> inputData,
            long[] inputShape,
            IReadOnlyDictionary<string, long[]> outputShapes)
        {
            return new OrtValueTensor("output", new float[] { 0f }, new long[] { 1 });
        }

        public void Dispose() { }
    }

    private readonly IOnnxInferenceEngine _engine = new FakeOnnxInferenceEngine();
    private readonly IRoiMapperService _roiMapper = new RoiMapperService();
    private readonly IGridSlicerService _gridSlicer;
    private readonly YoloDetectorService _service;

    public YoloDetectorServiceTests()
    {
        _gridSlicer = new GridSlicerService(_roiMapper);
        _service = new YoloDetectorService(_engine, _roiMapper, _gridSlicer);
    }

    [Fact]
    public void Constructor_NullEngine_ThrowsArgumentNull()
    {
        var act = () => new YoloDetectorService(null!, _roiMapper, _gridSlicer);
        act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("engine");
    }

    [Fact]
    public void Constructor_NullRoiMapper_ThrowsArgumentNull()
    {
        var act = () => new YoloDetectorService(_engine, null!, _gridSlicer);
        act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("roiMapper");
    }

    [Fact]
    public void Constructor_NullGridSlicer_ThrowsArgumentNull()
    {
        var act = () => new YoloDetectorService(_engine, _roiMapper, null!);
        act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("gridSlicer");
    }

    [Fact]
    public void Detect_NullFrame_ThrowsArgumentNull()
    {
        var act = () => _service.Detect(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Detect_EmptyFrame_ReturnsEmpty()
    {
        using var frame = new Mat();

        var result = _service.Detect(frame);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Detect_ValidFrame_ReturnsEmpty()
    {
        using var frame = new Mat(640, 640, MatType.CV_8UC3);

        var result = _service.Detect(frame);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Detect_ValidFrameWithCustomThresholds_ReturnsEmpty()
    {
        using var frame = new Mat(640, 640, MatType.CV_8UC3);

        var result = _service.Detect(frame, 0.5f, 0.6f);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Letterbox_NullImage_ThrowsArgumentNull()
    {
        var act = () => YoloDetectorService.Letterbox(null!, 640, out _, out _, out _);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Letterbox_SquareImage_ReturnsSquareResult()
    {
        using var image = new Mat(640, 640, MatType.CV_8UC3);
        image.SetTo(Scalar.Red);

        var result = YoloDetectorService.Letterbox(image, 640, out var scale, out var padX, out var padY);

        using (result)
        {
            result.Width.Should().Be(640);
            result.Height.Should().Be(640);
            scale.Should().Be(1.0f);
            padX.Should().Be(0);
            padY.Should().Be(0);
        }
    }

    [Fact]
    public void Letterbox_WideImage_ReturnsPaddedSquare()
    {
        using var image = new Mat(1080, 1920, MatType.CV_8UC3);
        image.SetTo(Scalar.Blue);

        var result = YoloDetectorService.Letterbox(image, 640, out var scale, out var padX, out var padY);

        using (result)
        {
            result.Width.Should().Be(640);
            result.Height.Should().Be(640);
            scale.Should().Be(640f / 1920f);
            padX.Should().Be(0);
            padY.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public void Letterbox_TallImage_ReturnsPaddedSquare()
    {
        using var image = new Mat(1920, 1080, MatType.CV_8UC3);
        image.SetTo(Scalar.Green);

        var result = YoloDetectorService.Letterbox(image, 640, out var scale, out var padX, out var padY);

        using (result)
        {
            result.Width.Should().Be(640);
            result.Height.Should().Be(640);
            scale.Should().Be(640f / 1920f);
            padX.Should().BeGreaterThan(0);
            padY.Should().Be(0);
        }
    }

    [Fact]
    public void Letterbox_SmallerImage_ScalesUp()
    {
        using var image = new Mat(100, 200, MatType.CV_8UC3);

        var result = YoloDetectorService.Letterbox(image, 640, out var scale, out var _, out var _);

        using (result)
        {
            result.Width.Should().Be(640);
            result.Height.Should().Be(640);
            scale.Should().BeGreaterThan(1.0f);
        }
    }

    [Fact]
    public void Letterbox_LargerImage_ScalesDown()
    {
        using var image = new Mat(2160, 3840, MatType.CV_8UC3);

        var result = YoloDetectorService.Letterbox(image, 640, out var scale, out var _, out var _);

        using (result)
        {
            result.Width.Should().Be(640);
            result.Height.Should().Be(640);
            scale.Should().BeLessThan(1.0f);
        }
    }

    [Fact]
    public void Letterbox_CustomTargetSize_ReturnsCorrectSize()
    {
        using var image = new Mat(100, 200, MatType.CV_8UC3);

        var result = YoloDetectorService.Letterbox(image, 320, out _, out _, out _);

        using (result)
        {
            result.Width.Should().Be(320);
            result.Height.Should().Be(320);
        }
    }

    [Fact]
    public void RestoreBox_IdentityMapping_ReturnsOriginalCoords()
    {
        var result = YoloDetectorService.RestoreBox(
            320, 320, 200, 100, 640, 1.0f, 0, 0, 640, 640);

        result.X.Should().Be(220);
        result.Y.Should().Be(270);
        result.Width.Should().Be(200);
        result.Height.Should().Be(100);
    }

    [Fact]
    public void RestoreBox_WithScale_ScalesCorrectly()
    {
        var result = YoloDetectorService.RestoreBox(
            320, 320, 200, 100, 640, 0.5f, 80, 40, 1280, 1280);

        result.X.Should().BeGreaterThan(0);
        result.Y.Should().BeGreaterThan(0);
        result.Width.Should().BeGreaterThan(0);
        result.Height.Should().BeGreaterThan(0);
    }

    [Fact]
    public void RestoreBox_ClampsToBounds()
    {
        var result = YoloDetectorService.RestoreBox(
            -100, -100, 200, 100, 640, 1.0f, 0, 0, 200, 200);

        result.X.Should().Be(0);
        result.Y.Should().Be(0);
        result.Right.Should().BeLessThanOrEqualTo(200);
        result.Bottom.Should().BeLessThanOrEqualTo(200);
    }

    [Fact]
    public void RestoreBox_MinimumSizeIsOne()
    {
        var result = YoloDetectorService.RestoreBox(
            0, 0, 0, 0, 640, 1.0f, 0, 0, 640, 640);

        result.Width.Should().Be(1);
        result.Height.Should().Be(1);
    }

    [Fact]
    public void ComputeIoU_PerfectOverlap_ReturnsOne()
    {
        var a = new Rect(0, 0, 100, 100);
        var b = new Rect(0, 0, 100, 100);

        var iou = YoloDetectorService.ComputeIoU(a, b);

        iou.Should().Be(1.0f);
    }

    [Fact]
    public void ComputeIoU_NoOverlap_ReturnsZero()
    {
        var a = new Rect(0, 0, 100, 100);
        var b = new Rect(200, 200, 100, 100);

        var iou = YoloDetectorService.ComputeIoU(a, b);

        iou.Should().Be(0.0f);
    }

    [Fact]
    public void ComputeIoU_PartialOverlap_ReturnsBetweenZeroAndOne()
    {
        var a = new Rect(0, 0, 100, 100);
        var b = new Rect(50, 50, 100, 100);

        var iou = YoloDetectorService.ComputeIoU(a, b);

        iou.Should().BeApproximately(50 * 50f / (10000 + 10000 - 2500), 0.01f);
    }

    [Fact]
    public void ComputeIoU_ZeroAreaRect_ReturnsZero()
    {
        var a = new Rect(0, 0, 0, 0);
        var b = new Rect(0, 0, 0, 0);

        var iou = YoloDetectorService.ComputeIoU(a, b);

        iou.Should().Be(0.0f);
    }

    [Fact]
    public void ComputeIoU_ContainedBox_GivesCorrectIoU()
    {
        var a = new Rect(0, 0, 100, 100);
        var b = new Rect(25, 25, 50, 50);

        var iou = YoloDetectorService.ComputeIoU(a, b);

        iou.Should().Be(2500f / 10000f);
    }

    [Fact]
    public void NonMaximumSuppression_EmptyBoxes_ReturnsEmpty()
    {
        var result = YoloDetectorService.NonMaximumSuppression(
            Array.Empty<Rect>(), Array.Empty<float>(), 0.5f);

        result.Should().BeEmpty();
    }

    [Fact]
    public void NonMaximumSuppression_MismatchedLengths_ThrowsArgumentException()
    {
        var act = () => YoloDetectorService.NonMaximumSuppression(
            new Rect[] { new(0, 0, 10, 10) },
            new float[] { 1f, 2f },
            0.5f);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NonMaximumSuppression_SingleBox_ReturnsIt()
    {
        var boxes = new Rect[] { new(0, 0, 100, 100) };
        var scores = new float[] { 0.9f };

        var result = YoloDetectorService.NonMaximumSuppression(boxes, scores, 0.5f);

        result.Should().Equal(new[] { 0 });
    }

    [Fact]
    public void NonMaximumSuppression_TwoOverlappingBoxes_KeepsHighestScore()
    {
        var boxes = new Rect[]
        {
            new(0, 0, 100, 100),
            new(10, 10, 100, 100),
        };
        var scores = new float[] { 0.6f, 0.9f };

        var result = YoloDetectorService.NonMaximumSuppression(boxes, scores, 0.5f);

        result.Should().Equal(new[] { 1 });
    }

    [Fact]
    public void NonMaximumSuppression_TwoNonOverlappingBoxes_KeepsBoth()
    {
        var boxes = new Rect[]
        {
            new(0, 0, 50, 50),
            new(200, 200, 50, 50),
        };
        var scores = new float[] { 0.8f, 0.7f };

        var result = YoloDetectorService.NonMaximumSuppression(boxes, scores, 0.5f);

        result.Should().BeEquivalentTo(new[] { 0, 1 });
    }

    [Fact]
    public void NonMaximumSuppression_ReturnsSortedByScore()
    {
        var boxes = new Rect[]
        {
            new(0, 0, 100, 100),
            new(10, 10, 90, 90),
            new(200, 200, 100, 100),
        };
        var scores = new float[] { 0.3f, 0.9f, 0.6f };

        var result = YoloDetectorService.NonMaximumSuppression(boxes, scores, 0.5f);

        result.Should().ContainInOrder(1, 2);
        result.Should().NotContain(0);
    }

    [Fact]
    public void NonMaximumSuppression_HighIouThreshold_KeepsMore()
    {
        var boxes = new Rect[]
        {
            new(0, 0, 100, 100),
            new(5, 5, 90, 90),
            new(10, 10, 80, 80),
        };
        var scores = new float[] { 0.5f, 0.8f, 0.9f };

        var result = YoloDetectorService.NonMaximumSuppression(boxes, scores, 0.95f);

        result.Count.Should().BeGreaterThan(1);
    }

    [Fact]
    public void NonMaximumSuppression_LowIouThreshold_KeepsFewer()
    {
        var boxes = new Rect[]
        {
            new(0, 0, 100, 100),
            new(5, 5, 90, 90),
            new(10, 10, 80, 80),
        };
        var scores = new float[] { 0.5f, 0.8f, 0.9f };

        var result = YoloDetectorService.NonMaximumSuppression(boxes, scores, 0.1f);

        result.Should().Equal(new[] { 2 });
    }

    [Fact]
    public void NormalizeForInference_NullImage_ThrowsArgumentNull()
    {
        var act = () => YoloDetectorService.NormalizeForInference(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void NormalizeForInference_ReturnsFloat32Mat()
    {
        using var image = new Mat(640, 640, MatType.CV_8UC3);

        var result = YoloDetectorService.NormalizeForInference(image);

        using (result)
        {
            result.Type().Should().Be(MatType.CV_32FC3);
            result.Width.Should().Be(640);
            result.Height.Should().Be(640);
        }
    }

    [Fact]
    public void NormalizeForInference_ValuesAreInZeroToOneRange()
    {
        using var image = new Mat(1, 1, MatType.CV_8UC3);
        image.SetTo(new Scalar(128, 64, 255));

        var result = YoloDetectorService.NormalizeForInference(image);

        using (result)
        {
            var vec = result.At<Vec3f>(0, 0);
            vec.Item0.Should().BeApproximately(128f / 255f, 0.01f);
            vec.Item1.Should().BeApproximately(64f / 255f, 0.01f);
            vec.Item2.Should().BeApproximately(255f / 255f, 0.01f);
        }
    }

    [Fact]
    public void NormalizeForInference_PreservesImageDimensions()
    {
        using var image = new Mat(480, 720, MatType.CV_8UC3);

        var result = YoloDetectorService.NormalizeForInference(image);

        using (result)
        {
            result.Width.Should().Be(720);
            result.Height.Should().Be(480);
        }
    }
}