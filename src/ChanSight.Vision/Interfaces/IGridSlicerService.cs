using ChanSight.Vision.Models;
using OpenCvSharp;

namespace ChanSight.Vision.Interfaces;

public interface IGridSlicerService
{
    IReadOnlyList<BenchSlot> SliceBenchSlots(Mat frame);

    IReadOnlyList<BoardHexSlot> SliceBoardHexagons(Mat frame);

    Mat? CropHexCell(Mat frame, int row, int col);
}