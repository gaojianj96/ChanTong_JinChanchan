using OpenCvSharp;

namespace ChanSight.Core.Models;

public sealed class CapturedFrame : IDisposable
{
    private readonly Mat _image;
    private bool _disposed;

    public CapturedFrame(Mat image, DateTimeOffset timestamp, long sequenceNumber)
    {
        _image = image ?? throw new ArgumentNullException(nameof(image));
        Timestamp = timestamp;
        SequenceNumber = sequenceNumber;
        Resolution = new FrameResolution(image.Width, image.Height);
    }

    public Mat Image
    {
        get
        {
            ThrowIfDisposed();
            return _image;
        }
    }

    public DateTimeOffset Timestamp { get; }

    public long SequenceNumber { get; }

    public FrameResolution Resolution { get; }

    public bool IsDisposed => _disposed;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _image.Dispose();
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CapturedFrame));
        }
    }
}
