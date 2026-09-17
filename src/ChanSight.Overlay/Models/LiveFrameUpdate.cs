using ChanSight.Core.Engine;
using ChanSight.Vision.Models;

namespace ChanSight.Overlay.Models;

/// <summary>
/// 实时链路单次产出: 最新识别结果 + 对应游戏状态快照 + 该帧落盘后的 frameId。
/// FrameId 为空表示该帧未被持久化为关键帧。
/// </summary>
public sealed record LiveFrameUpdate(
    GameStateSnapshot State,
    RecognitionFrame Frame,
    string? FrameId);