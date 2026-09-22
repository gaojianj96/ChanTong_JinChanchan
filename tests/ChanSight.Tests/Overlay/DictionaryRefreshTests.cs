using ChanSight.Core.Annotation;
using ChanSight.Core.Engine;
using ChanSight.Core.Interfaces;
using ChanSight.Core.Models;
using ChanSight.Core.Season;
using ChanSight.Overlay.ViewModels;
using ChanSight.Vision.Models;
using FluentAssertions;

namespace ChanSight.Tests.Overlay;

/// <summary>
/// 字典修改"退出字典视图实时生效"闭环: 确认候选(Confirm New) → SeasonRuntime 实时重读 →
/// 退出字典视图触发 RefreshCandidates → 实时视图候选立即含新条目(无需重启/新对局)。
/// </summary>
public sealed class DictionaryRefreshTests : IDisposable
{
    private const string SeasonId = "S16.5";

    private readonly string _rootDirectory;
    private bool _disposed;

    public DictionaryRefreshTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), "ChanSight.DictRefresh", Guid.NewGuid().ToString("N"));
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
    public void ConfirmNew_ThenExitDictionaryView_RefreshesLiveHeroCandidates()
    {
        var store = new SeasonDictionaryStore(_rootDirectory, BuiltInSeeds());
        var runtime = new SeasonRuntime(store);
        var live = CreateLiveViewModel(runtime);

        store.AddCandidate(SeasonId, NewCandidate("全新英雄"));
        live.HeroCandidates.Should().NotContain("全新英雄");

        store.Confirm(SeasonId, "全新英雄", ConfirmKind.New);
        runtime.Heroes.Should().Contain("全新英雄");

        // 退出字典视图: 通过 MainWindowViewModel 的退出钩子触发 RefreshCandidates。
        var main = new MainWindowViewModel(Finder(), live.RefreshCandidates);
        main.IsDictionaryMode = true;
        main.IsDictionaryMode = false;

        live.HeroCandidates.Should().Contain("全新英雄");
    }

    [Fact]
    public void RefreshCandidates_RebuildsCandidateList_NotOneTimeSnapshot()
    {
        var store = new SeasonDictionaryStore(_rootDirectory, BuiltInSeeds());
        var runtime = new SeasonRuntime(store);
        var live = CreateLiveViewModel(runtime);

        var initialCount = live.HeroCandidates.Count;

        store.AddCandidate(SeasonId, NewCandidate("刚确认英雄"));
        store.Confirm(SeasonId, "刚确认英雄", ConfirmKind.New);

        live.RefreshCandidates();

        live.HeroCandidates.Should().Contain("刚确认英雄");
        live.HeroCandidates.Should().HaveCount(initialCount + 1);
    }

    private static LiveViewModel CreateLiveViewModel(SeasonRuntime runtime)
    {
        var store = new AnnotationStore(new FakeFileSystem(), "corrections");

        // 重算闭包: 直接返回一条固定算法建议, 不触及真实 TacticalAdvisor。
        Func<GameStateSnapshot, DecisionPanelResult> evaluate = state => new DecisionPanelResult(
            state,
            new[]
            {
                new TacticalRecommendation(AdviceKind.EconomyDecision, Verdict.Hold, 60, "保持持息", 0.1, Array.Empty<string>()),
            },
            AdvisorAdvice: null);

        return new LiveViewModel(store, evaluate, runtime: runtime);
    }

    private static CandidateMeta NewCandidate(string entity) => new(entity, "hero", null, 0.9, DateTimeOffset.UtcNow);

    private static IWindowFinder Finder() => new StubWindowFinder();

    private static IReadOnlyDictionary<string, SeasonDictionary> BuiltInSeeds() =>
        new Dictionary<string, SeasonDictionary>(StringComparer.Ordinal)
        {
            [SeasonDictionarySeed.DefaultSeasonId] = SeasonDictionarySeed.DefaultDictionary,
        };

    private sealed class FakeFileSystem : IFileSystem
    {
        public void CreateDirectory(string path)
        {
        }

        public Task AppendLineAsync(string path, string line, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<string?> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);
    }

    private sealed class StubWindowFinder : IWindowFinder
    {
        public ValueTask<IReadOnlyList<WindowTarget>> FindWindowsAsync(
            WindowSearchOptions? options = null,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<WindowTarget>>(Array.Empty<WindowTarget>());

        public ValueTask<WindowTarget?> GetWindowTargetAsync(nint hwnd, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<WindowTarget?>(null);
    }
}
