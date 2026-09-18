using ChanSight.Core.FrameStorage;
using ChanSight.Vision.Models;
using OpenCvSharp;

namespace ChanSight.Overlay.Models;

/// <summary>
/// 回放窗口一次加载的产物: 关键帧原图 + 布局元数据 + 该帧识别结果(可能缺失)。
/// FrameImage 为独立读回的 Mat, 调用方负责 Dispose。HasRecognition 区分「已识别(直读缓存)」与「未识别」。
/// </summary>
public sealed record ReplayFrame(
    FrameKey Key,
    Mat FrameImage,
    IReadOnlyDictionary<string, string>? Meta,
    RecognitionFrame? Recognition,
    bool HasRecognition);