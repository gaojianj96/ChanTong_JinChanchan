using ChanSight.Core.Engine;
using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;
using OpenCvSharp;

namespace ChanSight.Vision.Services;

/// <summary>
/// Single recognition entry point (<c>Mat</c> → <see cref="RecognitionFrame"/>), route A:
/// local deterministic CV owns the deterministic fields (gold/stage/star/occupancy), the
/// VLM only supplies the semantic hero name for changed cells, and the fusion arbitrator
/// settles the final per-cell verdict.
///
/// Time consistency: the VLM is an async network call. While a call is in flight a newer
/// frame may arrive. Each pass captures a monotonically increasing snapshot version; when
/// a VLM result returns it is only committed if the pipeline is still on the same version,
/// otherwise the stale result is discarded (cells fall back to the carried-forward name or,
/// failing that, degrade to <see cref="SourceTier.T3"/>). Unchanged cells always carry
/// forward the previous frame's name without re-invoking the VLM.
/// </summary>
public sealed class RecognitionPipeline
{
    private const int BoardCellCount = 28;
    private const int BenchCellCount = 9;
    private const int UnitCellCount = BoardCellCount + BenchCellCount;

    private readonly IPaddleOcrService _ocr;
    private readonly LocalDeterministicRecognizer _localRecognizer;
    private readonly CellChangeDetector _changeDetector;
    private readonly VlmRecognitionAdapter _vlmAdapter;
    private readonly FusionArbitrator _fusion;
    private readonly IPhaseDetector _phaseDetector;
    private readonly IGridSlicerService _gridSlicer;

    private long _frameVersion;
    private Mat? _previousFrame;
    private readonly Dictionary<int, string> _knownNames = new();

    public RecognitionPipeline(
        IPaddleOcrService ocr,
        LocalDeterministicRecognizer localRecognizer,
        CellChangeDetector changeDetector,
        VlmRecognitionAdapter vlmAdapter,
        FusionArbitrator fusion,
        IPhaseDetector phaseDetector,
        IGridSlicerService gridSlicer)
    {
        _ocr = ocr ?? throw new ArgumentNullException(nameof(ocr));
        _localRecognizer = localRecognizer ?? throw new ArgumentNullException(nameof(localRecognizer));
        _changeDetector = changeDetector ?? throw new ArgumentNullException(nameof(changeDetector));
        _vlmAdapter = vlmAdapter ?? throw new ArgumentNullException(nameof(vlmAdapter));
        _fusion = fusion ?? throw new ArgumentNullException(nameof(fusion));
        _phaseDetector = phaseDetector ?? throw new ArgumentNullException(nameof(phaseDetector));
        _gridSlicer = gridSlicer ?? throw new ArgumentNullException(nameof(gridSlicer));
    }

    /// <summary>
    /// Recognizes a single frame end-to-end. Never throws for a null/empty frame; a
    /// default <see cref="RecognitionFrame"/> is returned instead so the caller can
    /// keep consuming frames.
    /// </summary>
    public async Task<RecognitionFrame> RecognizeAsync(Mat frame, CancellationToken ct = default)
    {
        var myVersion = System.Threading.Interlocked.Increment(ref _frameVersion);

        if (frame is null || frame.Empty())
            return new RecognitionFrame { Timestamp = DateTime.UtcNow.ToString("O") };

        // ---- deterministic fields (synchronous local CV) ----
        int gold = TryReadDeterministic(() =>
        {
            var result = _ocr.RecognizeGold(frame);
            return int.TryParse(result.MatchedDictKey, out var value)
                ? value
                : GameSeasonDictionary.TryParseNumeric(result.CleanedText) ?? 0;
        });

        string stage = TryReadDeterministic(() =>
        {
            var result = _ocr.RecognizeStage(frame);
            return string.IsNullOrWhiteSpace(result.MatchedDictKey)
                ? result.CleanedText ?? string.Empty
                : result.MatchedDictKey;
        });

        // Level/Hp have no dedicated OCR method in this delivery; leave as defaults.
        IReadOnlyList<ShopCard> shopCards = TryReadDeterministic(() => BuildShopCards(frame));

        // ---- phase (local heuristic) ----
        var phaseFrame = new RecognitionFrame { Stage = stage, ShopCards = shopCards, Phase = GamePhase.Planning };
        GamePhase phase = GamePhase.Planning;
        TryReadDeterministic(() =>
        {
            phase = _phaseDetector.Detect(phaseFrame);
            return true;
        });

        // ---- change detection (synchronous; uses _previousFrame) ----
        var changedCells = DetectChangedCells(frame);

        // ---- slice cells + local star counts + VLM crop encoding (all synchronous) ----
        var locals = new List<LocalCellResult>(UnitCellCount);
        var vlmCrops = new List<VlmCellInput>();
        var vlmTargetCells = new List<int>();
        IReadOnlyList<BoardHexSlot>? boardSlots = null;
        IReadOnlyList<BenchSlot>? benchSlots = null;

        try
        {
            boardSlots = _gridSlicer.SliceBoardHexagons(frame);
            benchSlots = _gridSlicer.SliceBenchSlots(frame);

            for (int i = 0; i < UnitCellCount; i++)
            {
                var crop = GetCrop(boardSlots, benchSlots, i);
                int star = crop is null || crop.Empty() ? 0 : _localRecognizer.CountStars(crop);
                bool occupied = star > 0;
                locals.Add(new LocalCellResult(i, star, occupied));

                if (occupied && changedCells.Contains(i) && crop is not null && !crop.Empty())
                {
                    vlmTargetCells.Add(i);
                    vlmCrops.Add(new VlmCellInput(i, crop.ImEncode(".png"), "image/png"));
                }
            }
        }
        finally
        {
            if (boardSlots is not null)
                foreach (var slot in boardSlots)
                    slot.Dispose();
            if (benchSlots is not null)
                foreach (var slot in benchSlots)
                    slot.Dispose();
        }

        // ---- VLM semantic pass (only changed + occupied cells), version gated ----
        var vlmVerdicts = new Dictionary<int, VlmCellVerdict>();
        var resolved = new Dictionary<int, (string? Name, double Confidence, bool Degraded)>(UnitCellCount);

        if (vlmCrops.Count > 0)
        {
            bool vlmStepped = false;
            VlmRecognitionResult vlmResult = new(Array.Empty<VlmCellVerdict>());
            try
            {
                vlmResult = await _vlmAdapter.RecognizeAsync(vlmCrops, ct).ConfigureAwait(false);
                vlmStepped = true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // VLM unavailable: fall through to degradation below.
            }

            bool stale = System.Threading.Interlocked.Read(ref _frameVersion) != myVersion;

            foreach (var verdict in vlmResult.Verdicts)
            {
                vlmVerdicts[verdict.CellIndex] = verdict;
            }

            foreach (var index in vlmTargetCells)
            {
                if (stale)
                {
                    // A newer frame started while this VLM call was in flight: the result
                    // is obsolete and must never backfill into this or any later frame.
                    resolved[index] = (null, 0.0, true);
                    continue;
                }

                bool degraded = !vlmStepped;
                string? name = null;
                double confidence = 0.0;

                if (!degraded &&
                    vlmVerdicts.TryGetValue(index, out var verdict) &&
                    !string.IsNullOrWhiteSpace(verdict.Name))
                {
                    name = verdict.Name;
                    confidence = verdict.Confidence;
                }
                else
                {
                    degraded = true;
                    if (_knownNames.TryGetValue(index, out var carried))
                    {
                        name = carried;
                        confidence = 1.0;
                    }
                }

                resolved[index] = (name, confidence, degraded);
            }
        }

        // ---- carry forward unchanged occupied cells + settle empty cells ----
        foreach (var local in locals)
        {
            if (resolved.ContainsKey(local.CellIndex))
                continue;

            if (local.IsOccupied)
            {
                if (_knownNames.TryGetValue(local.CellIndex, out var carried))
                    resolved[local.CellIndex] = (carried, 1.0, false);
                else
                    resolved[local.CellIndex] = (null, 0.0, true);
            }
            else
            {
                resolved[local.CellIndex] = (null, 0.0, false);
            }
        }

        // ---- fusion ----
        var fusedVerdicts = _fusion.Fuse(
            locals,
            resolved
                .Where(kv => kv.Value.Name is not null)
                .Select(kv => new VlmCellVerdict(kv.Key, kv.Value.Name, null, 1.0, SourceTier.T2))
                .ToList());

        var degradedCells = new HashSet<int>(
            resolved.Where(kv => kv.Value.Degraded).Select(kv => kv.Key));

        var cellByIndex = fusedVerdicts.ToDictionary(v => v.CellIndex);

        var boardCells = new List<UnitCell>(BoardCellCount);
        var benchCells = new List<UnitCell>(BenchCellCount);

        for (int i = 0; i < UnitCellCount; i++)
        {
            var cell = cellByIndex.TryGetValue(i, out var verdict)
                ? MapCell(verdict, degradedCells.Contains(i))
                : MapCell(new FusionVerdict(i, null, locals[i].Star, 0.0, SourceTier.T0, 0), degradedCells.Contains(i));

            if (i < BoardCellCount)
                boardCells.Add(cell);
            else
                benchCells.Add(cell);
        }

        // ---- assemble ----
        var result = new RecognitionFrame
        {
            SourceTier = ComputeFrameTier(cellByIndex.Values, degradedCells, shopCards),
            Confidence = ComputeFrameConfidence(cellByIndex.Values),
            Timestamp = DateTime.UtcNow.ToString("O"),
            Gold = gold,
            Level = 0,
            Stage = stage,
            Phase = phase,
            Hp = 0,
            Exp = 0,
            BoardCells = boardCells,
            BenchCells = benchCells,
            ShopCards = shopCards,
        };

        // ---- commit state (version gated) ----
        bool staleCommit = System.Threading.Interlocked.Read(ref _frameVersion) != myVersion;
        if (!staleCommit)
        {
            foreach (var kv in resolved)
            {
                if (kv.Value.Name is not null)
                    _knownNames[kv.Key] = kv.Value.Name;
                else
                    _knownNames.Remove(kv.Key);
            }

            _previousFrame?.Dispose();
            _previousFrame = frame.Clone();
        }

        return result;
    }

    public RecognitionFrame Recognize(Mat frame) =>
        RecognizeAsync(frame).GetAwaiter().GetResult();

    private static T TryReadDeterministic<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch
        {
            return default!;
        }
    }

    private IReadOnlyList<int> DetectChangedCells(Mat current)
    {
        if (_previousFrame is null)
            return Enumerable.Range(0, UnitCellCount).ToList();

        try
        {
            return _changeDetector.DetectChangedCells(_previousFrame, current)
                .Where(i => i is >= 0 and < UnitCellCount)
                .Distinct()
                .ToList();
        }
        catch
        {
            // Dimension mismatch or detector failure: treat everything as changed.
            return Enumerable.Range(0, UnitCellCount).ToList();
        }
    }

    private static Mat? GetCrop(IReadOnlyList<BoardHexSlot> board, IReadOnlyList<BenchSlot> bench, int flatIndex)
    {
        if (flatIndex < BoardCellCount)
        {
            if (board is null || flatIndex >= board.Count)
                return null;
            return board[flatIndex].CellImage;
        }

        int benchIndex = flatIndex - BoardCellCount;
        if (bench is null || benchIndex >= bench.Count)
            return null;
        return bench[benchIndex].CellImage;
    }

    private IReadOnlyList<ShopCard> BuildShopCards(Mat frame)
    {
        var detected = _ocr.RecognizeShopCards(frame);
        var bySlot = new Dictionary<int, DetectedShopCard>();
        foreach (var card in detected)
            bySlot[card.Slot] = card;

        var cards = new List<ShopCard>(5);
        for (int i = 0; i < 5; i++)
        {
            if (bySlot.TryGetValue(i, out var card))
                cards.Add(new ShopCard(card.Name, card.Cost, card.Confidence, SourceTier.T1));
            else
                cards.Add(new ShopCard(string.Empty, 0, 0.0, SourceTier.T0));
        }

        return cards;
    }

    private static UnitCell MapCell(FusionVerdict verdict, bool degraded)
    {
        var tier = degraded ? SourceTier.T3 : (verdict.Name is null ? SourceTier.T0 : SourceTier.T2);
        return new UnitCell(
            verdict.Name,
            verdict.Star,
            Array.Empty<ItemStack>(),
            verdict.Name is not null ? verdict.Confidence : 0.0,
            tier);
    }

    private static SourceTier ComputeFrameTier(
        IEnumerable<FusionVerdict> verdicts,
        IReadOnlySet<int> degradedCells,
        IReadOnlyList<ShopCard> shopCards)
    {
        if (degradedCells.Count > 0)
            return SourceTier.T3;
        if (verdicts.Any(v => v.Name is not null))
            return SourceTier.T2;
        if (shopCards.Any(c => !string.IsNullOrWhiteSpace(c.Name)))
            return SourceTier.T1;
        return SourceTier.T0;
    }

    private static double ComputeFrameConfidence(IEnumerable<FusionVerdict> verdicts)
    {
        var list = verdicts.Where(v => v.Name is not null).ToList();
        return list.Count == 0 ? 0.0 : list.Average(v => v.Confidence);
    }
}