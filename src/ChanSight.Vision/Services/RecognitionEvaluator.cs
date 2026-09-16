using ChanSight.Vision.Models;

namespace ChanSight.Vision.Services;

/// <summary>
/// Computes recognition observability: a confusion matrix of ground truth versus
/// fused verdicts plus a cost record. Purely algorithmic; the fusion arbitrator is
/// injected but not invoked, so evaluation never touches the network.
/// </summary>
public sealed class RecognitionEvaluator
{
    private readonly double _perCellUnitUsd;

    public RecognitionEvaluator(FusionArbitrator arbitrator, double perCellUnitUsd = 0.001)
    {
        ArgumentNullException.ThrowIfNull(arbitrator);
        _perCellUnitUsd = perCellUnitUsd;
    }

    /// <summary>
    /// Evaluates a sequence of samples and produces a confusion matrix plus cost
    /// accounting. TrueName non-null is the positive class (hero present); it is
    /// compared to Predicted.Name for strict equality.
    /// </summary>
    public RecognitionSessionMetrics Evaluate(
        IReadOnlyList<(string? TrueName, int TrueStar, FusionVerdict Predicted)> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        var truePositive = 0;
        var falsePositive = 0;
        var trueNegative = 0;
        var falseNegative = 0;
        var escalationCapReached = 0;
        var vlmDegradedCount = 0;

        foreach (var (trueName, _, predicted) in samples)
        {
            var actualPositive = trueName is not null;
            var predictedPositive = predicted.Name is not null;

            if (actualPositive)
            {
                if (predictedPositive && string.Equals(trueName, predicted.Name, StringComparison.Ordinal))
                    truePositive++;
                else
                    falseNegative++;
            }
            else
            {
                if (predictedPositive)
                    falsePositive++;
                else
                    trueNegative++;
            }

            if (predicted.EscalationLevel >= 3)
                escalationCapReached++;

            if (predicted.Name is null && trueName is not null)
                vlmDegradedCount++;
        }

        var cost = new CostRecord(
            samples.Count,
            samples.Count * _perCellUnitUsd,
            escalationCapReached);

        var confusion = new ConfusionMatrix(truePositive, falsePositive, trueNegative, falseNegative);

        return new RecognitionSessionMetrics(confusion, cost, vlmDegradedCount);
    }
}