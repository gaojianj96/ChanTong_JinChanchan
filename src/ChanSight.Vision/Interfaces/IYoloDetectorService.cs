using ChanSight.Vision.Models;
using OpenCvSharp;

namespace ChanSight.Vision.Interfaces;

public interface IYoloDetectorService
{
    IReadOnlyList<DetectedUnit> Detect(Mat frame, float confidenceThreshold = 0.25f, float iouThreshold = 0.45f);
}