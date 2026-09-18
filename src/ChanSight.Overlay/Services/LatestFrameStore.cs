using OpenCvSharp;

namespace ChanSight.Overlay.Services;

/// <summary>
/// Holds the most recently captured full frame for manual recognition. The live
/// recognition loop writes each new frame here; the manual VLM trigger reads a
/// snapshot of the latest one. Frames are cloned on write so the live pipeline
/// can keep mutating its own <see cref="Mat"/> without racing the manual path.
/// </summary>
public sealed class LatestFrameStore
{
    private readonly object _sync = new();
    private Mat? _latest;

    public void Store(Mat frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        lock (_sync)
        {
            _latest?.Dispose();
            _latest = frame.Clone();
        }
    }

    /// <summary>Returns a fresh clone of the latest frame, or null when none captured yet. Caller owns/disposes it.</summary>
    public Mat? TakeLatest()
    {
        lock (_sync)
        {
            return _latest?.Clone();
        }
    }

    public bool HasFrame
    {
        get
        {
            lock (_sync)
            {
                return _latest is not null;
            }
        }
    }
}