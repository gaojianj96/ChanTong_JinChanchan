using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using FluentAssertions;
using OpenCvSharp;

namespace ChanSight.Tests.Vision;

public sealed class CellChangeDetectorTests
{
    private const int FrameWidth = 1920;
    private const int FrameHeight = 1080;

    // Drawn block covers less than the detector's 0.8-pitch region (0.7 pitch),
    // keeping it clearly inside one cell while never reaching a neighbour.
    private const double BlockFraction = 0.7;

    private readonly CellChangeDetector _detector = new();

    [Fact]
    public void DetectChangedCells_SameFrame_ReturnsEmpty()
    {
        using var frame = CreateSolidFrame(FrameWidth, FrameHeight);

        var changed = _detector.DetectChangedCells(frame, frame);

        changed.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(27)]
    [InlineData(28)]
    [InlineData(36)]
    [InlineData(37)]
    [InlineData(41)]
    public void DetectChangedCells_BrightBlockInSingleCell_ReturnsOnlyThatCell(int flatIndex)
    {
        using var previous = CreateSolidFrame(FrameWidth, FrameHeight);
        using var current = CreateSolidFrame(FrameWidth, FrameHeight);

        DrawBlockInCell(current, flatIndex, 255);

        var changed = _detector.DetectChangedCells(previous, current);

        changed.Should().ContainSingle().Which.Should().Be(flatIndex);
    }

    [Fact]
    public void DetectChangedCells_HigherThreshold_SuppressesChange()
    {
        using var previous = CreateSolidFrame(FrameWidth, FrameHeight);
        using var current = CreateSolidFrame(FrameWidth, FrameHeight);
        DrawBlockInCell(current, 5, 255);

        var strict = new CellChangeDetector(threshold: 1000.0);

        strict.DetectChangedCells(previous, current).Should().BeEmpty();
    }

    [Fact]
    public void DetectChangedCells_LowerThreshold_IsMoreSensitive()
    {
        using var previous = CreateSolidFrame(FrameWidth, FrameHeight);
        using var current = CreateSolidFrame(FrameWidth, FrameHeight);
        DrawBlockInCell(current, 5, 5);

        // Dim patch (mean diff < 10) is invisible at the default threshold...
        _detector.DetectChangedCells(previous, current).Should().BeEmpty();

        // ...but a lowered threshold makes it detectable.
        var sensitive = new CellChangeDetector(threshold: 1.0);
        sensitive.DetectChangedCells(previous, current).Should().ContainSingle().Which.Should().Be(5);
    }

    [Fact]
    public void DetectChangedCells_DifferentSizes_ThrowsArgumentException()
    {
        using var a = new Mat(100, 100, MatType.CV_8UC3);
        using var b = new Mat(200, 100, MatType.CV_8UC3);

        var act = () => _detector.DetectChangedCells(a, b);

        act.Should().Throw<ArgumentException>();
    }

    private static Mat CreateSolidFrame(int width, int height)
    {
        var mat = new Mat(height, width, MatType.CV_8UC3);
        mat.SetTo(Scalar.Black);
        return mat;
    }

    private static void DrawBlockInCell(Mat frame, int flatIndex, byte grayValue)
    {
        var g = BoardGeometry.CreateCanonical();

        double centerX;
        double centerY;
        double blockWidth;
        double blockHeight;

        if (flatIndex < g.BoardCells.Count)
        {
            var cell = g.BoardCells[flatIndex];
            centerX = cell.X;
            centerY = cell.Y;
            blockWidth = g.ColPitchX * BlockFraction;
            blockHeight = g.RowPitchY * BlockFraction;
        }
        else if (flatIndex < g.BoardCells.Count + g.BenchCells.Count)
        {
            var cell = g.BenchCells[flatIndex - g.BoardCells.Count];
            centerX = cell.X;
            centerY = cell.Y;
            double pitch = g.BenchCells[1].X - g.BenchCells[0].X;
            blockWidth = pitch * BlockFraction;
            blockHeight = pitch * BlockFraction;
        }
        else
        {
            var cell = g.ShopCells[flatIndex - g.BoardCells.Count - g.BenchCells.Count];
            centerX = cell.X;
            centerY = cell.Y;
            double pitch = g.ShopCells[1].X - g.ShopCells[0].X;
            blockWidth = pitch * BlockFraction;
            blockHeight = pitch * BlockFraction;
        }

        int x0 = Math.Max(0, (int)Math.Round(centerX - blockWidth / 2.0));
        int y0 = Math.Max(0, (int)Math.Round(centerY - blockHeight / 2.0));
        int x1 = Math.Min(frame.Width, (int)Math.Round(centerX + blockWidth / 2.0));
        int y1 = Math.Min(frame.Height, (int)Math.Round(centerY + blockHeight / 2.0));

        if (x1 <= x0 || y1 <= y0)
            return;

        Cv2.Rectangle(frame, new Rect(x0, y0, x1 - x0, y1 - y0), new Scalar(grayValue, grayValue, grayValue), -1);
    }
}