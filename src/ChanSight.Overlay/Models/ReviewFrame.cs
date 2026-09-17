using ChanSight.Vision.Models;
using OpenCvSharp;

namespace ChanSight.Overlay.Models;

/// <summary>
/// 回顾窗口一次加载的产物: 关键帧原图 + 布局元数据 + 该帧识别结果。
/// FrameImage 为独立读回的 Mat, 调用方负责 Dispose。
/// </summary>
public sealed record ReviewFrame(
    string MatchId,
    string FrameId,
    Mat FrameImage,
    IReadOnlyDictionary<string, string> Meta,
    RecognitionFrame? Recognition);