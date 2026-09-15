using System.Text;
using System.Text.Json;

namespace ChanSight.Recorder.Services;

public sealed class MatchLogger
{
    private const string BaseDirectory = "manifests";
    private const int DefaultFlushThresholdBytes = 64 * 1024;
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = null };

    private readonly IFileSystem _fileSystem;
    private readonly string _baseDirectory;
    private readonly int _flushThresholdBytes;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, GameLog> _logs = new(StringComparer.Ordinal);

    public MatchLogger()
        : this(new FileSystem(), BaseDirectory, DefaultFlushThresholdBytes)
    {
    }

    internal MatchLogger(IFileSystem fileSystem, int flushThresholdBytes = DefaultFlushThresholdBytes)
        : this(fileSystem, BaseDirectory, flushThresholdBytes)
    {
    }

    internal MatchLogger(IFileSystem fileSystem, string baseDirectory, int flushThresholdBytes = DefaultFlushThresholdBytes)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _baseDirectory = baseDirectory ?? throw new ArgumentNullException(nameof(baseDirectory));
        if (flushThresholdBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(flushThresholdBytes));
        }

        _flushThresholdBytes = flushThresholdBytes;
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
            var line = JsonSerializer.Serialize(e, SerializerOptions);
            log.Pending.Add(line);
            log.PendingBytes += Encoding.UTF8.GetByteCount(line) + 1;

            if (log.PendingBytes >= _flushThresholdBytes)
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
        if (log.Pending.Count == 0)
        {
            return;
        }

        var path = GetPath(gameId);
        await _fileSystem.AppendAllLinesAsync(path, log.Pending, cancellationToken).ConfigureAwait(false);
        log.Pending.Clear();
        log.PendingBytes = 0;
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

    private string GetPath(string gameId) => Path.Combine(_baseDirectory, gameId, "events.jsonl");

    private sealed class GameLog
    {
        public List<string> Pending { get; } = new();

        public long PendingBytes { get; set; }

        public long MaxSeq { get; set; }
    }
}