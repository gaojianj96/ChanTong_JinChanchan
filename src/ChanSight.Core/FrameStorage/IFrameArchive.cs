using ChanSight.Core.Engine;
using OpenCvSharp;

namespace ChanSight.Core.FrameStorage;

/// <summary>
/// 关键帧归档的最小契约: 判断是否需要持久化(<see cref="ShouldPersist"/>)与落盘
/// (<see cref="StoreKeyFrame"/>)。<see cref="FrameArchive"/> 是磁盘实现; 测试用内存 fake。
/// </summary>
public interface IFrameArchive
{
    bool ShouldPersist(GameStateSnapshot prev, GameStateSnapshot next);

    string StoreKeyFrame(
        Mat frame,
        string matchId,
        long snapshotVersion,
        IReadOnlyDictionary<string, string>? layoutMeta = null);

    bool IsKeyFrame(string matchId, string frameId);
}