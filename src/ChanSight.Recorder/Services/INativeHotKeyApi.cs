using ChanSight.Core.Interfaces;

namespace ChanSight.Recorder.Services;

internal interface INativeHotKeyApi : IAsyncDisposable
{
    event EventHandler<HotKeyPressedEventArgs>? HotKeyPressed;

    bool Register(HotKeyDefinition hotKey);

    bool Unregister(HotKeyDefinition hotKey);
}