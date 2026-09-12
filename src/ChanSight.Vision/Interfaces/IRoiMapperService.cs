using ChanSight.Vision.Models;
using OpenCvSharp;

namespace ChanSight.Vision.Interfaces;

public interface IRoiMapperService
{
    Rect MapToPhysical(Rect canonicalRect, int frameWidth, int frameHeight);

    Point2d MapPointToPhysical(Point2d canonicalPoint, int frameWidth, int frameHeight);

    Mat CropRoi(Mat frame, RoiRegionType regionType);

    IReadOnlyList<Mat> CropShopSlots(Mat frame);
}