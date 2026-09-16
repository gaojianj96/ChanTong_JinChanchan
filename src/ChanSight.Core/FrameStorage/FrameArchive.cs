namespace ChanSight.Core.FrameStorage;

using System.Text.Json;
using ChanSight.Core.Engine;
using OpenCvSharp;

public sealed class FrameArchive
{
    private const string FramesDirectoryName = "frames";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _rootDirectory;
    private readonly string _framesDirectory;
    private readonly HashSet<string> _lockedMatches = new(StringComparer.Ordinal);

    public FrameArchive(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        _rootDirectory = Path.GetFullPath(rootDirectory);
        _framesDirectory = Path.Combine(_rootDirectory, FramesDirectoryName);
        Directory.CreateDirectory(_framesDirectory);
    }

    public string RootDirectory => _rootDirectory;

    public string StoreKeyFrame(
        Mat frame,
        string matchId,
        long snapshotVersion,
        IReadOnlyDictionary<string, string>? layoutMeta = null)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentException.ThrowIfNullOrWhiteSpace(matchId);

        var frameId = FrameKey.ComputeFrameId(frame);
        var matchDirectory = Path.Combine(_framesDirectory, matchId);
        var pngPath = Path.Combine(matchDirectory, $"{frameId}.png");
        var metaPath = Path.Combine(matchDirectory, $"{frameId}.json");

        if (File.Exists(pngPath) && File.Exists(metaPath))
        {
            return frameId;
        }

        Directory.CreateDirectory(matchDirectory);

        var encoded = frame.ImEncode(".png");
        File.WriteAllBytes(pngPath, encoded);

        var meta = new FrameLayoutMeta(frameId, matchId, snapshotVersion, frame.Cols, frame.Rows, layoutMeta);
        File.WriteAllText(metaPath, JsonSerializer.Serialize(meta, JsonOptions));

        return frameId;
    }

    public bool ShouldPersist(GameStateSnapshot prev, GameStateSnapshot next)
    {
        ArgumentNullException.ThrowIfNull(prev);
        ArgumentNullException.ThrowIfNull(next);

        if (!string.Equals(prev.Stage, next.Stage, StringComparison.Ordinal)) return true;
        if (prev.Gold != next.Gold) return true;
        if (prev.Level != next.Level) return true;
        if (prev.Exp != next.Exp) return true;
        if (prev.Hp != next.Hp) return true;
        if (!SequenceEquals(prev.ShopCards, next.ShopCards)) return true;
        if (!SequenceEquals(prev.BoardUnits, next.BoardUnits)) return true;
        if (!SequenceEquals(prev.BenchUnits, next.BenchUnits)) return true;

        return false;
    }

    public bool IsKeyFrame(string matchId, string frameId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(matchId);
        ArgumentException.ThrowIfNullOrWhiteSpace(frameId);

        return File.Exists(GetPngPath(matchId, frameId));
    }

    public Mat LoadKeyFrame(string matchId, string frameId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(matchId);
        ArgumentException.ThrowIfNullOrWhiteSpace(frameId);

        var pngPath = GetPngPath(matchId, frameId);
        if (!File.Exists(pngPath))
        {
            throw new FileNotFoundException("Key frame not found.", pngPath);
        }

        return Cv2.ImRead(pngPath, ImreadModes.Color);
    }

    public (Mat Frame, IReadOnlyDictionary<string, string>? Meta) LoadKeyFrameWithMeta(string matchId, string frameId)
    {
        var frame = LoadKeyFrame(matchId, frameId);
        var metaPath = GetMetaPath(matchId, frameId);

        if (!File.Exists(metaPath))
        {
            return (frame, null);
        }

        var stored = JsonSerializer.Deserialize<FrameLayoutMeta>(File.ReadAllText(metaPath), JsonOptions);
        if (stored is null)
        {
            return (frame, null);
        }

        var meta = new Dictionary<string, string>
        {
            ["frameId"] = stored.FrameId,
            ["matchId"] = stored.MatchId,
            ["snapshotVersion"] = stored.SnapshotVersion.ToString(),
            ["width"] = stored.Width.ToString(),
            ["height"] = stored.Height.ToString()
        };

        if (stored.Layout is not null)
        {
            foreach (var (key, value) in stored.Layout)
            {
                meta[key] = value;
            }
        }

        return (frame, meta);
    }

    public IReadOnlyList<FrameKey> ListKeyFrames(string matchId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(matchId);

        var matchDirectory = Path.Combine(_framesDirectory, matchId);
        if (!Directory.Exists(matchDirectory))
        {
            return Array.Empty<FrameKey>();
        }

        var keys = new List<FrameKey>();
        foreach (var metaPath in Directory.EnumerateFiles(matchDirectory, "*.json"))
        {
            var stored = JsonSerializer.Deserialize<FrameLayoutMeta>(File.ReadAllText(metaPath), JsonOptions);
            if (stored is not null)
            {
                keys.Add(new FrameKey(stored.MatchId, stored.FrameId, stored.SnapshotVersion));
            }
        }

        return keys.OrderBy(key => key.SnapshotVersion).ToList();
    }

    public void PruneOldMatches(int keepLatestN)
    {
        if (keepLatestN < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(keepLatestN));
        }

        if (!Directory.Exists(_framesDirectory))
        {
            return;
        }

        var matchDirectories = Directory.EnumerateDirectories(_framesDirectory)
            .Select(path => new MatchDirectory(path, Path.GetFileName(path), Directory.GetLastWriteTimeUtc(path)))
            .Where(directory => !string.IsNullOrEmpty(directory.Name))
            .OrderByDescending(directory => directory.LastWriteUtc)
            .ToList();

        var keep = new HashSet<string>(StringComparer.Ordinal);
        var kept = 0;

        foreach (var directory in matchDirectories)
        {
            if (_lockedMatches.Contains(directory.Name))
            {
                keep.Add(directory.Name);
                continue;
            }

            if (kept < keepLatestN)
            {
                keep.Add(directory.Name);
                kept++;
            }
        }

        foreach (var directory in matchDirectories)
        {
            if (!keep.Contains(directory.Name))
            {
                Directory.Delete(directory.Path, recursive: true);
            }
        }
    }

    public void Lock(string matchId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(matchId);
        _lockedMatches.Add(matchId);
    }

    public void Unlock(string matchId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(matchId);
        _lockedMatches.Remove(matchId);
    }

    private string GetPngPath(string matchId, string frameId)
        => Path.Combine(_framesDirectory, matchId, $"{frameId}.png");

    private string GetMetaPath(string matchId, string frameId)
        => Path.Combine(_framesDirectory, matchId, $"{frameId}.json");

    private static bool SequenceEquals<T>(IReadOnlyList<T>? left, IReadOnlyList<T>? right)
        where T : class
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return left.SequenceEqual(right);
    }

    private sealed record MatchDirectory(string Path, string Name, DateTime LastWriteUtc);
}

internal sealed record FrameLayoutMeta(
    string FrameId,
    string MatchId,
    long SnapshotVersion,
    int Width,
    int Height,
    IReadOnlyDictionary<string, string>? Layout);