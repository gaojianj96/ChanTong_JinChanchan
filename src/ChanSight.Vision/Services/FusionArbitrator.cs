using System.Collections.Concurrent;
using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;

namespace ChanSight.Vision.Services;

/// <summary>
/// Fuses local deterministic recognition (star/occupancy) with VLM semantic
/// recognition (hero name/confidence) into a single trusted per-cell verdict.
/// Local wins occupancy and star; VLM fills the name; conflicts vote by source
/// tier. A three-tier escalation chain records semantic degradation, and manual
/// corrections are replayed from a thread-safe few-shot dictionary.
/// </summary>
public sealed class FusionArbitrator
{
    private const int MaxCellIndex = 41;
    private const double VlmHighConfidence = 0.85;

    private readonly double _confidenceThreshold;
    private readonly VlmRecognitionAdapter _adapter;
    private readonly ConcurrentDictionary<int, string> _corrections = new();

    public FusionArbitrator(IVlmClient client, double confidenceThreshold = 0.5)
    {
        ArgumentNullException.ThrowIfNull(client);
        _adapter = new VlmRecognitionAdapter(client);
        _confidenceThreshold = confidenceThreshold;
    }

    /// <summary>
    /// Read-only view of the manual-correction few-shot library.
    /// </summary>
    public IReadOnlyDictionary<int, string> Corrections => _corrections;

    /// <summary>
    /// Records a manual name correction for a cell index; thread-safe and later
    /// wins over both VLM and local semantics (but never occupancy).
    /// </summary>
    public void ApplyCorrection(int cellIndex, string correctedName)
    {
        ArgumentNullException.ThrowIfNull(correctedName);
        _corrections.AddOrUpdate(
            cellIndex,
            correctedName,
            static (_, updatedName) => updatedName);
    }

    /// <summary>
    /// Attempts to read a manual correction for a cell index.
    /// </summary>
    public bool TryGetCorrection(int cellIndex, out string name) =>
        _corrections.TryGetValue(cellIndex, out name!);

    /// <summary>
    /// Pure algorithmic local-first fusion. Occupancy and star follow the local
    /// tier; name follows VLM, corrected name, or null. Returns verdicts sorted
    /// by cell index ascending.
    /// </summary>
    public IReadOnlyList<FusionVerdict> Fuse(
        IReadOnlyList<LocalCellResult> locals,
        IReadOnlyList<VlmCellVerdict> vlmVerdicts)
    {
        ArgumentNullException.ThrowIfNull(locals);
        ArgumentNullException.ThrowIfNull(vlmVerdicts);

        var vlmByIndex = new Dictionary<int, VlmCellVerdict>();
        foreach (var verdict in vlmVerdicts)
        {
            if (!IsValidCellIndex(verdict.CellIndex))
                continue;
            vlmByIndex[verdict.CellIndex] = verdict;
        }

        var results = new List<FusionVerdict>(locals.Count);
        foreach (var local in locals)
        {
            if (!IsValidCellIndex(local.CellIndex))
                continue;

            vlmByIndex.TryGetValue(local.CellIndex, out var verdict);
            results.Add(FuseCell(local, verdict));
        }

        results.Sort(static (a, b) => a.CellIndex.CompareTo(b.CellIndex));
        return results;
    }

    /// <summary>
    /// Fuses the local tier with a VLM semantic pass run through the isolated
    /// <see cref="IVlmClient"/>. Cells the local tier cannot determine are run up
    /// the escalation chain: T1 (base VLM) → T2 (retry) → T3 (give up, hand to
    /// evidence layer). Only <see cref="EscalationLevel"/> changes between tiers;
    /// the concrete model backend is not switched in this delivery.
    /// </summary>
    public async Task<IReadOnlyList<FusionVerdict>> FuseWithEscalationAsync(
        IReadOnlyList<LocalCellResult> locals,
        IReadOnlyList<VlmCellInput> crops,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(locals);
        ArgumentNullException.ThrowIfNull(crops);

        var validLocals = locals.Where(static l => IsValidCellIndex(l.CellIndex)).ToList();
        var validCrops = crops.Where(static c => IsValidCellIndex(c.CellIndex)).ToList();

        // T0: cells whose local tier already establishes occupancy and semantics.
        var determined = new HashSet<int>(
            validLocals.Where(static l => l.IsOccupied || l.Star > 0)
                       .Select(static l => l.CellIndex));

        var results = new Dictionary<int, FusionVerdict>();
        var pendingCells = new List<(LocalCellResult Local, VlmCellInput? Crop)>();

        foreach (var local in validLocals)
        {
            if (determined.Contains(local.CellIndex))
            {
                results[local.CellIndex] = FuseCell(local, null);
            }
            else
            {
                var crop = validCrops.FirstOrDefault(c => c.CellIndex == local.CellIndex);
                pendingCells.Add((local, crop));
            }
        }

        if (pendingCells.Count > 0)
        {
            var pendingCrops = pendingCells
                .Where(p => p.Crop is not null)
                .Select(static p => p.Crop!)
                .ToList();

            // T1 pass.
            var t1Verdicts = pendingCrops.Count > 0
                ? await RecognizeAsync(pendingCrops, ct).ConfigureAwait(false)
                : new Dictionary<int, VlmCellVerdict>();

            var failedCells = new List<(LocalCellResult Local, VlmCellInput Crop)>();
            foreach (var pending in pendingCells)
            {
                if (pending.Crop is null)
                {
                    results[pending.Local.CellIndex] = new FusionVerdict(
                        pending.Local.CellIndex, null, pending.Local.Star, 0.0, SourceTier.T3, 3);
                    continue;
                }

                var verdict = t1Verdicts.GetValueOrDefault(pending.Local.CellIndex);
                if (verdict is not null && IsSemanticPass(verdict))
                {
                    var name = ResolveName(pending.Local.CellIndex, verdict.Name);
                    results[pending.Local.CellIndex] = new FusionVerdict(
                        pending.Local.CellIndex, name, pending.Local.Star, verdict.Confidence, SourceTier.T1, 1);
                }
                else
                {
                    failedCells.Add((pending.Local, pending.Crop));
                }
            }

            // T2 retry for cells that failed T1.
            if (failedCells.Count > 0)
            {
                var retryCrops = failedCells.Select(static p => p.Crop).ToList();
                var t2Verdicts = await RecognizeAsync(retryCrops, ct).ConfigureAwait(false);

                foreach (var pending in failedCells)
                {
                    var verdict = t2Verdicts.GetValueOrDefault(pending.Local.CellIndex);
                    if (verdict is not null && IsSemanticPass(verdict))
                    {
                        var name = ResolveName(pending.Local.CellIndex, verdict.Name);
                        results[pending.Local.CellIndex] = new FusionVerdict(
                            pending.Local.CellIndex, name, pending.Local.Star, verdict.Confidence, SourceTier.T2, 2);
                    }
                    else
                    {
                        // T3: exhausted chain.
                        results[pending.Local.CellIndex] = new FusionVerdict(
                            pending.Local.CellIndex, null, pending.Local.Star, 0.0, SourceTier.T3, 3);
                    }
                }
            }
        }

        var ordered = results.Values.ToList();
        ordered.Sort(static (a, b) => a.CellIndex.CompareTo(b.CellIndex));
        return ordered;
    }

    private async Task<Dictionary<int, VlmCellVerdict>> RecognizeAsync(
        IReadOnlyList<VlmCellInput> cells,
        CancellationToken ct)
    {
        try
        {
            var result = await _adapter.RecognizeAsync(cells, ct).ConfigureAwait(false);
            return result.Verdicts.ToDictionary(static v => v.CellIndex);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new Dictionary<int, VlmCellVerdict>();
        }
    }

    private bool IsSemanticPass(VlmCellVerdict verdict) =>
        verdict.Confidence >= _confidenceThreshold && IsKnownHero(verdict.Name);

    private FusionVerdict FuseCell(LocalCellResult local, VlmCellVerdict? verdict)
    {
        // Manual correction wins the semantic name (but not occupancy/star).
        if (TryGetCorrection(local.CellIndex, out var corrected))
        {
            return new FusionVerdict(local.CellIndex, corrected, local.Star, 1.0, SourceTier.T0, 0);
        }

        // Local name is always unknown; high-confidence VLM name fills it.
        if (verdict is not null &&
            verdict.Confidence >= VlmHighConfidence &&
            !string.IsNullOrWhiteSpace(verdict.Name))
        {
            return new FusionVerdict(local.CellIndex, verdict.Name, local.Star, verdict.Confidence, SourceTier.T0, 0);
        }

        return new FusionVerdict(local.CellIndex, null, local.Star, 0.0, SourceTier.T0, 0);
    }

    private string? ResolveName(int cellIndex, string? fallbackName)
    {
        if (TryGetCorrection(cellIndex, out var corrected))
            return corrected;
        return fallbackName;
    }

    private static bool IsKnownHero(string? name) =>
        !string.IsNullOrWhiteSpace(name) && GameSeasonDictionary.Heroes.Contains(name!);

    private static bool IsValidCellIndex(int cellIndex) =>
        cellIndex is >= 0 and <= MaxCellIndex;
}