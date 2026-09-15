namespace ChanSight.Vision.Models;

/// <summary>
/// Batch result of a VLM recognition pass, one verdict per input cell.
/// </summary>
public sealed record VlmRecognitionResult(IReadOnlyList<VlmCellVerdict> Verdicts);