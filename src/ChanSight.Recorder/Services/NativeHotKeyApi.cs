using System.Runtime.InteropServices;
using ChanSight.Core.Interfaces;

namespace ChanSight.Recorder.Services;

internal sealed class NativeHotKeyApi : INativeHotKeyApi
{
    private readonly object _syncLock = new();
    private readonly Dictionary<HotKeyDefinition, int> _registered = new();
    private nint _hwnd;
    private Thread? _messageThread;
    private CancellationTokenSource? _messageLoopCts;
    private volatile bool _disposed;
    private int _nextId;
    private Exception? _initError;
    private NativeMethods.WndProcDelegate? _keptAliveWndProc;
    private static readonly string WindowClassName = $"ChanSight_HotKeyMessageWindow_{Guid.NewGuid():N}";

    public event EventHandler<HotKeyPressedEventArgs>? HotKeyPressed;

    public bool Register(HotKeyDefinition hotKey)
    {
        if (_disposed)
            return false;

        lock (_syncLock)
        {
            if (_registered.ContainsKey(hotKey))
                return false;

            if (_hwnd == IntPtr.Zero)
                InitializeMessageWindow();

            var id = Interlocked.Increment(ref _nextId);
            var result = NativeMethods.RegisterHotKey(_hwnd, id, (uint)hotKey.Modifiers, (uint)hotKey.Key);
            if (result)
                _registered[hotKey] = id;
            return result;
        }
    }

    public bool Unregister(HotKeyDefinition hotKey)
    {
        lock (_syncLock)
        {
            if (!_registered.Remove(hotKey, out var id))
                return false;

            return NativeMethods.UnregisterHotKey(_hwnd, id);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _messageLoopCts?.Cancel();

        lock (_syncLock)
        {
            foreach (var kv in _registered)
                NativeMethods.UnregisterHotKey(_hwnd, kv.Value);
            _registered.Clear();

            if (_hwnd != IntPtr.Zero)
            {
                NativeMethods.DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
            }
        }

        if (_messageThread is not null)
        {
            await Task.Run(() =>
            {
                if (!_messageThread.Join(TimeSpan.FromSeconds(3)))
                {
                }
            }).ConfigureAwait(false);
        }

        _messageLoopCts?.Dispose();
    }

    private void InitializeMessageWindow()
    {
        var readyEvent = new ManualResetEventSlim(false);

        using (ExecutionContext.SuppressFlow())
        {
            _messageThread = new Thread(() =>
            {
                try
                {
                    RunMessageLoop(readyEvent);
                }
                catch (Exception ex)
                {
                    _initError = ex;
                    readyEvent.Set();
                }
            })
            {
                Name = "ChanSight HotKey Message Thread",
                IsBackground = true
            };

            _messageThread.Start();

            if (!readyEvent.Wait(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("Timed out waiting for hotkey message window to be created.");
            }

            if (_initError is not null)
            {
                throw new InvalidOperationException("Hotkey message window initialization failed.", _initError);
            }
        }
    }

    private void RunMessageLoop(ManualResetEventSlim readyEvent)
    {
        var hInstance = NativeMethods.GetModuleHandle(null!);

        var wndProc = new NativeMethods.WndProcDelegate(WndProc);
        _keptAliveWndProc = wndProc;

        var wndClass = new NativeMethods.WNDCLASSEX
        {
            cbSize = Marshal.SizeOf<NativeMethods.WNDCLASSEX>(),
            lpfnWndProc = wndProc,
            hInstance = hInstance,
            lpszClassName = WindowClassName
        };

        var atom = NativeMethods.RegisterClassEx(ref wndClass);
        if (atom == 0)
            throw new InvalidOperationException($"RegisterClassEx failed: {Marshal.GetLastWin32Error()}");

        _hwnd = NativeMethods.CreateWindowEx(
            0, WindowClassName, string.Empty, 0,
            0, 0, 0, 0,
            new IntPtr(-3), // HWND_MESSAGE
            IntPtr.Zero, hInstance, IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
            throw new InvalidOperationException($"CreateWindowEx failed: {Marshal.GetLastWin32Error()}");

        readyEvent.Set();

        _messageLoopCts = new CancellationTokenSource();
        var cts = _messageLoopCts;

        while (!cts.Token.IsCancellationRequested)
        {
            var waitResult = NativeMethods.MsgWaitForMultipleObjectsEx(
                0, IntPtr.Zero, 100, 0x04FF, 0x0001);

            while (NativeMethods.PeekMessage(out var msg, IntPtr.Zero, 0, 0, 1))
            {
                if (msg.message == 0x0012) // WM_QUIT
                    break;
                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessage(ref msg);
            }

            if (cts.Token.IsCancellationRequested)
                break;
        }

        if (_hwnd != IntPtr.Zero)
        {
            NativeMethods.DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }

        NativeMethods.UnregisterClass(WindowClassName, hInstance);
    }

    private nint WndProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == 0x0312) // WM_HOTKEY
        {
            var id = (int)wParam;
            HotKeyDefinition? hotKey = null;

            lock (_syncLock)
            {
                foreach (var kv in _registered)
                {
                    if (kv.Value == id)
                    {
                        hotKey = kv.Key;
                        break;
                    }
                }
            }

            if (hotKey is not null)
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        HotKeyPressed?.Invoke(this, new HotKeyPressedEventArgs(hotKey, DateTimeOffset.UtcNow));
                    }
                    catch
                    {
                        // subscriber exceptions must not crash the process
                    }
                });
            }

            return IntPtr.Zero;
        }

        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private static class NativeMethods
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate nint WndProcDelegate(nint hWnd, uint msg, nint wParam, nint lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct WNDCLASSEX
        {
            public int cbSize;
            public uint style;
            public WndProcDelegate lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public nint hInstance;
            public nint hIcon;
            public nint hCursor;
            public nint hbrBackground;
            public string? lpszMenuName;
            public string lpszClassName;
            public nint hIconSm;
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool UnregisterClass(string lpClassName, nint hInstance);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern nint CreateWindowEx(
            uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
            int x, int y, int nWidth, int nHeight,
            nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyWindow(nint hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern nint DefWindowProc(nint hWnd, uint msg, nint wParam, nint lParam);

        [DllImport("user32.dll")]
        public static extern bool PeekMessage(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

        [DllImport("user32.dll")]
        public static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        public static extern nint DispatchMessage(ref MSG lpMsg);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern nint GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(nint hWnd, int id);

        [DllImport("user32.dll")]
        public static extern uint MsgWaitForMultipleObjectsEx(
            uint nCount, nint pHandles, uint dwMilliseconds, uint dwWakeMask, uint dwFlags);

        [StructLayout(LayoutKind.Sequential)]
        public struct MSG
        {
            public nint hwnd;
            public uint message;
            public nint wParam;
            public nint lParam;
            public uint time;
            public POINT pt;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }
    }
}