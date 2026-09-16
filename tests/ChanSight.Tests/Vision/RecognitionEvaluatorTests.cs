using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using FluentAssertions;

namespace ChanSight.Tests.Vision;

public sealed class RecognitionEvaluatorTests
{
    [Fact]
    public void Evaluate_KnownSamples_ComputesConfusionCounts()
    {
        var evaluator = new RecognitionEvaluator(NewArbitrator());
        var samples = new (string? TrueName, int TrueStar, FusionVerdict Predicted)[]
        {
            ("盖伦", 2, Verdict("盖伦", 0)),   // TP
            (null, 0, Verdict("盖伦", 0)),      // FP
            (null, 0, Verdict(null, 0)),        // TN
            ("亚索", 2, Verdict(null, 3)),      // FN
        };

        var result = evaluator.Evaluate(samples);

        var c = result.Confusion;
        c.TruePositive.Should().Be(1);
        c.FalsePositive.Should().Be(1);
        c.TrueNegative.Should().Be(1);
        c.FalseNegative.Should().Be(1);
        c.Total.Should().Be(4);
        c.Accuracy.Should().BeApproximately(0.5, 1e-12);
        c.Precision.Should().BeApproximately(0.5, 1e-12);
        c.Recall.Should().BeApproximately(0.5, 1e-12);
    }

    [Fact]
    public void Evaluate_NameMismatch_CountsAsFalseNegative()
    {
        var evaluator = new RecognitionEvaluator(NewArbitrator());
        var samples = new (string? TrueName, int TrueStar, FusionVerdict Predicted)[]
        {
            ("盖伦", 2, Verdict("亚索", 0)),
        };

        var result = evaluator.Evaluate(samples);

        result.Confusion.TruePositive.Should().Be(0);
        result.Confusion.FalseNegative.Should().Be(1);
    }

    [Fact]
    public void Evaluate_EmptyList_ReturnsZeroMetrics()
    {
        var evaluator = new RecognitionEvaluator(NewArbitrator());

        var result = evaluator.Evaluate(Array.Empty<(string?, int, FusionVerdict)>());

        var c = result.Confusion;
        c.Total.Should().Be(0);
        c.Accuracy.Should().Be(0.0);
        c.Precision.Should().Be(0.0);
        c.Recall.Should().Be(0.0);
        result.Cost.CellCount.Should().Be(0);
        result.Cost.EstimatedUsd.Should().Be(0.0);
        result.Cost.EscalationCapReached.Should().Be(0);
        result.VlmDegradedCount.Should().Be(0);
    }

    [Fact]
    public void Evaluate_DivideByZero_ReturnsZero()
    {
        var evaluator = new RecognitionEvaluator(NewArbitrator());
        var samples = new (string? TrueName, int TrueStar, FusionVerdict Predicted)[]
        {
            (null, 0, Verdict(null, 0)),
        };

        var result = evaluator.Evaluate(samples);

        result.Confusion.Precision.Should().Be(0.0);
        result.Confusion.Recall.Should().Be(0.0);
    }

    [Fact]
    public void Evaluate_CostRecord_AccumulatesCellCountAndEstimate()
    {
        var evaluator = new RecognitionEvaluator(NewArbitrator(), perCellUnitUsd: 0.001);
        var samples = new (string? TrueName, int TrueStar, FusionVerdict Predicted)[10];
        for (var i = 0; i < samples.Length; i++)
            samples[i] = ("盖伦", 2, Verdict("盖伦", 0));

        var result = evaluator.Evaluate(samples);

        result.Cost.CellCount.Should().Be(10);
        result.Cost.EstimatedUsd.Should().BeApproximately(0.010, 1e-12);
    }

    [Fact]
    public void Evaluate_EscalationCapReached_CountsT3Cells()
    {
        var evaluator = new RecognitionEvaluator(NewArbitrator());
        var samples = new (string? TrueName, int TrueStar, FusionVerdict Predicted)[]
        {
            ("盖伦", 0, Verdict(null, 3)),
            ("盖伦", 0, Verdict(null, 2)),
            ("盖伦", 0, Verdict(null, 4)),
        };

        var result = evaluator.Evaluate(samples);

        result.Cost.EscalationCapReached.Should().Be(2);
    }

    [Fact]
    public void Evaluate_VlmDegradedCount_CountsMissingNameWithTrueName()
    {
        var evaluator = new RecognitionEvaluator(NewArbitrator());
        var samples = new (string? TrueName, int TrueStar, FusionVerdict Predicted)[]
        {
            ("盖伦", 0, Verdict(null, 3)),
            (null, 0, Verdict(null, 3)),
            ("亚索", 0, Verdict("亚索", 0)),
        };

        var result = evaluator.Evaluate(samples);

        result.VlmDegradedCount.Should().Be(1);
    }

    [Fact]
    public void Evaluate_NullSamples_ThrowsArgumentNullException()
    {
        var evaluator = new RecognitionEvaluator(NewArbitrator());

        var act = () => evaluator.Evaluate(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_NullArbitrator_ThrowsArgumentNullException()
    {
        var act = () => new RecognitionEvaluator(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private static FusionArbitrator NewArbitrator() => new(new FakeNeverUsedVlmClient());

    private static FusionVerdict Verdict(string? name, int escalationLevel) =>
        new(0, name, 0, name is null ? 0.0 : 1.0, SourceTier.T0, escalationLevel);

    private sealed class FakeNeverUsedVlmClient : IVlmClient
    {
        public Task<string> CompleteAsync(
            string prompt,
            IReadOnlyList<(string mime, byte[] data)> images,
            CancellationToken ct) =>
            throw new InvalidOperationException("Evaluator must not call VLM.");
    }
}