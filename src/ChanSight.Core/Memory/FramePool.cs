namespace ChanSight.Core.Memory;

using ChanSight.Core.Models;
using OpenCvSharp;

public sealed class FramePool : IFramePool, IDisposable
{
    private readonly object _lock = new();
    private readonly List<CapturedFrame> _pool = new();
    private readonly int _capacity;
    private bool _disposedPool;

    public FramePool(int capacity = 4)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    public int Capacity => _capacity;

    public int RentedCount
    {
        get
        {
            lock (_lock)
            {
                var count = 0;
                for (var i = 0; i < _pool.Count; i++)
                {
                    if (!_pool[i].IsDisposed)
                        count++;
                }

                return count;
            }
        }
    }

    public CapturedFrame Rent(Mat image, DateTimeOffset timestamp, long sequenceNumber)
    {
        ArgumentNullException.ThrowIfNull(image);

        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposedPool, this);

            CapturedFrame? shell = null;
            for (var i = 0; i < _pool.Count; i++)
            {
                if (_pool[i].IsDisposed)
                {
                    shell = _pool[i];
                    break;
                }
            }

            if (shell is null)
            {
                if (_pool.Count >= _capacity)
                {
                    throw new InvalidOperationException($"FramePool exhausted (capacity {_capacity}). Return rented frames first.");
                }

                shell = new CapturedFrame(new Mat(), timestamp, sequenceNumber);
                _pool.Add(shell);
            }

            shell.Reset(image, timestamp, sequenceNumber);
            return shell;
        }
    }

    public void Return(CapturedFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        lock (_lock)
        {
            var owned = false;
            for (var i = 0; i < _pool.Count; i++)
            {
                if (ReferenceEquals(_pool[i], frame))
                {
                    owned = true;
                    break;
                }
            }

            if (!owned)
                return;

            if (frame.IsDisposed)
                return;

            frame.Dispose();
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposedPool)
                return;

            _disposedPool = true;
            for (var i = 0; i < _pool.Count; i++)
            {
                _pool[i].Dispose();
            }

            _pool.Clear();
        }
    }
}