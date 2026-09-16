using System.Text.Json.Serialization;

namespace ChanSight.Core.Annotation;

/// <summary>
/// 实时修正记录: "边运行边标注"第一阶段, 仅记录一次人工/自动修正, 不做二次确认。
/// JSON 字段名以 <see cref="JsonPropertyNameAttribute"/> 显式固定为 PascalCase,
/// 与 MatchLogger 的 events.jsonl 保持一致。
/// CorrectedValue 存结构化 JSON 子对象字符串, 如 {"hero":"盖伦","star":2,"items":["羊刀"]},
/// "空" 必须用合法 JSON 表达(如 {"empty":true}), 不用空串。
/// </summary>
public sealed record CorrectionRecord(
    [property: JsonPropertyName("Id")] string Id,
    [property: JsonPropertyName("MatchId")] string MatchId,
    [property: JsonPropertyName("FrameId")] string FrameId,
    [property: JsonPropertyName("Timestamp")] DateTimeOffset Timestamp,
    [property: JsonPropertyName("SnapshotVersion")] long SnapshotVersion,
    [property: JsonPropertyName("RegionType")] string RegionType,
    [property: JsonPropertyName("CellIndex")] int CellIndex,
    [property: JsonPropertyName("RecognizedValue")] string? RecognizedValue,
    [property: JsonPropertyName("CorrectedValue")] string CorrectedValue,
    [property: JsonPropertyName("Confidence")] double Confidence,
    [property: JsonPropertyName("SourceTier")] string SourceTier,
    [property: JsonPropertyName("RecognizerVersion")] string RecognizerVersion,
    [property: JsonPropertyName("SchemaVersion")] string SchemaVersion = "1");