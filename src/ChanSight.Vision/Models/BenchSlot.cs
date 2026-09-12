using OpenCvSharp;

namespace ChanSight.Vision.Models;

public sealed class BenchSlot : IDisposable
{
    public int Index { get; }
    public Point2d PhysicalCenter { get; }
    public Rect LocalCropRect { get; }
    public Mat? CellImage { get; private set; }
    private bool _disposed;

    public BenchSlot(int index, Point2d physicalCenter, Rect localCropRect, Mat? cellImage = null)
    {
        if (index < 0 || index > 8) throw new ArgumentOutOfRangeException(nameof(index));

        Index = index;
        PhysicalCenter = physicalCenter;
        LocalCropRect = localCropRect;
        CellImage = cellImage;
    }

    public void SetCellImage(Mat image)
    {
        CellImage?.Dispose();
        CellImage = image;
    }

    public void Dispose()
    {
        if (_disposed) return;
        CellImage?.Dispose();
        CellImage = null;
        _disposed = true;
    }
}