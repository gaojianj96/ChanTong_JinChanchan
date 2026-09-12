using OpenCvSharp;

namespace ChanSight.Vision.Interfaces;

public interface IPerceptualHashService
{
    ulong ComputeDHash(Mat image);
    ulong ComputeAHash(Mat image);
    int CalculateHammingDistance(ulong hash1, ulong hash2);
}