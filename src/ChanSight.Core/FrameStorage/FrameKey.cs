namespace ChanSight.Core.FrameStorage;

using System.Security.Cryptography;
using OpenCvSharp;

public sealed record FrameKey(string MatchId, string FrameId, long SnapshotVersion)
{
    public static string ComputeFrameId(Mat frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (frame.Empty())
        {
            throw new ArgumentException("Frame content is empty.", nameof(frame));
        }

        var encoded = frame.ImEncode(".png");
        var hash = SHA256.HashData(encoded);
        return Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant();
    }
}