using System.Runtime.InteropServices;
using ChanSight.Core.Interfaces;

namespace ChanSight.Recorder.Services;

internal sealed class NativeHotKeyApi : INativeHotKeyApi
{
    private readonly Dictionary<HotKeyDefinition, int> _registered = new();

    public event EventHandler<HotKeyPressedEventArgs>? HotKeyPressed;

    public bool Register(HotKeyDefinition hotKey)
    {
        if (_registered.ContainsKey(hotKey))
            return false;

        var id = _registered.Count + 1;
        var result = NativeMethods.RegisterHotKey(IntPtr.Zero, id, (uint)hotKey.Modifiers, (uint)hotKey.Key);
        if (result)
            _registered[hotKey] = id;
        return result;
    }

    public bool Unregister(HotKeyDefinition hotKey)
    {
        if (!_registered.Remove(hotKey, out var id))
            return false;

        return NativeMethods.UnregisterHotKey(IntPtr.Zero, id);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var kv in _registered)
            NativeMethods.UnregisterHotKey(IntPtr.Zero, kv.Value);
        _registered.Clear();
        await ValueTask.CompletedTask;
    }

    private static partial class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}