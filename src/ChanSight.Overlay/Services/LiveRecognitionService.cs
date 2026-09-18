using ChanSight.Core.Engine;
using ChanSight.Core.FrameStorage;
using ChanSight.Core.Interfaces;
using ChanSight.Overlay.Models;
using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using OpenCvSharp;

namespace ChanSight.Overlay.Services;

/// <summary>
/// 后台实时识别宿主(非 UI 线程): 从 <see cref="IScreenCaptureService.FrameSource"/>
/// 持续取帧 → <see cref="RecognitionPipeline.RecognizeAsync(Mat, CancellationToken)"/>
/// (经 <see cref="FrameRecognitionFunc"/> 注入以便测试) → 通过
/// <see cref="RecognitionToGameStateAdapter"/> 更新 <see cref="GameStateManager"/> →
/// 按 <see cref="IFrameArchive.ShouldPersist"/> 决定是否 <see cref="IFrameArchive.StoreKeyFrame"/> →
/// 以 <see cref="FrameUpdated"/> 事件推送 <see cref="LiveFrameUpdate"/>。
///
/// 帧率由外部捕获(默认 500ms / 2fps)节流; 本循环按帧到达即处理, 不额外累加延迟,
/// 仅在帧源不可读时回读等待。
/// </summary>
public sealed class LiveRecognitionService : IAsyncDisposable
{
    public const string DefaultMatchId = "live";

    public delegate Task<RecognitionFrame> FrameRecognitionFunc(Mat frame, CancellationToken ct);

    private readonly IScreenCaptureService _capture;
    private readonly FrameRecognitionFunc _recognize;
    private readonly IFrameArchive _archive;
    private readonly RecognitionToGameStateAdapter _adapter;
    private readonly GameStateManager _manager;
    private readonly LatestFrameStore? _latestFrameStore;
    private readonly string _matchId;

    private CancellationTokenSource? _cts;
    private Task? _loop;

    public LiveRecognitionService(
        IScreenCaptureService capture,
        FrameRecognitionFunc recognize,
        IFrameArchive archive,
        RecognitionToGameStateAdapter adapter,
        GameStateManager manager,
        string? matchId = null,
        LatestFrameStore? latestFrameStore = null)
    {
        _capture = capture ?? throw new ArgumentNullException(nameof(capture));
        _recognize = recognize ?? throw new ArgumentNullException(nameof(recognize));
        _archive = archive ?? throw new ArgumentNullException(nameof(archive));
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _latestFrameStore = latestFrameStore;
        _matchId = string.IsNullOrWhiteSpace(matchId) ? DefaultMatchId : matchId;
    }

    public bool IsRunning => _loop is { IsCompleted: false };

    public string MatchId => _matchId;

    /// <summary>每次帧识别-适配-归档完成后触发。订阅方应尽快返回, 不在回调里做耗时/阻塞工作。</summary>
    public event Action<LiveFrameUpdate>? FrameUpdated;

    public void Start(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("Live recognition is already running.");
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loop = RunLoopAsync(_cts.Token);
    }

    public async Task StopAsync()
    {
        if (_cts is null)
        {
            return;
        }

        _cts.Cancel();
        try
        {
            await (_loop ?? Task.CompletedTask).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            _loop = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        await foreach (var captured in _capture.FrameSource.ReadAllAsync(ct).ConfigureAwait(false))
        {
            try
            {
                _latestFrameStore?.Store(captured.Image);
                var update = await ProcessFrameAsync(captured.Image, ct).ConfigureAwait(false);
                FrameUpdated?.Invoke(update);
            }
            finally
            {
                captured.Dispose();
            }
        }
    }

    /// <summary>
    /// 处理单帧: 识别 → 适配 → 更新 manager → 按需归档 → 产出 <see cref="LiveFrameUpdate"/>。
    /// 独立公开以便纯逻辑测试直接验证「ShouldPersist → StoreKeyFrame」调用关系。
    /// </summary>
    public async Task<LiveFrameUpdate> ProcessFrameAsync(Mat frame, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var recognition = await _recognize(frame, ct).ConfigureAwait(false);

        var previous = _manager.Current;
        _adapter.Apply(recognition);
        var snapshot = _manager.Current;

        string? frameId = null;
        if (_archive.ShouldPersist(previous, snapshot))
        {
            frameId = _archive.StoreKeyFrame(frame, _matchId, snapshot.Version);
        }

        return new LiveFrameUpdate(snapshot, recognition, frameId);
    }
}