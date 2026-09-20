using ChanSight.Core.Season;
using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Services;
using FluentAssertions;
using OpenCvSharp;

namespace ChanSight.Tests.Season;

/// <summary>
/// 字典自学习闭环测试: 候选收录(识别侧) → 三选一确认(回顾侧) → 正式字典 + SeasonRuntime 刷新。
/// </summary>
public sealed class DictSelfLearnTests : IDisposable
{
    private const string SeasonId = "S16.5";
    private const string CandidateSource = "vlm-recognition";

    private static readonly DateTimeOffset Seen = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly string _rootDirectory;
    private bool _disposed;

    public DictSelfLearnTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), "ChanSight.DictSelfLearn", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDirectory);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public void AddCandidate_PendingContainsEntity_FormalDictionaryDoesNot()
    {
        var store = new SeasonDictionaryStore(_rootDirectory);

        store.AddCandidate(SeasonId, NewCandidate("全新英雄"));

        store.GetPendingCandidates(SeasonId).Should().ContainSingle(c => c.Entity == "全新英雄");
        store.Get(SeasonId).Should().BeNull();
    }

    [Fact]
    public void AddCandidate_SameRawTwice_DoesNotDuplicate()
    {
        var store = new SeasonDictionaryStore(_rootDirectory);

        store.AddCandidate(SeasonId, NewCandidate("全新英雄"));
        store.AddCandidate(SeasonId, NewCandidate("全新英雄"));

        store.GetPendingCandidates(SeasonId).Should().ContainSingle(c => c.Entity == "全新英雄");
    }

    [Fact]
    public void ConfirmNew_AddsHeroToFormal_RemovesCandidate()
    {
        var store = new SeasonDictionaryStore(_rootDirectory);

        store.AddCandidate(SeasonId, NewCandidate("全新英雄"));
        store.Confirm(SeasonId, "全新英雄", ConfirmKind.New);

        store.GetPendingCandidates(SeasonId).Should().BeEmpty();
        store.Get(SeasonId)!.Heroes.Should().Contain("全新英雄");
    }

    [Fact]
    public void ConfirmIgnore_RemovesCandidate_NoFormalAddition()
    {
        var store = new SeasonDictionaryStore(_rootDirectory);

        store.AddCandidate(SeasonId, NewCandidate("噪声条目"));
        store.Confirm(SeasonId, "噪声条目", ConfirmKind.Ignore);

        store.GetPendingCandidates(SeasonId).Should().BeEmpty();
        store.Get(SeasonId).Should().BeNull();
    }

    [Fact]
    public void ConfirmMapExisting_RemovesCandidate_NoFormalAddition()
    {
        var store = new SeasonDictionaryStore(_rootDirectory);

        store.AddCandidate(SeasonId, NewCandidate("盖伦别名"));
        store.Confirm(SeasonId, "盖伦别名", ConfirmKind.MapExisting);

        store.GetPendingCandidates(SeasonId).Should().BeEmpty();
        store.Get(SeasonId).Should().BeNull();
    }

    [Fact]
    public void ConfirmNew_SeasonRuntime_ReReadsNewHero()
    {
        var store = new SeasonDictionaryStore(_rootDirectory, BuiltInSeeds());
        var runtime = new SeasonRuntime(store);

        store.AddCandidate(SeasonId, NewCandidate("全新英雄"));
        runtime.Heroes.Should().NotContain("全新英雄");

        store.Confirm(SeasonId, "全新英雄", ConfirmKind.New);

        runtime.Heroes.Should().Contain("全新英雄");
    }

    [Fact]
    public async Task ManualFrameVlm_UnrecognizedHero_RecordsCandidateWithVlmSource()
    {
        const string json = """
            {
              "perspective": "self",
              "board": [ { "slot": 0, "hero": "神秘新英雄", "star": 1, "items": [], "confidence": 0.85 } ],
              "bench": [],
              "confidence": 0.9
            }
            """;
        var writer = new FakeWriter();
        var service = new ManualFrameVlmService(
            new FakeVlmClient(json),
            new RoiMapperService(),
            SeasonRuntime.CreateDefault(),
            writer);

        await service.RecognizeAsync(CreateFrame(), isSelf: true);

        writer.Additions.Should().ContainSingle();
        var (seasonId, candidate) = writer.Additions[0];
        seasonId.Should().Be(SeasonId);
        candidate.Entity.Should().Be("神秘新英雄");
        candidate.Source.Should().Be(CandidateSource);
        candidate.Confidence.Should().Be(0.85);
    }

    private static Mat CreateFrame() => new(1920, 1080, MatType.CV_8UC3, Scalar.All(128));

    private static CandidateMeta NewCandidate(string entity) => new(entity, "hero", null, 0.9, Seen);

    private static IReadOnlyDictionary<string, SeasonDictionary> BuiltInSeeds() =>
        new Dictionary<string, SeasonDictionary>(StringComparer.Ordinal)
        {
            [SeasonDictionarySeed.DefaultSeasonId] = SeasonDictionarySeed.DefaultDictionary,
        };

    private sealed class FakeWriter : ISeasonDictionaryWriter
    {
        public List<(string SeasonId, CandidateMeta Candidate)> Additions { get; } = new();
        public List<(string SeasonId, string Entity, ConfirmKind Kind)> Confirmations { get; } = new();

        public void AddCandidate(string seasonId, CandidateMeta candidate) => Additions.Add((seasonId, candidate));

        public void Confirm(string seasonId, string entity, ConfirmKind kind) => Confirmations.Add((seasonId, entity, kind));

        public void Revoke(string seasonId, string entity)
        {
        }

        public IReadOnlyList<CandidateMeta> GetPendingCandidates(string seasonId) =>
            Additions.Where(a => a.SeasonId == seasonId).Select(static a => a.Candidate).ToList();
    }

    private sealed class FakeVlmClient : IVlmClient
    {
        private readonly string _json;

        public FakeVlmClient(string json) => _json = json;

        public Task<string> CompleteAsync(
            string prompt,
            IReadOnlyList<(string mime, byte[] data)> images,
            CancellationToken ct) => Task.FromResult(_json);
    }
}
