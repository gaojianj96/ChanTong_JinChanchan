namespace ChanSight.Vision.Models;

/// <summary>
/// VLM verdict for a single cell. Name is null when the cell is empty or the
/// recognition confidence is too low to trust. RawJson carries the original
/// response fragment for auditability.
/// </summary>
public sealed record VlmCellVerdict(
    int CellIndex,
    string? Name,
    int? Star,
    double Confidence,
    SourceTier SourceTier = SourceTier.T2,
    string RawJson = "");