using ChanSight.Core.Data;
using ChanSight.Core.Engine;
using FluentAssertions;
using System.Globalization;

namespace ChanSight.Tests.Engine;

public sealed class TacticalAdvisorTests
{
    private static string ValidDirectory => Path.Combine(
        AppContext.BaseDirectory, "Engine", "CompTemplates", "valid");

    private static TacticalAdvisor CreateAdvisor(TacticalAdvisorOptions? options = null) =>
        new(new HypergeometricEngine(new PoolConfig()), new CompKnowledgeBase(ValidDirectory), options);

    [Fact]
    public void Evaluate_GoldZeroEmptyBoard_CompEmpty_EconomyHold_NoRollAdvice()
    {
        var advisor = CreateAdvisor();
        var state = Snapshot(gold: 0);

        var result = advisor.Evaluate(state);

        result.Where(r => r.Kind == AdviceKind.CompRecommend).Should().BeEmpty();
        result.Where(r => r.Kind == AdviceKind.RollDecision).Should().BeEmpty();

        var economy = result.Where(r => r.Kind == AdviceKind.EconomyDecision).ToList();
        economy.Should().ContainSingle();
        economy[0].Verdict.Should().Be(Verdict.Hold);
    }

    [Fact]
    public void Evaluate_Gold50Plus_Level1_EconomyLevelUp_NeverRoll()
    {
        var advisor = CreateAdvisor();
        var state = Snapshot(gold: 50, level: 1);

        var result = advisor.Evaluate(state);

        var economy = result.Where(r => r.Kind == AdviceKind.EconomyDecision).ToList();
        economy.Should().ContainSingle();
        economy[0].Verdict.Should().BeOneOf(Verdict.LevelUp, Verdict.Hold);

        result.Should().NotContain(r => r.Verdict == Verdict.Roll || r.Verdict == Verdict.SmallRoll);
    }

    [Fact]
    public void Evaluate_FourCostTwoStar_LowGold_RollIsStop()
    {
        var advisor = CreateAdvisor();
        var state = Snapshot(
            gold: 5,
            level: 8,
            board: new[] { Unit(0, "亚索", 2) });

        var result = advisor.Evaluate(state);

        var roll = result.Where(r => r.Kind == AdviceKind.RollDecision).ToList();
        roll.Should().ContainSingle();
        roll[0].Verdict.Should().Be(Verdict.Stop);
    }

    [Fact]
    public void Evaluate_OpponentContest_LowersRollProbability()
    {
        var advisor = CreateAdvisor();

        var withoutContest = advisor.Evaluate(Snapshot(
            gold: 50,
            level: 6,
            board: new[] { Unit(0, "盖伦", 1) }));

        var withContest = advisor.Evaluate(Snapshot(
            gold: 50,
            level: 6,
            board: new[] { Unit(0, "盖伦", 1) },
            opponents: new[]
            {
                new OpponentSnapshot(0, PlayerName: null, Hp: 100, Level: 6, GoldEstimate: 0,
                    Enumerable.Range(0, 4).Select(i => new BoardUnitState(i, "盖伦", 1, 1, Array.Empty<string>())).ToArray(),
                    BenchUnits: Array.Empty<BoardUnitState>(),
                    Version: 0),
            }));

        var pNoContest = ProbabilityOf(SingleRoll(withoutContest));
        var pContest = ProbabilityOf(SingleRoll(withContest));

        pNoContest.Should().BeGreaterThan(0.0);
        pContest.Should().BeGreaterThan(0.0);
        pContest.Should().BeLessThan(pNoContest);
    }

    [Fact]
    public void Evaluate_ShopVisibleWithFiveCards_NeverRolls()
    {
        var advisor = CreateAdvisor();
        var state = Snapshot(
            gold: 50,
            board: new[] { Unit(0, "盖伦", 1) },
            shop: Enumerable.Range(0, 5).Select(i => new ShopCardState(i, "索拉卡", 4)).ToArray());

        var result = advisor.Evaluate(state);

        result.Should().NotContain(r => r.Kind == AdviceKind.RollDecision && (r.Verdict == Verdict.Roll || r.Verdict == Verdict.SmallRoll));
    }

    [Fact]
    public void Evaluate_ShopShowsCandidateHero_Locks()
    {
        var advisor = CreateAdvisor();
        var state = Snapshot(
            gold: 50,
            board: new[] { Unit(0, "盖伦", 1) },
            shop: new[] { new ShopCardState(0, "盖伦", 1) });

        var result = advisor.Evaluate(state);

        var lockAdvice = result.Single(r => r.Kind == AdviceKind.RollDecision);
        lockAdvice.Verdict.Should().Be(Verdict.Lock);
    }

    [Fact]
    public void Evaluate_HeldTemplates_RecommendsTop3WithMissingCoreUnits()
    {
        var advisor = CreateAdvisor();
        var state = Snapshot(
            gold: 30,
            board: new[]
            {
                Unit(0, "拉克丝", 1),
                Unit(1, "佐伊", 1),
            },
            bench: new[] { Unit(0, "艾希", 1) });

        var result = advisor.Evaluate(state);

        var comps = result.Where(r => r.Kind == AdviceKind.CompRecommend).ToList();
        comps.Should().HaveCount(2);

        comps[0].Reason.Should().Contain("2");
        comps[0].Evidence.Should().Contain(e => e.Contains("测试法师"));
        comps[0].Evidence.Should().NotContain(e => e.Contains("缺失核心"));

        comps[1].Reason.Should().Contain("1");
        comps[1].Evidence.Should().Contain(e => e.Contains("缺失核心") && e.Contains("图奇") && e.Contains("崔丝塔娜"));
    }

    [Fact]
    public void Evaluate_NoTemplateHits_CompRecommendEmpty()
    {
        var advisor = CreateAdvisor();
        var state = Snapshot(
            gold: 20,
            board: new[] { Unit(0, "亚索", 1), Unit(1, "阿狸", 1) });

        var result = advisor.Evaluate(state);

        result.Where(r => r.Kind == AdviceKind.CompRecommend).Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_GoldAboveThreshold_BelowMaxLevel_LevelsUp()
    {
        var advisor = CreateAdvisor();
        var state = Snapshot(gold: 60, level: 5);

        var result = advisor.Evaluate(state);

        var economy = result.Single(r => r.Kind == AdviceKind.EconomyDecision);
        economy.Verdict.Should().Be(Verdict.LevelUp);
    }

    [Fact]
    public void Evaluate_NonPlanningPhase_NoEconomyAndNoRollAdvice()
    {
        var advisor = CreateAdvisor();
        var state = Snapshot(gold: 50, level: 5, phase: GamePhase.Combat, board: new[] { Unit(0, "盖伦", 1) });

        var result = advisor.Evaluate(state);

        result.Where(r => r.Kind == AdviceKind.EconomyDecision).Should().BeEmpty();
        result.Where(r => r.Kind == AdviceKind.RollDecision).Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_HighProbability_CustomThreshold_Rolls()
    {
        var advisor = CreateAdvisor(new TacticalAdvisorOptions(RollThreshold: 0.01));
        var state = Snapshot(gold: 50, level: 6, board: new[] { Unit(0, "盖伦", 1) });

        var result = advisor.Evaluate(state);

        SingleRoll(result).Verdict.Should().Be(Verdict.Roll);
    }

    [Fact]
    public void Evaluate_MediumProbability_CustomThreshold_SmallRolls()
    {
        var advisor = CreateAdvisor(new TacticalAdvisorOptions(RollThreshold: 0.5, StopThreshold: 0.00001));
        var state = Snapshot(gold: 50, level: 6, board: new[] { Unit(0, "盖伦", 1) });

        var result = advisor.Evaluate(state);

        SingleRoll(result).Verdict.Should().Be(Verdict.SmallRoll);
    }

    [Fact]
    public void Evaluate_EmptyHeroNames_ReturnsWithoutThrowing()
    {
        var advisor = CreateAdvisor();
        var state = Snapshot(
            gold: 30,
            board: new[] { new BoardUnitState(0, null, 0, 0, Array.Empty<string>()) });

        var act = () => advisor.Evaluate(state);

        var result = act.Should().NotThrow().Which;
        result.Where(r => r.Kind == AdviceKind.RollDecision).Should().BeEmpty();
        result.Where(r => r.Kind == AdviceKind.CompRecommend).Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_AllRecommendations_HaveRiskInUnitIntervalAndEvidence()
    {
        var advisor = CreateAdvisor();
        var state = Snapshot(
            gold: 40,
            level: 7,
            board: new[]
            {
                Unit(0, "拉克丝", 1),
                Unit(1, "佐伊", 1),
                Unit(2, "劫", 2),
            });

        var result = advisor.Evaluate(state);

        result.Should().NotBeEmpty();
        foreach (var r in result)
        {
            r.Risk.Should().BeInRange(0.0, 1.0);
            double.IsNaN(r.Risk).Should().BeFalse();
            r.Reason.Should().NotBeNullOrWhiteSpace();
            r.Evidence.Should().NotBeEmpty();
        }
    }

    [Fact]
    public void Evaluate_SameInput_ProducesDeterministicOutput()
    {
        var advisor = CreateAdvisor();
        var state = Snapshot(
            gold: 40,
            level: 7,
            board: new[] { Unit(0, "劫", 2), Unit(1, "拉克丝", 1) });

        var first = advisor.Evaluate(state);
        var second = advisor.Evaluate(state);

        first.Should().BeEquivalentTo(second);
    }

    private static TacticalRecommendation SingleRoll(IReadOnlyList<TacticalRecommendation> result)
    {
        var rolls = result.Where(r => r.Kind == AdviceKind.RollDecision).ToList();
        rolls.Should().ContainSingle();
        return rolls[0];
    }

    private static double ProbabilityOf(TacticalRecommendation r)
    {
        var prob = r.Evidence.Single(e => e.StartsWith("prob=", StringComparison.Ordinal));
        return double.Parse(prob["prob=".Length..], CultureInfo.InvariantCulture);
    }

    private static GameStateSnapshot Snapshot(
        int gold = 0,
        int level = 1,
        GamePhase phase = GamePhase.Planning,
        IReadOnlyList<BoardUnitState>? board = null,
        IReadOnlyList<BoardUnitState>? bench = null,
        IReadOnlyList<ShopCardState>? shop = null,
        IReadOnlyList<OpponentSnapshot>? opponents = null) =>
        new(
            Stage: "1-1",
            Phase: phase,
            Gold: gold,
            Level: level,
            Exp: 0,
            Hp: 100,
            Streak: 0,
            PlayerName: null,
            BoardUnits: board ?? Array.Empty<BoardUnitState>(),
            BenchUnits: bench ?? Array.Empty<BoardUnitState>(),
            ShopCards: shop ?? Array.Empty<ShopCardState>(),
            Opponents: opponents ?? Array.Empty<OpponentSnapshot>(),
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