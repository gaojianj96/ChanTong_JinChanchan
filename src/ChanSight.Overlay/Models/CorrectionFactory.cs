using ChanSight.Core.Annotation;

namespace ChanSight.Overlay.Models;

/// <summary>
/// 从人工修正操作构造 <see cref="CorrectionRecord"/> 的纯工厂。
/// 严格遵循「钉帧锚 FrameId」语义: FrameId 一律使用用户看到的被钉帧, 而不是最新快照;
/// 不参与 SnapshotVersion 校验(避免 2fps 频繁失配)。
/// </summary>
public static class CorrectionFactory
{
    public static CorrectionRecord Create(
        string id,
        string matchId,
        string frameId,
        long snapshotVersion,
        string regionType,
        int cellIndex,
        string? recognizedValue,
        string correctedValue,
        double confidence = 1.0,
        string sourceTier = "local",
        string recognizerVersion = "1")
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Id must not be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(frameId))
        {
            throw new ArgumentException("FrameId (pinned frame) must not be empty.", nameof(frameId));
        }

        if (!RegionTypes.IsValid(regionType))
        {
            throw new ArgumentException($"Unknown RegionType '{regionType}'.", nameof(regionType));
        }

        return new CorrectionRecord(
            Id: id,
            MatchId: matchId,
            FrameId: frameId,
            Timestamp: DateTimeOffset.UtcNow,
            SnapshotVersion: snapshotVersion,
            RegionType: regionType,
            CellIndex: cellIndex,
            RecognizedValue: recognizedValue,
            CorrectedValue: correctedValue,
            Confidence: confidence,
            SourceTier: sourceTier,
            RecognizerVersion: recognizerVersion);
    }
}