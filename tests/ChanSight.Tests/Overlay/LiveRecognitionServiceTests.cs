using ChanSight.Core.Engine;
using ChanSight.Core.FrameStorage;
using ChanSight.Core.Interfaces;
using ChanSight.Core.Models;
using ChanSight.Overlay.Services;
using ChanSight.Tests.Mocks;
using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using FluentAssertions;
using OpenCvSharp;

namespace ChanSight.Tests.Overlay;

public sealed class LiveRecognitionServiceTests
{
    [Fact]
    public async Task ProcessFrameAsync_WhenShouldPersistTrue_StoresKeyFrame()
    {
        var archive = new FakeFrameArchive(shouldPersist: true);
        var service = CreateService(archive, out var recognize);
        recognize.NextFrame = CreateFrameResult();

        using var mat = CreateMat();
        var update = await service.ProcessFrameAsync(mat);

        archive.StoreCallCount.Should().Be(1);
        archive.ShouldPersistCalls.Should().Be(1);
        update.FrameId.Should().Be("stored-frame");
    }

    [Fact]
    public async Task ProcessFrameAsync_WhenShouldPersistFalse_DoesNotStore()
    {
        var archive = new FakeFrameArchive(shouldPersist: false);
        var service = CreateService(archive, out var recognize);
        recognize.NextFrame = CreateFrameResult();

        using var mat = CreateMat();
        var update = await service.ProcessFrameAsync(mat);

        archive.StoreCallCount.Should().Be(0);
        archive.ShouldPersistCalls.Should().Be(1);
        update.FrameId.Should().BeNull();
    }

    [Fact]
    public async Task ProcessFrameAsync_UpdatesGameStateManager()
    {
        var archive = new FakeFrameArchive(shouldPersist: false);
        var manager = new GameStateManager();
        var service = CreateService(archive, out var recognize, manager);
        recognize.NextFrame = CreateFrameResult(gold: 47, stage: "4-1");

        using var mat = CreateMat();
        var update = await service.ProcessFrameAsync(mat);

        update.State.Gold.Should().Be(47);
        update.State.Stage.Should().Be("4-1");
        manager.Current.Gold.Should().Be(47);
    }

    private static LiveRecognitionService CreateService(
        FakeFrameArchive archive,
        out FakeRecognizer recognize,
        GameStateManager? manager = null)
    {
        manager ??= new GameStateManager();
        var adapter = new RecognitionToGameStateAdapter(manager);
        recognize = new FakeRecognizer();

        var capture = new FakeCaptureService();

        return new LiveRecognitionService(
            capture,
            recognize.RecognizeAsync,
            archive,
            adapter,
            manager,
            matchId: "test-match");
    }

    private static RecognitionFrame CreateFrameResult(int gold = 10, string stage = "2-1")
        => new()
        {
            Gold = gold,
            Stage = stage,
            Phase = GamePhase.Planning,
            BoardCells = Enumerable.Range(0, 28).Select(_ => EmptyCell()).ToArray(),
            BenchCells = Enumerable.Range(0, 9).Select(_ => EmptyCell()).ToArray(),
            ShopCards = Enumerable.Range(0, 5).Select(_ => new ShopCard(string.Empty, 0, 0.0, SourceTier.T0)).ToArray(),
        };

    private static UnitCell EmptyCell() => new(null, 0, Array.Empty<ItemStack>(), 0.0, SourceTier.T0);

    private static Mat CreateMat()
    {
        var mat = new Mat(48, 64, MatType.CV_8UC3);
        mat.SetTo(new Scalar(10, 20, 30));
        return mat;
    }

    private sealed class FakeRecognizer
    {
        public RecognitionFrame NextFrame { get; set; } = CreateFrameResult();

        public Task<RecognitionFrame> RecognizeAsync(Mat frame, CancellationToken ct) =>
            Task.FromResult(NextFrame);
    }

    private sealed class FakeFrameArchive : IFrameArchive
    {
        private readonly bool _shouldPersist;
        public int ShouldPersistCalls { get; private set; }
        public int StoreCallCount { get; private set; }

        public FakeFrameArchive(bool shouldPersist) => _shouldPersist = shouldPersist;

        public bool ShouldPersist(GameStateSnapshot prev, GameStateSnapshot next)
        {
            ShouldPersistCalls++;
            return _shouldPersist;
        }

        public string StoreKeyFrame(
            Mat frame,
            string matchId,
            long snapshotVersion,
            IReadOnlyDictionary<string, string>? layoutMeta = null)
        {
            StoreCallCount++;
            return "stored-frame";
        }

        public bool IsKeyFrame(string matchId, string frameId) => false;
    }

    private sealed class FakeCaptureService : IScreenCaptureService
    {
        private readonly MockFrameSource _source = new();

        public bool IsRunning => true;

        public IFrameSource FrameSource => _source;

        public ValueTask StartAsync(WindowTarget target, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask StopAsync(CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}