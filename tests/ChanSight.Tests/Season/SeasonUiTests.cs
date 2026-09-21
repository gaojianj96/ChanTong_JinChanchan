using ChanSight.Core.Annotation;
using ChanSight.Core.Engine;
using ChanSight.Core.Interfaces;
using ChanSight.Core.Season;
using ChanSight.Overlay.Services;
using ChanSight.Overlay.ViewModels;
using ChanSight.Vision.Models;
using FluentAssertions;

namespace ChanSight.Tests.Season;

/// <summary>
/// 契约 SEASON-UI 的验收测试: 赛季/模式选择器切换生效链 + 网络搜索字典 seeder。
/// </summary>
public sealed class SeasonUiTests
{
    [Fact]
    public void RuntimeSelect_S18Matching_ChangesHeroes()
    {
        var reader = new FakeReader(new Dictionary<string, SeasonDictionary>(StringComparer.Ordinal)
        {
            ["S16.5"] = Dict("S16.5", "亚索", "阿狸"),
            ["S18"] = Dict("S18", "悠米", "泽丽"),
        });
        var runtime = new SeasonRuntime(reader);

        runtime.Select("S18", "匹配");

        runtime.Context.SeasonId.Should().Be("S18");
        runtime.Context.Mode.Should().Be("匹配");
        runtime.Heroes.Should().Contain("悠米");
        runtime.Heroes.Should().NotContain("亚索");
    }

    [Fact]
    public void LiveViewModel_ChangeSeason_RebuildsHeroCandidates()
    {
        var runtime = new SeasonRuntime(new FakeReader(new Dictionary<string, SeasonDictionary>(StringComparer.Ordinal)
        {
            ["S16.5"] = Dict("S16.5", "亚索", "阿狸"),
            ["S18"] = Dict("S18", "悠米", "泽丽"),
        }));
        var vm = new LiveViewModel(
            new AnnotationStore(new FakeFileSystem(), "corrections"),
            Evaluate,
            runtime: runtime);

        vm.HeroCandidates.Should().Contain("亚索");
        vm.HeroCandidates.Should().NotContain("悠米");

        vm.SelectedSeason = "S18";

        vm.HeroCandidates.Should().Contain("悠米");
        vm.HeroCandidates.Should().NotContain("亚索");
        runtime.Context.SeasonId.Should().Be("S18");
    }

    [Fact]
    public void LiveViewModel_ChangeMode_KeepsSeasonDictionary_CallsSelect()
    {
        var runtime = new SeasonRuntime(new FakeReader(new Dictionary<string, SeasonDictionary>(StringComparer.Ordinal)
        {
            ["S16.5"] = Dict("S16.5", "亚索"),
        }));
        var vm = CreateViewModel(runtime);

        vm.SelectedMode = "匹配";

        runtime.Context.Mode.Should().Be("匹配");
        vm.HeroCandidates.Should().Contain("亚索");
    }

    [Fact]
    public async Task Seeder_SearchAsync_ReturnsDeduplicatedCandidates_WithWebSearchSource()
    {
        var seeder = new DictionarySeederService(new FakeWriter());

        var candidates = await seeder.SearchAsync("S16.5", "亚索");

        candidates.Should().NotBeEmpty();
        candidates.Should().OnlyContain(c => c.Source == DictionarySeederService.WebSearchSource);
        candidates.Select(static c => c.Url).Should().OnlyHaveUniqueItems();
        candidates.Should().OnlyContain(c => c.Confidence == DictionarySeederService.DefaultConfidence);
    }

    [Fact]
    public void Seeder_AddCandidates_WritesPending_FormalDictionaryEmpty_UntilConfirm()
    {
        using var fx = new StoreFixture();
        var store = new SeasonDictionaryStore(fx.Root);
        var seeder = new DictionarySeederService(store);

        var candidates = new List<CandidateMeta>
        {
            new("悠米", DictionarySeederService.WebSearchSource, "https://example.com/yuumi", 0.5, DateTimeOffset.UtcNow),
        };
        seeder.AddCandidates("S18", candidates);

        store.GetPendingCandidates("S18").Should().ContainSingle(c => c.Entity == "悠米");
        store.Get("S18").Should().BeNull();

        store.Confirm("S18", "悠米", ConfirmKind.New);
        store.Get("S18")!.Heroes.Should().Contain("悠米");
    }

    [Fact]
    public void SeasonRegistry_MapSeasonToPatch_ReturnsCorrectMapping()
    {
        var registry = new SeasonRegistry();

        registry.MapSeasonToPatch("S16.5").Should().Be("9.11-9.16");
        registry.MapSeasonToPatch("unknown").Should().BeNull();
    }

    private static LiveViewModel CreateViewModel(SeasonRuntime runtime) =>
        CreateViewModel(null, runtime);

    private static LiveViewModel CreateViewModel(
        AnnotationStore? store,
        SeasonRuntime runtime)
    {
        store ??= new AnnotationStore(new FakeFileSystem(), "corrections");
        return new LiveViewModel(store, Evaluate, runtime: runtime);
    }

    private static DecisionPanelResult Evaluate(GameStateSnapshot state) =>
        new(state, Array.Empty<TacticalRecommendation>(), AdvisorAdvice: null);

    private static SeasonDictionary Dict(string seasonId, params string[] heroes) =>
        new(
            seasonId,
            new HashSet<string>(heroes, StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    private sealed class FakeReader : ISeasonDictionaryReader
    {
        private readonly IReadOnlyDictionary<string, SeasonDictionary> _dictionaries;

        public FakeReader(IReadOnlyDictionary<string, SeasonDictionary> dictionaries) =>
            _dictionaries = dictionaries;

        public SeasonDictionary? Get(string seasonId) =>
            _dictionaries.TryGetValue(seasonId, out var d) ? d : null;

        public bool TryGetHero(string seasonId, string name) =>
            Get(seasonId)?.Heroes.Contains(name) ?? false;

        public bool TryGetItem(string seasonId, string name) =>
            Get(seasonId)?.Items.Contains(name) ?? false;
    }

    private sealed class FakeWriter : ISeasonDictionaryWriter
    {
        public void AddCandidate(string seasonId, CandidateMeta candidate)
        {
        }

        public void Confirm(string seasonId, string entity, ConfirmKind kind)
        {
        }

        public void Revoke(string seasonId, string entity)
        {
        }

        public IReadOnlyList<CandidateMeta> GetPendingCandidates(string seasonId) => Array.Empty<CandidateMeta>();
    }

    private sealed class FakeFileSystem : IFileSystem
    {
        private readonly List<string> _lines = new();

        public void CreateDirectory(string path)
        {
        }

        public Task AppendLineAsync(string path, string line, CancellationToken cancellationToken = default)
        {
            _lines.Add(line);
            return Task.CompletedTask;
        }

        public Task<string?> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(string.Join("\n", _lines));
    }

    private sealed class StoreFixture : IDisposable
    {
        private bool _disposed;

        public StoreFixture() => Directory.CreateDirectory(Root);

        public string Root { get; } = Path.Combine(Path.GetTempPath(), "ChanSight.SeasonUi", Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}