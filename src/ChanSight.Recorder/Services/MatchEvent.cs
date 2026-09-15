using System.Text.Json.Serialization;

namespace ChanSight.Recorder.Services;

public sealed record MatchEvent
{
    [JsonPropertyName("Seq")]
    public long Seq { get; init; }

    [JsonPropertyName("Timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    [JsonPropertyName("GameId")]
    public string GameId { get; init; } = string.Empty;

    [JsonPropertyName("Type")]
    public MatchEventType Type { get; init; }

    [JsonPropertyName("SchemaVersion")]
    public string SchemaVersion { get; init; } = "1";

    [JsonPropertyName("PayloadJson")]
    public string PayloadJson { get; init; } = string.Empty;
}
