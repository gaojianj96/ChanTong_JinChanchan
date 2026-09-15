using ChanSight.Vision.Models;
using FluentAssertions;
using System.Text.Json;

namespace ChanSight.Tests.Vision;

public sealed class RecognitionFrameTests
{
    private static RecognitionFrame BuildValidFrame() => new()
    {
        SourceTier = SourceTier.T0,
        CorrectionFlag = false,
        Timestamp = "2026-09-15T00:00:00Z",
        Confidence = 0.98,
        Gold = 50,
        Level = 7,
        Stage = "3-2",
        Hp = 72,
        Exp = 24,
        BoardCells = Enumerable.Range(0, 28)
            .Select(i => new UnitCell(
                i == 0 ? "提莫" : null,
                i == 0 ? 2 : 0,
                i == 0 ? new[] { new ItemStack("bf_sword", 1) } : Array.Empty<ItemStack>(),
                i == 0 ? 0.95 : 1.0,
                SourceTier.T1))
            .ToArray(),
        BenchCells = Enumerable.Range(0, 9)
            .Select(i => new UnitCell(
                i == 0 ? "安妮" : null,
                i == 0 ? 1 : 0,
                Array.Empty<ItemStack>(),
                i == 0 ? 0.93 : 1.0,
                SourceTier.T1))
            .ToArray(),
        ShopCards = Enumerable.Range(0, 5)
            .Select(i => new ShopCard($"card{i}", i + 1, 0.9 - i * 0.01, SourceTier.T2))
            .ToArray(),
    };

    [Fact]
    public void Deserialize_ValidJson_PreservesFields()
    {
        var json = RecognitionFrame.Serialize(BuildValidFrame());

        var frame = RecognitionFrame.Deserialize(json);

        frame.Gold.Should().Be(50);
        frame.Level.Should().Be(7);
        frame.Stage.Should().Be("3-2");
        frame.Hp.Should().Be(72);
        frame.Exp.Should().Be(24);
        frame.BoardCells.Should().HaveCount(28);
        frame.BenchCells.Should().HaveCount(9);
        frame.ShopCards.Should().HaveCount(5);
        frame.BoardCells[0].Name.Should().Be("提莫");
        frame.BoardCells[0].Star.Should().Be(2);
        frame.BoardCells[0].Items.Should().HaveCount(1);
        frame.BoardCells[0].Items[0].IconId.Should().Be("bf_sword");
        frame.SourceTier.Should().Be(SourceTier.T0);
        frame.CorrectionFlag.Should().BeFalse();
    }

    [Fact]
    public void Validate_ValidFrame_ReturnsNoErrors()
    {
        var errors = RecognitionFrameSchemaValidator.Validate(BuildValidFrame());

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WrongBoardCount_Fails()
    {
        var frame2 = BuildValidFrame();
        var bad = frame2 with { BoardCells = frame2.BoardCells.Take(27).ToArray() };

        var errors = RecognitionFrameSchemaValidator.Validate(bad);

        errors.Should().ContainSingle(e => e.Contains("boardCells"));
    }

    [Fact]
    public void Validate_WrongBenchCount_Fails()
    {
        var frame = BuildValidFrame();
        var bad = frame with { BenchCells = frame.BenchCells.Take(8).ToArray() };

        var errors = RecognitionFrameSchemaValidator.Validate(bad);

        errors.Should().ContainSingle(e => e.Contains("benchCells"));
    }

    [Fact]
    public void Validate_WrongShopCount_Fails()
    {
        var frame = BuildValidFrame();
        var bad = frame with { ShopCards = frame.ShopCards.Take(4).ToArray() };

        var errors = RecognitionFrameSchemaValidator.Validate(bad);

        errors.Should().ContainSingle(e => e.Contains("shopCards"));
    }

    [Fact]
    public void Validate_ConfidenceOutOfRange_Fails()
    {
        var frame = BuildValidFrame();
        var badCell = new UnitCell("x", 0, Array.Empty<ItemStack>(), 1.5, SourceTier.T0);
        var cells = frame.BoardCells.ToArray();
        cells[0] = badCell;
        var bad = frame with { BoardCells = cells };

        var errors = RecognitionFrameSchemaValidator.Validate(bad);

        errors.Should().Contain(e => e.Contains("confidence"));
    }

    [Fact]
    public void Validate_StarOutOfRange_Fails()
    {
        var frame = BuildValidFrame();
        var badCell = new UnitCell("x", 4, Array.Empty<ItemStack>(), 1.0, SourceTier.T0);
        var cells = frame.BoardCells.ToArray();
        cells[0] = badCell;
        var bad = frame with { BoardCells = cells };

        var errors = RecognitionFrameSchemaValidator.Validate(bad);

        errors.Should().Contain(e => e.Contains("star"));
    }

    [Fact]
    public void Validate_LevelOutOfRange_Fails()
    {
        var frame = BuildValidFrame();
        var bad = frame with { Level = 10 };

        var errors = RecognitionFrameSchemaValidator.Validate(bad);

        errors.Should().Contain(e => e.Contains("level"));
    }

    [Fact]
    public void SchemaFieldNames_MatchContractDocument()
    {
        // board/bench/shop field names present in serialized JSON.
        var json = RecognitionFrame.Serialize(BuildValidFrame());
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("boardCells", out _).Should().BeTrue();
        root.TryGetProperty("benchCells", out _).Should().BeTrue();
        root.TryGetProperty("shopCards", out _).Should().BeTrue();

        var boardCell = root.GetProperty("boardCells")[0];
        boardCell.TryGetProperty("name", out _).Should().BeTrue();
        boardCell.TryGetProperty("star", out _).Should().BeTrue();
        boardCell.TryGetProperty("items", out _).Should().BeTrue();
        boardCell.TryGetProperty("confidence", out _).Should().BeTrue();
        boardCell.TryGetProperty("sourceTier", out _).Should().BeTrue();

        var shopCard = root.GetProperty("shopCards")[0];
        shopCard.TryGetProperty("name", out _).Should().BeTrue();
        shopCard.TryGetProperty("cost", out _).Should().BeTrue();
    }
}