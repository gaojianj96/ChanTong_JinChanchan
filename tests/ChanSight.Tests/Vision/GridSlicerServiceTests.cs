using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using FluentAssertions;
using OpenCvSharp;

namespace ChanSight.Tests.Vision;

public sealed class GridSlicerServiceTests
{
    private readonly RoiMapperService _roiMapper = new();
    private readonly GridSlicerService _service;

    public GridSlicerServiceTests()
    {
        _service = new GridSlicerService(_roiMapper);
    }

    [Fact]
    public void SliceBenchSlots_1920x1080_ReturnsNineSlots()
    {
        using var frame = new Mat(1080, 1920, MatType.CV_8UC3);
        frame.SetTo(Scalar.Blue);

        var slots = _service.SliceBenchSlots(frame);

        slots.Should().HaveCount(9);

        for (int i = 0; i < 9; i++)
        {
            slots[i].Index.Should().Be(i);
            slots[i].CellImage.Should().NotBeNull();
            slots[i].CellImage!.Width.Should().BeGreaterThan(0);
            slots[i].LocalCropRect.Width.Should().BeGreaterThan(0);
        }

        CleanupBenchSlots(slots);
    }

    [Fact]
    public void SliceBenchSlots_2560x1440_ReturnsNineSlots()
    {
        using var frame = new Mat(1440, 2560, MatType.CV_8UC3);
        frame.SetTo(Scalar.Green);

        var slots = _service.SliceBenchSlots(frame);

        slots.Should().HaveCount(9);
        foreach (var slot in slots)
        {
            slot.CellImage.Should().NotBeNull();
            slot.CellImage!.Width.Should().BeGreaterThan(0);
        }

        CleanupBenchSlots(slots);
    }

    [Fact]
    public void SliceBenchSlots_1280x720_ReturnsNineSlots()
    {
        using var frame = new Mat(720, 1280, MatType.CV_8UC3);
        frame.SetTo(Scalar.Red);

        var slots = _service.SliceBenchSlots(frame);

        slots.Should().HaveCount(9);
        foreach (var slot in slots)
        {
            slot.CellImage.Should().NotBeNull();
        }

        CleanupBenchSlots(slots);
    }

    [Fact]
    public void SliceBenchSlots_TinyFrame_HandlesGracefully()
    {
        using var frame = new Mat(50, 50, MatType.CV_8UC3);

        var slots = _service.SliceBenchSlots(frame);

        slots.Should().HaveCount(9);
        foreach (var slot in slots)
        {
            slot.Index.Should().BeGreaterThanOrEqualTo(0);
        }

        CleanupBenchSlots(slots);
    }

    [Fact]
    public void SliceBenchSlots_CentersAreMonotonicallyIncreasingX()
    {
        using var frame = new Mat(1080, 1920, MatType.CV_8UC3);

        var slots = _service.SliceBenchSlots(frame);

        for (int i = 1; i < slots.Count; i++)
        {
            slots[i].PhysicalCenter.X.Should().BeGreaterThan(slots[i - 1].PhysicalCenter.X);
        }

        CleanupBenchSlots(slots);
    }

    [Fact]
    public void SliceBenchSlots_Non16x9_ReturnsValidResults()
    {
        using var frame = new Mat(600, 2000, MatType.CV_8UC3);

        var slots = _service.SliceBenchSlots(frame);

        slots.Should().HaveCount(9);
        foreach (var slot in slots)
        {
            slot.CellImage.Should().NotBeNull();
        }

        CleanupBenchSlots(slots);
    }

    [Fact]
    public void SliceBoardHexagons_1920x1080_Returns28Slots()
    {
        using var frame = new Mat(1080, 1920, MatType.CV_8UC3);
        frame.SetTo(Scalar.Blue);

        var slots = _service.SliceBoardHexagons(frame);

        slots.Should().HaveCount(28);

        for (int i = 0; i < 28; i++)
        {
            slots[i].CellImage.Should().NotBeNull();
            slots[i].CellImage!.Width.Should().BeGreaterThan(0);
        }

        var rows = slots.Select(s => s.Row).Distinct().OrderBy(r => r).ToList();
        var cols = slots.Select(s => s.Col).Distinct().OrderBy(c => c).ToList();

        rows.Should().Equal(new[] { 0, 1, 2, 3 });
        cols.Should().Equal(new[] { 0, 1, 2, 3, 4, 5, 6 });

        CleanupBoardHexSlots(slots);
    }

    [Fact]
    public void SliceBoardHexagons_2560x1440_Returns28Slots()
    {
        using var frame = new Mat(1440, 2560, MatType.CV_8UC3);
        frame.SetTo(Scalar.Green);

        var slots = _service.SliceBoardHexagons(frame);

        slots.Should().HaveCount(28);
        foreach (var slot in slots)
        {
            slot.CellImage.Should().NotBeNull();
            slot.CellImage!.Width.Should().BeGreaterThan(0);
        }

        CleanupBoardHexSlots(slots);
    }

    [Fact]
    public void SliceBoardHexagons_1280x720_Returns28Slots()
    {
        using var frame = new Mat(720, 1280, MatType.CV_8UC3);
        frame.SetTo(Scalar.Red);

        var slots = _service.SliceBoardHexagons(frame);

        slots.Should().HaveCount(28);
        foreach (var slot in slots)
        {
            slot.CellImage.Should().NotBeNull();
        }

        CleanupBoardHexSlots(slots);
    }

    [Fact]
    public void SliceBoardHexagons_StaggeredRowsHaveOffsets()
    {
        using var frame = new Mat(1080, 1920, MatType.CV_8UC3);

        var slots = _service.SliceBoardHexagons(frame);

        var evenRowFirst = slots.First(s => s.Row == 0 && s.Col == 0);
        var oddRowFirst = slots.First(s => s.Row == 1 && s.Col == 0);

        oddRowFirst.PhysicalCenter.X.Should().BeGreaterThan(evenRowFirst.PhysicalCenter.X,
            "odd rows should be horizontally offset");

        CleanupBoardHexSlots(slots);
    }

    [Fact]
    public void SliceBoardHexagons_TinyFrame_HandlesGracefully()
    {
        using var frame = new Mat(50, 50, MatType.CV_8UC3);

        var slots = _service.SliceBoardHexagons(frame);

        slots.Should().HaveCount(28);
        foreach (var slot in slots)
        {
            slot.Row.Should().BeGreaterThanOrEqualTo(0);
            slot.Col.Should().BeGreaterThanOrEqualTo(0);
        }

        CleanupBoardHexSlots(slots);
    }

    [Fact]
    public void SliceBoardHexagons_WithinRowCentersIncreaseWithCol()
    {
        using var frame = new Mat(1080, 1920, MatType.CV_8UC3);

        var slots = _service.SliceBoardHexagons(frame);

        for (int row = 0; row < 4; row++)
        {
            var rowSlots = slots.Where(s => s.Row == row).OrderBy(s => s.Col).ToList();
            for (int i = 1; i < rowSlots.Count; i++)
            {
                rowSlots[i].PhysicalCenter.X.Should().BeGreaterThan(rowSlots[i - 1].PhysicalCenter.X,
                    $"centers should increase with col in row {row}");
            }
        }

        CleanupBoardHexSlots(slots);
    }

    [Fact]
    public void SliceBoardHexagons_BetweenRowsCentersIncreaseWithRow()
    {
        using var frame = new Mat(1080, 1920, MatType.CV_8UC3);

        var slots = _service.SliceBoardHexagons(frame);

        for (int col = 0; col < 7; col++)
        {
            var colSlots = slots.Where(s => s.Col == col).OrderBy(s => s.Row).ToList();
            for (int i = 1; i < colSlots.Count; i++)
            {
                colSlots[i].PhysicalCenter.Y.Should().BeGreaterThan(colSlots[i - 1].PhysicalCenter.Y,
                    $"centers should increase with row in col {col}");
            }
        }

        CleanupBoardHexSlots(slots);
    }

    [Fact]
    public void CropHexCell_1920x1080_ReturnsValidMat()
    {
        using var frame = new Mat(1080, 1920, MatType.CV_8UC3);
        frame.SetTo(Scalar.Red);

        using var cell = _service.CropHexCell(frame, 1, 3);

        cell.Should().NotBeNull();
        cell!.Width.Should().BeGreaterThan(0);
        cell.Height.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CropHexCell_AllRowsAndCols_ReturnValidMat()
    {
        using var frame = new Mat(1080, 1920, MatType.CV_8UC3);

        for (int row = 0; row < 4; row++)
        {
            for (int col = 0; col < 7; col++)
            {
                using var cell = _service.CropHexCell(frame, row, col);
                cell.Should().NotBeNull();
                cell!.Width.Should().BeGreaterThan(0);
            }
        }
    }

    [Fact]
    public void CropHexCell_OutOfRangeRow_ThrowsArgumentOutOfRange()
    {
        using var frame = new Mat(1080, 1920, MatType.CV_8UC3);

        var act = () => _service.CropHexCell(frame, 4, 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CropHexCell_OutOfRangeCol_ThrowsArgumentOutOfRange()
    {
        using var frame = new Mat(1080, 1920, MatType.CV_8UC3);

        var act = () => _service.CropHexCell(frame, 0, 7);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CropHexCell_NegativeRow_ThrowsArgumentOutOfRange()
    {
        using var frame = new Mat(1080, 1920, MatType.CV_8UC3);

        var act = () => _service.CropHexCell(frame, -1, 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SliceBoardHexagons_SameResolution_ReturnsConsistentPositions()
    {
        using var frame1 = new Mat(1080, 1920, MatType.CV_8UC3);
        using var frame2 = new Mat(1080, 1920, MatType.CV_8UC3);

        var slots1 = _service.SliceBoardHexagons(frame1);
        var slots2 = _service.SliceBoardHexagons(frame2);

        for (int i = 0; i < 28; i++)
        {
            slots1[i].PhysicalCenter.X.Should().BeApproximately(slots2[i].PhysicalCenter.X, 1.0);
            slots1[i].PhysicalCenter.Y.Should().BeApproximately(slots2[i].PhysicalCenter.Y, 1.0);
        }

        CleanupBoardHexSlots(slots1);
        CleanupBoardHexSlots(slots2);
    }

    [Fact]
    public void SliceBoardHexagons_CellImagesAreRoISubmatrices()
    {
        using var frame = new Mat(1080, 1920, MatType.CV_8UC3);
        frame.SetTo(Scalar.All(128));

        var slots = _service.SliceBoardHexagons(frame);

        foreach (var slot in slots)
        {
            slot.CellImage.Should().NotBeNull();
            slot.LocalCropRect.X.Should().BeGreaterThanOrEqualTo(0);
            slot.LocalCropRect.Y.Should().BeGreaterThanOrEqualTo(0);
        }

        CleanupBoardHexSlots(slots);
    }

    [Fact]
    public void SliceBenchSlots_CellImagesAreValidSubmatrices()
    {
        using var frame = new Mat(1080, 1920, MatType.CV_8UC3);

        var slots = _service.SliceBenchSlots(frame);

        foreach (var slot in slots)
        {
            slot.CellImage.Should().NotBeNull();
            slot.LocalCropRect.X.Should().BeGreaterThanOrEqualTo(0);
            slot.LocalCropRect.Y.Should().BeGreaterThanOrEqualTo(0);
            slot.PhysicalCenter.X.Should().BeGreaterThan(0);
            slot.PhysicalCenter.Y.Should().BeGreaterThan(0);
        }

        CleanupBenchSlots(slots);
    }

    [Fact]
    public void GridSlicerService_Constructor_NullRoiMapper_ThrowsArgumentNull()
    {
        var act = () => new GridSlicerService(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    private static void CleanupBenchSlots(IReadOnlyList<BenchSlot> slots)
    {
        foreach (var slot in slots)
        {
            slot.Dispose();
        }
    }

    private static void CleanupBoardHexSlots(IReadOnlyList<BoardHexSlot> slots)
    {
        foreach (var slot in slots)
        {
            slot.Dispose();
        }
    }
}