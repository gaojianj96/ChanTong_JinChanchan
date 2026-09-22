using ChanSight.Core.Annotation;
using ChanSight.Core.FrameStorage;
using ChanSight.Overlay.Models;
using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using OpenCvSharp;

namespace ChanSight.Overlay.Services;

/// <summary>
/// 回放重模拟核心服务: 载入已录制的关键帧序列并逐帧回放; 每帧识别结果优先直读缓存,
/// 绝不自动重调 VLM(避免 token 成本); 仅当用户手动点帧触发时才调
/// <see cref="ManualFrameVlmService"/> 重识别并把结果写回缓存。
/// </summary>
public sealed class ReplaySimService
{
    private const string ReplayDirectoryName = "replay";

    private readonly FrameArchive _archive;
    private readonly ManualFrameVlmService _vlm;
    private readonly AnnotationStore _annotations;
    private readonly string _replayDirectory;

    public ReplaySimService(
        FrameArchive archive,
        ManualFrameVlmService vlm,
        AnnotationStore annotations,
        string? replayDirectory = null)
    {
        _archive = archive ?? throw new ArgumentNullException(nameof(archive));
        _vlm = vlm ?? throw new ArgumentNullException(nameof(vlm));
        _annotations = annotations ?? throw new ArgumentNullException(nameof(annotations));
        _replayDirectory = replayDirectory ?? Path.Combine(_archive.RootDirectory, ReplayDirectoryName);
    }

    /// <summary>该局已持久化的关键帧列表(按 SnapshotVersion 升序), 透传 <see cref="FrameArchive.ListKeyFrames"/>。</summary>
    public IReadOnlyList<FrameKey> ListFrames(string matchId) => _archive.ListKeyFrames(matchId);

    /// <summary>
    /// 读回单帧图 + 布局元数据 + 识别结果。reRecognize=false 时直读缓存(无结果则
    /// HasRecognition=false 且不抛); reRecognize=true 时同步调 VLM(己方视角) 重识别并写回缓存。
    /// </summary>
    public ReplayFrame Load(string matchId, string frameId, bool reRecognize = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(matchId);
        ArgumentException.ThrowIfNullOrWhiteSpace(frameId);

        var (image, meta) = _archive.LoadKeyFrameWithMeta(matchId, frameId);
        var key = ResolveKey(matchId, frameId, meta);

        if (reRecognize)
        {
            try
            {
                var recognition = _vlm.RecognizeAsync(image, isSelf: true, matchId: matchId, source: "replay").GetAwaiter().GetResult();
                WriteRecognition(matchId, frameId, recognition);
                return new ReplayFrame(key, image, meta, recognition, HasRecognition: true);
            }
            catch
            {
                image.Dispose();
                throw;
            }
        }

        var cached = ReadRecognition(matchId, frameId);
        return new ReplayFrame(key, image, meta, cached, cached is not null);
    }

    /// <summary>手动点帧触发的单帧重识别: 调 VLM(isSelf 由调用方指定), 结果写回缓存并返回。不影响其它帧。</summary>
    public async Task<RecognitionFrame> ReRecognizeAsync(
        string matchId,
        string frameId,
        bool isSelf,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(matchId);
        ArgumentException.ThrowIfNullOrWhiteSpace(frameId);

        var image = _archive.LoadKeyFrame(matchId, frameId);
        try
        {
            var recognition = await _vlm.RecognizeAsync(image, isSelf, ct, matchId, "replay").ConfigureAwait(false);
            WriteRecognition(matchId, frameId, recognition);
            return recognition;
        }
        finally
        {
            image.Dispose();
        }
    }

    private FrameKey ResolveKey(string matchId, string frameId, IReadOnlyDictionary<string, string>? meta)
    {
        var key = _archive
            .ListKeyFrames(matchId)
            .FirstOrDefault(k => string.Equals(k.FrameId, frameId, StringComparison.Ordinal));

        if (key is not null)
        {
            return key;
        }

        long version = 0;
        if (meta is not null && meta.TryGetValue("snapshotVersion", out var value) && long.TryParse(value, out var parsed))
        {
            version = parsed;
        }

        return new FrameKey(matchId, frameId, version);
    }

    private string RecognitionPath(string matchId, string frameId)
        => Path.Combine(_replayDirectory, matchId, $"{frameId}.recognition.json");

    private RecognitionFrame? ReadRecognition(string matchId, string frameId)
    {
        var path = RecognitionPath(matchId, frameId);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return RecognitionFrame.Deserialize(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    private void WriteRecognition(string matchId, string frameId, RecognitionFrame recognition)
    {
        var path = RecognitionPath(matchId, frameId);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, RecognitionFrame.Serialize(recognition));
    }
}