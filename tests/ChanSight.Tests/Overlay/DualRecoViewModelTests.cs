using ChanSight.Core.Annotation;
using ChanSight.Core.Engine;
using ChanSight.Overlay.Models;
using ChanSight.Overlay.ViewModels;
using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using FluentAssertions;

namespace ChanSight.Tests.Overlay;

public sealed class DualRecoViewModelTests
{
    [Fact]
    public void RefreshAlgorithm_PopulatesAlgorithmRecommendations_WithAlgorithmSourceTag()
    {
        var vm = CreateViewModel();
        vm.ApplyUpdate(CreateUpdate(gold: 60, level: 5));

        vm.RefreshAlgorithmRecommendations();

        vm.AlgorithmRecommendations.Should().NotBeEmpty();
        vm.AlgorithmRecommendations.Should().OnlyContain(r => r.SourceTag == "算法");
        vm.AlgorithmRecommendations.Should().OnlyContain(r => !string.IsNullOrEmpty(r.Verdict));
        vm.AlgorithmRecommendations.Should().OnlyContain(r => !string.IsNullOrEmpty(r.Reason));
        vm.AlgorithmRecommendations.Should().OnlyContain(r => r.GeneratedAt != default);
    }

    [Fact]
    public async Task RequestLlmAdvice_PopulatesLlmAdvice_WithLlmSourceTag()
    {
        var vm = CreateViewModel();
        vm.ApplyUpdate(CreateUpdate(gold: 40, level: 6));

        await vm.RequestLlmAdviceAsync();

        vm.LlmAdvice.Should().NotBeNull();
        vm.LlmAdvice!.SourceTag.Should().Be("LLM");
        vm.LlmAdvice.Suggestion.Should().Be("升级人口");
        vm.LlmAdvice.Reason.Should().Be("金币充裕");
        vm.LlmAdvice.Confidence.Should().BeInRange(0.0, 1.0);
        vm.HasLlmAdvice.Should().BeTrue();
    }

    [Fact]
    public async Task AlgorithmAndLlm_Coexist_WithoutOverwriting()
    {
        var vm = CreateViewModel();
        vm.ApplyUpdate(CreateUpdate(gold: 60, level: 5));

        vm.RefreshAlgorithmRecommendations();
        var algorithmCount = vm.AlgorithmRecommendations.Count;
        await vm.RequestLlmAdviceAsync();

        vm.AlgorithmRecommendations.Should().HaveCount(algorithmCount);
        vm.AlgorithmRecommendations.Should().OnlyContain(r => r.SourceTag == "算法");
        vm.LlmAdvice.Should().NotBeNull();
        vm.LlmAdvice!.SourceTag.Should().Be("LLM");

        // 再次刷新算法不应清掉 LLM 建议。
        vm.RefreshAlgorithmRecommendations();
        vm.LlmAdvice.Should().NotBeNull();
    }

    [Fact]
    public void DataHealth_Uncalibrated_IsStaleAndWarningContainsUncalibrated()
    {
        var vm = CreateViewModel();

        // 自动识别产物 = 未校准。
        vm.ApplyUpdate(CreateUpdate(frameId: "auto-1"));
        vm.DataHealth.Should().Be(DataHealth.Uncalibrated);
        vm.IsStale.Should().BeTrue();
        vm.StaleWarning.Should().Contain("未校准");

        // 手动 VLM 识别标记已校准。
        var manager = new GameStateManager();
        var calibrated = CreateViewModel(manager);
        calibrated.ApplyUpdate(CreateUpdate(frameId: "auto-2"));
        calibrated.DataHealth = DataHealth.Calibrated;
        calibrated.IsStale.Should().BeFalse();
        calibrated.StaleWarning.Should().BeEmpty();
    }

    [Fact]
    public async Task RequestLlmAdvice_ConstructsManualRequestAdvisorEvent()
    {
        AdvisorEvent? capturedEvent = null;
        var vm = CreateViewModel(
            evaluateWithAdvisor: (state, e, _) =>
            {
                capturedEvent = e;
                return Task.FromResult(new DecisionPanelResult(
                    state,
                    Array.Empty<TacticalRecommendation>(),
                    new AdvisorSuggestion("建议", "理由", 0.5)));
            });
        vm.ApplyUpdate(CreateUpdate(gold: 30, level: 4));

        await vm.RequestLlmAdviceAsync();

        capturedEvent.Should().NotBeNull();
        capturedEvent!.Type.Should().Be(AdvisorEventType.ManualRequest);
        capturedEvent.Context.Should().Contain("阶段");
        capturedEvent.Context.Should().Contain("金币");
        capturedEvent.Context.Should().Contain("己方阵容");
    }

    [Fact]
    public async Task RequestLlmAdvice_WithoutClosure_DoesNotThrow()
    {
        var store = new AnnotationStore(new FakeFileSystem(), "corrections");
        var evaluate = EvaluateAlgorithm();
        var vm = new LiveViewModel(store, evaluate, evaluateWithAdvisor: null);
        vm.ApplyUpdate(CreateUpdate(gold: 30));

        await vm.RequestLlmAdviceAsync();

        vm.LlmAdvice.Should().BeNull();
        vm.Status.Should().Contain("不可用");
    }

    private static LiveViewModel CreateViewModel(
        GameStateManager? manager = null,
        Func<GameStateSnapshot, AdvisorEvent, CancellationToken, Task<DecisionPanelResult>>? evaluateWithAdvisor = null)
    {
        var store = new AnnotationStore(new FakeFileSystem(), "corrections");

        evaluateWithAdvisor ??= (state, _, _) => Task.FromResult(new DecisionPanelResult(
            state,
            Array.Empty<TacticalRecommendation>(),
            new AdvisorSuggestion("升级人口", "金币充裕", 0.9)));

        return new LiveViewModel(store, EvaluateAlgorithm(), evaluateWithAdvisor: evaluateWithAdvisor);
    }

    private static Func<GameStateSnapshot, DecisionPanelResult> EvaluateAlgorithm() =>
        state => new DecisionPanelResult(
            state,
            new[]
            {
                new TacticalRecommendation(AdviceKind.EconomyDecision, Verdict.Hold, 60, "保持持息", 0.1, Array.Empty<string>()),
            },
            AdvisorAdvice: null);

    private static LiveFrameUpdate CreateUpdate(
        int gold = 0,
        int level = 1,
        string stage = "1-1",
        int hp = 100,
        string frameId = "frame-1")
    {
        var state = new GameStateSnapshot(
            Stage: stage,
            Phase: GamePhase.Planning,
            Gold: gold,
            Level: level,
            Exp: 0,
            Hp: hp,
            Streak: 0,
            PlayerName: null,
            BoardUnits: Enumerable.Range(0, 28).Select(i => new BoardUnitState(i, i == 0 ? "盖伦" : null, i == 0 ? 1 : 0, i == 0 ? 1 : 0, Array.Empty<string>())).ToArray(),
            BenchUnits: Enumerable.Range(0, 9).Select(i => new BoardUnitState(i, null, 0, 0, Array.Empty<string>())).ToArray(),
            ShopCards: Enumerable.Range(0, 5).Select(i => new ShopCardState(i, null, 0)).ToArray(),
            Opponents: Array.Empty<OpponentSnapshot>(),
            Version: 1);

        return new LiveFrameUpdate(state, new RecognitionFrame
        {
            Gold = gold,
            Level = level,
            Stage = stage,
            Hp = hp,
        }, frameId);
    }

    private sealed class FakeFileSystem : IFileSystem
    {
        public void CreateDirectory(string path)
        {
        }

        public Task AppendLineAsync(string path, string line, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<string?> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }
}