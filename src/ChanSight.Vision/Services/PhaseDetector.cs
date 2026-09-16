using System.Text.Json;
using System.Text.RegularExpressions;
using ChanSight.Core.Engine;
using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;

namespace ChanSight.Vision.Services;

/// <summary>
/// Detects the game phase from a recognition frame. <see cref="Detect"/> is a
/// pure local heuristic (zero network, deterministic): shop visibility wins
/// (Planning), then a PvE round match, then defaults to Combat. <see cref="DetectAsync"/>
/// tries the VLM first and falls back to the heuristic on any failure.
/// </summary>
public sealed class PhaseDetector : IPhaseDetector
{
    private static readonly Regex StageRoundPattern = new(@"^\s*(\d+)\s*[-–—]\s*(\d+)\s*$", RegexOptions.Compiled);

    private static readonly IReadOnlySet<string> DefaultPveRounds = new HashSet<string>(StringComparer.Ordinal)
    {
        "1-1", "1-2", "1-3", "1-4", "2-6", "3-6", "4-6", "5-6", "6-6", "7-6",
    };

    private static readonly string[] CarouselKeywords = { "选秀", "carousel" };

    private readonly IVlmClient? _vlm;
    private readonly IReadOnlySet<string> _pveRounds;

    public PhaseDetector(IVlmClient? vlm = null, IReadOnlySet<string>? pveRounds = null)
    {
        if (pveRounds is not null && pveRounds.Count > 0)
            _pveRounds = pveRounds;
        else
            _pveRounds = DefaultPveRounds;
        _vlm = vlm;
    }

    public GamePhase Detect(RecognitionFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (frame.Phase != GamePhase.Planning && Enum.IsDefined(frame.Phase))
            return frame.Phase;

        if (IsShopVisible(frame))
            return GamePhase.Planning;

        var stage = CleanStage(frame.Stage);
        if (stage is not null && IsCarouselSignal(stage))
            return GamePhase.Carousel;

        if (stage is not null && IsPveRound(stage))
            return GamePhase.PvE;

        return GamePhase.Combat;
    }

    public async Task<GamePhase> DetectAsync(RecognitionFrame frame, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (_vlm is null)
            return Detect(frame);

        try
        {
            var prompt = BuildPrompt(frame);
            var raw = await _vlm.CompleteAsync(prompt, Array.Empty<(string, byte[])>(), ct).ConfigureAwait(false);
            if (TryParsePhase(raw, out var phase))
                return phase;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // degrade to heuristic on any VLM failure
        }

        return Detect(frame);
    }

    public static bool TryParsePhase(string? json, out GamePhase phase)
    {
        phase = default;

        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            if (!doc.RootElement.TryGetProperty("phase", out var phaseElement) ||
                phaseElement.ValueKind != JsonValueKind.String)
                return false;

            var value = phaseElement.GetString();
            if (value is null)
                return false;

            if (!Enum.TryParse<GamePhase>(value, ignoreCase: true, out var parsed) ||
                !Enum.IsDefined(parsed))
                return false;

            phase = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string BuildPrompt(RecognitionFrame frame) =>
        "You are a TFT game-phase classifier. Given a recognition frame, decide the current " +
        "phase from: Planning, Combat, Carousel, PvE.\n" +
        $"stage: \"{frame.Stage}\"\n" +
        $"shop cards (name|cost): {DescribeShop(frame.ShopCards)}\n" +
        "Reply with exactly one JSON object: {\"phase\":\"<one of Planning|Combat|Carousel|PvE>\"}.";

    private static string DescribeShop(IReadOnlyList<ShopCard> cards)
    {
        if (cards is null || cards.Count == 0)
            return "none";
        var described = cards.Where(c => c is not null)
            .Select(c => $"{c.Name ?? "?"}({c.Cost})");
        return string.Join(", ", described);
    }

    private static bool IsShopVisible(RecognitionFrame frame)
    {
        if (frame.ShopCards is null || frame.ShopCards.Count == 0)
            return false;

        return frame.ShopCards.Any(c => c is not null &&
            (!string.IsNullOrWhiteSpace(c.Name) || c.Cost > 0));
    }

    private static string? CleanStage(string? stage)
    {
        if (string.IsNullOrWhiteSpace(stage))
            return null;

        var cleaned = PaddleOcrService.CleanStageText(stage);
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }

    private bool IsPveRound(string stage)
    {
        var match = StageRoundPattern.Match(stage);
        if (!match.Success)
            return false;

        // Rebuild the canonical "<n>-<m>" form from parsed ints to drop any
        // whitespace/separator variance before comparing against the round set.
        var canonical = $"{int.Parse(match.Groups[1].Value)}-{int.Parse(match.Groups[2].Value)}";
        return _pveRounds.Contains(canonical);
    }

    private static bool IsCarouselSignal(string stage)
    {
        var lower = stage.ToLowerInvariant();
        return CarouselKeywords.Any(k => lower.Contains(k));
    }
}