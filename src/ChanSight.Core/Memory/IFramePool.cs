using ChanSight.Core.Models;
using OpenCvSharp;

namespace ChanSight.Core.Memory;

public interface IFramePool
{
    CapturedFrame Rent(Mat image, DateTimeOffset timestamp, long sequenceNumber);

    void Return(CapturedFrame frame);
}
