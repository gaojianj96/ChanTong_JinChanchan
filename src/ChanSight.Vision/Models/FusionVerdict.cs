namespace ChanSight.Vision.Models;

/// <summary>
/// Final trusted per-cell conclusion produced by the fusion arbitrator. Star and
/// occupancy come from the local deterministic tier; Name comes from VLM unless a
/// manual correction overrides it. EscalationLevel records 0..3 (T0..T3) for which
/// tier produced the semantic conclusion.
/// </summary>
public sealed record FusionVerdict(
    int CellIndex,
    string? Name,
    int Star,
    double Confidence,
    SourceTier SourceTier,
    int EscalationLevel);

/// <summary>
/// Local deterministic result for a single cell (star count + occupancy). Occupancy
/// is derived from star &gt; 0 or a non-empty digit read upstream (see V2).
/// </summary>
public sealed record LocalCellResult(int CellIndex, int Star, bool IsOccupied);