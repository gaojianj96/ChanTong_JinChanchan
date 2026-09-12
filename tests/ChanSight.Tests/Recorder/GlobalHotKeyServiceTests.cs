using ChanSight.Core.Interfaces;
using ChanSight.Core.Models;
using ChanSight.Recorder.Services;
using ChanSight.Tests.Mocks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ChanSight.Tests.Recorder;

public sealed class GlobalHotKeyServiceTests
{
    [Fact]
    public async Task RegisterAsync_RegistersHotKeyWithApi()
    {
        var api = new FakeHotKeyApi();
        await using var service = CreateService(api);

        var hotKey = new HotKeyDefinition(VirtualKeyCode.F6, HotKeyModifiers.Control, "test");
        await service.RegisterAsync(hotKey);

        api.RegisteredKeys.Should().ContainSingle().Which.Should().Be(hotKey);
    }

    [Fact]
    public async Task UnregisterAsync_UnregistersHotKeyFromApi()
    {
        var api = new FakeHotKeyApi();
        await using var service = CreateService(api);
        var hotKey = new HotKeyDefinition(VirtualKeyCode.F6, HotKeyModifiers.Control);

        await service.RegisterAsync(hotKey);
        await service.UnregisterAsync(hotKey);

        api.RegisteredKeys.Should().BeEmpty();
    }

    [Fact]
    public async Task UnregisterAsync_NonExistentHotKey_DoesNotThrow()
    {
        var api = new FakeHotKeyApi();
        await using var service = CreateService(api);

        var act = async () => await service.UnregisterAsync(
            new HotKeyDefinition(VirtualKeyCode.F7, HotKeyModifiers.None));
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RegisterAsync_DuplicateHotKey_ThrowsInvalidOperationException()
    {
        var api = new FakeHotKeyApi();
        await using var service = CreateService(api);
        var hotKey = new HotKeyDefinition(VirtualKeyCode.F6, HotKeyModifiers.None);

        await service.RegisterAsync(hotKey);
        var act = async () => await service.RegisterAsync(hotKey);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task RegisterAsync_WhenApiFails_ThrowsInvalidOperationException()
    {
        var api = new FakeHotKeyApi { FailOnRegister = true };
        await using var service = CreateService(api);

        var act = async () => await service.RegisterAsync(
            new HotKeyDefinition(VirtualKeyCode.F8, HotKeyModifiers.Alt));
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task HotKeyPressed_ForwardsEventFromApi()
    {
        var api = new FakeHotKeyApi();
        await using var service = CreateService(api);
        HotKeyPressedEventArgs? received = null;
        service.HotKeyPressed += (_, args) => received = args;

        var expected = new HotKeyPressedEventArgs(
            new HotKeyDefinition(VirtualKeyCode.F7, HotKeyModifiers.Shift),
            DateTimeOffset.UtcNow);
        api.RaiseHotKeyPressed(expected);

        received.Should().NotBeNull();
        received!.HotKey.Should().Be(expected.HotKey);
    }

    [Fact]
    public async Task DisposeAsync_DisposesApi()
    {
        var api = new FakeHotKeyApi();
        var service = CreateService(api);

        await service.DisposeAsync();

        api.DisposeCalled.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotent()
    {
        var api = new FakeHotKeyApi();
        var service = CreateService(api);

        await service.DisposeAsync();
        await service.DisposeAsync();

        api.DisposeCalled.Should().BeTrue();
        api.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task RegisterAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var api = new FakeHotKeyApi();
        var service = CreateService(api);
        await service.DisposeAsync();

        var act = async () => await service.RegisterAsync(
            new HotKeyDefinition(VirtualKeyCode.F7, HotKeyModifiers.None));
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    private static GlobalHotKeyService CreateService(FakeHotKeyApi api)
        => new(api, NullLogger<GlobalHotKeyService>.Instance);

    private sealed class FakeHotKeyApi : INativeHotKeyApi
    {
        public List<HotKeyDefinition> RegisteredKeys { get; } = new();
        public bool FailOnRegister { get; set; }
        public bool DisposeCalled { get; private set; }
        public int DisposeCount { get; private set; }

#pragma warning disable CS0067
        public event EventHandler<HotKeyPressedEventArgs>? HotKeyPressed;
#pragma warning restore CS0067

        public bool Register(HotKeyDefinition hotKey)
        {
            if (FailOnRegister)
                return false;
            if (RegisteredKeys.Contains(hotKey))
                return false;
            RegisteredKeys.Add(hotKey);
            return true;
        }

        public bool Unregister(HotKeyDefinition hotKey)
        {
            return RegisteredKeys.Remove(hotKey);
        }

        public void RaiseHotKeyPressed(HotKeyPressedEventArgs args)
        {
            HotKeyPressed?.Invoke(this, args);
        }

        public ValueTask DisposeAsync()
        {
            DisposeCalled = true;
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}