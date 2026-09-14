namespace ChanSight.Vision.Interfaces;

using ChanSight.Vision.Models;
using OpenCvSharp;

public interface IAnchorCalibrator
{
    AnchorCalibrationResult Calibrate(Mat frame, AnchorCalibrationOptions? options = null, CancellationToken cancellationToken = default);
}