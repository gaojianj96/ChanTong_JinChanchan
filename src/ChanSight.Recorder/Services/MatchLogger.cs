using System.Text;
using System.Text.Json;

namespace ChanSight.Recorder.Services;

public sealed class MatchLogger
{
    private const string BaseDirectory = "manifests";
    private const int FlushThresholdBytes = 64 * 1024;
    private static readonly JsonSerializerOptions SerializerOptions = new();

    private readonly IFileSystem _fileSystem;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, GameLog> _logs = new(StringComparer.Ordinal);

    public MatchLogger()
        : this(new FileSystem())
    {
    }

    internal MatchLogger(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public async Task AppendAsync(MatchEvent e, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(e);
        cancellationToken.ThrowIfCancellationRequested();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var log = await GetOrLoadAsync(e.GameId, cancellationToken).ConfigureAwait(false);

            if (e.Seq <= log.MaxSeq)
            {
                return;
            }

            log.MaxSeq = e.Seq;
            log.Pending.Append(JsonSerializer.Serialize(e, SerializerOptions)).Append('\n');

            if (log.Pending.Length >= FlushThresholdBytes)
            {
                await FlushGameAsync(e.GameId, log, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var (gameId, log) in _logs)
            {
                await FlushGameAsync(gameId, log, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<GameLog> GetOrLoadAsync(string gameId, CancellationToken cancellationToken)
    {
        if (_logs.TryGetValue(gameId, out var existing))
        {
            return existing;
        }

        var log = new GameLog();
        var path = GetPath(gameId);
        var content = await _fileSystem.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(content))
        {
            foreach (var line in content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var seq = ReadSeq(line);
                if (seq > log.MaxSeq)
                {
                    log.MaxSeq = seq;
                }
            }
        }

        _logs[gameId] = log;
        return log;
    }

    private async Task FlushGameAsync(string gameId, GameLog log, CancellationToken cancellationToken)
    {
        if (log.Pending.Length == 0)
        {
            return;
        }

        var path = GetPath(gameId);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !_fileSystem.DirectoryExists(directory))
        {
            _fileSystem.CreateDirectory(directory);
        }

        var existing = await _fileSystem.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        var combined = string.IsNullOrEmpty(existing)
            ? log.Pending.ToString()
            : existing + log.Pending.ToString();
        await _fileSystem.WriteAllTextAsync(path, combined, cancellationToken).ConfigureAwait(false);
        log.Pending.Clear();
    }

    private static long ReadSeq(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            if (doc.RootElement.TryGetProperty("Seq", out var prop) && prop.TryGetInt64(out var seq))
            {
                return seq;
            }
        }
        catch (JsonException)
        {
        }

        return 0;
    }

    private static string GetPath(string gameId) => Path.Combine(BaseDirectory, gameId, "events.jsonl");

    private sealed class GameLog
    {
        public StringBuilder Pending { get; } = new();

        public long MaxSeq { get; set; }
    }
}
