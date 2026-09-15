using ChanSight.Recorder.Services;
using FluentAssertions;

namespace ChanSight.Tests.Recorder;

public sealed class RetrospectiveScorerTests
{
    private readonly RetrospectiveScorer _scorer = new();

    [Fact]
    public void Score_EmptyInput_ReturnsDefaultsAndUnknown()
    {
        var score = _scorer.Score(Array.Empty<MatchEvent>());

        score.Factual.FinalStage.Should().BeEmpty();
        score.Factual.FinalHp.Should().Be(0);
        score.Factual.Outcome.Should().Be(MatchOutcome.Unknown);
        score.Expectation.DecisionCount.Should().Be(0);
        score.Expectation.AverageRisk.Should().Be(0);
        score.Expectation.ConfidenceScore.Should().Be(0);
        score.Execution.Rate.Should().Be(0);
        score.Luck.Should().BeEmpty();
        score.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Score_NullEvents_ThrowsArgumentNullException()
    {
        var act = () => _scorer.Score(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Score_SystemOutcomeWin_OutcomeIsWin()
    {
        var score = _scorer.Score(new[]
        {
            Event(1, MatchEventType.System, """{"outcome":"win"}""")
        });

        score.Factual.Outcome.Should().Be(MatchOutcome.Win);
    }

    [Fact]
    public void Score_SystemOutcomeLoss_OutcomeIsLoss()
    {
        var score = _scorer.Score(new[]
        {
            Event(1, MatchEventType.System, """{"outcome":"LOSS"}""")
        });

        score.Factual.Outcome.Should().Be(MatchOutcome.Loss);
    }

    [Fact]
    public void Score_StageChangeWithoutOutcome_AddsOpponentStrengthUnknownLuck()
    {
        var score = _scorer.Score(new[]
        {
            Event(1, MatchEventType.StageChange, """{"stage":"3-1","hp":72}""")
        });

        score.Factual.FinalStage.Should().Be("3-1");
        score.Factual.FinalHp.Should().Be(72);
        score.Factual.Outcome.Should().Be(MatchOutcome.Unknown);
        score.Luck.Should().ContainSingle(l => l.Label == "对手强度未知" && l.Severity == 0.5);
    }

    [Fact]
    public void Score_NotResultOnly_OutcomeIndependentOfDecisionQuality()
    {
        var a = _scorer.Score(new[]
        {
            Event(1, MatchEventType.Decision, """{"decisionId":"a1","kind":"roll","verdict":"risky","reason":"low gold","risk":0.9}"""),
            Event(2, MatchEventType.System, """{"outcome":"win"}""")
        });

        var b = _scorer.Score(new[]
        {
            Event(1, MatchEventType.Decision, """{"decisionId":"b1","kind":"roll","verdict":"safe","reason":"x","risk":0.2}"""),
            Event(2, MatchEventType.Decision, """{"decisionId":"b2","kind":"roll","verdict":"safe","reason":"x","risk":0.2}"""),
            Event(3, MatchEventType.Decision, """{"decisionId":"b3","kind":"roll","verdict":"safe","reason":"x","risk":0.2}"""),
            Event(4, MatchEventType.System, """{"outcome":"loss"}""")
        });

        a.Factual.Outcome.Should().Be(MatchOutcome.Win);
        b.Factual.Outcome.Should().Be(MatchOutcome.Loss);
        a.Expectation.AverageRisk.Should().Be(0.9);
        b.Expectation.AverageRisk.Should().BeApproximately(0.2, 1e-9);
        a.Expectation.AverageRisk.Should().BeGreaterThan(b.Expectation.AverageRisk);
        a.Expectation.DecisionCount.Should().Be(1);
        b.Expectation.DecisionCount.Should().Be(3);
    }

    [Fact]
    public void Score_InvalidDecisionPayload_NotCountedAndIssueRecorded()
    {
        var score = _scorer.Score(new[]
        {
            Event(1, MatchEventType.Decision, "{ not json"),
            Event(2, MatchEventType.Decision, """{"decisionId":"d1","kind":"roll","verdict":"ok","reason":"x","risk":1.5}"""),
            Event(3, MatchEventType.Decision, """{"decisionId":"d2","kind":"roll","verdict":"ok","reason":"x","risk":0.5}""")
        });

        score.Expectation.DecisionCount.Should().Be(1);
        score.Issues.Should().HaveCount(2);
        score.Issues.Should().Contain(i => i.Contains("Seq 1") && i.Contains("载荷非法"));
        score.Issues.Should().Contain(i => i.Contains("Seq 2") && i.Contains("载荷非法"));
    }

    [Fact]
    public void Score_ActionMatchingDecision_RateIsOne()
    {
        var score = _scorer.Score(new[]
        {
            Event(1, MatchEventType.Decision, """{"decisionId":"d1","kind":"roll","verdict":"ok","reason":"x","risk":0.5}"""),
            Event(2, MatchEventType.Action, """{"decisionId":"d1"}""")
        });

        score.Execution.TotalDecisions.Should().Be(1);
        score.Execution.DecisionsWithAction.Should().Be(1);
        score.Execution.Rate.Should().Be(1.0);
        score.Luck.Should().NotContain(l => l.Label == "执行缺失");
    }

    [Fact]
    public void Score_NoActionForDecision_RateZeroAndExecutionMissingLuck()
    {
        var score = _scorer.Score(new[]
        {
            Event(1, MatchEventType.Decision, """{"decisionId":"d1","kind":"roll","verdict":"ok","reason":"x","risk":0.5}"""),
            Event(2, MatchEventType.Decision, """{"decisionId":"d2","kind":"level","verdict":"ok","reason":"x","risk":0.3}""")
        });

        score.Execution.TotalDecisions.Should().Be(2);
        score.Execution.DecisionsWithAction.Should().Be(0);
        score.Execution.Rate.Should().Be(0);
        score.Luck.Should().Contain(l => l.Label == "执行缺失" && l.Severity == 0.3);
    }

    [Fact]
    public void Score_ActionWithUnknownDecisionId_NotCountedAsExecution()
    {
        var score = _scorer.Score(new[]
        {
            Event(1, MatchEventType.Decision, """{"decisionId":"d1","kind":"roll","verdict":"ok","reason":"x","risk":0.5}"""),
            Event(2, MatchEventType.Action, """{"decisionId":"unmatched"}""")
        });

        score.Execution.Rate.Should().Be(0);
        score.Luck.Should().Contain(l => l.Label == "执行缺失");
    }

    [Fact]
    public void Score_ConfidenceScore_IsOneMinusAverageRisk()
    {
        var score = _scorer.Score(new[]
        {
            Event(1, MatchEventType.Decision, """{"decisionId":"d1","kind":"roll","verdict":"ok","reason":"x","risk":0.4}""")
        });

        score.Expectation.AverageRisk.Should().Be(0.4);
        score.Expectation.ConfidenceScore.Should().BeApproximately(0.6, 1e-9);
    }

    [Fact]
    public void DecisionPayload_TryParse_ValidPayload_ReturnsParsed()
    {
        var payload = DecisionPayload.TryParse("""{"decisionId":"d1","kind":"roll","verdict":"ok","reason":"x","risk":0.7}""");

        payload.Should().NotBeNull();
        payload!.DecisionId.Should().Be("d1");
        payload.Kind.Should().Be("roll");
        payload.Verdict.Should().Be("ok");
        payload.Reason.Should().Be("x");
        payload.Risk.Should().Be(0.7);
    }

    [Fact]
    public void DecisionPayload_TryParse_IgnoresFieldNameCase()
    {
        var payload = DecisionPayload.TryParse("""{"DecisionId":"d1","KIND":"roll","Verdict":"ok","Reason":"x","RISK":0.7}""");

        payload.Should().NotBeNull();
        payload!.DecisionId.Should().Be("d1");
        payload.Risk.Should().Be(0.7);
    }

    [Fact]
    public void DecisionPayload_TryParse_MissingField_ReturnsNull()
    {
        DecisionPayload.TryParse("""{"decisionId":"d1","kind":"roll","verdict":"ok","risk":0.7}""").Should().BeNull();
        DecisionPayload.TryParse("""{"kind":"roll","verdict":"ok","reason":"x","risk":0.7}""").Should().BeNull();
    }

    [Fact]
    public void DecisionPayload_TryParse_RiskOutOfRange_ReturnsNull()
    {
        DecisionPayload.TryParse("""{"decisionId":"d1","kind":"roll","verdict":"ok","reason":"x","risk":1.1}""").Should().BeNull();
        DecisionPayload.TryParse("""{"decisionId":"d1","kind":"roll","verdict":"ok","reason":"x","risk":-0.1}""").Should().BeNull();
    }

    [Fact]
    public void DecisionPayload_TryParse_InvalidJson_ReturnsNull()
    {
        DecisionPayload.TryParse("{ not json").Should().BeNull();
        DecisionPayload.TryParse("null").Should().BeNull();
    }

    private static MatchEvent Event(long seq, MatchEventType type, string payload) => new()
    {
        Seq = seq,
        Timestamp = DateTimeOffset.UtcNow,
        GameId = "g1",
        Type = type,
        PayloadJson = payload
    };
}