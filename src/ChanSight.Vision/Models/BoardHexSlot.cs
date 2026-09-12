using OpenCvSharp;

namespace ChanSight.Vision.Models;

public sealed class BoardHexSlot : IDisposable
{
    public int Row { get; }
    public int Col { get; }
    public Point2d PhysicalCenter { get; }
    public Rect LocalCropRect { get; }
    public Mat? CellImage { get; private set; }
    private bool _disposed;

    public BoardHexSlot(int row, int col, Point2d physicalCenter, Rect localCropRect, Mat? cellImage = null)
    {
        Row = row;
        Col = col;
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