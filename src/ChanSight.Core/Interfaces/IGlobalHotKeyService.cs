namespace ChanSight.Core.Interfaces;

public sealed record HotKeyDefinition(VirtualKeyCode Key, HotKeyModifiers Modifiers, string? Description = null);

[Flags]
public enum HotKeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8
}

public enum VirtualKeyCode
{
    F6 = 0x75,
    F7 = 0x76,
    F8 = 0x77
}

public sealed record HotKeyPressedEventArgs(HotKeyDefinition HotKey, DateTimeOffset Timestamp);

public interface IGlobalHotKeyService : IAsyncDisposable
{
    event EventHandler<HotKeyPressedEventArgs>? HotKeyPressed;

    ValueTask RegisterAsync(HotKeyDefinition hotKey, CancellationToken cancellationToken = default);

    ValueTask UnregisterAsync(HotKeyDefinition hotKey, CancellationToken cancellationToken = default);
}