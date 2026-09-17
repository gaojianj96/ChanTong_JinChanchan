using System.Text.Json;
using ChanSight.Core.Annotation;
using ChanSight.Core.Evaluation;
using FluentAssertions;

namespace ChanSight.Tests.Evaluation;

public sealed class EvaluationRunnerTests
{
    private const string MatchId = "m1";

    [Fact]
    public void Evaluate_MixedLabels_ComputesMetricsExactly()
    {
        var labels = new[]
        {
            Label(id: "g1", region: RegionTypes.BoardHero, tier: "T0",
                recognized: "{\"hero\":\"盖伦\"}", corrected: "{\"hero\":\"盖伦\"}", type: CorrectionTypes.ConfirmCorrect),
            Label(id: "g2", region: RegionTypes.BenchHero, tier: "T1",
                recognized: "亚索", corrected: "{\"hero\":\"盖伦\"}", type: CorrectionTypes.Classification),
            Label(id: "g3", region: RegionTypes.ShopHero, tier: "T2",
                recognized: null, corrected: "{\"hero\":\"盖伦\"}", type: CorrectionTypes.Missed),
        };

        var report = EvaluationRunner.Evaluate(labels);

        report.TotalLabels.Should().Be(3);
        report.TotalCorrect.Should().Be(1);
        report.OverallAccuracy.Should().BeApproximately(1.0 / 3.0, 1e-9);

        report.ByRegion.Should().HaveCount(3);
        report.ByRegion.Should().ContainSingle(r => r.RegionType == RegionTypes.BoardHero)
            .Which.Confusion.Should().Be(new Confusion(1, 0, 0, 0));
        report.ByRegion.Should().ContainSingle(r => r.RegionType == RegionTypes.BenchHero)
            .Which.Confusion.Should().Be(new Confusion(0, 0, 0, 1));
        report.ByRegion.Should().ContainSingle(r => r.RegionType == RegionTypes.ShopHero)
            .Which.Confusion.Should().Be(new Confusion(0, 0, 0, 1));

        report.ByTier.Should().ContainSingle(t => t.SourceTier == "T0")
            .Which.Confusion.Should().Be(new Confusion(1, 0, 0, 0));
        report.ByTier.Should().ContainSingle(t => t.SourceTier == "T1")
            .Which.Confusion.Should().Be(new Confusion(0, 0, 0, 1));
        report.ByTier.Should().ContainSingle(t => t.SourceTier == "T2")
            .Which.Confusion.Should().Be(new Confusion(0, 0, 0, 1));

        report.Issues.Should().BeEmpty();
    }

    [Fact]
    public void CorrectionValue_StructuredEquivalence_ComparesHeroStarItems()
    {
        CorrectionValue.Equals("{\"hero\":\"盖伦\",\"star\":2}", "{\"hero\":\"盖伦\",\"star\":3}").Should().BeFalse();
        CorrectionValue.Equals("{\"hero\":\"盖伦\"}", "{\"hero\":\"盖伦\"}").Should().BeTrue();
        CorrectionValue.Equals(null, "{\"empty\":true}").Should().BeTrue();
        CorrectionValue.Equals("{\"empty\":true}", "{\"empty\":true}").Should().BeTrue();
        CorrectionValue.Equals("盖伦", "{\"hero\":\"盖伦\"}").Should().BeTrue();
        CorrectionValue.Equals("亚索", "{\"hero\":\"盖伦\"}").Should().BeFalse();
        CorrectionValue.Equals("{\"hero\":\"盖伦\",\"items\":[\"羊刀\"]}", "{\"hero\":\"盖伦\",\"items\":[\"反曲弓\"]}").Should().BeFalse();
        CorrectionValue.Equals("{\"hero\":\"盖伦\",\"items\":[\"羊刀\"]}", "{\"hero\":\"盖伦\",\"items\":[\"羊刀\"]}").Should().BeTrue();
    }

    [Fact]
    public void Evaluate_ExcludesRevokedLabels()
    {
        var labels = new[]
        {
            Label(id: "g1"),
            Label(id: "g2") with { Revoked = true },
        };

        var report = EvaluationRunner.Evaluate(labels);

        report.TotalLabels.Should().Be(1);
        report.TotalCorrect.Should().Be(1);
        report.OverallAccuracy.Should().Be(1.0);
    }

    [Fact]
    public void Evaluate_EmptySet_AllZeroNoThrow()
    {
        var report = EvaluationRunner.Evaluate(Array.Empty<GoldLabel>());

        report.TotalLabels.Should().Be(0);
        report.TotalCorrect.Should().Be(0);
        report.OverallAccuracy.Should().Be(0.0);
        report.ByRegion.Should().BeEmpty();
        report.ByTier.Should().BeEmpty();
        report.ByType.Should().BeEmpty();
        report.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_ByTypeDistribution_CountsEachCorrectionType()
    {
        var labels = new[]
        {
            Label(id: "g1", type: CorrectionTypes.ConfirmCorrect),
            Label(id: "g2", type: CorrectionTypes.ConfirmCorrect),
            Label(id: "g3", type: CorrectionTypes.Classification),
            Label(id: "g4", type: CorrectionTypes.Missed),
            Label(id: "g5", type: CorrectionTypes.FalsePositive, recognized: "盖伦", corrected: "{\"empty\":true}"),
        };

        var report = EvaluationRunner.Evaluate(labels);

        report.ByType.Should().BeEquivalentTo(new[]
        {
            new TypeMetric(CorrectionTypes.ConfirmCorrect, 2),
            new TypeMetric(CorrectionTypes.Classification, 1),
            new TypeMetric(CorrectionTypes.Missed, 1),
            new TypeMetric(CorrectionTypes.FalsePositive, 1),
        });
    }

    [Fact]
    public void Evaluate_Report_SerializesWithSystemTextJson()
    {
        var report = EvaluationRunner.Evaluate(new[] { Label(id: "g1") });

        var json = JsonSerializer.Serialize(report);

        json.Should().Contain("\"TotalLabels\":1");
        json.Should().Contain("\"OverallAccuracy\":1");
        json.Should().Contain("\"ByRegion\":[");
    }

    private static GoldLabel Label(
        string id,
        string region = RegionTypes.BoardHero,
        string tier = "T0",
        string? recognized = "{\"hero\":\"盖伦\"}",
        string corrected = "{\"hero\":\"盖伦\"}",
        string? type = CorrectionTypes.ConfirmCorrect)
    {
        return new GoldLabel(
            id,
            CorrectionId: "corr-" + id,
            MatchId,
            "f1",
            1,
            region,
            0,
            recognized,
            corrected,
            0.9,
            tier,
            "v1",
            type,
            Revoked: false,
            ConfirmedAt: new DateTimeOffset(2026, 9, 16, 10, 0, 0, TimeSpan.Zero));
    }
}