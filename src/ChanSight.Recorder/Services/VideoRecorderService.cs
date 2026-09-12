using System.Text.Json;
using ChanSight.Core.Interfaces;
using ChanSight.Core.Models;
using Microsoft.Extensions.Logging;

namespace ChanSight.Recorder.Services;

public sealed class VideoRecorderService : IVideoRecorder
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<VideoRecorderService> _logger;
    private SessionMeta? _session;
    private IFrameSource? _frameSource;
    private CancellationTokenSource? _cts;
    private Task? _recordingTask;
    private bool _paused;
    private bool _disposed;

    public bool IsRecording => _session is not null && _recordingTask is not null && !_recordingTask.IsCompleted;

    public bool IsPaused => _paused;

    public SessionMeta? CurrentSession => _session;

    public VideoRecorderService(ILogger<VideoRecorderService> logger)
        : this(new FileSystem(), logger)
    {
    }

    internal VideoRecorderService(IFileSystem fileSystem, ILogger<VideoRecorderService> logger)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ValueTask StartAsync(SessionMeta session, IFrameSource frameSource, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsRecording)
            throw new InvalidOperationException("Recording is already in progress.");

        _session = session ?? throw new ArgumentNullException(nameof(session));
        _frameSource = frameSource ?? throw new ArgumentNullException(nameof(frameSource));
        _paused = false;

        _fileSystem.CreateDirectory(session.OutputDirectory);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _recordingTask = RecordFramesAsync(_cts.Token);

        _logger.LogInformation("Recording started: {Name} -> {Dir}", session.Name, session.OutputDirectory);
        return ValueTask.CompletedTask;
    }

    public ValueTask PauseAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsRecording)
            throw new InvalidOperationException("No recording in progress.");

        _paused = true;
        _logger.LogInformation("Recording paused");
        return ValueTask.CompletedTask;
    }

    public ValueTask ResumeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsRecording)
            throw new InvalidOperationException("No recording in progress.");

        _paused = false;
        _logger.LogInformation("Recording resumed");
        return ValueTask.CompletedTask;
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_session is null)
            return;

        _cts?.Cancel();

        if (_recordingTask is not null)
        {
            try { await _recordingTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        await WriteMetaAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Recording stopped: {Name}", _session.Name);
        _session = null;
        _frameSource = null;
        _recordingTask = null;
        _paused = false;
    }

    private async Task RecordFramesAsync(CancellationToken cancellationToken)
    {
        var frameCount = 0L;
        var reader = _frameSource!.Frames;
        try
        {
            while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (reader.TryRead(out var frame))
                {
                    if (_paused)
                    {
                        frame.Dispose();
                        continue;
                    }

                    frameCount++;
                    var fileName = $"frame_{frameCount:D8}.png";
                    var filePath = Path.Combine(_session!.OutputDirectory, fileName);
                    var bytes = frame.Image.ImEncode(".png");
                    await _fileSystem.WriteAllBytesAsync(filePath, bytes, CancellationToken.None).ConfigureAwait(false);
                    frame.Dispose();
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task WriteMetaAsync(CancellationToken cancellationToken)
    {
        if (_session is null)
            return;

        var meta = new
        {
            sessionId = _session.Id.ToString(),
            name = _session.Name,
            outputDirectory = _session.OutputDirectory,
            target = new
            {
                hwnd = _session.Target.Hwnd.ToString(),
                title = _session.Target.Title
            },
            startedAt = _session.StartedAt.ToString("O"),
            targetFps = _session.TargetFps,
            resolution = new
            {
                width = _session.Resolution.Width,
                height = _session.Resolution.Height
            }
        };

        var json = JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true });
        var metaPath = Path.Combine(_session.OutputDirectory, "meta.json");
        await _fileSystem.WriteAllTextAsync(metaPath, json, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        await StopAsync().ConfigureAwait(false);
        _disposed = true;
        _cts?.Dispose();
    }
}