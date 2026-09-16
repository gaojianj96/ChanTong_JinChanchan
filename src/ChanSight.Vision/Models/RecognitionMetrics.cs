namespace ChanSight.Vision.Models;

/// <summary>
/// Confusion matrix over a recognition evaluation: ground truth (hero present or
/// empty) versus the fused prediction (name present or null). Positive means a
/// hero (non-empty name) is present.
/// </summary>
public sealed record ConfusionMatrix(int TruePositive, int FalsePositive, int TrueNegative, int FalseNegative)
{
    public int Total => TruePositive + FalsePositive + TrueNegative + FalseNegative;

    public double Accuracy =>
        Total == 0 ? 0.0 : (double)(TruePositive + TrueNegative) / Total;

    public double Precision =>
        TruePositive + FalsePositive == 0 ? 0.0 : (double)TruePositive / (TruePositive + FalsePositive);

    public double Recall =>
        TruePositive + FalseNegative == 0 ? 0.0 : (double)TruePositive / (TruePositive + FalseNegative);
}

/// <summary>
/// Estimated unit cost of a recognition session. EstimatedUsd is derived from the
/// cell count and the per-cell unit price; EscalationCapReached counts cells that
/// reached the top of the escalation chain (T3).
/// </summary>
public sealed record CostRecord(int CellCount, double EstimatedUsd, int EscalationCapReached);

/// <summary>
/// Aggregate observability snapshot for a single recognition session.
/// </summary>
public sealed record RecognitionSessionMetrics(
    ConfusionMatrix Confusion,
    CostRecord Cost,
    int VlmDegradedCount);