using ChanSight.Core.Data;
using ChanSight.Core.Engine;
using ChanSight.Core.Season;
using ChanSight.Vision.Services;
using FluentAssertions;

namespace ChanSight.Tests.Engine;

public sealed class ModeStrategyTests
{
    private static TacticalAdvisor CreateAdvisor(TacticalAdvisorOptions? options = null, SeasonRuntime? runtime = null) =>
        new(new HypergeometricEngine(new PoolConfig()), new CompKnowledgeBase(), options, runtime);

    [Fact]
    public void Resolve_KnownModes_ReturnDifferentParameterSets()
    {
        var fortune = ModeStrategy.Resolve("恭喜发财");
        var match = ModeStrategy.Resolve("匹配");
        var turbo = ModeStrategy.Resolve("狂暴");

        fortune.Should().NotBe(match);
        fortune.Should().NotBe(turbo);
        match.Should().NotBe(turbo);
    }

    [Fact]
    public void Resolve_NullOrEmpty_ReturnsDefaultOptions()
    {
        ModeStrategy.Resolve(null).Should().Be(new TacticalAdvisorOptions());
        ModeStrategy.Resolve(string.Empty).Should().Be(new TacticalAdvisorOptions());
        ModeStrategy.Resolve("未知模式").Should().Be(new TacticalAdvisorOptions());
    }

    [Fact]
    public void GetModeHint_Fortune_NonEmptyAndContainsModeName()
    {
        var hint = ManualFrameVlmService.GetModeHint("恭喜发财");

        hint.Should().NotBeNullOrWhiteSpace();
        hint.Should().Contain("恭喜发财");
    }

    [Fact]
    public void GetModeHint_Unknown_ReturnsEmpty()
    {
        ManualFrameVlmService.GetModeHint("未知").Should().BeEmpty();
        ManualFrameVlmService.GetModeHint(null).Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_DifferentModeOptions_ProduceDifferentAdvice()
    {
        var fortune = CreateAdvisor(ModeStrategy.Resolve("恭喜发财"));
        var match = CreateAdvisor(ModeStrategy.Resolve("匹配"));
        var state = Snapshot(gold: 40, level: 5);

        var fortuneVerdict = SingleEconomy(fortune.Evaluate(state)).Verdict;
        var matchVerdict = SingleEconomy(match.Evaluate(state)).Verdict;

        fortuneVerdict.Should().Be(Verdict.LevelUp);
        matchVerdict.Should().Be(Verdict.Hold);
        fortuneVerdict.Should().NotBe(matchVerdict);
    }

    [Fact]
    public void Evaluate_RuntimeModeSwitch_ChangesAdviceWithoutReconstruction()
    {
        var runtime = SeasonRuntime.CreateDefault();
        runtime.Select("S16.5", "恭喜发财");
        var advisor = CreateAdvisor(runtime: runtime);
        var state = Snapshot(gold: 40, level: 5);

        SingleEconomy(advisor.Evaluate(state)).Verdict.Should().Be(Verdict.LevelUp);

        runtime.Select("S16.5", "匹配");

        SingleEconomy(advisor.Evaluate(state)).Verdict.Should().Be(Verdict.Hold);
    }

    private static TacticalRecommendation SingleEconomy(IReadOnlyList<TacticalRecommendation> result)
    {
        var economy = result.Where(r => r.Kind == AdviceKind.EconomyDecision).ToList();
        economy.Should().ContainSingle();
        return economy[0];
    }

    private static GameStateSnapshot Snapshot(
        int gold = 0,
        int level = 1,
        GamePhase phase = GamePhase.Planning,
        IReadOnlyList<BoardUnitState>? board = null) =>
        new(
            Stage: "3-1",
            Phase: phase,
            Gold: gold,
            Level: level,
            Exp: 0,
            Hp: 80,
            Streak: 0,
            PlayerName: null,
            BoardUnits: board ?? Array.Empty<BoardUnitState>(),
            BenchUnits: Array.Empty<BoardUnitState>(),
            ShopCards: Array.Empty<ShopCardState>(),
            Opponents: Array.Empty<OpponentSnapshot>(),
            Version: 0);
}
