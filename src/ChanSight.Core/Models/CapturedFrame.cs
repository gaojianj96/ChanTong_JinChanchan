using OpenCvSharp;

namespace ChanSight.Core.Models;

public sealed class CapturedFrame : IDisposable
{
    private Mat _image;
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

    public DateTimeOffset Timestamp { get; private set; }

    public long SequenceNumber { get; private set; }

    public FrameResolution Resolution { get; private set; }

    public object? Tag { get; set; }

    public bool IsDisposed => _disposed;

    internal void Reset(Mat image, DateTimeOffset timestamp, long sequenceNumber)
    {
        ArgumentNullException.ThrowIfNull(image);

        var wasDisposed = _disposed;
        _disposed = false;

        if (wasDisposed || _image.Size() != image.Size() || _image.Type() != image.Type())
        {
            if (!wasDisposed)
            {
                _image.Dispose();
            }

            _image = new Mat();
        }

        image.CopyTo(_image);
        Timestamp = timestamp;
        SequenceNumber = sequenceNumber;
        Resolution = new FrameResolution(image.Width, image.Height);
        Tag = null;
    }

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
