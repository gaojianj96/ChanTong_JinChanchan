namespace ChanSight.Vision.Interfaces;

using OpenCvSharp;

public interface IDetection
{
    Rect Box { get; }
    double Confidence { get; }
    int ClassId { get; }
}