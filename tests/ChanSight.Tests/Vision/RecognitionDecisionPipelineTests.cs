using ChanSight.Core.Data;
using ChanSight.Core.Engine;
using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using FluentAssertions;

namespace ChanSight.Tests.Vision;

public sealed class RecognitionDecisionPipelineTests
{
    private static RecognitionDecisionPipeline CreatePipeline(FakeAdvisorVlmClient? fake = null)
    {
        var manager = new GameStateManager();
        var adapter = new RecognitionToGameStateAdapter(manager);
        var tacticalAdvisor = new TacticalAdvisor(
            new HypergeometricEngine(new PoolConfig()),
            new CompKnowledgeBase());
        var llmAdvisor = new LLMAdvisor(fake ?? new FakeAdvisorVlmClient());
        var panel = new DecisionPanelService(tacticalAdvisor, llmAdvisor);
        return new RecognitionDecisionPipeline(adapter, manager, panel);
    }

    [Fact]
    public void Process_KnownHeroes_PopulatesManagerAndReturnsAdvice()
    {
        var manager = new GameStateManager();
        var adapter = new RecognitionToGameStateAdapter(manager);
        var panel = new DecisionPanelService(
            new TacticalAdvisor(new HypergeometricEngine(new PoolConfig()), new CompKnowledgeBase()),
            new LLMAdvisor(new FakeAdvisorVlmClient()));
        var pipeline = new RecognitionDecisionPipeline(adapter, manager, panel);

        var result = pipeline.Process(BuildKnownHeroFrame(gold: 60, level: 5));

        result.Should().NotBeNull();
        result.AdvisorAdvice.Should().BeNull();
        result.State.Gold.Should().Be(60);
        result.State.Level.Should().Be(5);
        result.State.Stage.Should().Be("3-1");
        result.AlgorithmAdvice.Should().Contain(r => r.Kind == AdviceKind.EconomyDecision);

        var snapshot = manager.Current;
        snapshot.BoardUnits.Should().Contain(u => u.Name != null);
        snapshot.Gold.Should().Be(60);
        snapshot.Level.Should().Be(5);
        snapshot.Stage.Should().Be("3-1");
    }

    [Fact]
    public void Process_EmptyFrame_DoesNotThrowAndYieldsNoOrOnlyHoldAdvice()
    {
        var pipeline = CreatePipeline();

        var result = pipeline.Process(BuildEmptyFrame());

        result.Should().NotBeNull();
        result.AdvisorAdvice.Should().BeNull();
        result.AlgorithmAdvice.Should().OnlyContain(r => r.Verdict == Verdict.Hold);
    }

    [Fact]
    public async Task ProcessWithAdvisorAsync_ValidJson_ReturnsAdvisorAdvice()
    {
        var fake = new FakeAdvisorVlmClient()
            .QueueResult("""{"suggestion":"锁定法师转阵","reason":"已集齐核心法师","confidence":0.9}""");
        var pipeline = CreatePipeline(fake);

        var result = await pipeline.ProcessWithAdvisorAsync(
            BuildKnownHeroFrame(gold: 60, level: 5),
            new AdvisorEvent(AdvisorEventType.MajorTransition, "3-1 大转阵"));

        result.AdvisorAdvice.Should().NotBeNull();
        result.AdvisorAdvice!.Suggestion.Should().NotBeNullOrWhiteSpace();
        result.AdvisorAdvice.Reason.Should().NotBeNullOrWhiteSpace();
        result.AdvisorAdvice.Confidence.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public void Process_NullFrame_ThrowsArgumentNullException()
    {
        var pipeline = CreatePipeline();

        var act = () => pipeline.Process(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ProcessWithAdvisorAsync_NullFrame_ThrowsArgumentNullException()
    {
        var pipeline = CreatePipeline();

        var act = async () => await pipeline.ProcessWithAdvisorAsync(
            null!,
            new AdvisorEvent(AdvisorEventType.HexAugment, "海克斯三选一"));

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ProcessWithAdvisorAsync_NullEvent_ThrowsArgumentNullException()
    {
        var pipeline = CreatePipeline();

        var act = async () => await pipeline.ProcessWithAdvisorAsync(BuildKnownHeroFrame(), null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Process_MapsBoardBenchShopGridsToStateObjects()
    {
        var pipeline = CreatePipeline();

        var result = pipeline.Process(BuildKnownHeroFrame(gold: 30));

        var snapshot = result.State;

        snapshot.BoardUnits.Should().HaveCount(28);
        snapshot.BenchUnits.Should().HaveCount(9);
        snapshot.ShopCards.Should().HaveCount(5);

        var boardGaren = snapshot.BoardUnits[0];
        boardGaren.Name.Should().Be("盖伦");
        boardGaren.Star.Should().Be(1);
        boardGaren.CopyCount.Should().Be(1);

        var benchLux = snapshot.BenchUnits[0];
        benchLux.Name.Should().Be("拉克丝");
        benchLux.Star.Should().Be(1);

        var shopCard = snapshot.ShopCards[0];
        shopCard.Name.Should().Be("盖伦");
        shopCard.Cost.Should().Be(1);
    }

    private static RecognitionFrame BuildKnownHeroFrame(int gold = 60, int level = 5)
    {
        static UnitCell unit(string name) =>
            new(name, 1, Array.Empty<ItemStack>(), 0.95, SourceTier.T0);
        static UnitCell empty() => new(null, 0, Array.Empty<ItemStack>(), 1.0, SourceTier.T3);

        var board = Enumerable.Range(0, 28).Select(_ => empty()).ToArray();
        board[0] = unit("盖伦");
        board[1] = unit("拉克丝");
        board[2] = unit("佐伊");

        var bench = Enumerable.Range(0, 9).Select(_ => empty()).ToArray();
        bench[0] = unit("拉克丝");
        bench[1] = unit("艾希");

        var shop = Enumerable.Range(0, 5)
            .Select(_ => new ShopCard(string.Empty, 0, 1.0, SourceTier.T3))
            .ToArray();
        shop[0] = new ShopCard("盖伦", 1, 0.9, SourceTier.T1);

        return new RecognitionFrame
        {
            SourceTier = SourceTier.T0,
            CorrectionFlag = false,
            Timestamp = "2026-09-15T00:00:00Z",
            Confidence = 1.0,
            Gold = gold,
            Level = level,
            Stage = "3-1",
            Hp = 80,
            Exp = 0,
            BoardCells = board,
            BenchCells = bench,
            ShopCards = shop,
        };
    }

    private static RecognitionFrame BuildEmptyFrame() => new()
    {
        SourceTier = SourceTier.T0,
        CorrectionFlag = false,
        Timestamp = "2026-09-15T00:00:00Z",
        Confidence = 1.0,
        Gold = 0,
        Level = 1,
        Stage = "1-1",
        Hp = 100,
        Exp = 0,
        BoardCells = Enumerable.Range(0, 28)
            .Select(_ => new UnitCell(null, 0, Array.Empty<ItemStack>(), 1.0, SourceTier.T3))
            .ToArray(),
        BenchCells = Enumerable.Range(0, 9)
            .Select(_ => new UnitCell(null, 0, Array.Empty<ItemStack>(), 1.0, SourceTier.T3))
            .ToArray(),
        ShopCards = Enumerable.Range(0, 5)
            .Select(_ => new ShopCard(string.Empty, 0, 1.0, SourceTier.T3))
            .ToArray(),
    };
}