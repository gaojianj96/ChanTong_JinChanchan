using System.Text;
using System.Text.Json;
using ChanSight.Core.Engine;
using ChanSight.Vision.Models;

namespace ChanSight.Vision.Services;

/// <summary>
/// Append-only file logger for the recognition chain. Writes one JSON object per line
/// (JSONL) so the debug trail of "what the VLM saw" and "what each frame recognized"
/// can be inspected later. Writes are synchronous and guarded by a single lock, which is
/// safe on the 2fps hot path; set <see cref="Enabled"/> to false to silence it. No
/// heavyweight logging framework — just System.IO.
/// </summary>
public sealed class RecognitionLogger
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly object _gate = new();
    private readonly string _directory;
    private readonly bool _enabled;

    public RecognitionLogger(string? logDirectory = null, bool enabled = true)
    {
        _enabled = enabled;
        _directory = Path.GetFullPath(
            logDirectory ?? Path.Combine(AppContext.BaseDirectory, "logs", "recognition"));
    }

    public bool Enabled => _enabled;

    public string LogDirectory => _directory;

    public string GetVlmLogPath(string matchId) => CombinePath("vlm", matchId);

    public string GetFrameLogPath(string matchId) => CombinePath("frame", matchId);

    /// <summary>
    /// Writes one VLM whole-frame recognition record: every parsed field plus the issues
    /// collected during parsing/validation.
    /// </summary>
    public void LogVlmRecognition(string matchId, string source, RecognitionFrame frame, IReadOnlyList<string> issues)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var payload = new Dictionary<string, object?>
        {
            ["type"] = "vlm",
            ["timestamp"] = DateTimeOffset.UtcNow.ToString("o"),
            ["matchId"] = matchId,
            ["source"] = source,
            ["perspective"] = frame.Perspective,
            ["opponentIndex"] = frame.OpponentIndex,
            ["playerName"] = frame.PlayerName,
            ["stage"] = frame.Stage,
            ["gold"] = frame.Gold,
            ["level"] = frame.Level,
            ["hp"] = frame.Hp,
            ["exp"] = frame.Exp,
            ["goldEstimate"] = frame.GoldEstimate,
            ["confidence"] = frame.Confidence,
            ["sourceTier"] = frame.SourceTier.ToString(),
            ["boardCellCount"] = frame.BoardCells.Count,
            ["boardOccupiedCount"] = frame.BoardCells.Count(c => !string.IsNullOrWhiteSpace(c.Name)),
            ["benchCellCount"] = frame.BenchCells.Count,
            ["benchOccupiedCount"] = frame.BenchCells.Count(c => !string.IsNullOrWhiteSpace(c.Name)),
            ["board"] = frame.BoardCells.Select(ToCellEntry).ToArray(),
            ["bench"] = frame.BenchCells.Select(ToCellEntry).ToArray(),
            ["issues"] = (issues ?? Array.Empty<string>()).ToArray(),
        };

        AppendLine(GetVlmLogPath(matchId), payload);
    }

    /// <summary>
    /// Writes one frame-recognition record: the game-state snapshot plus the frame id /
    /// snapshot version / source identifying where the recognition came from.
    /// </summary>
    public void LogFrameRecognition(string matchId, string frameId, long snapshotVersion, GameStateSnapshot state, string source)
    {
        ArgumentNullException.ThrowIfNull(state);

        var payload = new Dictionary<string, object?>
        {
            ["type"] = "frame",
            ["timestamp"] = DateTimeOffset.UtcNow.ToString("o"),
            ["matchId"] = matchId,
            ["frameId"] = frameId,
            ["snapshotVersion"] = snapshotVersion,
            ["source"] = source,
            ["stage"] = state.Stage,
            ["phase"] = state.Phase.ToString(),
            ["gold"] = state.Gold,
            ["level"] = state.Level,
            ["hp"] = state.Hp,
            ["exp"] = state.Exp,
            ["streak"] = state.Streak,
            ["playerName"] = state.PlayerName,
            ["boardUnitCount"] = state.BoardUnits.Count,
            ["boardOccupiedCount"] = state.BoardUnits.Count(u => !string.IsNullOrWhiteSpace(u.Name)),
            ["benchUnitCount"] = state.BenchUnits.Count,
            ["benchOccupiedCount"] = state.BenchUnits.Count(u => !string.IsNullOrWhiteSpace(u.Name)),
            ["shopCount"] = state.ShopCards.Count,
            ["opponentCount"] = state.Opponents.Count,
        };

        AppendLine(GetFrameLogPath(matchId), payload);
    }

    private static object ToCellEntry(UnitCell cell) => new Dictionary<string, object?>
    {
        ["name"] = cell.Name,
        ["star"] = cell.Star,
        ["items"] = cell.Items.Select(i => i.IconId).ToArray(),
    };

    private string CombinePath(string kind, string matchId)
        => Path.Combine(_directory, $"{kind}-{Sanitize(matchId)}.jsonl");

    private static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0)
            {
                chars[i] = '_';
            }
        }

        return new string(chars);
    }

    private void AppendLine(string path, object payload)
    {
        if (!_enabled)
        {
            return;
        }

        var json = JsonSerializer.Serialize(payload, JsonOptions);

        lock (_gate)
        {
            Directory.CreateDirectory(_directory);
            File.AppendAllText(path, json + Environment.NewLine, Encoding.UTF8);
        }
    }
}
