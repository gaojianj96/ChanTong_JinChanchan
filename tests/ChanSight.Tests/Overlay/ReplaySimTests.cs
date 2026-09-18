using ChanSight.Core.Annotation;
using ChanSight.Core.FrameStorage;
using ChanSight.Overlay.Services;
using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Services;
using FluentAssertions;
using OpenCvSharp;

namespace ChanSight.Tests.Overlay;

public sealed class ReplaySimTests
{
    private const string MatchId = "match-1";

    [Fact]
    public void ListFrames_PassesThroughArchive()
    {
        using var fx = new Fixture();
        using var m1 = CreateMat(new Scalar(1, 0, 0));
        using var m2 = CreateMat(new Scalar(2, 0, 0));
        fx.Archive.StoreKeyFrame(m1, MatchId, snapshotVersion: 10);
        fx.Archive.StoreKeyFrame(m2, MatchId, snapshotVersion: 20);

        var keys = fx.Service.ListFrames(MatchId);

        keys.Should().HaveCount(2);
        keys.Select(k => k.SnapshotVersion).Should().Equal(10L, 20L);
    }

    [Fact]
    public void Load_WithoutCache_ReturnsUnrecognizedAndDoesNotCallVlm()
    {
        using var fx = new Fixture();
        var frameId = StoreFrame(fx, snapshotVersion: 1);

        var replay = fx.Service.Load(MatchId, frameId, reRecognize: false);
        try
        {
            replay.HasRecognition.Should().BeFalse();
            replay.Recognition.Should().BeNull();
            fx.Vlm.CallCount.Should().Be(0);
        }
        finally
        {
            replay.FrameImage.Dispose();
        }
    }

    [Fact]
    public async Task Load_WithCache_ReadsDirectlyWithoutCallingVlm()
    {
        using var fx = new Fixture();
        fx.Vlm.QueueResult(Json(gold: 47, stage: "3-2"));
        var frameId = StoreFrame(fx, snapshotVersion: 1);

        var recognized = await fx.Service.ReRecognizeAsync(MatchId, frameId, isSelf: true, CancellationToken.None);
        recognized.Gold.Should().Be(47);
        fx.Vlm.CallCount.Should().Be(1);

        var replay = fx.Service.Load(MatchId, frameId, reRecognize: false);
        try
        {
            replay.HasRecognition.Should().BeTrue();
            replay.Recognition.Should().NotBeNull();
            replay.Recognition!.Gold.Should().Be(47);
            replay.Recognition.Stage.Should().Be("3-2");
            // 直读缓存, 不再触发 VLM(无网络)。
            fx.Vlm.CallCount.Should().Be(1);
        }
        finally
        {
            replay.FrameImage.Dispose();
        }
    }

    [Fact]
    public async Task ReRecognizeAsync_ReturnsFrame_AndWritesBackCache()
    {
        using var fx = new Fixture();
        fx.Vlm.QueueResult(Json(gold: 11, stage: "2-1"));
        var frameId = StoreFrame(fx, snapshotVersion: 3);

        var result = await fx.Service.ReRecognizeAsync(MatchId, frameId, isSelf: true, CancellationToken.None);

        result.Gold.Should().Be(11);
        result.Stage.Should().Be("2-1");

        // 结果已写回缓存: 直读可再次取到, 且不新增 VLM 调用。
        var replay = fx.Service.Load(MatchId, frameId, reRecognize: false);
        try
        {
            replay.HasRecognition.Should().BeTrue();
            replay.Recognition!.Gold.Should().Be(11);
            fx.Vlm.CallCount.Should().Be(1);
        }
        finally
        {
            replay.FrameImage.Dispose();
        }
    }

    [Fact]
    public async Task ReRecognizeAsync_OnlyAffectsTargetFrame()
    {
        using var fx = new Fixture();
        var frameA = StoreFrame(fx, snapshotVersion: 1, new Scalar(1, 0, 0));
        var frameB = StoreFrame(fx, snapshotVersion: 2, new Scalar(2, 0, 0));

        fx.Vlm.QueueResult(Json(gold: 9));
        await fx.Service.ReRecognizeAsync(MatchId, frameA, isSelf: true, CancellationToken.None);

        // 目标帧 A 已有识别结果。
        var replayA = fx.Service.Load(MatchId, frameA, reRecognize: false);
        try
        {
            replayA.HasRecognition.Should().BeTrue();
        }
        finally
        {
            replayA.FrameImage.Dispose();
        }

        // 其它帧 B 不受影响, 仍为未识别, 且未额外触发 VLM。
        var replayB = fx.Service.Load(MatchId, frameB, reRecognize: false);
        try
        {
            replayB.HasRecognition.Should().BeFalse();
            fx.Vlm.CallCount.Should().Be(1);
        }
        finally
        {
            replayB.FrameImage.Dispose();
        }
    }

    private static string StoreFrame(Fixture fx, long snapshotVersion, Scalar? color = null)
    {
        using var mat = CreateMat(color);
        return fx.Archive.StoreKeyFrame(mat, MatchId, snapshotVersion);
    }

    private static string Json(int gold, string? stage = null, string perspective = "self")
        => $$"""
             { "perspective": "{{perspective}}", "stage": "{{stage ?? "1-1"}}", "gold": {{gold}}, "board": [], "bench": [], "confidence": 0.9 }
             """;

    private static Mat CreateMat(Scalar? color = null)
    {
        var mat = new Mat(48, 64, MatType.CV_8UC3);
        mat.SetTo(color ?? new Scalar(10, 20, 30));
        return mat;
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), $"ChanSight_ReplaySim_{Guid.NewGuid():N}");
        public FrameArchive Archive { get; }
        public AnnotationStore Annotations { get; }
        public FakeVlmClient Vlm { get; } = new();
        public ReplaySimService Service { get; }

        public Fixture()
        {
            Archive = new FrameArchive(Root);
            Annotations = new AnnotationStore(Path.Combine(Root, "corrections"));
            var vlm = new ManualFrameVlmService(Vlm, new RoiMapperService());
            Service = new ReplaySimService(Archive, vlm, Annotations);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class FakeVlmClient : IVlmClient
    {
        private readonly Queue<object> _script = new();

        public int CallCount { get; private set; }

        public FakeVlmClient QueueResult(string json)
        {
            _script.Enqueue(json);
            return this;
        }

        public Task<string> CompleteAsync(
            string prompt,
            IReadOnlyList<(string mime, byte[] data)> images,
            CancellationToken ct)
        {
            CallCount++;

            var next = _script.Count > 0
                ? _script.Dequeue()
                : throw new InvalidOperationException("FakeVlmClient script exhausted.");

            if (next is Exception exception)
            {
                throw exception;
            }

            return Task.FromResult((string)next);
        }
    }
}