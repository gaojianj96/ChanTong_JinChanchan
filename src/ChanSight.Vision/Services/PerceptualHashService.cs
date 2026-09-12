using System.Numerics;
using ChanSight.Vision.Interfaces;
using OpenCvSharp;

namespace ChanSight.Vision.Services;

public sealed class PerceptualHashService : IPerceptualHashService
{
    public ulong ComputeDHash(Mat image)
    {
        using var resized = new Mat();
        Cv2.Resize(image, resized, new Size(9, 8));
        using var gray = new Mat();
        Cv2.CvtColor(resized, gray, ColorConversionCodes.BGR2GRAY);

        ulong hash = 0;
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                byte current = gray.At<byte>(y, x);
                byte next = gray.At<byte>(y, x + 1);
                if (current > next)
                {
                    int bitIndex = y * 8 + x;
                    hash |= 1UL << bitIndex;
                }
            }
        }

        return hash;
    }

    public ulong ComputeAHash(Mat image)
    {
        using var resized = new Mat();
        Cv2.Resize(image, resized, new Size(8, 8));
        using var gray = new Mat();
        Cv2.CvtColor(resized, gray, ColorConversionCodes.BGR2GRAY);

        double mean = Cv2.Mean(gray).Val0;

        ulong hash = 0;
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                if (gray.At<byte>(y, x) > mean)
                {
                    int bitIndex = y * 8 + x;
                    hash |= 1UL << bitIndex;
                }
            }
        }

        return hash;
    }

    public int CalculateHammingDistance(ulong hash1, ulong hash2)
    {
        return BitOperations.PopCount(hash1 ^ hash2);
    }
}