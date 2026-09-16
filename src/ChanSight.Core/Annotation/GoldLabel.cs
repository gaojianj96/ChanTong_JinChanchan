using System.Text.Json.Serialization;

namespace ChanSight.Core.Annotation;

/// <summary>
/// 金标: 回顾二次确认的最终标签。撤销通过追加同 Id 的 Revoked=true 记录实现;
/// 加载时按 latest-wins 规则解析(最后一条同 Id 记录生效)。
/// CorrectedValue 语义与 <see cref="CorrectionRecord"/> 一致, "空" 必须用合法 JSON 表达。
/// </summary>
public sealed record GoldLabel(
    [property: JsonPropertyName("Id")] string Id,
    [property: JsonPropertyName("CorrectionId")] string? CorrectionId,
    [property: JsonPropertyName("MatchId")] string MatchId,
    [property: JsonPropertyName("FrameId")] string FrameId,
    [property: JsonPropertyName("SnapshotVersion")] long SnapshotVersion,
    [property: JsonPropertyName("RegionType")] string RegionType,
    [property: JsonPropertyName("CellIndex")] int CellIndex,
    [property: JsonPropertyName("RecognizedValue")] string? RecognizedValue,
    [property: JsonPropertyName("CorrectedValue")] string CorrectedValue,
    [property: JsonPropertyName("Confidence")] double Confidence,
    [property: JsonPropertyName("SourceTier")] string SourceTier,
    [property: JsonPropertyName("RecognizerVersion")] string RecognizerVersion,
    [property: JsonPropertyName("CorrectionType")] string? CorrectionType,
    [property: JsonPropertyName("Revoked")] bool Revoked = false,
    [property: JsonPropertyName("ConfirmedAt")] DateTimeOffset ConfirmedAt = default);