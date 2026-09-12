using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;
using OpenCvSharp;

namespace ChanSight.Vision.Services;

public sealed class YoloDetectorService : IYoloDetectorService
{
    private readonly IOnnxInferenceEngine _engine;
    private readonly IRoiMapperService _roiMapper;
    private readonly IGridSlicerService _gridSlicer;

    private const int InputSize = 640;
    private const int NumClasses = 3;

    public YoloDetectorService(
        IOnnxInferenceEngine engine,
        IRoiMapperService roiMapper,
        IGridSlicerService gridSlicer)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _roiMapper = roiMapper ?? throw new ArgumentNullException(nameof(roiMapper));
        _gridSlicer = gridSlicer ?? throw new ArgumentNullException(nameof(gridSlicer));
    }

    public IReadOnlyList<DetectedUnit> Detect(Mat frame, float confidenceThreshold = 0.25f, float iouThreshold = 0.45f)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (frame.Empty())
            return Array.Empty<DetectedUnit>();

        // Placeholder: In production, this would run the ONNX YOLO model.
        // For now, returns empty to allow compilation and testing of core algorithms.
        return Array.Empty<DetectedUnit>();
    }

    public static Mat Letterbox(Mat image, int targetSize, out float scale, out int padX, out int padY)
    {
        ArgumentNullException.ThrowIfNull(image);

        scale = Math.Min((float)targetSize / image.Width, (float)targetSize / image.Height);
        int newWidth = (int)Math.Round(image.Width * scale);
        int newHeight = (int)Math.Round(image.Height * scale);

        padX = (targetSize - newWidth) / 2;
        padY = (targetSize - newHeight) / 2;

        using var resized = new Mat();
        Cv2.Resize(image, resized, new Size(newWidth, newHeight));

        var result = new Mat(targetSize, targetSize, image.Type());
        result.SetTo(Scalar.All(114));

        var roi = new Rect(padX, padY, newWidth, newHeight);
        using var roiMat = new Mat(result, roi);
        resized.CopyTo(roiMat);

        return result;
    }

    public static Rect RestoreBox(
        float cx, float cy, float w, float h,
        int letterboxSize, float scale, int padX, int padY,
        int originalWidth, int originalHeight)
    {
        float x1 = (cx - w / 2f - padX) / scale;
        float y1 = (cy - h / 2f - padY) / scale;
        float x2 = (cx + w / 2f - padX) / scale;
        float y2 = (cy + h / 2f - padY) / scale;

        x1 = Math.Clamp(x1, 0, originalWidth);
        y1 = Math.Clamp(y1, 0, originalHeight);
        x2 = Math.Clamp(x2, 0, originalWidth);
        y2 = Math.Clamp(y2, 0, originalHeight);

        int bx = (int)x1;
        int by = (int)y1;
        int bw = (int)(x2 - x1);
        int bh = (int)(y2 - y1);

        if (bw < 1) bw = 1;
        if (bh < 1) bh = 1;

        return new Rect(bx, by, bw, bh);
    }

    public static float ComputeIoU(Rect a, Rect b)
    {
        int x1 = Math.Max(a.X, b.X);
        int y1 = Math.Max(a.Y, b.Y);
        int x2 = Math.Min(a.X + a.Width, b.X + b.Width);
        int y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);

        int interW = Math.Max(0, x2 - x1);
        int interH = Math.Max(0, y2 - y1);
        float intersection = (float)interW * interH;

        float areaA = (float)a.Width * a.Height;
        float areaB = (float)b.Width * b.Height;
        float union = areaA + areaB - intersection;

        if (union <= 0) return 0;

        return intersection / union;
    }

    public static IReadOnlyList<int> NonMaximumSuppression(
        IReadOnlyList<Rect> boxes,
        IReadOnlyList<float> scores,
        float iouThreshold)
    {
        if (boxes.Count == 0) return Array.Empty<int>();
        if (boxes.Count != scores.Count)
            throw new ArgumentException("Boxes and scores must have the same length");

        var indices = Enumerable.Range(0, boxes.Count)
            .OrderByDescending(i => scores[i])
            .ToList();

        var keep = new List<int>();

        while (indices.Count > 0)
        {
            int current = indices[0];
            keep.Add(current);
            indices.RemoveAt(0);

            indices.RemoveAll(i =>
                ComputeIoU(boxes[current], boxes[i]) >= iouThreshold);
        }

        return keep;
    }

    public static Mat NormalizeForInference(Mat image)
    {
        ArgumentNullException.ThrowIfNull(image);

        var result = new Mat();
        image.ConvertTo(result, MatType.CV_32FC3, 1.0 / 255.0);
        return result;
    }
}