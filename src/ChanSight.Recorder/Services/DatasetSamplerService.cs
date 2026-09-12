using System.Text.Json;
using ChanSight.Core.Interfaces;
using ChanSight.Core.Models;
using Microsoft.Extensions.Logging;

namespace ChanSight.Recorder.Services;

public sealed class DatasetSamplerService : IDatasetSampler
{
    private readonly DatasetSamplerOptions _options;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<DatasetSamplerService> _logger;
    private SessionMeta? _session;
    private IFrameSource? _frameSource;
    private CancellationTokenSource? _cts;
    private Task? _samplingTask;
    private readonly List<SampledImageEntry> _sampledImages = new();
    private CapturedFrame? _latestFrame;
    private readonly object _latestFrameLock = new();
    private bool _disposed;

    public DatasetSamplerService(DatasetSamplerOptions options, ILogger<DatasetSamplerService> logger)
        : this(options, new FileSystem(), logger)
    {
    }

    internal DatasetSamplerService(
        DatasetSamplerOptions options,
        IFileSystem fileSystem,
        ILogger<DatasetSamplerService> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options.Validate();
    }

    public ValueTask StartAsync(SessionMeta session, IFrameSource frameSource, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_session is not null)
            throw new InvalidOperationException("Sampling is already in progress.");

        _session = session ?? throw new ArgumentNullException(nameof(session));
        _frameSource = frameSource ?? throw new ArgumentNullException(nameof(frameSource));

        var outputDir = string.IsNullOrEmpty(_options.OutputDirectory)
            ? session.OutputDirectory
            : _options.OutputDirectory;
        _fileSystem.CreateDirectory(outputDir);
        _fileSystem.CreateDirectory(Path.Combine(outputDir, "snapshots"));

        _sampledImages.Clear();

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _samplingTask = SampleFramesAsync(_cts.Token);

        _logger.LogInformation("Dataset sampling started: {Name} -> {Dir}", session.Name, outputDir);
        return ValueTask.CompletedTask;
    }

    public async ValueTask TakeManualSnapshotAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_session is null || _frameSource is null)
            throw new InvalidOperationException("Sampling has not been started.");

        CapturedFrame? frame = null;

        if (_frameSource.Frames.TryRead(out var channelFrame))
        {
            frame = channelFrame;
        }
        else
        {
            lock (_latestFrameLock)
            {
                frame = _latestFrame;
                _latestFrame = null;
            }
        }

        if (frame is not null)
        {
            using (frame)
            {
                await SaveFrameAsync(frame, isManual: true, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_session is null)
            return;

        _cts?.Cancel();

        if (_samplingTask is not null)
        {
            try { await _samplingTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        await WriteDatasetIndexAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Dataset sampling stopped: {Name}", _session.Name);
        _session = null;
        _frameSource = null;
        _samplingTask = null;
        lock (_latestFrameLock)
        {
            _latestFrame?.Dispose();
            _latestFrame = null;
        }
        _sampledImages.Clear();
    }

    private async Task SampleFramesAsync(CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromSeconds(_options.IntervalSeconds);
        var lastSampleTime = DateTimeOffset.MinValue;
        var reader = _frameSource!.Frames;

        try
        {
            while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (reader.TryRead(out var frame))
                {
                    lock (_latestFrameLock)
                    {
                        _latestFrame?.Dispose();
                        _latestFrame = new CapturedFrame(frame.Image.Clone(), frame.Timestamp, frame.SequenceNumber);
                    }

                    var now = frame.Timestamp;
                    if (now - lastSampleTime >= interval)
                    {
                        lastSampleTime = now;
                        using (frame)
                        {
                            await SaveFrameAsync(frame, isManual: false, CancellationToken.None).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        frame.Dispose();
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task SaveFrameAsync(CapturedFrame frame, bool isManual, CancellationToken cancellationToken)
    {
        var ext = _options.ImageFormat.ToLowerInvariant() switch
        {
            "jpg" or "jpeg" => ".jpg",
            "png" => ".png",
            _ => ".jpg"
        };

        var prefix = isManual ? "manual" : "auto";
        var fileName = $"{prefix}_{frame.SequenceNumber:D8}_{frame.Timestamp:yyyyMMddHHmmssfff}{ext}";
        var subDir = isManual ? "snapshots" : string.Empty;
        var filePath = string.IsNullOrEmpty(subDir)
            ? Path.Combine(_session!.OutputDirectory, fileName)
            : Path.Combine(_session!.OutputDirectory, subDir, fileName);

        var bytes = frame.Image.ImEncode(ext);
        await _fileSystem.WriteAllBytesAsync(filePath, bytes, CancellationToken.None).ConfigureAwait(false);

        var entry = new SampledImageEntry
        {
            FileName = fileName,
            Timestamp = frame.Timestamp,
            SequenceNumber = frame.SequenceNumber,
            IsManual = isManual,
            Resolution = new FrameResolutionEntry
            {
                Width = frame.Resolution.Width,
                Height = frame.Resolution.Height
            }
        };
        _sampledImages.Add(entry);

        _logger.LogDebug("Sampled frame {Seq} -> {File}", frame.SequenceNumber, fileName);
    }

    private async Task WriteDatasetIndexAsync(CancellationToken cancellationToken)
    {
        if (_session is null)
            return;

        var index = new
        {
            sessionId = _session.Id.ToString(),
            name = _session.Name,
            intervalSeconds = _options.IntervalSeconds,
            imageFormat = _options.ImageFormat,
            totalSamples = _sampledImages.Count,
            samples = _sampledImages.Select(e => new
            {
                e.FileName,
                e.Timestamp,
                e.SequenceNumber,
                e.IsManual,
                resolution = new { e.Resolution.Width, e.Resolution.Height }
            }).ToList()
        };

        var json = JsonSerializer.Serialize(index, new JsonSerializerOptions { WriteIndented = true });
        var indexPath = Path.Combine(_session.OutputDirectory, "dataset_index.json");
        await _fileSystem.WriteAllTextAsync(indexPath, json, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        await StopAsync().ConfigureAwait(false);
        _disposed = true;
        _cts?.Dispose();
    }

    private sealed record SampledImageEntry
    {
        public string FileName { get; init; } = string.Empty;
        public DateTimeOffset Timestamp { get; init; }
        public long SequenceNumber { get; init; }
        public bool IsManual { get; init; }
        public FrameResolutionEntry Resolution { get; init; } = new();
    }

    private sealed record FrameResolutionEntry
    {
        public int Width { get; init; }
        public int Height { get; init; }
    }
}