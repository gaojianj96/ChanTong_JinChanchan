using ChanSight.Vision.Models;
using FluentAssertions;
using System.Text.Json;

namespace ChanSight.Tests.Vision;

public sealed class BoardGeometryTests
{
    [Fact]
    public void CreateCanonical_ProducesCalibratedCountsAndCorners()
    {
        var g = BoardGeometry.CreateCanonical();

        g.BoardCells.Should().HaveCount(28);
        g.BenchCells.Should().HaveCount(9);
        g.ShopCells.Should().HaveCount(5);

        var r0c0 = g.BoardCells[0];
        r0c0.Row.Should().Be(0);
        r0c0.Col.Should().Be(0);
        r0c0.X.Should().BeApproximately(633.74, 0.01);
        r0c0.Y.Should().BeApproximately(444.75, 0.01);
        r0c0.Nx.Should().BeApproximately(633.74 / 1920.0, 1e-6);
        r0c0.Ny.Should().BeApproximately(444.75 / 1080.0, 1e-6);

        var r3c6 = g.BoardCells[27];
        r3c6.X.Should().BeApproximately(1334.25, 0.01);
        r3c6.Y.Should().BeApproximately(675.0, 0.01);

        g.BenchCells[0].X.Should().BeApproximately(527.25, 0.01);
        g.BenchCells[8].X.Should().BeApproximately(1351.5, 0.01);
        g.BenchCells[4].X.Should().BeApproximately(939.37, 0.01);
        g.BenchCells[0].Y.Should().BeApproximately(784.0, 0.01);
        g.ShopCells[4].X.Should().BeApproximately(870.0, 0.01);
    }

    [Fact]
    public void CreateCanonical_DerivedMetrics_MatchVerifiedConstants()
    {
        var g = BoardGeometry.CreateCanonical();

        g.RowPitchY.Should().BeInRange(70.0, 85.0);
        g.ColPitchX.Should().BeInRange(95.0, 130.0);
        g.StaggerX.Should().BeInRange(45.0, 70.0);
    }

    [Fact]
    public void JsonRoundTrip_PreservesAllCells()
    {
        var g = BoardGeometry.CreateCanonical();
        var json = JsonSerializer.Serialize(g);

        var restored = BoardGeometry.FromJson(json);

        restored.BoardCells.Should().HaveCount(28);
        restored.BenchCells.Should().HaveCount(9);
        restored.ShopCells.Should().HaveCount(5);
        restored.BoardCells[27].X.Should().BeApproximately(1334.25, 0.01);
        restored.ShopCells[4].Y.Should().BeApproximately(915.0, 0.01);
    }

    [Fact]
    public void FromJson_InvalidCounts_Throws()
    {
        var json = """{"version":1,"boardCells":[],"benchCells":[],"shopCells":[]}""";

        var act = () => BoardGeometry.FromJson(json);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FromJson_MalformedJson_Throws()
    {
        var act = () => BoardGeometry.FromJson("""{"version":1,"boardCells":[}""");
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void FromJson_DuplicateBoardCell_Throws()
    {
        var cell = """{"row":0,"col":0,"x":1,"y":1,"nx":0,"ny":0}""";
        var board = string.Join(",", Enumerable.Repeat(cell, 28));
        var json = $$"""{"version":1,"boardCells":[{{board}}],"benchCells":[],"shopCells":[]}""";

        var act = () => BoardGeometry.FromJson(json);
        act.Should().Throw<InvalidOperationException>();
    }
}