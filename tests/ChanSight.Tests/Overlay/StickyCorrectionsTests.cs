using System.Text.Json;
using ChanSight.Core.Annotation;
using ChanSight.Overlay.Models;
using FluentAssertions;

namespace ChanSight.Tests.Overlay;

public sealed class StickyCorrectionsTests
{
    [Fact]
    public void Set_ThenSameRecognizedValue_RemainsSticky()
    {
        var sticky = new StickyCorrections();
        sticky.Set(new StickyEntry(RegionTypes.BoardHero, 3, "盖伦", CorrectionValue.Hero("厄斐琉斯")));

        sticky.IsSticky(RegionTypes.BoardHero, 3, "盖伦").Should().BeTrue();
        sticky.Get(RegionTypes.BoardHero, 3).Should().NotBeNull();
    }

    [Fact]
    public void Set_ThenRecognizedValueChanges_Invalidates()
    {
        var sticky = new StickyCorrections();
        sticky.Set(new StickyEntry(RegionTypes.BoardHero, 3, "盖伦", CorrectionValue.Hero("厄斐琉斯")));

        sticky.IsSticky(RegionTypes.BoardHero, 3, "拉克丝").Should().BeFalse();
        sticky.Get(RegionTypes.BoardHero, 3).Should().BeNull();
    }

    [Fact]
    public void StarCorrection_ThenStarChanges_Invalidates()
    {
        var sticky = new StickyCorrections();
        sticky.Set(new StickyEntry(RegionTypes.BoardStar, 3, "1", CorrectionValue.Star(3)));

        sticky.IsSticky(RegionTypes.BoardStar, 3, "1").Should().BeTrue();
        sticky.IsSticky(RegionTypes.BoardStar, 3, "2").Should().BeFalse();
    }

    [Fact]
    public void NullVsEmptyBaseline_TreatedAsEqualWhenBothEmpty()
    {
        var sticky = new StickyCorrections();
        sticky.Set(new StickyEntry(RegionTypes.ShopHero, 0, null, CorrectionValue.Empty()));

        sticky.IsSticky(RegionTypes.ShopHero, 0, null).Should().BeTrue();
        sticky.IsSticky(RegionTypes.ShopHero, 0, string.Empty).Should().BeTrue();
    }

    [Fact]
    public void CorrectionFactory_CreatesFieldLevelRecord_WithPinnedFrameId()
    {
        var record = CorrectionFactory.Create(
            id: "c1",
            matchId: "live",
            frameId: "deadbeef",
            snapshotVersion: 0,
            regionType: RegionTypes.BoardHero,
            cellIndex: 3,
            recognizedValue: "盖伦",
            correctedValue: CorrectionValue.Hero("厄斐琉斯"));

        record.Id.Should().Be("c1");
        record.FrameId.Should().Be("deadbeef");
        record.RegionType.Should().Be(RegionTypes.BoardHero);
        record.CellIndex.Should().Be(3);
        record.RecognizedValue.Should().Be("盖伦");
        record.SnapshotVersion.Should().Be(0);
        RegionTypes.IsValid(record.RegionType).Should().BeTrue();
    }

    [Fact]
    public void CorrectionFactory_RecognizedValueCanBeNull_AndCorrectedValueIsValidJson()
    {
        var record = CorrectionFactory.Create(
            id: "c2",
            matchId: "live",
            frameId: "f1",
            snapshotVersion: 0,
            regionType: RegionTypes.ShopHero,
            cellIndex: 2,
            recognizedValue: null,
            correctedValue: CorrectionValue.Empty());

        record.RecognizedValue.Should().BeNull();

        using var doc = JsonDocument.Parse(record.CorrectedValue);
        doc.RootElement.GetProperty("empty").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void CorrectionFactory_EmptyFrameId_Throws()
    {
        var act = () => CorrectionFactory.Create(
            id: "c1",
            matchId: "live",
            frameId: string.Empty,
            snapshotVersion: 0,
            regionType: RegionTypes.BoardHero,
            cellIndex: 0,
            recognizedValue: null,
            correctedValue: CorrectionValue.Hero("盖伦"));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CorrectionFactory_InvalidRegionType_Throws()
    {
        var act = () => CorrectionFactory.Create(
            id: "c1",
            matchId: "live",
            frameId: "f1",
            snapshotVersion: 0,
            regionType: "nope",
            cellIndex: 0,
            recognizedValue: null,
            correctedValue: CorrectionValue.Hero("盖伦"));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CorrectionValue_HeroProducesJsonWithHeroField()
    {
        var json = CorrectionValue.Hero("盖伦");
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("hero").GetString().Should().Be("盖伦");
    }

    [Fact]
    public void CorrectionValue_StarProducesJsonWithStarField()
    {
        var json = CorrectionValue.Star(3);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("star").GetInt32().Should().Be(3);
    }
}