using ChanSight.Core.Data;
using ChanSight.Core.Engine;
using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using FluentAssertions;

namespace ChanSight.Tests.Vision;

public sealed class DecisionPanelServiceTests
{
    private static DecisionPanelService CreateService(FakeAdvisorVlmClient? fake = null)
    {
        var tacticalAdvisor = new TacticalAdvisor(
            new HypergeometricEngine(new PoolConfig()),
            new CompKnowledgeBase());
        var llmAdvisor = new LLMAdvisor(fake ?? new FakeAdvisorVlmClient());
        return new DecisionPanelService(tacticalAdvisor, llmAdvisor);
    }

    [Fact]
    public void EvaluateAlgorithm_EmptyBoard_GoldAboveZero_ReturnsEconomyAdviceAndNullAdvisor()
    {
        var service = CreateService();
        var state = Snapshot(gold: 60, level: 5);

        var result = service.EvaluateAlgorithm(state);

        result.Should().NotBeNull();
        result.State.Should().BeSameAs(state);
        result.AdvisorAdvice.Should().BeNull();
        result.AlgorithmAdvice.Should().Contain(r => r.Kind == AdviceKind.EconomyDecision);
    }

    [Fact]
    public void EvaluateAlgorithm_GoldZeroEmptyBoard_DoesNotThrow()
    {
        var service = CreateService();
        var state = Snapshot(gold: 0);

        var result = service.EvaluateAlgorithm(state);

        result.Should().NotBeNull();
        result.AdvisorAdvice.Should().BeNull();
        result.AlgorithmAdvice.Should().OnlyContain(r => r.Kind != AdviceKind.RollDecision);
    }

    [Fact]
    public async Task EvaluateWithAdvisorAsync_ValidJson_ReturnsBothAdviceBranches()
    {
        var fake = new FakeAdvisorVlmClient()
            .QueueResult("""{"suggestion":"锁定法师转阵","reason":"已集齐核心法师","confidence":0.9}""");
        var service = CreateService(fake);
        var state = Snapshot(gold: 60, level: 5);

        var result = await service.EvaluateWithAdvisorAsync(
            state,
            new AdvisorEvent(AdvisorEventType.MajorTransition, "3-1 大转阵"));

        result.AlgorithmAdvice.Should().NotBeEmpty();
        result.AdvisorAdvice.Should().NotBeNull();
        result.AdvisorAdvice!.Suggestion.Should().NotBeNullOrWhiteSpace();
        result.AdvisorAdvice.Reason.Should().NotBeNullOrWhiteSpace();
        result.AdvisorAdvice.Confidence.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public void EvaluateAlgorithm_RepeatedCalls_DoNotConsumeAdvisorBudgetOrNetwork()
    {
        var fake = new FakeAdvisorVlmClient();
        var service = CreateService(fake);
        var state = Snapshot(gold: 60, level: 5);

        var results = Enumerable.Range(0, 10)
            .Select(_ => service.EvaluateAlgorithm(state))
            .ToList();

        results.Should().HaveCount(10);
        results.Should().OnlyContain(r => r.AdvisorAdvice == null);
        fake.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task EvaluateWithAdvisorAsync_NullState_ThrowsArgumentNullException()
    {
        var service = CreateService();

        var act = async () => await service.EvaluateWithAdvisorAsync(
            null!,
            new AdvisorEvent(AdvisorEventType.HexAugment, "海克斯三选一"));

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task EvaluateWithAdvisorAsync_NullEvent_ThrowsArgumentNullException()
    {
        var service = CreateService();

        var act = async () => await service.EvaluateWithAdvisorAsync(Snapshot(gold: 60), null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void EvaluateAlgorithm_NullState_ThrowsArgumentNullException()
    {
        var service = CreateService();

        var act = () => service.EvaluateAlgorithm(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DryRun_DemoSnapshot_EvaluatesWithoutThrowing()
    {
        var service = CreateService();

        var demoState = Snapshot(
            gold: 60,
            level: 5,
            board: new[]
            {
                Unit(0, "盖伦", 1),
                Unit(1, "拉克丝", 1),
                Unit(2, "佐伊", 1),
            },
            bench: new[]
            {
                Unit(3, "拉克丝", 1),
                Unit(4, "艾希", 1),
            });

        var result = service.EvaluateAlgorithm(demoState);

        result.Should().NotBeNull();
        result.AdvisorAdvice.Should().BeNull();
    }

    private static GameStateSnapshot Snapshot(
        int gold = 0,
        int level = 1,
        GamePhase phase = GamePhase.Planning,
        IReadOnlyList<BoardUnitState>? board = null,
        IReadOnlyList<BoardUnitState>? bench = null) =>
        new(
            Stage: "3-1",
            Phase: phase,
            Gold: gold,
            Level: level,
            Exp: 0,
            Hp: 80,
            Streak: 2,
            PlayerName: null,
            BoardUnits: board ?? Array.Empty<BoardUnitState>(),
            BenchUnits: bench ?? Array.Empty<BoardUnitState>(),
            ShopCards: Array.Empty<ShopCardState>(),
            Opponents: Array.Empty<OpponentSnapshot>(),
            Version: 0);

    private static BoardUnitState Unit(int slot, string name, int star) =>
        new(slot, name, star, StarCopies(star), Array.Empty<string>());

    private static int StarCopies(int star) => star switch
    {
        1 => 1,
        2 => 3,
        3 => 9,
        _ => 0,
    };
}