namespace ChanSight.Recorder.Services;

public sealed record MatchEvent
{
    public long Seq { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public string GameId { get; init; } = string.Empty;

    public MatchEventType Type { get; init; }

    public string SchemaVersion { get; init; } = "1";

    public string PayloadJson { get; init; } = string.Empty;
}
