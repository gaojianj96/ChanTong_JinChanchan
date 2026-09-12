using ChanSight.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ChanSight.Recorder.Services;

public sealed class GlobalHotKeyService : IGlobalHotKeyService
{
    private readonly INativeHotKeyApi _api;
    private readonly ILogger<GlobalHotKeyService> _logger;
    private bool _disposed;

    public event EventHandler<HotKeyPressedEventArgs>? HotKeyPressed
    {
        add => _api.HotKeyPressed += value;
        remove => _api.HotKeyPressed -= value;
    }

    public GlobalHotKeyService(ILogger<GlobalHotKeyService> logger)
        : this(new NativeHotKeyApi(), logger)
    {
    }

    internal GlobalHotKeyService(INativeHotKeyApi api, ILogger<GlobalHotKeyService> logger)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ValueTask RegisterAsync(HotKeyDefinition hotKey, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogDebug("Registering hotkey: {Key} + {Modifiers}", hotKey.Key, hotKey.Modifiers);
        if (!_api.Register(hotKey))
            throw new InvalidOperationException($"Failed to register hotkey: {hotKey.Key} + {hotKey.Modifiers}");
        return ValueTask.CompletedTask;
    }

    public ValueTask UnregisterAsync(HotKeyDefinition hotKey, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogDebug("Unregistering hotkey: {Key} + {Modifiers}", hotKey.Key, hotKey.Modifiers);
        _api.Unregister(hotKey);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;
        await _api.DisposeAsync().ConfigureAwait(false);
    }
}