namespace ChanSight.Core.Models;

public enum FrameDropReason
{
    Throttled = 0,
    ChannelFull = 1,
    CaptureEnded = 2,
}