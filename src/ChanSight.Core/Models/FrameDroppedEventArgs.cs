namespace ChanSight.Core.Models;

public sealed record FrameDroppedEventArgs(
    long SequenceNumber,
    FrameDropReason Reason,
    DateTimeOffset Timestamp);